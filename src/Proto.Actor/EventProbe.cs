using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Proto
{
    public static class EventStreamExtensions
    {
        public static EventProbe<T> GetProbe<T>(this EventStream<T> eventStream)
        {
            return new EventProbe<T>(eventStream);
        }
    }
    public class EventProbe<T>
    {
        private readonly Subscription<T> _subscription;
        private readonly ConcurrentQueue<T> _events = new ConcurrentQueue<T>();
        private readonly object _lock = new object();
        private EventExpectation<T> _currentExpectation;

        public EventProbe(EventStream<T> eventStream) 
        {
            _subscription = eventStream.Subscribe(e =>
                {
                    _events.Enqueue(e);
                    NotifyChanges();
                }
            );
        }

        public Task Expect<TE>() where TE : T
        {
            lock (_lock)
            {
                var expectation = new EventExpectation<T>(@event => @event is TE);
                _currentExpectation = expectation;
                NotifyChanges();
                return expectation.Task;
            }
        }

        public Task<T> Expect<TE>(Func<TE, bool> predicate) where TE : T
        {
            lock (_lock)
            {
                var expectation = new EventExpectation<T>(@event =>
                    {
                        return @event switch
                        {
                            TE e when predicate(e) => true,
                            _ => false
                        };
                    }
                );
                _currentExpectation = expectation;
                NotifyChanges();
                return expectation.Task;
            }
        }

        public void Stop()
        {
            _currentExpectation = null;
            _subscription.Unsubscribe();
        }

        //TODO: make lockfree
        private void NotifyChanges()
        {
            lock (_lock)
            {
                if (_currentExpectation == null)
                {
                    return;
                }

                while (_events.TryDequeue(out var @event))
                {
                    _currentExpectation.Evaluate(@event);
                    if (_currentExpectation.Done)
                    {
                        _currentExpectation = null;
                    }
                }
            }
        }
    }
}