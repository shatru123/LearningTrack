namespace Day50.Phase2Benchmark.Tests;

using System;
using System.Collections.Generic;
using Day50.Phase2Benchmark;
using Xunit;

public class Day50Tests
{
    [Fact]
    public void Test1_ChaosSimulator_CircuitBreakerTripsAndFallsBack()
    {
        var chaos = new ChaosFaultSimulator(failureThreshold: 2);

        // First 2 failures
        string r1 = chaos.ExecuteWithCircuitBreaker(() => throw new Exception("Downstream timeout"), () => "fallback-cached");
        string r2 = chaos.ExecuteWithCircuitBreaker(() => throw new Exception("Downstream timeout"), () => "fallback-cached");

        Assert.Equal("fallback-cached", r1);
        Assert.Equal("fallback-cached", r2);
        Assert.Equal(CircuitState.Open, chaos.State);

        // Circuit open: fast fallback without invoking action
        bool actionInvoked = false;
        string r3 = chaos.ExecuteWithCircuitBreaker(() => { actionInvoked = true; return "ok"; }, () => "fast-fallback");

        Assert.False(actionInvoked);
        Assert.Equal("fast-fallback", r3);
    }

    [Fact]
    public void Test2_OutboxBuffer_RetainsMessagesDuringBrokerDowntimeAndDrains()
    {
        var outbox = new TransactionalOutboxBuffer();

        outbox.StoreOutbox("event-order-created");
        outbox.StoreOutbox("event-payment-processed");

        // Broker down
        int drained0 = outbox.DrainPending(brokerAvailable: false);
        Assert.Equal(0, drained0);
        Assert.Equal(2, outbox.PendingCount);

        // Broker recovered
        int drainedRecovered = outbox.DrainPending(brokerAvailable: true);
        Assert.Equal(2, drainedRecovered);
        Assert.Equal(0, outbox.PendingCount);
        Assert.Equal(2, outbox.PublishedCount);
    }

    [Fact]
    public void Test3_LatencyProfiler_ComputesPercentilesAccurately()
    {
        var latencies = new List<double>();
        for (int i = 1; i <= 100; i++)
        {
            latencies.Add(i * 2.0); // 2, 4, ..., 200 ms
        }

        var (p50, p95, p99) = LatencyProfiler.CalculatePercentiles(latencies);

        Assert.Equal(100.0, p50); // 50th item
        Assert.Equal(190.0, p95); // 95th item
        Assert.Equal(198.0, p99); // 99th item
    }

    [Fact]
    public void Test4_AuditScorecard_Requires100PercentForCertification()
    {
        var scorecard = new Phase2AuditScorecard();
        scorecard.RegisterCheckpoint("Microservices", true);
        scorecard.RegisterCheckpoint("YARP", true);
        scorecard.RegisterCheckpoint("Polly", true);
        scorecard.RegisterCheckpoint("RabbitMQ", true);
        scorecard.RegisterCheckpoint("Outbox", true);
        scorecard.RegisterCheckpoint("Sagas", true);
        scorecard.RegisterCheckpoint("OpenTelemetry", true);
        scorecard.RegisterCheckpoint("Kubernetes", false); // 1 failure

        Assert.Equal(87.5, scorecard.ComputeReadinessPercent());
        Assert.False(scorecard.IsCertified());

        scorecard.RegisterCheckpoint("Kubernetes", true);
        Assert.Equal(100.0, scorecard.ComputeReadinessPercent());
        Assert.True(scorecard.IsCertified());
    }

    [Fact]
    public void Test5_MergeKLists_MinHeap_MergesMultipleSortedLists()
    {
        var l1 = MergeKSortedListsSolver.FromArray(new[] { 1, 4, 5 });
        var l2 = MergeKSortedListsSolver.FromArray(new[] { 1, 3, 4 });
        var l3 = MergeKSortedListsSolver.FromArray(new[] { 2, 6 });

        var merged = MergeKSortedListsSolver.MergeKListsMinHeap(new[] { l1, l2, l3 });
        var actual = MergeKSortedListsSolver.ToArray(merged);

        Assert.Equal(new[] { 1, 1, 2, 3, 4, 4, 5, 6 }, actual);
    }

