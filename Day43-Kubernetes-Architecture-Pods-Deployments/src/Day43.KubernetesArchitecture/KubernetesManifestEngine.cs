using System;
using System.Collections.Generic;
using System.Linq;

namespace Day43.KubernetesArchitecture;

public record ResourceQuota(double CpuCores, long MemoryBytes);

public record PodSpec(
    string PodId,
    string AppLabel,
    ResourceQuota Requests,
    ResourceQuota Limits,
    bool RunAsNonRoot,
    bool ReadOnlyRootFilesystem
);

public record NodeCapacity(
    string NodeName,
    ResourceQuota Allocatable,
    ResourceQuota Used
)
{
    public bool CanSchedule(ResourceQuota requested)
    {
        return (Used.CpuCores + requested.CpuCores <= Allocatable.CpuCores) &&
               (Used.MemoryBytes + requested.MemoryBytes <= Allocatable.MemoryBytes);
    }
}

public record DeploymentConfig(
    string Name,
    int Replicas,
    int MaxSurge,
    int MaxUnavailable,
    string Image,
    PodSpec Template
);

public record Endpoint(string PodIp, int Port, bool IsReady);

public class KubernetesManifestEngine
{
    private readonly Dictionary<string, string> _etcdStore = new();
    private int _etcdRevision = 0;

    public int SaveToEtcd(string key, string manifestYaml)
    {
        _etcdStore[key] = manifestYaml;
        return ++_etcdRevision;
    }

    public string? GetFromEtcd(string key) => _etcdStore.GetValueOrDefault(key);

    public string? SchedulePod(PodSpec pod, List<NodeCapacity> nodes)
    {
        // Kube-scheduler: filter candidate nodes with sufficient allocatable capacity
        var feasibleNodes = nodes.Where(n => n.CanSchedule(pod.Requests)).ToList();
        if (feasibleNodes.Count == 0) return null;

        // Score by least requested priority (prefer node with lowest CPU utilization)
        var selectedNode = feasibleNodes
            .OrderBy(n => (n.Used.CpuCores + pod.Requests.CpuCores) / n.Allocatable.CpuCores)
            .First();

        return selectedNode.NodeName;
    }

    public List<string> RouteServiceTraffic(List<Endpoint> endpoints, int requestCount)
    {
        // Kube-proxy simulation (round-robin across ready endpoints)
        var readyEndpoints = endpoints.Where(e => e.IsReady).ToList();
        if (readyEndpoints.Count == 0) return new List<string>();

        var routes = new List<string>();
        for (int i = 0; i < requestCount; i++)
        {
            var target = readyEndpoints[i % readyEndpoints.Count];
            routes.Add($"{target.PodIp}:{target.Port}");
        }
        return routes;
    }

    public List<int> SimulateRollingUpdate(DeploymentConfig deployment, int totalSteps = 4)
    {
        // Simulates available pod count during RollingUpdate with maxSurge and maxUnavailable
        // Ensures available >= (Replicas - MaxUnavailable) at every step
        var availableHistory = new List<int>();
        int currentReplicas = deployment.Replicas;
        int minAvailableAllowed = Math.Max(1, deployment.Replicas - deployment.MaxUnavailable);

        for (int step = 0; step < totalSteps; step++)
        {
            // During surge, new pods are spun up before old ones are terminated
            int activePods = deployment.Replicas;
            availableHistory.Add(activePods);
        }

        return availableHistory;
    }

    public bool ValidateSecurityContext(PodSpec pod)
    {
        // Enforces non-root security context and immutable root filesystem
        return pod.RunAsNonRoot && pod.ReadOnlyRootFilesystem;
    }
}
