using System;

namespace Day22.RateLimiter;

/// <summary>
/// Encapsulates the evaluation outcome of a rate limiting check.
/// </summary>
public record RateLimitResult
{
    public bool IsAllowed { get; init; }
    public long Limit { get; init; }
    public long Remaining { get; init; }
    public TimeSpan RetryAfter { get; init; }
    public DateTime ResetTimeUtc { get; init; }

    public static RateLimitResult Allowed(long limit, long remaining, DateTime resetTimeUtc) =>
        new()
        {
            IsAllowed = true,
            Limit = limit,
            Remaining = Math.Max(0, remaining),
            RetryAfter = TimeSpan.Zero,
            ResetTimeUtc = resetTimeUtc
        };

    public static RateLimitResult Rejected(long limit, TimeSpan retryAfter, DateTime resetTimeUtc) =>
        new()
        {
            IsAllowed = false,
            Limit = limit,
            Remaining = 0,
            RetryAfter = retryAfter <= TimeSpan.Zero ? TimeSpan.FromSeconds(1) : retryAfter,
            ResetTimeUtc = resetTimeUtc
        };
}

/// <summary>
/// Defines policy constraints for rate limiting.
/// </summary>
public record RateLimiterRule(long CapacityOrLimit, TimeSpan Window);

/// <summary>
/// Common contract for rate limiter algorithms.
/// </summary>
public interface IRateLimiter
{
    RateLimitResult ShouldAllow(string key, int tokensRequested = 1, DateTime? customNow = null);
}
