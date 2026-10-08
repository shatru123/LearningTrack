using System;
using System.Threading;
using System.Threading.Tasks;

namespace Day19.RedisRedLock;

/// <summary>
/// Configuration options for the RedLock distributed algorithm.
/// </summary>
public record RedLockOptions
{
    public TimeSpan LockTtl { get; init; } = TimeSpan.FromSeconds(10);
    public TimeSpan NodeTimeout { get; init; } = TimeSpan.FromMilliseconds(50);
    public double DriftFactor { get; init; } = 0.01; // 1% clock drift factor
    public TimeSpan ClockSkewTolerance { get; init; } = TimeSpan.FromMilliseconds(2);
    public TimeSpan AutoRenewalInterval { get; init; } = TimeSpan.FromSeconds(3);
    public int RetryCount { get; init; } = 3;
    public TimeSpan RetryDelay { get; init; } = TimeSpan.FromMilliseconds(50);
}

/// <summary>
/// Result of a RedLock acquisition attempt.
/// </summary>
public record RedLockAcquireResult(
    bool Success,
    string ResourceKey,
    string LockValue,
    TimeSpan ValidityTime,
    int AcquiredNodes,
    int TotalNodes,
    long FencingToken,
    string? ErrorMessage = null);

/// <summary>
/// Interface representing an independent Redis node in the multi-instance cluster.
/// Can be backed by StackExchange.Redis or simulated in-memory instances.
/// </summary>
public interface IRedisNodeInstance
{
    string NodeId { get; }
    bool IsHealthy { get; set; }
    TimeSpan SimulatedLatency { get; set; }

    Task<bool> TrySetLockAsync(string resource, string value, TimeSpan ttl, CancellationToken ct = default);
    Task<bool> TryReleaseLockAsync(string resource, string value, CancellationToken ct = default);
    Task<bool> TryExtendLockAsync(string resource, string value, TimeSpan ttl, CancellationToken ct = default);
    Task<string?> GetLockValueAsync(string resource, CancellationToken ct = default);
}

/// <summary>
/// Monotonically increasing fencing token generator to defend against zombie writes.
/// </summary>
public class FencingTokenGenerator
{
    private long _counter = 0;

    public long Next() => Interlocked.Increment(ref _counter);
    public long Current => Volatile.Read(ref _counter);
}

/// <summary>
/// Represents an active acquired lock lease handle with automated background heartbeat renewal.
/// Disposing this handle stops renewal and safely releases the multi-instance lock.
/// </summary>
public sealed class RedLockLeaseHandle : IAsyncDisposable
{
    private readonly RedLockEngine _engine;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task? _renewalTask;
    private int _disposed = 0;

    public RedLockAcquireResult LockInfo { get; }
    public bool IsActive => _disposed == 0 && !_cts.IsCancellationRequested;

    public RedLockLeaseHandle(RedLockEngine engine, RedLockAcquireResult lockInfo, TimeSpan renewalInterval)
    {
        _engine = engine;
        LockInfo = lockInfo;

        if (lockInfo.Success && renewalInterval > TimeSpan.Zero)
        {
            _renewalTask = StartRenewalLoopAsync(renewalInterval, _cts.Token);
        }
    }

    private async Task StartRenewalLoopAsync(TimeSpan interval, CancellationToken ct)
    {
        try
        {
            using var timer = new PeriodicTimer(interval);
            while (await timer.WaitForNextTickAsync(ct))
            {
                if (ct.IsCancellationRequested) break;

                bool renewed = await _engine.ExtendLockAsync(LockInfo.ResourceKey, LockInfo.LockValue, _engine.Options.LockTtl, ct);
                if (!renewed)
                {
                    // Failed to achieve quorum renewal; abort lease
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected upon disposal
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;

        _cts.Cancel();
        if (_renewalTask != null)
        {
            try { await _renewalTask; } catch { /* Ignore task cancellation */ }
        }
        _cts.Dispose();

        await _engine.ReleaseLockAsync(LockInfo.ResourceKey, LockInfo.LockValue);
    }
}
