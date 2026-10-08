using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Day19.RedisRedLock;

/// <summary>
/// In-memory simulated Redis node implementation for deterministic testing of distributed edge cases:
/// network latency, node crashes, split-brain, and lease expiry.
/// </summary>
public class SimulatedRedisNode : IRedisNodeInstance
{
    private record LockEntry(string Value, DateTime ExpiresAtUtc);

    private readonly ConcurrentDictionary<string, LockEntry> _store = new();
    private readonly object _syncLock = new();

    public string NodeId { get; }
    public bool IsHealthy { get; set; } = true;
    public TimeSpan SimulatedLatency { get; set; } = TimeSpan.Zero;

    public SimulatedRedisNode(string nodeId)
    {
        NodeId = nodeId;
    }

    public async Task<bool> TrySetLockAsync(string resource, string value, TimeSpan ttl, CancellationToken ct = default)
    {
        if (SimulatedLatency > TimeSpan.Zero)
        {
            await Task.Delay(SimulatedLatency, ct);
        }

        if (!IsHealthy) return false;

        var now = DateTime.UtcNow;
        lock (_syncLock)
        {
            // Clean expired key if any
            if (_store.TryGetValue(resource, out var existing))
            {
                if (now >= existing.ExpiresAtUtc)
                {
                    _store.TryRemove(resource, out _);
                }
                else
                {
                    return false; // Key already locked by another client
                }
            }

            var entry = new LockEntry(value, now.Add(ttl));
            return _store.TryAdd(resource, entry);
        }
    }

    public async Task<bool> TryReleaseLockAsync(string resource, string value, CancellationToken ct = default)
    {
        if (SimulatedLatency > TimeSpan.Zero)
        {
            await Task.Delay(SimulatedLatency, ct);
        }

        if (!IsHealthy) return false;

        // Atomic Lua script emulation:
        // if redis.call('get', KEYS[1]) == ARGV[1] then return redis.call('del', KEYS[1]) else return 0 end
        lock (_syncLock)
        {
            if (_store.TryGetValue(resource, out var existing))
            {
                if (existing.Value == value)
                {
                    _store.TryRemove(resource, out _);
                    return true;
                }
            }
            return false;
        }
    }

    public async Task<bool> TryExtendLockAsync(string resource, string value, TimeSpan ttl, CancellationToken ct = default)
    {
        if (SimulatedLatency > TimeSpan.Zero)
        {
            await Task.Delay(SimulatedLatency, ct);
        }

        if (!IsHealthy) return false;

        // Atomic Lua script emulation for lease extension:
        // if redis.call('get', KEYS[1]) == ARGV[1] then return redis.call('pexpire', KEYS[1], ARGV[2]) else return 0 end
        var now = DateTime.UtcNow;
        lock (_syncLock)
        {
            if (_store.TryGetValue(resource, out var existing))
            {
                if (existing.Value == value && now < existing.ExpiresAtUtc)
                {
                    _store[resource] = new LockEntry(value, now.Add(ttl));
                    return true;
                }
            }
            return false;
        }
    }

    public async Task<string?> GetLockValueAsync(string resource, CancellationToken ct = default)
    {
        if (!IsHealthy) return null;
        var now = DateTime.UtcNow;
        lock (_syncLock)
        {
            if (_store.TryGetValue(resource, out var existing))
            {
                if (now < existing.ExpiresAtUtc)
                {
                    return existing.Value;
                }
                _store.TryRemove(resource, out _);
            }
            return null;
        }
    }
}

/// <summary>
/// Implementation of the RedLock multi-instance distributed locking algorithm.
/// Coordinates mutual exclusion across N independent Redis nodes without single points of failure.
/// </summary>
public class RedLockEngine
{
    private readonly IReadOnlyList<IRedisNodeInstance> _nodes;
    private readonly RedLockOptions _options;
    private readonly FencingTokenGenerator _fencing = new();

    public RedLockOptions Options => _options;
    public IReadOnlyList<IRedisNodeInstance> Nodes => _nodes;
    public int Quorum => (_nodes.Count / 2) + 1;

    public RedLockEngine(IEnumerable<IRedisNodeInstance> nodes, RedLockOptions? options = null)
    {
        _nodes = nodes.ToList();
        if (_nodes.Count < 1)
        {
            throw new ArgumentException("RedLock requires at least 1 node instance (recommended 3 or 5).", nameof(nodes));
        }

        _options = options ?? new RedLockOptions();
    }

