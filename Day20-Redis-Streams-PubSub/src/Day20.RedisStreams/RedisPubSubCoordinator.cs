using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Day20.RedisStreams;

/// <summary>
/// In-memory coordinator simulating Redis Pub/Sub mechanics:
/// - Ephemeral fire-and-forget delivery
/// - Zero persistence (offline/disconnected subscribers miss messages completely)
/// - Fanout to all active subscribers on a channel
/// </summary>
public class RedisPubSubCoordinator
{
    private readonly ConcurrentDictionary<string, List<Func<string, Task>>> _subscribers = new();
    private readonly object _syncLock = new();

    public int GetSubscriberCount(string channel)
    {
        lock (_syncLock)
        {
            return _subscribers.TryGetValue(channel, out var list) ? list.Count : 0;
        }
    }

    /// <summary>
    /// Subscribes an async handler to a specific pub/sub channel.
    /// Returns an IDisposable subscription token to unsubscribe.
    /// </summary>
    public IDisposable Subscribe(string channel, Func<string, Task> handler)
    {
        lock (_syncLock)
        {
            var list = _subscribers.GetOrAdd(channel, _ => new List<Func<string, Task>>());
            list.Add(handler);
        }

        return new SubscriptionToken(() =>
        {
            lock (_syncLock)
            {
                if (_subscribers.TryGetValue(channel, out var list))
                {
                    list.Remove(handler);
                    if (list.Count == 0)
                    {
                        _subscribers.TryRemove(channel, out _);
                    }
                }
            }
        });
    }

    /// <summary>
    /// Publishes a message to a channel.
    /// Returns the number of clients that received the message.
    /// </summary>
    public async Task<int> PublishAsync(string channel, string message)
    {
        List<Func<string, Task>> targets;
        lock (_syncLock)
        {
            if (!_subscribers.TryGetValue(channel, out var list) || list.Count == 0)
            {
                // Message is dropped immediately; zero persistence
                return 0;
            }
            targets = new List<Func<string, Task>>(list);
        }

        var tasks = targets.Select(handler => handler(message));
        await Task.WhenAll(tasks);
        return targets.Count;
    }

    private sealed class SubscriptionToken : IDisposable
    {
        private Action? _unsubscribe;

        public SubscriptionToken(Action unsubscribe)
        {
            _unsubscribe = unsubscribe;
        }

        public void Dispose()
        {
            Interlocked.Exchange(ref _unsubscribe, null)?.Invoke();
        }
    }
}
