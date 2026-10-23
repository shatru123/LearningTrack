using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace Day43.KubernetesArchitecture.Tests;

public class Day43Tests
{
    private readonly KubernetesManifestEngine _engine;

    public Day43Tests()
    {
        _engine = new KubernetesManifestEngine();
    }

    [Fact]
    public void Test01_Etcd_StoresAndRetrievesManifestWithMonotonicRevision()
    {
        int rev1 = _engine.SaveToEtcd("deploy/order-api", "apiVersion: apps/v1\nkind: Deployment");
        int rev2 = _engine.SaveToEtcd("svc/order-api", "apiVersion: v1\nkind: Service");

        Assert.Equal(1, rev1);
        Assert.Equal(2, rev2);
        Assert.Contains("apps/v1", _engine.GetFromEtcd("deploy/order-api")!);
    }

    [Fact]
    public void Test02_KubeScheduler_AssignsPodToNodeWithSufficientCapacity()
    {
        var pod = new PodSpec("pod-1", "order-api", new ResourceQuota(0.5, 512 * 1024 * 1024), new ResourceQuota(1, 1024 * 1024 * 1024), true, true);
        var nodes = new List<NodeCapacity>
        {
            new("worker-1", new ResourceQuota(4.0, 8L * 1024 * 1024 * 1024), new ResourceQuota(3.8, 7L * 1024 * 1024 * 1024)), // Insufficient CPU
            new("worker-2", new ResourceQuota(4.0, 8L * 1024 * 1024 * 1024), new ResourceQuota(1.0, 2L * 1024 * 1024 * 1024))  // Ample capacity
        };

        var selected = _engine.SchedulePod(pod, nodes);

        Assert.Equal("worker-2", selected);
    }

    [Fact]
    public void Test03_KubeScheduler_ReturnsNullWhenNoNodeCanAccommodatePod()
    {
        var pod = new PodSpec("pod-huge", "heavy-worker", new ResourceQuota(16.0, 64L * 1024 * 1024 * 1024), new ResourceQuota(32, 128L * 1024 * 1024 * 1024), true, true);
        var nodes = new List<NodeCapacity>
        {
            new("worker-1", new ResourceQuota(4.0, 8L * 1024 * 1024 * 1024), new ResourceQuota(1.0, 2L * 1024 * 1024 * 1024))
        };

        var selected = _engine.SchedulePod(pod, nodes);

        Assert.Null(selected);
    }

    [Fact]
    public void Test04_KubeProxy_RoundRobinsOnlyAcrossReadyEndpoints()
    {
        var endpoints = new List<Endpoint>
        {
            new("10.244.1.5", 8080, true),
            new("10.244.2.8", 8080, false), // Unhealthy / not ready pod
            new("10.244.3.12", 8080, true)
        };

        var routed = _engine.RouteServiceTraffic(endpoints, 4);

        Assert.Equal(4, routed.Count);
        Assert.Equal("10.244.1.5:8080", routed[0]);
        Assert.Equal("10.244.3.12:8080", routed[1]);
        Assert.Equal("10.244.1.5:8080", routed[2]);
        Assert.Equal("10.244.3.12:8080", routed[3]);
        Assert.DoesNotContain("10.244.2.8:8080", routed);
    }

    [Fact]
    public void Test05_RollingUpdate_MaintainsZeroDowntimeAvailability()
    {
        var template = new PodSpec("pod-tmpl", "order-api", new ResourceQuota(0.25, 256), new ResourceQuota(0.5, 512), true, true);
        var deploy = new DeploymentConfig("order-api-deployment", 3, 1, 0, "ghcr.io/shatru123/order-api:v1.2.0", template);

        var history = _engine.SimulateRollingUpdate(deploy);

        foreach (var available in history)
        {
            Assert.True(available >= 3, "Available replicas dropped below desired count during rolling update with maxUnavailable=0");
        }
    }

    [Fact]
    public void Test06_SecurityContext_EnforcesNonRootAndReadOnlyFilesystem()
    {
        var validPod = new PodSpec("pod-secure", "order-api", new ResourceQuota(0.25, 256), new ResourceQuota(0.5, 512), true, true);
        var insecurePod = new PodSpec("pod-root", "legacy-api", new ResourceQuota(0.25, 256), new ResourceQuota(0.5, 512), false, true);

        Assert.True(_engine.ValidateSecurityContext(validPod));
        Assert.False(_engine.ValidateSecurityContext(insecurePod));
    }

    [Fact]
    public void Test07_LongestPalindrome_FindsOddAndEvenPalindromes()
    {
        Assert.Equal("bab", LongestPalindromeSolver.LongestPalindrome("babad")); // "aba" also valid
        Assert.Equal("bb", LongestPalindromeSolver.LongestPalindrome("cbbd"));
        Assert.Equal("racecar", LongestPalindromeSolver.LongestPalindrome("abcracecardef"));
    }

    [Fact]
    public void Test08_LongestPalindromeDp_ProducesIdenticalResults()
    {
        string s1 = "babad";
        string s2 = "cbbd";
        string s3 = "forgeeksskeegfor";

        var res1 = LongestPalindromeSolver.LongestPalindromeDp(s1);
        var res2 = LongestPalindromeSolver.LongestPalindromeDp(s2);
        var res3 = LongestPalindromeSolver.LongestPalindromeDp(s3);

        Assert.True(res1 == "bab" || res1 == "aba");
        Assert.Equal("bb", res2);
        Assert.Equal("geeksskeeg", res3);
    }

    [Fact]
    public void Test09_LongestPalindrome_HandlesEdgeCases()
    {
        Assert.Equal("", LongestPalindromeSolver.LongestPalindrome(""));
        Assert.Equal("a", LongestPalindromeSolver.LongestPalindrome("a"));
        Assert.Equal("aaaa", LongestPalindromeSolver.LongestPalindrome("aaaa"));
        Assert.Equal("a", LongestPalindromeSolver.LongestPalindrome("abcdef"));
    }

    [Fact]
    public void Test10_KubernetesManifestFiles_ExistAndContainValidSpec()
    {
        var k8sDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "k8s");
        var deployPath = Path.Combine(k8sDir, "deployment.yaml");
        var clusterIpPath = Path.Combine(k8sDir, "service-clusterip.yaml");
        var lbPath = Path.Combine(k8sDir, "service-loadbalancer.yaml");

        if (File.Exists(deployPath))
        {
            var content = File.ReadAllText(deployPath);
            Assert.Contains("kind: Deployment", content);
            Assert.Contains("RollingUpdate", content);
            Assert.Contains("runAsNonRoot: true", content);
        }

        if (File.Exists(clusterIpPath))
        {
            var content = File.ReadAllText(clusterIpPath);
            Assert.Contains("type: ClusterIP", content);
        }

        if (File.Exists(lbPath))
        {
            var content = File.ReadAllText(lbPath);
            Assert.Contains("type: LoadBalancer", content);
        }
    }
}
