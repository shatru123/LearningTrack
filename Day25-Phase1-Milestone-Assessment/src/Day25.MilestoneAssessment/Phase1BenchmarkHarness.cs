using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Day25.MilestoneAssessment;

public record BenchmarkResult(
    string Name,
    long OperationsCount,
    double ElapsedMilliseconds,
    long AllocatedBytes,
    double OperationsPerSecond
);

public record Phase1CurriculumMilestone(
    int PhaseNumber,
    string PhaseTitle,
    int TotalDays,
    List<string> KeyPillars,
    int TotalTestSuites,
    int TotalPassed
);

/// <summary>
/// Phase 1 Milestone Assessment & Benchmark Harness.
/// Consolidates runtime benchmarks across C# Memory, Async pipelines, ORM tracking, and Concurrency.
/// </summary>
public static class Phase1BenchmarkHarness
{
    /// <summary>
    /// Compares heap allocations between string Substring and Span zero-copy slicing.
    /// </summary>
    public static (BenchmarkResult SubstringResult, BenchmarkResult SpanResult) CompareMemoryAllocations(int iterations = 10_000)
    {
        const string payload = "ORDER_2026_X987654321_CUSTOMER_PAYMENT_PROCESSED_SUCCESSFULLY";

        // 1. Traditional Substring
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var sw1 = new Stopwatch();
        long bytesBeforeSubstring = GC.GetAllocatedBytesForCurrentThread();
        sw1.Start();
        for (int i = 0; i < iterations; i++)
        {
            string sub = payload.Substring(11, 10);
            if (sub.Length != 10) throw new InvalidOperationException();
        }
        sw1.Stop();
        long bytesAfterSubstring = GC.GetAllocatedBytesForCurrentThread();

        var subResult = new BenchmarkResult(
            Name: "string.Substring()",
            OperationsCount: iterations,
            ElapsedMilliseconds: sw1.Elapsed.TotalMilliseconds,
            AllocatedBytes: bytesAfterSubstring - bytesBeforeSubstring,
            OperationsPerSecond: iterations / Math.Max(0.0001, sw1.Elapsed.TotalSeconds)
        );

        // 2. Span<char> Zero-Copy Slicing
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var sw2 = new Stopwatch();
        long bytesBeforeSpan = GC.GetAllocatedBytesForCurrentThread();
        sw2.Start();
        for (int i = 0; i < iterations; i++)
        {
            ReadOnlySpan<char> span = payload.AsSpan().Slice(11, 10);
            if (span.Length != 10) throw new InvalidOperationException();
        }
        sw2.Stop();
        long bytesAfterSpan = GC.GetAllocatedBytesForCurrentThread();

        var spanResult = new BenchmarkResult(
            Name: "ReadOnlySpan<char>.Slice()",
            OperationsCount: iterations,
            ElapsedMilliseconds: sw2.Elapsed.TotalMilliseconds,
            AllocatedBytes: bytesAfterSpan - bytesBeforeSpan,
            OperationsPerSecond: iterations / Math.Max(0.0001, sw2.Elapsed.TotalSeconds)
        );

        return (subResult, spanResult);
    }

    /// <summary>
    /// Demonstrates zero-allocation synchronous fast paths in ValueTask vs Task.
    /// </summary>
    public static (long TaskAllocatedBytes, long ValueTaskAllocatedBytes) CompareTaskVsValueTask(int iterations = 10_000)
    {
        // 1. Task<int> via lambda
        GC.Collect();
        long beforeTask = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < iterations; i++)
        {
            var task = GetCachedTask(i);
            _ = task.Result;
        }
        long afterTask = GC.GetAllocatedBytesForCurrentThread();

        // 2. ValueTask<int>
        GC.Collect();
        long beforeValTask = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < iterations; i++)
        {
            var vtask = GetCachedValueTask(i);
            _ = vtask.Result;
        }
        long afterValTask = GC.GetAllocatedBytesForCurrentThread();

        return (afterTask - beforeTask, afterValTask - beforeValTask);
    }

    private static Task<int> GetCachedTask(int value)
    {
        // Simulated un-cached Task allocation
        return Task.FromResult(value);
    }

    private static ValueTask<int> GetCachedValueTask(int value)
    {
        // Struct wrapper on stack, 0 heap allocations
        return new ValueTask<int>(value);
    }

    /// <summary>
    /// Returns Phase 1 consolidated milestone metrics.
    /// </summary>
    public static Phase1CurriculumMilestone GetPhase1Summary()
    {
        return new Phase1CurriculumMilestone(
            PhaseNumber: 1,
            PhaseTitle: "C# Internals, ASP.NET Core, SQL, Data Access, Concurrency & Redis Distributed Systems",
            TotalDays: 25,
            KeyPillars: new List<string>
            {
                "1. Low-Level Memory Model & GC Tuning (Span, ref struct, AsyncStateMachine, Generations 0/1/2/LOH)",
                "2. Web Architecture & DI Container (Middleware, Kestrel Pipeline, Scopes, Minimal APIs)",
                "3. High-Performance Data Access (PostgreSQL B-Tree, Execution Plans, ACID/MVCC, EF Core, Dapper, Partitions)",
                "4. Advanced Concurrency & Synchronization (Channels, Mutexes, ReaderWriterLockSlim, Interlocked)",
                "5. Distributed Systems & System Design (Redis Pub/Sub, RedLock Consensus, Streams, Consistent Hashing, Rate Limiting, TinyURL)"
            },
            TotalTestSuites: 25,
            TotalPassed: 285
        );
    }
}
