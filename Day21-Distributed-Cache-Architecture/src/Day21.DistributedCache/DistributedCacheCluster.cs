using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Day21.DistributedCache;

/// <summary>
/// An independent distributed cache shard node.
/// </summary>
public class CacheShardNode
{
    private record CacheEntry(object Value, DateTime ExpiresAtUtc, DateTime CreatedUtc, TimeSpan ComputeDuration);

    private readonly ConcurrentDictionary<string, CacheEntry> _storage = new();

    public string NodeId { get; }
    public int KeyCount => _storage.Count;

    public CacheShardNode(string nodeId)
    {
        NodeId = nodeId;
    }

    public void Set(string key, object value, TimeSpan ttl, TimeSpan computeDuration = default)
    {
        var now = DateTime.UtcNow;
        _storage[key] = new CacheEntry(value, now.Add(ttl), now, computeDuration);
    }

    public object? Get(string key)
    {
        var now = DateTime.UtcNow;
        if (_storage.TryGetValue(key, out var entry))
        {
            if (now < entry.ExpiresAtUtc)
            {
                return entry.Value;
            }
            _storage.TryRemove(key, out _);
        }
        return null;
    }

    public bool ShouldRefreshEarly(string key, double beta = 1.0)
    {
        var now = DateTime.UtcNow;
        if (_storage.TryGetValue(key, out var entry))
        {
            double remainingSec = (entry.ExpiresAtUtc - now).TotalSeconds;
            if (remainingSec <= 0) return true;

            double deltaSec = entry.ComputeDuration.TotalSeconds > 0 ? entry.ComputeDuration.TotalSeconds : 0.1;
            double rand = Random.Shared.NextDouble();
            if (rand <= 0.0) rand = 0.0001;

            // XFetch probabilistic threshold
            return (deltaSec * beta * -Math.Log(rand)) > remainingSec;
        }
        return false;
    }

    public void Invalidate(string key) => _storage.TryRemove(key, out _);
    public void Clear() => _storage.Clear();
}

/// <summary>
/// Production Distributed Caching Cluster coordinating:
/// - 2-Tier Caching: L1 Local In-Memory + L2 Sharded Cluster
/// - Consistent Hashing Ring key partitioning with virtual nodes
/// - Hot-Key Read Mitigation via Suffix Scattering
/// - Cache Stampede Protection (Single-Flight Mutex + XFetch early refresh)
/// </summary>
public class DistributedCacheCluster
{
    private readonly ConsistentHashRing<CacheShardNode> _hashRing;
    private readonly ConcurrentDictionary<string, (object Value, DateTime ExpiresAtUtc)> _l1Cache = new();
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();
    private readonly TimeSpan _l1Ttl;

    public ConsistentHashRing<CacheShardNode> HashRing => _hashRing;
    public int L1CacheCount => _l1Cache.Count;

    public DistributedCacheCluster(
        IEnumerable<CacheShardNode> initialShards,
        int virtualNodesPerShard = 100,
        TimeSpan? l1Ttl = null)
    {
        _hashRing = new ConsistentHashRing<CacheShardNode>(s => s.NodeId, virtualNodesPerShard, initialShards);
        _l1Ttl = l1Ttl ?? TimeSpan.FromSeconds(5);
    }

    /// <summary>
    /// Multi-tier GetOrSet with Single-Flight Stampede Protection.
    /// </summary>
    public async Task<T> GetOrSetAsync<T>(
        string key,
        Func<Task<T>> valueFactory,
        TimeSpan ttl,
        bool useL1 = true)
    {
        var now = DateTime.UtcNow;

        // 1. Check L1 local in-process cache
        if (useL1 && _l1Cache.TryGetValue(key, out var l1Entry))
        {
            if (now < l1Entry.ExpiresAtUtc)
            {
                return (T)l1Entry.Value;
            }
            _l1Cache.TryRemove(key, out _);
        }

        // 2. Resolve owning L2 shard via Consistent Hash Ring
        var targetShard = _hashRing.GetNode(key);

        // Check L2 shard
        var l2Val = targetShard.Get(key);
        if (l2Val != null)
        {
            if (useL1)
            {
                _l1Cache[key] = (l2Val, now.Add(_l1Ttl));
            }
            return (T)l2Val;
        }

        // 3. Single-Flight Lock: Only one worker queries factory; concurrent requests await
        var gate = _locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync();

        try
        {
            // Recheck inside lock
            l2Val = targetShard.Get(key);
            if (l2Val != null)
            {
                if (useL1) _l1Cache[key] = (l2Val, DateTime.UtcNow.Add(_l1Ttl));
                return (T)l2Val;
            }

            var start = DateTime.UtcNow;
            T computed = await valueFactory();
            var duration = DateTime.UtcNow - start;

            targetShard.Set(key, computed!, ttl, duration);

            if (useL1)
            {
                _l1Cache[key] = (computed!, DateTime.UtcNow.Add(_l1Ttl));
            }

            return computed;
        }
        finally
        {
            gate.Release();
            _locks.TryRemove(key, out _);
        }
    }

    /// <summary>
    /// Hot-Key Mitigation: Scatters high-volume keys across M suffixes (e.g. key:#hk_0, key:#hk_1)
    /// to distribute query throughput evenly across multiple physical shard machines.
    /// </summary>
    public async Task<T> GetOrSetHotKeyAsync<T>(
        string key,
        Func<Task<T>> valueFactory,
        TimeSpan ttl,
        int scatterShards = 4)
    {
        // Read from randomly selected replica shard
        int readIndex = Random.Shared.Next(scatterShards);
        string shardKey = $"{key}:#hk_{readIndex}";

        return await GetOrSetAsync(shardKey, async () =>
        {
            // On miss, compute once and populate all scatter replica shards
            T computed = await valueFactory();
            for (int i = 0; i < scatterShards; i++)
            {
                string sk = $"{key}:#hk_{i}";
                var targetNode = _hashRing.GetNode(sk);
                targetNode.Set(sk, computed!, ttl);
            }
            return computed;
        }, ttl, useL1: true);
    }

    /// <summary>
    /// Explicitly invalidates key across L1 and target L2 shard.
    /// </summary>
    public void Invalidate(string key)
    {
        _l1Cache.TryRemove(key, out _);
        var shard = _hashRing.GetNode(key);
        shard.Invalidate(key);
    }
}
