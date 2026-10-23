using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace Day46.CiCdGitHubActions.Tests;

public class Day46Tests
{
    private readonly CiCdPipelineEngine _engine;

    public Day46Tests()
    {
        _engine = new CiCdPipelineEngine();
    }

    [Fact]
    public void Test01_PipelineDAG_ValidatesAcyclicJobDependencies()
    {
        var validGraph = new Dictionary<string, List<string>>
        {
            ["lint"] = new(),
            ["test"] = new() { "lint" },
            ["security-scan"] = new() { "test" },
            ["publish"] = new() { "security-scan" }
        };

        Assert.True(_engine.ValidateJobDependencies(validGraph));
    }

    [Fact]
    public void Test02_PipelineDAG_DetectsCircularJobDependencies()
    {
        var cyclicGraph = new Dictionary<string, List<string>>
        {
            ["jobA"] = new() { "jobB" },
            ["jobB"] = new() { "jobC" },
            ["jobC"] = new() { "jobA" } // Circular dependency
        };

        Assert.False(_engine.ValidateJobDependencies(cyclicGraph));
    }

    [Fact]
    public void Test03_TrivySecurityGate_FailsOnCriticalFixedVulnerabilities()
    {
        var findings = new List<VulnerabilityFinding>
        {
            new("System.Text.Json", "8.0.0", "CRITICAL", 8.8, true),
            new("Microsoft.Data.SqlClient", "5.1.0", "LOW", 3.2, false)
        };

        var (passed, violations) = _engine.EvaluateTrivySecurityGate(findings, maxAllowedCvss: 7.0);

        Assert.False(passed);
        Assert.Single(violations);
        Assert.Contains("System.Text.Json", violations[0]);
    }

    [Fact]
    public void Test04_TrivySecurityGate_PassesWhenNoCriticalVulnerabilitiesExist()
    {
        var findings = new List<VulnerabilityFinding>
        {
            new("Serilog", "3.1.0", "LOW", 2.5, false),
            new("Newtonsoft.Json", "13.0.3", "MEDIUM", 5.3, false)
        };

        var (passed, violations) = _engine.EvaluateTrivySecurityGate(findings, maxAllowedCvss: 7.0);

        Assert.True(passed);
        Assert.Empty(violations);
    }

    [Fact]
    public void Test05_SemVer_CalculatesMajorBumpOnBreakingChange()
    {
        var current = new SemVer(1, 4, 2);
        var commits = new[] { "fix: minor typo", "feat!: overhaul order api schema" };

        var next = _engine.ComputeNextVersion(current, commits);

        Assert.Equal("v2.0.0", next.ToString());
    }

    [Fact]
    public void Test06_SemVer_CalculatesMinorBumpOnFeature()
    {
        var current = new SemVer(1, 4, 2);
        var commits = new[] { "feat: add payment refund webhook", "fix: adjust timeout" };

        var next = _engine.ComputeNextVersion(current, commits);

        Assert.Equal("v1.5.0", next.ToString());
    }

    [Fact]
    public void Test07_SemVer_CalculatesPatchBumpOnBugfix()
    {
        var current = new SemVer(1, 4, 2);
        var commits = new[] { "fix: prevent null pointer exception on missing header" };

        var next = _engine.ComputeNextVersion(current, commits);

        Assert.Equal("v1.4.3", next.ToString());
    }

    [Fact]
    public void Test08_SecretMasking_RedactsSensitiveTokensFromLogs()
    {
        string rawLog = "Executing curl with token: ghp_9876543210abcdef to api endpoint";
        string masked = _engine.MaskSecret(rawLog, "ghp_9876543210abcdef");

        Assert.DoesNotContain("ghp_9876543210abcdef", masked);
        Assert.Contains("***", masked);
    }

    [Fact]
    public void Test09_WordBreak_ValidWordSegmentations()
    {
        Assert.True(WordBreakSolver.WordBreak("leetcode", new[] { "leet", "code" }));
        Assert.True(WordBreakSolver.WordBreak("applepenapple", new[] { "apple", "pen" }));
        Assert.True(WordBreakSolver.WordBreak("cars", new[] { "car", "ca", "rs" }));
    }

    [Fact]
    public void Test10_WordBreak_InvalidSegmentationsAndEdgeCases()
    {
        Assert.False(WordBreakSolver.WordBreak("catsandog", new[] { "cats", "dog", "sand", "and", "cat" }));
        Assert.False(WordBreakSolver.WordBreak("", new[] { "test" }));
        Assert.False(WordBreakSolver.WordBreak("test", Array.Empty<string>()));
    }
}