    /// <summary>
    /// Calculates clock drift: (TTL * factor) + clockSkewTolerance.
    /// </summary>
    public TimeSpan CalculateClockDrift(TimeSpan ttl)
    {
        double driftMs = (ttl.TotalMilliseconds * _options.DriftFactor) + _options.ClockSkewTolerance.TotalMilliseconds;
        return TimeSpan.FromMilliseconds(driftMs);
    }

    /// <summary>
    /// Attempts to acquire a distributed lock across the multi-instance cluster.
    /// </summary>
    public async Task<RedLockAcquireResult> TryAcquireLockAsync(
        string resourceKey,
        TimeSpan? customTtl = null,
        string? customLockValue = null,
        CancellationToken ct = default)
    {
        var ttl = customTtl ?? _options.LockTtl;
        var lockValue = customLockValue ?? Guid.NewGuid().ToString("N");
        var drift = CalculateClockDrift(ttl);

        var startTimestamp = Stopwatch.GetTimestamp();

        // 1. Attempt to acquire lock on all N instances concurrently with node timeout
        var tasks = _nodes.Select(async node =>
        {
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(_options.NodeTimeout);
                bool success = await node.TrySetLockAsync(resourceKey, lockValue, ttl, cts.Token);
                return (Node: node, Success: success);
            }
            catch
            {
                return (Node: node, Success: false);
            }
        });

        var results = await Task.WhenAll(tasks);
        int acquiredCount = results.Count(r => r.Success);

        // 2. Measure elapsed time and remaining validity time
        var elapsed = Stopwatch.GetElapsedTime(startTimestamp);
        var validityTime = ttl - elapsed - drift;

        // 3. Quorum check: requires >= Quorum nodes AND positive validity time
        if (acquiredCount >= Quorum && validityTime > TimeSpan.Zero)
        {
            long fencingToken = _fencing.Next();
            return new RedLockAcquireResult(
                Success: true,
                ResourceKey: resourceKey,
                LockValue: lockValue,
                ValidityTime: validityTime,
                AcquiredNodes: acquiredCount,
                TotalNodes: _nodes.Count,
                FencingToken: fencingToken);
        }

        // 4. Compensation / Rollback: Quorum not met or lock took too long; unlock all nodes
        await ReleaseLockInternalAsync(resourceKey, lockValue);

        string reason = acquiredCount < Quorum
            ? $"Quorum not met (acquired {acquiredCount}/{_nodes.Count}, required {Quorum})."
            : $"Lock validity expired during acquisition (elapsed: {elapsed.TotalMilliseconds:F1}ms, drift: {drift.TotalMilliseconds:F1}ms).";

        return new RedLockAcquireResult(
            Success: false,
            ResourceKey: resourceKey,
            LockValue: lockValue,
            ValidityTime: TimeSpan.Zero,
            AcquiredNodes: acquiredCount,
            TotalNodes: _nodes.Count,
            FencingToken: 0,
            ErrorMessage: reason);
    }

    /// <summary>
    /// Acquires a managed RedLock lease with background auto-renewal heartbeat.
    /// </summary>
    public async Task<RedLockLeaseHandle?> AcquireLeaseAsync(
        string resourceKey,
        TimeSpan? customTtl = null,
        CancellationToken ct = default)
    {
        var result = await TryAcquireLockAsync(resourceKey, customTtl, ct: ct);
        if (!result.Success) return null;

        return new RedLockLeaseHandle(this, result, _options.AutoRenewalInterval);
    }

    /// <summary>
    /// Extends an active lock lease across the cluster.
    /// </summary>
    public async Task<bool> ExtendLockAsync(string resourceKey, string lockValue, TimeSpan ttl, CancellationToken ct = default)
    {
        var tasks = _nodes.Select(async node =>
        {
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(_options.NodeTimeout);
                return await node.TryExtendLockAsync(resourceKey, lockValue, ttl, cts.Token);
            }
            catch
            {
                return false;
            }
        });

        var results = await Task.WhenAll(tasks);
        int successCount = results.Count(s => s);
        return successCount >= Quorum;
    }

    /// <summary>
    /// Releases the lock from all instances using atomic Lua scripts.
    /// </summary>
    public async Task<int> ReleaseLockAsync(string resourceKey, string lockValue)
    {
        return await ReleaseLockInternalAsync(resourceKey, lockValue);
    }

    private async Task<int> ReleaseLockInternalAsync(string resourceKey, string lockValue)
    {
        var tasks = _nodes.Select(async node =>
        {
            try
            {
                using var cts = new CancellationTokenSource(_options.NodeTimeout);
                return await node.TryReleaseLockAsync(resourceKey, lockValue, cts.Token);
            }
            catch
            {
                return false;
            }
        });

        var results = await Task.WhenAll(tasks);
        return results.Count(r => r);
    }
}
