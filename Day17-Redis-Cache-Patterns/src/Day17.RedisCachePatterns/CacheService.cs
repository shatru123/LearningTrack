using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Day17.RedisCachePatterns;

public interface IDistributedCacheProvider
{
    Task<T?> GetAsync<T>(string key);
    Task SetAsync<T>(string key, T value, TimeSpan ttl);
    Task<bool> RemoveAsync(string key);
    Task<bool> AcquireLockAsync(string key, string owner, TimeSpan ttl);
    Task<bool> ReleaseLockAsync(string key, string owner);
}

public class MockRedisCacheProvider : IDistributedCacheProvider
{
    private readonly ConcurrentDictionary<string, (object Value, DateTime ExpiresAt)> _store = new();
    private readonly ConcurrentDictionary<string, (string Owner, DateTime ExpiresAt)> _locks = new();

    public Task<T?> GetAsync<T>(string key)
    {
        if (_store.TryGetValue(key, out var item))
        {
            if (DateTime.UtcNow < item.ExpiresAt)
            {
                return Task.FromResult<T?>((T)item.Value);
            }
            _store.TryRemove(key, out _);
        }
        return Task.FromResult<T?>(default);
    }

    public Task SetAsync<T>(string key, T value, TimeSpan ttl)
    {
        _store[key] = (value!, DateTime.UtcNow.Add(ttl));
        return Task.CompletedTask;
    }

    public Task<bool> RemoveAsync(string key)
    {
        return Task.FromResult(_store.TryRemove(key, out _));
    }

    public Task<bool> AcquireLockAsync(string key, string owner, TimeSpan ttl)
    {
        var now = DateTime.UtcNow;
        if (_locks.TryGetValue(key, out var existing))
        {
            if (now < existing.ExpiresAt)
            {
                return Task.FromResult(false);
            }
        }

        _locks[key] = (owner, now.Add(ttl));
        return Task.FromResult(true);
    }

    public Task<bool> ReleaseLockAsync(string key, string owner)
    {
        if (_locks.TryGetValue(key, out var existing) && existing.Owner == owner)
        {
            return Task.FromResult(_locks.TryRemove(key, out _));
        }
        return Task.FromResult(false);
    }
}

/// <summary>
/// Cache-Aside (Lazy Loading) with Cache Stampede / Thundering Herd Mutex Protection
/// </summary>
public class CacheAsideManager
{
    private readonly IDistributedCacheProvider _cache;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _keyLocks = new();
    private int _hits = 0;
    private int _misses = 0;
    private int _dbLoads = 0;

    public CacheMetrics GetMetrics() => new(_hits, _misses, _dbLoads, 0);

    public CacheAsideManager(IDistributedCacheProvider cache)
    {
        _cache = cache;
    }

    public async Task<T> GetOrCreateAsync<T>(
        string key,
        Func<Task<T>> databaseLoader,
        TimeSpan ttl)
    {
        // 1. Check Cache
        var cached = await _cache.GetAsync<T>(key);
        if (cached != null)
        {
            Interlocked.Increment(ref _hits);
            return cached;
        }

        Interlocked.Increment(ref _misses);

        // 2. Cache Stampede Mutex: only one task queries the database for this specific key
        var keyLock = _keyLocks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        await keyLock.WaitAsync();
        try
        {
            // Double check cache
            cached = await _cache.GetAsync<T>(key);
            if (cached != null)
            {
                Interlocked.Increment(ref _hits);
                return cached;
            }

            // 3. Query Database
            var dbValue = await databaseLoader();
            Interlocked.Increment(ref _dbLoads);

            // 4. Populate Cache
            await _cache.SetAsync(key, dbValue, ttl);
            return dbValue;
        }
        finally
        {
            keyLock.Release();
        }
    }
}

/// <summary>
/// Write-Through: Simultaneously writes to cache and database in one call
/// </summary>
public class WriteThroughManager
{
    private readonly IDistributedCacheProvider _cache;
    private readonly ConcurrentDictionary<int, Product> _database = new();

    public WriteThroughManager(IDistributedCacheProvider cache)
    {
        _cache = cache;
    }

    public async Task SaveProductAsync(Product product, TimeSpan ttl)
    {
        // 1. Write to database synchronously
        _database[product.Id] = product;

        // 2. Write to cache synchronously
        await _cache.SetAsync($"product:{product.Id}", product, ttl);
    }

    public async Task<Product?> GetProductAsync(int id)
    {
        var cached = await _cache.GetAsync<Product>($"product:{id}");
        if (cached != null) return cached;

        if (_database.TryGetValue(id, out var dbProduct))
        {
            await _cache.SetAsync($"product:{id}", dbProduct, TimeSpan.FromMinutes(10));
            return dbProduct;
        }

        return null;
    }
}

/// <summary>
/// Write-Behind (Write-Back): Enqueues writes into an asynchronous background channel for batch persistence
/// </summary>
public class WriteBehindManager
{
    private readonly IDistributedCacheProvider _cache;
    private readonly Channel<Product> _writeQueue;
    private readonly ConcurrentDictionary<int, Product> _backingDatabase = new();
    private int _flushedCount = 0;

    public int FlushedCount => _flushedCount;

    public WriteBehindManager(IDistributedCacheProvider cache, int queueCapacity = 1000)
    {
        _cache = cache;
        _writeQueue = Channel.CreateBounded<Product>(new BoundedChannelOptions(queueCapacity)
        {
            FullMode = BoundedChannelFullMode.Wait
        });
    }

    public async Task WriteAsync(Product product, TimeSpan ttl)
    {
        // 1. Update cache immediately for sub-millisecond client reads
        await _cache.SetAsync($"product:{product.Id}", product, ttl);

        // 2. Enqueue for asynchronous database flush
        await _writeQueue.Writer.WriteAsync(product);
    }

    public async Task<int> FlushBatchAsync(int maxBatchSize = 100)
    {
        int processed = 0;
        while (processed < maxBatchSize && _writeQueue.Reader.TryRead(out var product))
        {
            _backingDatabase[product.Id] = product;
            processed++;
            Interlocked.Increment(ref _flushedCount);
        }
        return processed;
    }

    public Product? GetFromDatabaseDirectly(int id)
    {
        _backingDatabase.TryGetValue(id, out var prod);
        return prod;
    }
}

/// <summary>
/// Probabilistic Early Expiration (XFetch Algorithm)
/// Evaluates: delta * beta * (-ln(rand)) > timeRemaining
/// </summary>
public static class XFetchProtector
{
    private static readonly Random _rng = new();

    public static bool ShouldRefreshEarly(
        DateTime expiresAtUtc,
        TimeSpan computeDuration,
        double beta = 1.0)
    {
        var now = DateTime.UtcNow;
        if (now >= expiresAtUtc) return true; // Already expired

        double timeRemainingSec = (expiresAtUtc - now).TotalSeconds;
        double deltaSec = computeDuration.TotalSeconds;

        double randomVal = _rng.NextDouble();
        if (randomVal <= 0.0) randomVal = 0.0001; // Avoid log(0)

        double xfetch = deltaSec * beta * -Math.Log(randomVal);

        return xfetch > timeRemainingSec;
    }
}