    [Fact]
    public void Test6_MergeKLists_DivideAndConquer_MergesMultipleSortedLists()
    {
        var l1 = MergeKSortedListsSolver.FromArray(new[] { 1, 4, 5 });
        var l2 = MergeKSortedListsSolver.FromArray(new[] { 1, 3, 4 });
        var l3 = MergeKSortedListsSolver.FromArray(new[] { 2, 6 });

        var merged = MergeKSortedListsSolver.MergeKListsDivideAndConquer(new[] { l1, l2, l3 });
        var actual = MergeKSortedListsSolver.ToArray(merged);

        Assert.Equal(new[] { 1, 1, 2, 3, 4, 4, 5, 6 }, actual);
    }

    [Fact]
    public void Test7_MergeKLists_MinHeapAndDivideConquer_ProduceIdenticalOutputs()
    {
        var listsA = new[]
        {
            MergeKSortedListsSolver.FromArray(new[] { -10, -5, 0 }),
            MergeKSortedListsSolver.FromArray(new[] { -8, 2, 10 }),
            MergeKSortedListsSolver.FromArray(new[] { -1, 3, 7 }),
            MergeKSortedListsSolver.FromArray(new[] { 1, 2, 3 })
        };

        var listsB = new[]
        {
            MergeKSortedListsSolver.FromArray(new[] { -10, -5, 0 }),
            MergeKSortedListsSolver.FromArray(new[] { -8, 2, 10 }),
            MergeKSortedListsSolver.FromArray(new[] { -1, 3, 7 }),
            MergeKSortedListsSolver.FromArray(new[] { 1, 2, 3 })
        };

        var resHeap = MergeKSortedListsSolver.ToArray(MergeKSortedListsSolver.MergeKListsMinHeap(listsA));
        var resDac = MergeKSortedListsSolver.ToArray(MergeKSortedListsSolver.MergeKListsDivideAndConquer(listsB));

        Assert.Equal(resHeap, resDac);
    }

    [Fact]
    public void Test8_MergeKLists_EmptyAndNullInputs_Handled()
    {
        Assert.Null(MergeKSortedListsSolver.MergeKListsMinHeap(null!));
        Assert.Null(MergeKSortedListsSolver.MergeKListsMinHeap(Array.Empty<ListNode?>()));
        Assert.Null(MergeKSortedListsSolver.MergeKListsMinHeap(new ListNode?[] { null, null }));

        Assert.Null(MergeKSortedListsSolver.MergeKListsDivideAndConquer(null!));
        Assert.Null(MergeKSortedListsSolver.MergeKListsDivideAndConquer(Array.Empty<ListNode?>()));
    }

    [Fact]
    public void Test9_MergeKLists_SingleList_ReturnsIntact()
    {
        var single = MergeKSortedListsSolver.FromArray(new[] { 10, 20, 30 });
        var merged = MergeKSortedListsSolver.MergeKListsMinHeap(new[] { single });
        var actual = MergeKSortedListsSolver.ToArray(merged);

        Assert.Equal(new[] { 10, 20, 30 }, actual);
    }

    [Fact]
    public void Test10_MergeKLists_UnequalListLengths()
    {
        var l1 = MergeKSortedListsSolver.FromArray(new[] { 1 });
        var l2 = MergeKSortedListsSolver.FromArray(new[] { 2, 3, 4, 5, 6, 7 });
        var l3 = MergeKSortedListsSolver.FromArray(new[] { 0, 8 });

        var merged = MergeKSortedListsSolver.MergeKListsMinHeap(new[] { l1, l2, l3 });
        var actual = MergeKSortedListsSolver.ToArray(merged);

        Assert.Equal(new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8 }, actual);
    }
}
