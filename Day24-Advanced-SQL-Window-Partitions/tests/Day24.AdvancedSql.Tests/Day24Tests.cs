using System;
using System.Collections.Generic;
using FluentAssertions;
using Xunit;

namespace Day24.AdvancedSql.Tests;

public class Day24Tests
{
    private record TestReading(string DeviceId, DateTime RecordedAt, decimal Value);

    // ==========================================
    // 1. WINDOW FUNCTIONS TESTS
    // ==========================================

    [Fact]
    public void WindowFunctions_RowNumber_GeneratesSequentialIndexPerPartition()
    {
        var t0 = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
        var readings = new List<TestReading>
        {
            new("DeviceA", t0.AddHours(1), 10m),
            new("DeviceA", t0.AddHours(2), 20m),
            new("DeviceB", t0.AddHours(1), 50m),
            new("DeviceB", t0.AddHours(2), 60m),
            new("DeviceA", t0.AddHours(3), 30m)
        };

        var evaluated = WindowFunctionsEngine.EvaluateWindowFunctions(
            readings,
            r => r.DeviceId,
            r => r.RecordedAt,
            r => r.Value
        );

        var deviceARows = evaluated.FindAll(x => x.Item.DeviceId == "DeviceA");
        deviceARows.ConvertAll(x => x.RowNumber).Should().Equal(1, 2, 3);

        var deviceBRows = evaluated.FindAll(x => x.Item.DeviceId == "DeviceB");
        deviceBRows.ConvertAll(x => x.RowNumber).Should().Equal(1, 2);
    }

    [Fact]
    public void WindowFunctions_RankAndDenseRank_HandleTiesCorrectly()
    {
        // Scores: 100, 90, 90, 80
        // Expected RANK():       1, 2, 2, 4 (skips 3 due to tie)
        // Expected DENSE_RANK(): 1, 2, 2, 3 (no gap)
        var scores = new List<TestReading>
        {
            new("G1", DateTime.UtcNow, 100m),
            new("G1", DateTime.UtcNow.AddSeconds(1), 90m),
            new("G1", DateTime.UtcNow.AddSeconds(2), 90m),
            new("G1", DateTime.UtcNow.AddSeconds(3), 80m),
        };

        var evaluated = WindowFunctionsEngine.EvaluateWindowFunctions(
            scores,
            r => r.DeviceId,
            r => r.Value,
            r => r.Value,
            ascending: false
        );

        evaluated.ConvertAll(x => x.Rank).Should().Equal(1, 2, 2, 4);
        evaluated.ConvertAll(x => x.DenseRank).Should().Equal(1, 2, 2, 3);
    }

    [Fact]
    public void WindowFunctions_LagAndLead_AccessAdjacentRowMetrics()
    {
        var t0 = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
        var series = new List<TestReading>
        {
            new("Dev1", t0, 10m),
            new("Dev1", t0.AddDays(1), 15m),
            new("Dev1", t0.AddDays(2), 25m)
        };

        var results = WindowFunctionsEngine.EvaluateWindowFunctions(
            series,
            r => r.DeviceId,
            r => r.RecordedAt,
            r => r.Value
        );

        // Row 1: Lag is null, Lead is 15
        results[0].LagValue.Should().BeNull();
        results[0].LeadValue.Should().Be(15m);

        // Row 2: Lag is 10, Lead is 25
        results[1].LagValue.Should().Be(10m);
        results[1].LeadValue.Should().Be(25m);

        // Row 3: Lag is 15, Lead is null
        results[2].LagValue.Should().Be(15m);
        results[2].LeadValue.Should().BeNull();
    }

    [Fact]
    public void WindowFunctions_RunningTotalAndMovingAverage_CalculateFrameCorrectly()
    {
        var t0 = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
        var series = new List<TestReading>
        {
            new("D1", t0, 10m),
            new("D1", t0.AddMinutes(1), 20m),
            new("D1", t0.AddMinutes(2), 30m),
            new("D1", t0.AddMinutes(3), 40m)
        };

        var results = WindowFunctionsEngine.EvaluateWindowFunctions(
            series,
            r => r.DeviceId,
            r => r.RecordedAt,
            r => r.Value,
            movingAverageWindow: 3
        );

        // Running totals: 10, 30, 60, 100
        results.ConvertAll(x => x.RunningTotal).Should().Equal(10m, 30m, 60m, 100m);

        // 3-point moving average:
        // row 0: avg(10) = 10
        // row 1: avg(10, 20) = 15
        // row 2: avg(10, 20, 30) = 20
        // row 3: avg(20, 30, 40) = 30
        results.ConvertAll(x => x.MovingAverage).Should().Equal(10m, 15m, 20m, 30m);
    }

