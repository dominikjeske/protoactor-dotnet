﻿using System.Threading;
using System.Threading.Tasks;
using Proto.Remote.Tests.Messages;

namespace Proto.Cluster.Tests
{
    public static class Extensions
    {
        public static Task<Pong> Ping(this Cluster cluster, string id, string message, CancellationToken token, string kind = EchoActor.Kind)
        {
            return cluster.RequestAsync<Pong>(id, kind, new Ping{ Message = message}, token);
        } 
    }
}