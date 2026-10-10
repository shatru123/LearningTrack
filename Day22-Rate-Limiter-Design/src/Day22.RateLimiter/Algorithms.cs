using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Day22.RateLimiter;

/// <summary>
/// Token Bucket Algorithm:
/// Allows bursts up to capacity while enforcing average throughput via constant token refill.
/// </summary>
public class TokenBucketRateLimiter : IRateLimiter
{
    private readonly double _capacity;
    private readonly double _refillRatePerSecond;
    private readonly ConcurrentDictionary<string, (double tokens, DateTime lastRefill)> _buckets = new();
    private readonly object _lock = new();

    public double Capacity => _capacity;
    public double RefillRatePerSecond => _refillRatePerSecond;

    public TokenBucketRateLimiter(double capacity, double refillRatePerSecond)
    {
        if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be positive.");
        if (refillRatePerSecond <= 0) throw new ArgumentOutOfRangeException(nameof(refillRatePerSecond), "Refill rate must be positive.");

        _capacity = capacity;
        _refillRatePerSecond = refillRatePerSecond;
    }

    public RateLimitResult ShouldAllow(string key, int tokensRequested = 1, DateTime? customNow = null)
    {
        var now = customNow ?? DateTime.UtcNow;

        lock (_lock)
        {
            var (tokens, lastRefill) = _buckets.GetOrAdd(key, _ => (_capacity, now));

            // Refill tokens based on elapsed duration
            var elapsedSeconds = Math.Max(0, (now - lastRefill).TotalSeconds);
            var refilledTokens = Math.Min(_capacity, tokens + elapsedSeconds * _refillRatePerSecond);

            var resetSeconds = (_capacity - refilledTokens) / _refillRatePerSecond;
            var resetTimeUtc = now.AddSeconds(Math.Max(0, resetSeconds));

            if (refilledTokens >= tokensRequested)
            {
                var remaining = refilledTokens - tokensRequested;
                _buckets[key] = (remaining, now);
                return RateLimitResult.Allowed((long)_capacity, (long)remaining, resetTimeUtc);
            }
            else
            {
                var neededTokens = tokensRequested - refilledTokens;
                var retryAfterSeconds = neededTokens / _refillRatePerSecond;
                _buckets[key] = (refilledTokens, now);
                return RateLimitResult.Rejected((long)_capacity, TimeSpan.FromSeconds(retryAfterSeconds), resetTimeUtc);
            }
        }
    }
}

/// <summary>
/// Leaky Bucket Algorithm:
/// Buffers incoming requests and processes them at a smooth, constant outflow rate.
/// Excess requests overflowing bucket capacity are immediately discarded.
/// </summary>
public class LeakyBucketRateLimiter : IRateLimiter
{
    private readonly double _capacity;
    private readonly double _leakRatePerSecond;
    private readonly ConcurrentDictionary<string, (double waterLevel, DateTime lastLeak)> _buckets = new();
    private readonly object _lock = new();

    public LeakyBucketRateLimiter(double capacity, double leakRatePerSecond)
    {
        if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be positive.");
        if (leakRatePerSecond <= 0) throw new ArgumentOutOfRangeException(nameof(leakRatePerSecond), "Leak rate must be positive.");

        _capacity = capacity;
        _leakRatePerSecond = leakRatePerSecond;
    }

    public RateLimitResult ShouldAllow(string key, int tokensRequested = 1, DateTime? customNow = null)
    {
        var now = customNow ?? DateTime.UtcNow;

        lock (_lock)
        {
            var (waterLevel, lastLeak) = _buckets.GetOrAdd(key, _ => (0.0, now));

            // Leak water based on elapsed time
            var elapsedSeconds = Math.Max(0, (now - lastLeak).TotalSeconds);
            var leaked = elapsedSeconds * _leakRatePerSecond;
            var currentWater = Math.Max(0.0, waterLevel - leaked);

            var resetSeconds = currentWater / _leakRatePerSecond;
            var resetTimeUtc = now.AddSeconds(resetSeconds);

            if (currentWater + tokensRequested <= _capacity)
            {
                var newWater = currentWater + tokensRequested;
                _buckets[key] = (newWater, now);
                var remaining = (long)(_capacity - newWater);
                return RateLimitResult.Allowed((long)_capacity, remaining, resetTimeUtc);
            }
            else
            {
                var overflow = (currentWater + tokensRequested) - _capacity;
                var retryAfter = TimeSpan.FromSeconds(overflow / _leakRatePerSecond);
                _buckets[key] = (currentWater, now);
                return RateLimitResult.Rejected((long)_capacity, retryAfter, resetTimeUtc);
            }
        }
    }
}

/// <summary>
/// Fixed Window Counter Algorithm:
/// Divides time into fixed discrete slots and increments a counter per window.
/// Vulnerable to boundary burst (2x the configured limit across window transitions).
/// </summary>
public class FixedWindowCounterRateLimiter : IRateLimiter
{
    private readonly long _limit;
    private readonly TimeSpan _window;
    private readonly ConcurrentDictionary<string, (long windowIndex, long count)> _windows = new();
    private readonly object _lock = new();

    public FixedWindowCounterRateLimiter(long limit, TimeSpan window)
    {
        if (limit <= 0) throw new ArgumentOutOfRangeException(nameof(limit), "Limit must be positive.");
        if (window <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(window), "Window must be positive.");

        _limit = limit;
        _window = window;
    }

