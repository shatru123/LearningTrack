using Polly;
using Polly.CircuitBreaker;
using Polly.Hedging;
using Polly.Retry;
using Polly.Timeout;

namespace Day28.ResilientHttpRefitPolly;

/// <summary>
/// State tracker for Circuit Breaker transitions.
/// </summary>
public class CircuitBreakerStateMonitor
{
    public CircuitState CurrentState { get; private set; } = CircuitState.Closed;
    public int OpenTransitionCount { get; private set; }
    public int HalfOpenTransitionCount { get; private set; }
    public int ClosedTransitionCount { get; private set; }

    public void RecordOpened()
    {
        CurrentState = CircuitState.Open;
        OpenTransitionCount++;
    }

    public void RecordHalfOpened()
    {
        CurrentState = CircuitState.HalfOpen;
        HalfOpenTransitionCount++;
    }

    public void RecordClosed()
    {
        CurrentState = CircuitState.Closed;
        ClosedTransitionCount++;
    }
}

/// <summary>
/// Configures production-grade Polly v8 Resilience Pipelines combining:
/// 1. Timeout (Total & Attempt)
/// 2. Retry with Exponential Backoff & Decorrelated Jitter
/// 3. Advanced Circuit Breaker
/// 4. Hedging (Speculative Parallel Execution)
/// </summary>
public class ResiliencePipelineFactory
{
    /// <summary>
    /// Builds standard HTTP client pipeline: Timeout -> Retry -> Circuit Breaker.
    /// </summary>
    public static ResiliencePipeline CreateStandardHttpPipeline(
        CircuitBreakerStateMonitor? monitor = null,
        int maxRetries = 3,
        TimeSpan? initialDelay = null,
        TimeSpan? attemptTimeout = null,
        TimeSpan? circuitBreakDuration = null)
    {
        var delay = initialDelay ?? TimeSpan.FromMilliseconds(50);
        var timeout = attemptTimeout ?? TimeSpan.FromSeconds(2);
        var breakDuration = circuitBreakDuration ?? TimeSpan.FromSeconds(1);

        var builder = new ResiliencePipelineBuilder()
            .AddTimeout(new TimeoutStrategyOptions
            {
                Timeout = timeout
            });

        if (maxRetries > 0)
        {
            builder.AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = maxRetries,
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                Delay = delay,
                ShouldHandle = new PredicateBuilder()
                    .Handle<HttpRequestException>()
                    .Handle<TimeoutRejectedException>()
            });
        }

        return builder
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                FailureRatio = 0.5,
                MinimumThroughput = 4,
                SamplingDuration = TimeSpan.FromSeconds(10),
                BreakDuration = breakDuration,
                ShouldHandle = new PredicateBuilder()
                    .Handle<HttpRequestException>()
                    .Handle<TimeoutRejectedException>(),
                OnOpened = args =>
                {
                    monitor?.RecordOpened();
                    return ValueTask.CompletedTask;
                },
                OnHalfOpened = args =>
                {
                    monitor?.RecordHalfOpened();
                    return ValueTask.CompletedTask;
                },
                OnClosed = args =>
                {
                    monitor?.RecordClosed();
                    return ValueTask.CompletedTask;
                }
            })
            .Build();
    }

    /// <summary>
    /// Builds Hedged resilience pipeline for latency-sensitive read operations.
    /// If primary attempt doesn't complete within delay threshold, a concurrent speculative request is issued.
    /// </summary>
    public static ResiliencePipeline<TResult> CreateHedgedPipeline<TResult>(
        TimeSpan hedgingDelay,
        int maxHedgedAttempts = 2)
    {
        return new ResiliencePipelineBuilder<TResult>()
            .AddHedging(new HedgingStrategyOptions<TResult>
            {
                MaxHedgedAttempts = maxHedgedAttempts,
                Delay = hedgingDelay,
                ShouldHandle = new PredicateBuilder<TResult>()
                    .Handle<Exception>()
            })
            .Build();
    }
}
