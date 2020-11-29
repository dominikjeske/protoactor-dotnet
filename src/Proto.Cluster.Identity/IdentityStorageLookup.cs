﻿using Microsoft.Extensions.Logging;

namespace Proto.Cluster.Identity
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using IdentityLookup;
    using Router;

    public class IdentityStorageLookup : IIdentityLookup
    {
        private const string PlacementActorName = "placement-activator";
        private static readonly int PidClusterIdentityStartIndex = PlacementActorName.Length + 1;
        private readonly ILogger _logger = Log.CreateLogger<IdentityStorageLookup>();
        internal IIdentityStorage Storage { get; }
        internal Cluster Cluster;
        internal MemberList MemberList;
        private bool _isClient;
        private PID _placementActor;
        private ActorSystem _system;
        private PID _router;
        private string _memberId;

        public IdentityStorageLookup(IIdentityStorage storage)
        {
            Storage = storage;
        }

        public async Task<PID?> GetAsync(ClusterIdentity clusterIdentity, CancellationToken ct)
        {
            var msg = new GetPid(clusterIdentity, ct);

            var res = await _system.Root.RequestAsync<PidResult>(_router, msg, ct);
            return res?.Pid;
        }

        public async Task SetupAsync(Cluster cluster, string[] kinds, bool isClient)
        {
            Cluster = cluster;
            _system = cluster.System;
            _memberId = cluster.Id.ToString();
            MemberList = cluster.MemberList;
            _isClient = isClient;

            var workerProps = Props.FromProducer(() => new IdentityStorageWorker(this));
            //TODO: should pool size be configurable?

            var routerProps = _system.Root.NewConsistentHashPool(workerProps, 50);

            _router = _system.Root.Spawn(routerProps);

            //hook up events
            cluster.System.EventStream.Subscribe<ClusterTopology>(e =>
                {
                    //delete all members that have left from the lookup
                    foreach (var left in e.Left)
                        //YOLO. event stream is not async
                        _ = RemoveMemberAsync(left.Id);
                }
            );

            if (isClient) return;
            var props = Props.FromProducer(() => new IdentityStoragePlacementActor(Cluster, this));
            _placementActor = _system.Root.SpawnNamed(props, PlacementActorName);

            await Storage.Init();
        }

        public async Task ShutdownAsync()
        {
            if (!_isClient)
            {
                //TODO: rewrite to respond to pending activations
                await Cluster.System.Root.StopAsync(_placementActor);
                try
                {
                    await RemoveMemberAsync(_memberId);
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Failed to remove stored member activations for {MemberId}", _memberId);
                }
            }
        }

        internal Task RemoveMemberAsync(string memberId)
        {
            return Storage.RemoveMember(memberId, CancellationToken.None);
        }

        internal PID RemotePlacementActor(string address)
        {
            return PID.FromAddress(address, PlacementActorName);
        }

        public Task RemovePidAsync(PID pid, CancellationToken ct)
        {
            return Storage.RemoveActivation(pid, ct);
        }

        public static bool TryGetClusterIdentityShortString(string pidId, out string? clusterIdentity)
        {
            var idIndex = pidId.LastIndexOf("$", StringComparison.Ordinal);
            if (idIndex > PidClusterIdentityStartIndex)
            {
                clusterIdentity = pidId.Substring(PidClusterIdentityStartIndex,
                    idIndex - PidClusterIdentityStartIndex
                );
                return true;
            }

            clusterIdentity = default;
            return false;
        }
    }
}