    [Fact]
    public void WindowFunctions_GeneratesValidPostgreSqlQueryWithCte()
    {
        var sql = WindowFunctionsEngine.GeneratePostgreSqlAnalyticalQuery();
        sql.Should().Contain("WITH TimeSeriesDailyAggregates AS");
        sql.Should().Contain("ROW_NUMBER() OVER");
        sql.Should().Contain("DENSE_RANK() OVER");
        sql.Should().Contain("LAG(avg_daily_temp, 1) OVER");
        sql.Should().Contain("ROWS BETWEEN 6 PRECEDING AND CURRENT ROW");
    }

    // ==========================================
    // 2. TABLE PARTITIONING TESTS
    // ==========================================

    [Fact]
    public void PartitioningStrategy_PrunesIrrelevantPartitions_ForDateRange()
    {
        var strategy = new TablePartitioningStrategy();
        strategy.RegisterPartition("p_2026_08", new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc), 1_000_000, 120.0);
        strategy.RegisterPartition("p_2026_09", new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc), 1_200_000, 145.0);
        strategy.RegisterPartition("p_2026_10", new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc), 1_500_000, 180.0);

        // Query only targets October 2026
        var plan = strategy.SimulatePruning(
            new DateTime(2026, 10, 5, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 10, 10, 0, 0, 0, DateTimeKind.Utc)
        );

        plan.ScannedPartitions.Should().ContainSingle().Which.Should().Be("p_2026_10");
        plan.PrunedPartitions.Should().Equal("p_2026_08", "p_2026_09");
        plan.TotalScannedSizeMb.Should().Be(180.0);
        plan.TotalSavingsSizeMb.Should().Be(265.0);
    }

    [Fact]
    public void PartitioningStrategy_MultiMonthQuery_ScansOnlyRequiredPartitions()
    {
        var strategy = new TablePartitioningStrategy();
        strategy.RegisterPartition("p_2026_07", new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc), 100_000, 10.0);
        strategy.RegisterPartition("p_2026_08", new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc), 200_000, 20.0);
        strategy.RegisterPartition("p_2026_09", new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc), 300_000, 30.0);
        strategy.RegisterPartition("p_2026_10", new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc), 400_000, 40.0);

        // Query spans mid-August to mid-September
        var plan = strategy.SimulatePruning(
            new DateTime(2026, 8, 15, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 9, 15, 0, 0, 0, DateTimeKind.Utc)
        );

        plan.ScannedPartitions.Should().Equal("p_2026_08", "p_2026_09");
        plan.PrunedPartitions.Should().Equal("p_2026_07", "p_2026_10");
    }

    // ==========================================
    // 3. DSA: BINARY TREE MAXIMUM PATH SUM (LC #124)
    // ==========================================

    [Fact]
    public void LC124_MaxPathSum_PositiveTree_ReturnsCorrectSum()
    {
        var solver = new MaxPathSumSolver();
        // Tree:
        //     1
        //    / \
        //   2   3
        // Max path: 2 + 1 + 3 = 6
        var root = new TreeNode(1, new TreeNode(2), new TreeNode(3));
        solver.MaxPathSum(root).Should().Be(6);
    }

    [Fact]
    public void LC124_MaxPathSum_NegativeBranchesAndRoot_ClampsCorrectly()
    {
        var solver = new MaxPathSumSolver();
        // Tree:
        //       -10
        //       /  \
        //      9   20
        //         /  \
        //        15   7
        // Max path: 15 + 20 + 7 = 42
        var root = new TreeNode(-10,
            new TreeNode(9),
            new TreeNode(20, new TreeNode(15), new TreeNode(7))
        );
        solver.MaxPathSum(root).Should().Be(42);
    }

    [Fact]
    public void LC124_MaxPathSum_AllNegativeNodes_ReturnsLeastNegativeNode()
    {
        var solver = new MaxPathSumSolver();
        // Tree:
        //      -3
        //      / \
        //    -5  -1
        // Max path: -1 (must select at least 1 node)
        var root = new TreeNode(-3, new TreeNode(-5), new TreeNode(-1));
        solver.MaxPathSum(root).Should().Be(-1);
    }
}
