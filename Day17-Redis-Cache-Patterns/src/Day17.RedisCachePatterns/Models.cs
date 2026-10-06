using System;

namespace Day17.RedisCachePatterns;

public record Product(
    int Id,
    string Name,
    decimal Price,
    int StockQuantity,
    DateTime UpdatedAtUtc);

public record CacheEntry<T>(
    T Value,
    DateTime ExpiresAtUtc,
    TimeSpan DeltaComputeTime);

public record CacheMetrics(
    int Hits,
    int Misses,
    int DatabaseLoads,
    int Writes);
