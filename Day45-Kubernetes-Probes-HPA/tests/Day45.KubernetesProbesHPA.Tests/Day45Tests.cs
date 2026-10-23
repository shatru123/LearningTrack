using System;
using System.IO;
using Xunit;

namespace Day45.KubernetesProbesHPA.Tests;

public class Day45Tests
{
    private readonly KubernetesHpaAndProbeEngine _engine;

    public Day45Tests()
    {
        _engine = new KubernetesHpaAndProbeEngine();
    }

    [Fact]
    public void Test01_LivenessProbe_TriggersRestartOnlyAfterFailureThreshold()
    {
        var liveness = new ProbeState(0, 0, true);
        var readiness = new ProbeState(0, 0, true);

        // First failure: no restart
        var (restart1, inEndpoints1) = _engine.EvaluateProbes(ProbeResult.Failure, ProbeResult.Success, ref liveness, ref readiness, 3, 2);
        Assert.False(restart1);
        Assert.True(inEndpoints1);

        // Second failure: no restart
        var (restart2, _) = _engine.EvaluateProbes(ProbeResult.Failure, ProbeResult.Success, ref liveness, ref readiness, 3, 2);
        Assert.False(restart2);

        // Third failure: threshold reached, triggers container restart
        var (restart3, _) = _engine.EvaluateProbes(ProbeResult.Failure, ProbeResult.Success, ref liveness, ref readiness, 3, 2);
        Assert.True(restart3);
    }

    [Fact]
    public void Test02_ReadinessProbe_EvictsPodFromServiceEndpointsWithoutRestarting()
    {
        var liveness = new ProbeState(0, 0, true);
        var readiness = new ProbeState(0, 0, true);

        // First failure: threshold is 2
        var (restart1, inEndpoints1) = _engine.EvaluateProbes(ProbeResult.Success, ProbeResult.Failure, ref liveness, ref readiness, 3, 2);
        Assert.False(restart1);
        Assert.True(inEndpoints1);

        // Second failure: readiness threshold exceeded, removed from service endpoints, container NOT restarted
        var (restart2, inEndpoints2) = _engine.EvaluateProbes(ProbeResult.Success, ProbeResult.Failure, ref liveness, ref readiness, 3, 2);
        Assert.False(restart2);
        Assert.False(inEndpoints2);
    }

    [Fact]
    public void Test03_Hpa_CalculatesDesiredReplicasAccurately()
    {
        // 3 replicas with 140% CPU utilization vs 70% target -> ratio = 2.0 -> desired = 6
        int desired = _engine.CalculateDesiredReplicas(3, 140.0, 70.0, minReplicas: 2, maxReplicas: 10);
        Assert.Equal(6, desired);
    }

    [Fact]
    public void Test04_Hpa_AppliesToleranceToPreventFlapping()
    {
        // 4 replicas with 73% CPU vs 70% target -> ratio = 1.042 (within 10% tolerance) -> stays 4
        int desired = _engine.CalculateDesiredReplicas(4, 73.0, 70.0, minReplicas: 2, maxReplicas: 10, tolerance: 0.1);
        Assert.Equal(4, desired);
    }

    [Fact]
    public void Test05_Hpa_ClampsToConfiguredMinAndMaxBounds()
    {
        // Scale down request wanting 1 replica clamped to min 2
        int clampedMin = _engine.CalculateDesiredReplicas(4, 10.0, 70.0, minReplicas: 2, maxReplicas: 10);
        Assert.Equal(2, clampedMin);

        // Massive spike wanting 25 replicas clamped to max 10
        int clampedMax = _engine.CalculateDesiredReplicas(4, 500.0, 70.0, minReplicas: 2, maxReplicas: 10);
        Assert.Equal(10, clampedMax);
    }

    [Fact]
    public void Test06_CoinChange_FindsMinimumCoins()
    {
        int[] coins1 = { 1, 2, 5 };
        Assert.Equal(3, CoinChangeAndMaxProductSolver.CoinChange(coins1, 11)); // 5 + 5 + 1

        int[] coins2 = { 2 };
        Assert.Equal(-1, CoinChangeAndMaxProductSolver.CoinChange(coins2, 3)); // impossible

        Assert.Equal(0, CoinChangeAndMaxProductSolver.CoinChange(coins1, 0));
    }

    [Fact]
    public void Test07_CoinChange_HandlesLargeCoinsAndEdgeCases()
    {
        int[] coins = { 186, 419, 83, 408 };
        Assert.Equal(20, CoinChangeAndMaxProductSolver.CoinChange(coins, 6249));

        Assert.Equal(-1, CoinChangeAndMaxProductSolver.CoinChange(new[] { 5 }, 7));
    }

    [Fact]
    public void Test08_MaxProduct_CalculatesOptimalSubarrayProduct()
    {
        int[] nums1 = { 2, 3, -2, 4 };
        Assert.Equal(6, CoinChangeAndMaxProductSolver.MaxProduct(nums1)); // [2, 3]

        int[] nums2 = { -2, 0, -1 };
        Assert.Equal(0, CoinChangeAndMaxProductSolver.MaxProduct(nums2));
    }

    [Fact]
    public void Test09_MaxProduct_HandlesOddAndEvenNegativeNumbers()
    {
        // Two negatives multiply to positive: (-2) * (-4) = 8
        int[] nums1 = { -2, 3, -4 };
        Assert.Equal(24, CoinChangeAndMaxProductSolver.MaxProduct(nums1)); // -2 * 3 * -4 = 24

        int[] nums2 = { -2 };
        Assert.Equal(-2, CoinChangeAndMaxProductSolver.MaxProduct(nums2));
    }

    [Fact]
    public void Test10_ManifestFiles_ValidateProbesAndHpaConfiguration()
    {
        var k8sDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "k8s");
        var probesPath = Path.Combine(k8sDir, "probes.yaml");
        var hpaPath = Path.Combine(k8sDir, "hpa.yaml");

        if (File.Exists(probesPath))
        {
            var content = File.ReadAllText(probesPath);
            Assert.Contains("startupProbe:", content);
            Assert.Contains("livenessProbe:", content);
            Assert.Contains("readinessProbe:", content);
            Assert.Contains("/health/ready", content);
        }

        if (File.Exists(hpaPath))
        {
            var content = File.ReadAllText(hpaPath);
            Assert.Contains("kind: HorizontalPodAutoscaler", content);
            Assert.Contains("minReplicas: 2", content);
            Assert.Contains("maxReplicas: 10", content);
            Assert.Contains("stabilizationWindowSeconds: 300", content);
        }
    }
}
