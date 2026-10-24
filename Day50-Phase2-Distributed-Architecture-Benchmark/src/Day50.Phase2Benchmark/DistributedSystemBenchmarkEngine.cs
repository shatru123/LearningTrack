namespace Day50.Phase2Benchmark;

using System;
using System.Collections.Generic;
using System.Linq;

public enum CircuitState
{
    Closed,
    Open,
    HalfOpen
}

public class ChaosFaultSimulator
{
    private int _consecutiveFailures;
    private readonly int _failureThreshold;
    public CircuitState State { get; private set; } = CircuitState.Closed;

    public ChaosFaultSimulator(int failureThreshold = 3)
    {
        _failureThreshold = failureThreshold;
    }

    public string ExecuteWithCircuitBreaker(Func<string> action, Func<string> fallback)
    {
        if (State == CircuitState.Open)
        {
            return fallback();
        }

        try
        {
            var result = action();
            if (State == CircuitState.HalfOpen)
            {
                State = CircuitState.Closed;
                _consecutiveFailures = 0;
            }
            return result;
        }
        catch
        {
            _consecutiveFailures++;
            if (_consecutiveFailures >= _failureThreshold)
            {
                State = CircuitState.Open;
            }
            return fallback();
        }
    }

    public void Reset()
    {
        State = CircuitState.Closed;
        _consecutiveFailures = 0;
    }

    public void TransitionToHalfOpen()
    {
        State = CircuitState.HalfOpen;
    }
}

public class TransactionalOutboxBuffer
{
    private readonly List<string> _pendingMessages = new();
    private readonly List<string> _publishedMessages = new();

    public void StoreOutbox(string message)
    {
        _pendingMessages.Add(message);
    }

    public int DrainPending(bool brokerAvailable)
    {
        if (!brokerAvailable) return 0;

        int count = _pendingMessages.Count;
        _publishedMessages.AddRange(_pendingMessages);
        _pendingMessages.Clear();
        return count;
    }

    public int PendingCount => _pendingMessages.Count;
    public int PublishedCount => _publishedMessages.Count;
}

public class LatencyProfiler
{
    public static (double p50, double p95, double p99) CalculatePercentiles(List<double> latenciesMs)
    {
        if (latenciesMs == null || latenciesMs.Count == 0) return (0, 0, 0);

        var sorted = latenciesMs.OrderBy(x => x).ToList();
        int n = sorted.Count;

        double GetPercentile(double p)
        {
            int idx = (int)Math.Ceiling(p * n) - 1;
            idx = Math.Clamp(idx, 0, n - 1);
            return sorted[idx];
        }

        return (GetPercentile(0.50), GetPercentile(0.95), GetPercentile(0.99));
    }
}

public class Phase2AuditScorecard
{
    private readonly Dictionary<string, bool> _checkpoints = new();

    public void RegisterCheckpoint(string category, bool passed)
    {
        _checkpoints[category] = passed;
    }

    public double ComputeReadinessPercent()
    {
        if (_checkpoints.Count == 0) return 0.0;
        int passed = _checkpoints.Values.Count(v => v);
        return (double)passed / _checkpoints.Count * 100.0;
    }

    public bool IsCertified() => ComputeReadinessPercent() >= 100.0;
}
