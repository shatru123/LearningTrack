namespace Day48.CloudArchitecture.Tests;

using System;
using Day48.CloudArchitecture;
using Xunit;

public class Day48Tests
{
    [Fact]
    public void Test1_ActivePassiveRouter_RoutesToPrimaryWhenHealthy()
    {
        var router = new MultiRegionTrafficRouter("eastus", "westus2", RoutingMode.ActivePassiveWarmStandby);
        var primaryHealth = new RegionHealth("eastus", IsHealthy: true, LatencyMs: 25.0, ErrorRatePercent: 0.5);
        var secondaryHealth = new RegionHealth("westus2", IsHealthy: true, LatencyMs: 70.0, ErrorRatePercent: 0.1);

        string target = router.RouteTraffic(primaryHealth, secondaryHealth);

        Assert.Equal("eastus", target);
    }

    [Fact]
    public void Test2_ActivePassiveRouter_FailsOverToSecondaryOnHighErrorRate()
    {
        var router = new MultiRegionTrafficRouter("eastus", "westus2", RoutingMode.ActivePassiveWarmStandby);
        var primaryHealth = new RegionHealth("eastus", IsHealthy: true, LatencyMs: 25.0, ErrorRatePercent: 45.0);
        var secondaryHealth = new RegionHealth("westus2", IsHealthy: true, LatencyMs: 70.0, ErrorRatePercent: 0.2);

        string target = router.RouteTraffic(primaryHealth, secondaryHealth);

        Assert.Equal("westus2", target);
    }

    [Fact]
    public void Test3_ActiveActiveRouter_RoutesToLowestLatencyHealthyRegion()
    {
        var router = new MultiRegionTrafficRouter("eastus", "westus2", RoutingMode.ActiveActive);
        var primaryHealth = new RegionHealth("eastus", IsHealthy: true, LatencyMs: 80.0, ErrorRatePercent: 1.0);
        var secondaryHealth = new RegionHealth("westus2", IsHealthy: true, LatencyMs: 35.0, ErrorRatePercent: 0.8);

        string target = router.RouteTraffic(primaryHealth, secondaryHealth);

        Assert.Equal("westus2", target);
    }

    [Fact]
    public void Test4_EstimateDisasterRecoveryBounds_CalculatesRtoAndRpo()
    {
        var warmStandby = new MultiRegionTrafficRouter("eastus", "westus2", RoutingMode.ActivePassiveWarmStandby);
        var (rtoWarm, rpoWarm) = warmStandby.EstimateDisasterRecoveryBounds(replicationLagSec: 8.5);

        Assert.Equal(5, rtoWarm);
        Assert.Equal(9, rpoWarm);

        var activeActive = new MultiRegionTrafficRouter("eastus", "westus2", RoutingMode.ActiveActive);
        var (rtoActive, rpoActive) = activeActive.EstimateDisasterRecoveryBounds(replicationLagSec: 2.1);

        Assert.Equal(1, rtoActive);
        Assert.Equal(3, rpoActive);
    }

    [Fact]
    public void Test5_DatabaseReplicationLagTracker_EvaluatesStaleReadBound()
    {
        var tracker = new DatabaseReplicationLagTracker();
        var now = DateTime.UtcNow;

        tracker.RecordReplicationEvent(now.AddSeconds(-2), now); // 2000 ms lag

        Assert.False(tracker.IsReadConsistent(maxAcceptableStalenessMs: 500));
        Assert.True(tracker.IsReadConsistent(maxAcceptableStalenessMs: 3000));
    }

    [Fact]
    public void Test6_BlobStorageLifecycle_TransitionsTiersByAge()
    {
        Assert.Equal(StorageTier.Hot, BlobStorageLifecycleEngine.EvaluateTier(daysSinceModified: 10));
        Assert.Equal(StorageTier.Cool, BlobStorageLifecycleEngine.EvaluateTier(daysSinceModified: 45));
        Assert.Equal(StorageTier.Cold, BlobStorageLifecycleEngine.EvaluateTier(daysSinceModified: 120));
        Assert.Equal(StorageTier.Archive, BlobStorageLifecycleEngine.EvaluateTier(daysSinceModified: 200));

        double hotCost = BlobStorageLifecycleEngine.CalculateMonthlyStorageCost(1000, StorageTier.Hot);
        double archiveCost = BlobStorageLifecycleEngine.CalculateMonthlyStorageCost(1000, StorageTier.Archive);

        Assert.True(archiveCost < hotCost);
    }

    [Fact]
    public void Test7_CloudCostOptimization_ComputeSavingsCalculatedAccurately()
    {
        double onDemand = CloudCostOptimizationCalculator.ComputeMonthlyComputeCost(10, 0.50, "ondemand");
        double reserved1Yr = CloudCostOptimizationCalculator.ComputeMonthlyComputeCost(10, 0.50, "1year_reserved");
        double spot = CloudCostOptimizationCalculator.ComputeMonthlyComputeCost(10, 0.50, "spot");

        Assert.True(reserved1Yr < onDemand);
        Assert.True(spot < reserved1Yr);
    }

    [Fact]
    public void Test8_CloudCostOptimization_EgressNetworkCostReduction()
    {
        double internetCost = CloudCostOptimizationCalculator.CalculateEgressCost(10000, "internet");
        double privateEndpointCost = CloudCostOptimizationCalculator.CalculateEgressCost(10000, "private_endpoint");

        Assert.Equal(900.0, internetCost);
        Assert.Equal(0.0, privateEndpointCost);
    }

    [Theory]
    [InlineData(new int[] { 1, 5, 11, 5 }, true)]
    [InlineData(new int[] { 1, 2, 3, 5 }, false)]
    [InlineData(new int[] { 1, 2, 3, 4, 5, 6, 7 }, true)]
    [InlineData(new int[] { 2, 2, 2, 2 }, true)]
    public void Test9_PartitionEqualSubsetSum_1DAnd2DProduceIdenticalResults(int[] nums, bool expected)
    {
        bool res2D = PartitionEqualSubsetSumSolver.CanPartition2D(nums);
        bool res1D = PartitionEqualSubsetSumSolver.CanPartition1D(nums);

        Assert.Equal(expected, res2D);
        Assert.Equal(expected, res1D);
    }

    [Fact]
    public void Test10_PartitionEqualSubsetSum_EdgeCasesHandled()
    {
        Assert.False(PartitionEqualSubsetSumSolver.CanPartition1D(null!));
        Assert.False(PartitionEqualSubsetSumSolver.CanPartition1D(Array.Empty<int>()));
        Assert.False(PartitionEqualSubsetSumSolver.CanPartition1D(new int[] { 100 }));
        Assert.False(PartitionEqualSubsetSumSolver.CanPartition1D(new int[] { 1, 2, 4 })); // sum 7 is odd
    }
}
