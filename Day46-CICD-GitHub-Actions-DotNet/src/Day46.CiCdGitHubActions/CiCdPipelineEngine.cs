using System;
using System.Collections.Generic;
using System.Linq;

namespace Day46.CiCdGitHubActions;

public record VulnerabilityFinding(
    string PackageId,
    string InstalledVersion,
    string Severity, // "LOW", "MEDIUM", "HIGH", "CRITICAL"
    double CvssScore,
    bool HasFixedVersion
);

public record SemVer(int Major, int Minor, int Patch)
{
    public override string ToString() => $"v{Major}.{Minor}.{Patch}";
}

public class CiCdPipelineEngine
{
    public bool ValidateJobDependencies(Dictionary<string, List<string>> jobDependencies)
    {
        // Cycle detection in job DAG
        var visited = new HashSet<string>();
        var recursionStack = new HashSet<string>();

        foreach (var job in jobDependencies.Keys)
        {
            if (HasCycle(job, jobDependencies, visited, recursionStack))
            {
                return false;
            }
        }
        return true;
    }

    private bool HasCycle(string current, Dictionary<string, List<string>> graph, HashSet<string> visited, HashSet<string> stack)
    {
        if (stack.Contains(current)) return true;
        if (visited.Contains(current)) return false;

        visited.Add(current);
        stack.Add(current);

        if (graph.TryGetValue(current, out var dependencies))
        {
            foreach (var dep in dependencies)
            {
                if (HasCycle(dep, graph, visited, stack)) return true;
            }
        }

        stack.Remove(current);
        return false;
    }

    public (bool Passed, List<string> Violations) EvaluateTrivySecurityGate(
        IEnumerable<VulnerabilityFinding> findings,
        double maxAllowedCvss = 7.0
    )
    {
        var violations = new List<string>();

        foreach (var v in findings)
        {
            if (v.CvssScore >= maxAllowedCvss && v.HasFixedVersion)
            {
                violations.Add($"Security Gate Failed: {v.PackageId} ({v.InstalledVersion}) has {v.Severity} vulnerability with CVSS {v.CvssScore:F1}");
            }
        }

        return (violations.Count == 0, violations);
    }

    public SemVer ComputeNextVersion(SemVer currentVersion, IEnumerable<string> commitMessages)
    {
        bool hasBreakingChange = false;
        bool hasFeature = false;
        bool hasFix = false;

        foreach (var msg in commitMessages)
        {
            var trimmed = msg.Trim();
            if (trimmed.Contains("BREAKING CHANGE:") || trimmed.StartsWith("feat!:") || trimmed.StartsWith("fix!:"))
            {
                hasBreakingChange = true;
            }
            else if (trimmed.StartsWith("feat:") || trimmed.StartsWith("feat("))
            {
                hasFeature = true;
            }
            else if (trimmed.StartsWith("fix:") || trimmed.StartsWith("perf:") || trimmed.StartsWith("refactor:"))
            {
                hasFix = true;
            }
        }

        if (hasBreakingChange)
        {
            return new SemVer(currentVersion.Major + 1, 0, 0);
        }
        if (hasFeature)
        {
            return new SemVer(currentVersion.Major, currentVersion.Minor + 1, 0);
        }
        if (hasFix)
        {
            return new SemVer(currentVersion.Major, currentVersion.Minor, currentVersion.Patch + 1);
        }

        return currentVersion;
    }

    public string MaskSecret(string logOutput, string secretValue)
    {
        if (string.IsNullOrEmpty(secretValue)) return logOutput;
        return logOutput.Replace(secretValue, "***");
    }
}
