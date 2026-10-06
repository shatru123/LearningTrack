using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Day16.SynchronizationLocks;

/// <summary>
/// 1. Lock-Free Atomic State Management using Interlocked.CompareExchange
/// </summary>
public class LockFreeCounter
{
    private long _value;

    public long Value => Interlocked.Read(ref _value);

    public long Increment() => Interlocked.Increment(ref _value);

    public long Add(long delta) => Interlocked.Add(ref _value, delta);

    public bool UpdateIfGreaterThan(long candidate)
    {
        while (true)
        {
            long current = Interlocked.Read(ref _value);
            if (candidate <= current) return false;

            if (Interlocked.CompareExchange(ref _value, candidate, current) == current)
            {
                return true;
            }
        }
    }
}

/// <summary>
/// 2. Asynchronous Resource Throttling using SemaphoreSlim with IDisposable Lease Pattern
/// </summary>
public class AsyncResourceThrottle
{
    private readonly SemaphoreSlim _semaphore;
    private readonly int _maxConcurrency;

    public int CurrentAvailable => _semaphore.CurrentCount;

    public AsyncResourceThrottle(int maxConcurrency)
    {
        _maxConcurrency = maxConcurrency;
        _semaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency);
    }

    public async Task<IDisposable?> TryAcquireLeaseAsync(TimeSpan timeout, CancellationToken ct = default)
    {
        bool entered = await _semaphore.WaitAsync(timeout, ct);
        if (!entered) return null;

        return new SemaphoreLease(_semaphore);
    }

    private sealed class SemaphoreLease : IDisposable
    {
        private readonly SemaphoreSlim _sem;
        private int _disposed = 0;

        public SemaphoreLease(SemaphoreSlim sem) => _sem = sem;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                _sem.Release();
            }
        }
    }
}

/// <summary>
/// 3. Read-Heavy In-Memory Cache using ReaderWriterLockSlim
/// </summary>
public class ReadHeavyCache<TKey, TValue> : IDisposable where TKey : notnull
{
    private readonly Dictionary<TKey, TValue> _cache = new();
    private readonly ReaderWriterLockSlim _rwLock = new(LockRecursionPolicy.NoRecursion);

    public bool TryGet(TKey key, out TValue? value)
    {
        _rwLock.EnterReadLock();
        try
        {
            return _cache.TryGetValue(key, out value);
        }
        finally
        {
            _rwLock.ExitReadLock();
        }
    }

    public void Set(TKey key, TValue value)
    {
        _rwLock.EnterWriteLock();
        try
        {
            _cache[key] = value;
        }
        finally
        {
            _rwLock.ExitWriteLock();
        }
    }

    public TValue GetOrAdd(TKey key, Func<TKey, TValue> valueFactory)
    {
        _rwLock.EnterUpgradeableReadLock();
        try
        {
            if (_cache.TryGetValue(key, out var existing))
            {
                return existing;
            }

            _rwLock.EnterWriteLock();
            try
            {
                if (_cache.TryGetValue(key, out var doubleChecked))
                {
                    return doubleChecked;
                }

                var created = valueFactory(key);
                _cache[key] = created;
                return created;
            }
            finally
            {
                _rwLock.ExitWriteLock();
            }
        }
        finally
        {
            _rwLock.ExitUpgradeableReadLock();
        }
    }

    public void Dispose()
    {
        _rwLock.Dispose();
    }
}

/// <summary>
/// 4. Distributed Lock Simulator with Fencing Tokens & Lease Renewals
/// </summary>
public class DistributedLockSimulator
{
    private readonly ConcurrentDictionary<string, DistributedLockToken> _activeLocks = new();
    private long _fencingCounter = 0;
    private long _lastCommittedFencingToken = 0;

    public LockAcquisitionResult AcquireLock(string resourceName, string ownerId, TimeSpan ttl)
    {
        var now = DateTime.UtcNow;

        while (true)
        {
            if (_activeLocks.TryGetValue(resourceName, out var existing))
            {
                if (!existing.IsExpired)
                {
                    return new LockAcquisitionResult(false, null, $"Locked by owner {existing.OwnerId} until {existing.ExpiresAtUtc:O}");
                }

                // Lock is expired: try to replace
                var newFencing = Interlocked.Increment(ref _fencingCounter);
                var newToken = new DistributedLockToken(resourceName, ownerId, newFencing, now.Add(ttl));

                if (_activeLocks.TryUpdate(resourceName, newToken, existing))
                {
                    return new LockAcquisitionResult(true, newToken, null);
                }
                continue;
            }
            else
            {
                var newFencing = Interlocked.Increment(ref _fencingCounter);
                var newToken = new DistributedLockToken(resourceName, ownerId, newFencing, now.Add(ttl));

                if (_activeLocks.TryAdd(resourceName, newToken))
                {
                    return new LockAcquisitionResult(true, newToken, null);
                }
            }
        }
    }

    public bool ReleaseLock(DistributedLockToken token)
    {
        if (_activeLocks.TryGetValue(token.ResourceName, out var existing))
        {
            // Only current owner and same fencing token can release
            if (existing.OwnerId == token.OwnerId && existing.FencingToken == token.FencingToken)
            {
                return _activeLocks.TryRemove(token.ResourceName, out _);
            }
        }
        return false;
    }

    public bool RenewLock(DistributedLockToken token, TimeSpan extension)
    {
        if (_activeLocks.TryGetValue(token.ResourceName, out var existing))
        {
            if (existing.OwnerId == token.OwnerId && existing.FencingToken == token.FencingToken && !existing.IsExpired)
            {
                var renewed = existing with { ExpiresAtUtc = DateTime.UtcNow.Add(extension) };
                return _activeLocks.TryUpdate(token.ResourceName, renewed, existing);
            }
        }
        return false;
    }

    public bool ValidateFencingTokenForStorageWrite(long tokenValue)
    {
        // Out-of-order write rejection: storage service only accepts strictly monotonically increasing tokens
        while (true)
        {
            long currentCommitted = Interlocked.Read(ref _lastCommittedFencingToken);
            if (tokenValue <= currentCommitted)
            {
                return false; // Stale / Zombie worker write rejected!
            }

            if (Interlocked.CompareExchange(ref _lastCommittedFencingToken, tokenValue, currentCommitted) == currentCommitted)
            {
                return true;
            }
        }
    }
}
