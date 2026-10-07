using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Day18.RedisDataStructures;

public class MemoryEvictionSimulator
{
    private readonly int _maxSizeBytes;
    private readonly EvictionPolicy _policy;
    private readonly ConcurrentDictionary<string, CacheItemMetadata> _store = new();
    private int _currentSizeBytes = 0;
    private int _evictedCount = 0;

    public int CurrentSizeBytes => _currentSizeBytes;
    public int EvictedCount => _evictedCount;
    public int KeyCount => _store.Count;

    public MemoryEvictionSimulator(int maxSizeBytes, EvictionPolicy policy)
    {
        _maxSizeBytes = maxSizeBytes;
        _policy = policy;
    }

    public void Put(string key, object value, int sizeBytes, TimeSpan? ttl = null, DateTime? customAccessTime = null)
    {
        var now = customAccessTime ?? DateTime.UtcNow;

        lock (_store)
        {
            // If key already exists, deduct old size
            if (_store.TryGetValue(key, out var oldItem))
            {
                _currentSizeBytes -= oldItem.SizeBytes;
            }

            // Evict if over budget
            while (_currentSizeBytes + sizeBytes > _maxSizeBytes)
            {
                var victim = SelectEvictionVictim(now);
                if (victim == null)
                {
                    if (_policy == EvictionPolicy.NoEviction)
                    {
                        throw new InvalidOperationException("OOM command not allowed when used memory > 'maxmemory' under noeviction policy.");
                    }
                    // Cannot evict further (e.g. volatile policy with no volatile keys left)
                    throw new InvalidOperationException("Memory full: No suitable keys available for eviction under current policy.");
                }

                _store.TryRemove(victim.Key, out _);
                _currentSizeBytes -= victim.SizeBytes;
                _evictedCount++;
            }

            // Insert new item
            var metadata = new CacheItemMetadata
            {
                Key = key,
                Value = value,
                SizeBytes = sizeBytes,
                ExpiresAtUtc = ttl.HasValue ? now.Add(ttl.Value) : null,
                LastAccessedUtc = now,
                LfuCounter = 5,
                LastDecayTimeUtc = now
            };

            _store[key] = metadata;
            _currentSizeBytes += sizeBytes;
        }
    }

    public object? Get(string key, DateTime? accessTime = null)
    {
        lock (_store)
        {
            if (!_store.TryGetValue(key, out var item)) return null;

            var now = accessTime ?? DateTime.UtcNow;
            if (item.ExpiresAtUtc.HasValue && now >= item.ExpiresAtUtc.Value)
            {
                _store.TryRemove(key, out _);
                _currentSizeBytes -= item.SizeBytes;
                return null;
            }

            // Update LRU access timestamp
            item.LastAccessedUtc = now;

            // Update LFU Morris counter with logarithmic increment and decay
            UpdateLfuCounter(item, now);

            return item.Value;
        }
    }

    private CacheItemMetadata? SelectEvictionVictim(DateTime now)
    {
        IEnumerable<CacheItemMetadata> pool = _store.Values;

        // Filter volatile keys if volatile policy
        if (_policy is EvictionPolicy.VolatileLru or EvictionPolicy.VolatileLfu or EvictionPolicy.VolatileTtl)
        {
            pool = pool.Where(x => x.ExpiresAtUtc.HasValue);
        }

        var candidateList = pool.ToList();
        if (candidateList.Count == 0) return null;

        return _policy switch
        {
            EvictionPolicy.AllKeysLru or EvictionPolicy.VolatileLru =>
                candidateList.OrderBy(x => x.LastAccessedUtc).FirstOrDefault(),

            EvictionPolicy.AllKeysLfu or EvictionPolicy.VolatileLfu =>
                candidateList.OrderBy(x => x.LfuCounter).ThenBy(x => x.LastAccessedUtc).FirstOrDefault(),

            EvictionPolicy.VolatileTtl =>
                candidateList.Where(x => x.ExpiresAtUtc.HasValue)
                             .OrderBy(x => x.ExpiresAtUtc!.Value)
                             .FirstOrDefault(),

            _ => null
        };
    }

    private static void UpdateLfuCounter(CacheItemMetadata item, DateTime now)
    {
        // 1. Decay: Redis decays 1 point per elapsed minute (lfu-decay-time)
        var minutesPassed = (int)(now - item.LastDecayTimeUtc).TotalMinutes;
        if (minutesPassed > 0)
        {
            item.LfuCounter = (byte)Math.Max(0, item.LfuCounter - minutesPassed);
            item.LastDecayTimeUtc = now;
        }

        // 2. Morris Logarithmic increment
        // Probability = 1 / (base + counter * factor)
        double r = Random.Shared.NextDouble();
        double p = 1.0 / (item.LfuCounter + 1);
        if (r < p && item.LfuCounter < 255)
        {
            item.LfuCounter++;
        }
    }
}