    public RateLimitResult ShouldAllow(string key, int tokensRequested = 1, DateTime? customNow = null)
    {
        var now = customNow ?? DateTime.UtcNow;
        var currentWindowIndex = now.Ticks / _window.Ticks;
        var currentWindowStart = new DateTime(currentWindowIndex * _window.Ticks, DateTimeKind.Utc);
        var resetTimeUtc = currentWindowStart.Add(_window);

        lock (_lock)
        {
            var (windowIndex, count) = _windows.GetOrAdd(key, _ => (currentWindowIndex, 0));

            // If entering a new window, reset count
            if (windowIndex != currentWindowIndex)
            {
                windowIndex = currentWindowIndex;
                count = 0;
            }

            if (count + tokensRequested <= _limit)
            {
                count += tokensRequested;
                _windows[key] = (windowIndex, count);
                var remaining = _limit - count;
                return RateLimitResult.Allowed(_limit, remaining, resetTimeUtc);
            }
            else
            {
                var retryAfter = resetTimeUtc - now;
                return RateLimitResult.Rejected(_limit, retryAfter, resetTimeUtc);
            }
        }
    }
}

/// <summary>
/// Sliding Window Log Algorithm:
/// Stores precise timestamps of each request (equivalent to Redis Sorted Set ZADD/ZREMRANGEBYSCORE).
/// Provides 100% accuracy, but memory scales with request count O(M).
/// </summary>
public class SlidingWindowLogRateLimiter : IRateLimiter
{
    private readonly long _limit;
    private readonly TimeSpan _window;
    private readonly ConcurrentDictionary<string, List<DateTime>> _logs = new();
    private readonly object _lock = new();

    public SlidingWindowLogRateLimiter(long limit, TimeSpan window)
    {
        if (limit <= 0) throw new ArgumentOutOfRangeException(nameof(limit), "Limit must be positive.");
        if (window <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(window), "Window must be positive.");

        _limit = limit;
        _window = window;
    }

    public RateLimitResult ShouldAllow(string key, int tokensRequested = 1, DateTime? customNow = null)
    {
        var now = customNow ?? DateTime.UtcNow;
        var windowStart = now - _window;
        var resetTimeUtc = now.Add(_window);

        lock (_lock)
        {
            var log = _logs.GetOrAdd(key, _ => new List<DateTime>());

            // Purge expired entries older than (now - window)
            log.RemoveAll(t => t <= windowStart);

            if (log.Count + tokensRequested <= _limit)
            {
                for (int i = 0; i < tokensRequested; i++)
                {
                    log.Add(now);
                }
                var remaining = _limit - log.Count;
                var earliest = log.Count > 0 ? log[0] : now;
                var reset = earliest.Add(_window);
                return RateLimitResult.Allowed(_limit, remaining, reset);
            }
            else
            {
                var oldestInWindow = log[0];
                var retryAfter = (oldestInWindow + _window) - now;
                return RateLimitResult.Rejected(_limit, retryAfter, oldestInWindow.Add(_window));
            }
        }
    }
}

/// <summary>
/// Sliding Window Counter Algorithm (Cloudflare Hybrid):
/// Combines the current window counter with the weighted proportion of the previous window counter.
/// Memory footprint is strictly O(1) (only 2 counters), while eliminating 2x boundary spikes.
/// Formula: EstimatedCount = CurrentCount + PreviousCount * (1 - ElapsedInCurrent / WindowSize)
/// </summary>
public class SlidingWindowCounterRateLimiter : IRateLimiter
{
    private readonly long _limit;
    private readonly TimeSpan _window;
    private readonly ConcurrentDictionary<string, ClientWindowState> _clientStates = new();
    private readonly object _lock = new();

    private class ClientWindowState
    {
        public long CurrentWindowIndex { get; set; }
        public long CurrentCount { get; set; }
        public long PreviousCount { get; set; }
    }

    public SlidingWindowCounterRateLimiter(long limit, TimeSpan window)
    {
        if (limit <= 0) throw new ArgumentOutOfRangeException(nameof(limit), "Limit must be positive.");
        if (window <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(window), "Window must be positive.");

        _limit = limit;
        _window = window;
    }

    public RateLimitResult ShouldAllow(string key, int tokensRequested = 1, DateTime? customNow = null)
    {
        var now = customNow ?? DateTime.UtcNow;
        var currentWindowIndex = now.Ticks / _window.Ticks;
        var currentWindowStart = new DateTime(currentWindowIndex * _window.Ticks, DateTimeKind.Utc);
        var resetTimeUtc = currentWindowStart.Add(_window);

        lock (_lock)
        {
            var state = _clientStates.GetOrAdd(key, _ => new ClientWindowState
            {
                CurrentWindowIndex = currentWindowIndex,
                CurrentCount = 0,
                PreviousCount = 0
            });

            // Adjust window transitions
            if (currentWindowIndex > state.CurrentWindowIndex)
            {
                var diff = currentWindowIndex - state.CurrentWindowIndex;
                if (diff == 1)
                {
                    state.PreviousCount = state.CurrentCount;
                }
                else
                {
                    state.PreviousCount = 0;
                }
                state.CurrentCount = 0;
                state.CurrentWindowIndex = currentWindowIndex;
            }

            // Calculate overlap weight of the previous window
            var elapsedInCurrent = (now - currentWindowStart).TotalMilliseconds;
            var windowTotalMs = _window.TotalMilliseconds;
            var weightOfPrevious = Math.Max(0.0, 1.0 - (elapsedInCurrent / windowTotalMs));

            var estimatedCount = state.CurrentCount + (state.PreviousCount * weightOfPrevious);

            if (estimatedCount + tokensRequested <= _limit)
            {
                state.CurrentCount += tokensRequested;
                var remaining = Math.Max(0, _limit - (long)Math.Ceiling(estimatedCount + tokensRequested));
                return RateLimitResult.Allowed(_limit, remaining, resetTimeUtc);
            }
            else
            {
                var retryAfter = resetTimeUtc - now;
                return RateLimitResult.Rejected(_limit, retryAfter, resetTimeUtc);
            }
        }
    }
}
