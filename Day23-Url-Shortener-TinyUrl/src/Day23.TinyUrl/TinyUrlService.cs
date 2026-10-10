using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Day23.TinyUrl;

public record UrlRecord
{
    public long Id { get; init; }
    public string ShortCode { get; init; } = string.Empty;
    public string LongUrl { get; init; } = string.Empty;
    public DateTime CreatedAtUtc { get; init; }
    public DateTime? ExpiresAtUtc { get; init; }
    public int ShardId { get; init; }
    public long ClickCount;
}

/// <summary>
/// Core URL Shortener Service integrating:
/// - Distributed Snowflake 64-bit ID Generation
/// - Loss-less Base62 Encoding
/// - Database Sharding by Hash(ID)
/// - Two-tier Cache-Aside Layer with TTL Eviction
/// - Collision-safe Custom Alias Support
/// </summary>
public class TinyUrlService
{
    private readonly SnowflakeIdGenerator _idGenerator;
    private readonly int _shardCount;
    private readonly List<ConcurrentDictionary<string, UrlRecord>> _dbShards = new();
    private readonly ConcurrentDictionary<string, UrlRecord> _redisCache = new();

    private long _cacheHits = 0;
    private long _cacheMisses = 0;

    public long CacheHits => Interlocked.Read(ref _cacheHits);
    public long CacheMisses => Interlocked.Read(ref _cacheMisses);
    public int ShardCount => _shardCount;

    public TinyUrlService(SnowflakeIdGenerator? idGenerator = null, int shardCount = 4)
    {
        _idGenerator = idGenerator ?? new SnowflakeIdGenerator(datacenterId: 1, workerId: 1);
        _shardCount = Math.Max(1, shardCount);

        for (int i = 0; i < _shardCount; i++)
        {
            _dbShards.Add(new ConcurrentDictionary<string, UrlRecord>());
        }
    }

    /// <summary>
    /// Shortens a long URL using Snowflake ID and Base62 encoding, or applies a custom alias.
    /// </summary>
    public async Task<UrlRecord> ShortenAsync(
        string longUrl,
        string? customAlias = null,
        TimeSpan? ttl = null,
        DateTime? customNow = null)
    {
        if (string.IsNullOrWhiteSpace(longUrl))
            throw new ArgumentException("Long URL cannot be null or empty.", nameof(longUrl));

        if (!Uri.TryCreate(longUrl, UriKind.Absolute, out _))
            throw new ArgumentException("Invalid URL format.", nameof(longUrl));

        var now = customNow ?? DateTime.UtcNow;
        long id;
        string shortCode;

        if (!string.IsNullOrWhiteSpace(customAlias))
        {
            shortCode = customAlias.Trim();
            // Verify alias uniqueness across all shards
            if (FindRecordInShards(shortCode) != null)
            {
                throw new InvalidOperationException($"Custom alias '{shortCode}' is already taken.");
            }
            id = _idGenerator.NextId();
        }
        else
        {
            id = _idGenerator.NextId();
            shortCode = Base62Codec.Encode(id);
        }

        var shardId = (int)(Math.Abs(id) % _shardCount);
        var record = new UrlRecord
        {
            Id = id,
            ShortCode = shortCode,
            LongUrl = longUrl,
            CreatedAtUtc = now,
            ExpiresAtUtc = ttl.HasValue ? now.Add(ttl.Value) : null,
            ShardId = shardId,
            ClickCount = 0
        };

        // 1. Write to database shard
        _dbShards[shardId][shortCode] = record;

        // 2. Populate Cache (Cache-Aside / Write-Through)
        _redisCache[shortCode] = record;

        return await Task.FromResult(record);
    }

    /// <summary>
    /// Resolves a short code to its original long URL, executing Cache-Aside lookup.
    /// Increments click analytics counter upon successful resolution.
    /// </summary>
    public async Task<string?> ResolveAsync(string shortCode, DateTime? customNow = null)
    {
        if (string.IsNullOrWhiteSpace(shortCode)) return null;

        var now = customNow ?? DateTime.UtcNow;

        // 1. Check Cache
        if (_redisCache.TryGetValue(shortCode, out var cachedRecord))
        {
            if (cachedRecord.ExpiresAtUtc.HasValue && now >= cachedRecord.ExpiresAtUtc.Value)
            {
                // Expired in cache
                _redisCache.TryRemove(shortCode, out _);
                return null;
            }

            Interlocked.Increment(ref _cacheHits);
            Interlocked.Increment(ref cachedRecord.ClickCount);
            return await Task.FromResult(cachedRecord.LongUrl);
        }

        // 2. Cache Miss: Query Database Shards
        Interlocked.Increment(ref _cacheMisses);
        var dbRecord = FindRecordInShards(shortCode);

        if (dbRecord == null) return null;

        // Check DB expiration
        if (dbRecord.ExpiresAtUtc.HasValue && now >= dbRecord.ExpiresAtUtc.Value)
        {
            // Expired in DB
            _dbShards[dbRecord.ShardId].TryRemove(shortCode, out _);
            return null;
        }

        // Cache-Aside replenishment
        _redisCache[shortCode] = dbRecord;
        Interlocked.Increment(ref dbRecord.ClickCount);

        return await Task.FromResult(dbRecord.LongUrl);
    }

    /// <summary>
    /// Retrieves full record and analytics for a short code.
    /// </summary>
    public UrlRecord? GetStats(string shortCode)
    {
        if (_redisCache.TryGetValue(shortCode, out var cached)) return cached;
        return FindRecordInShards(shortCode);
    }

    private UrlRecord? FindRecordInShards(string shortCode)
    {
        for (int i = 0; i < _shardCount; i++)
        {
            if (_dbShards[i].TryGetValue(shortCode, out var record))
            {
                return record;
            }
        }
        return null;
    }
}
