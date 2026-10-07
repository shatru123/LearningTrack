using System;
using System.Collections.Generic;

namespace Day18.RedisDataStructures;

public record UserProfileHash(
    string UserId,
    string Username,
    string Email,
    int LoginCount,
    DateTime LastLoginUtc);

public record LeaderboardScore(
    string PlayerId,
    double Score,
    long Rank);

public enum EvictionPolicy
{
    NoEviction,
    AllKeysLru,
    VolatileLru,
    AllKeysLfu,
    VolatileLfu,
    VolatileTtl
}

public class CacheItemMetadata
{
    public string Key { get; set; } = string.Empty;
    public object Value { get; set; } = string.Empty;
    public int SizeBytes { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }
    public DateTime LastAccessedUtc { get; set; } = DateTime.UtcNow;
    
    // Morris Logarithmic Counter for LFU emulation (0-255)
    public byte LfuCounter { get; set; } = 5; // Initial counter
    public DateTime LastDecayTimeUtc { get; set; } = DateTime.UtcNow;
}
