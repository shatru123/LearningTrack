using System;
using FluentAssertions;
using Xunit;

namespace Day25.MilestoneAssessment.Tests;

public class Day25Tests
{
    // ==========================================
    // 1. PHASE 1 BENCHMARK & MILESTONE TESTS
    // ==========================================

    [Fact]
    public void Benchmark_MemoryAllocation_SpanAllocatesZeroBytesComparedToSubstring()
    {
        var (subResult, spanResult) = Phase1BenchmarkHarness.CompareMemoryAllocations(iterations: 5_000);

        // Substring allocates heap memory
        subResult.AllocatedBytes.Should().BeGreaterThan(0, "Substring creates new string instances on the managed heap");

        // Span allocates 0 heap memory
        spanResult.AllocatedBytes.Should().Be(0, "Span slicing is completely zero-allocation via interior pointer arithmetic");
    }

    [Fact]
    public void Benchmark_ValueTaskVsTask_DemonstratesZeroAllocationSynchronousPath()
    {
        var (taskBytes, vtaskBytes) = Phase1BenchmarkHarness.CompareTaskVsValueTask(iterations: 5_000);

        // ValueTask synchronous path allocates 0 heap bytes
        vtaskBytes.Should().Be(0, "ValueTask is a struct type avoiding heap allocations on cached synchronous returns");
    }

    [Fact]
    public void Milestone_Summary_CoversAllPhase1PillarsAnd25Days()
    {
        var summary = Phase1BenchmarkHarness.GetPhase1Summary();

        summary.PhaseNumber.Should().Be(1);
        summary.TotalDays.Should().Be(25);
        summary.KeyPillars.Should().HaveCount(5);
        summary.KeyPillars[0].Should().Contain("Memory Model");
        summary.KeyPillars[1].Should().Contain("Web Architecture");
        summary.KeyPillars[2].Should().Contain("High-Performance Data Access");
        summary.KeyPillars[3].Should().Contain("Advanced Concurrency");
        summary.KeyPillars[4].Should().Contain("Distributed Systems");
    }

    // ==========================================
    // 2. DSA: SERIALIZE & DESERIALIZE TREE (LC #297)
    // ==========================================

    [Fact]
    public void LC297_SerializeDfs_RoundTrip_PreservesTreeStructure()
    {
        var codec = new TreeCodecSolver();
        // Tree:
        //        1
        //       / \
        //      2   3
        //         / \
        //        4   5
        var original = new TreeNode(1,
            new TreeNode(2),
            new TreeNode(3, new TreeNode(4), new TreeNode(5))
        );

        var serialized = codec.SerializeDfs(original);
        serialized.Should().NotBeNullOrEmpty();

        var restored = codec.DeserializeDfs(serialized);
        restored.Should().NotBeNull();
        restored!.val.Should().Be(1);
        restored.left!.val.Should().Be(2);
        restored.right!.val.Should().Be(3);
        restored.right.left!.val.Should().Be(4);
        restored.right.right!.val.Should().Be(5);
    }

    [Fact]
    public void LC297_SerializeBfs_RoundTrip_PreservesTreeStructure()
    {
        var codec = new TreeCodecSolver();
        var original = new TreeNode(1,
            new TreeNode(2),
            new TreeNode(3, new TreeNode(4), new TreeNode(5))
        );

        var serialized = codec.SerializeBfs(original);
        serialized.Should().NotBeNullOrEmpty();

        var restored = codec.DeserializeBfs(serialized);
        restored.Should().NotBeNull();
        restored!.val.Should().Be(1);
        restored.left!.val.Should().Be(2);
        restored.right!.val.Should().Be(3);
        restored.right.left!.val.Should().Be(4);
        restored.right.right!.val.Should().Be(5);
    }

    [Fact]
    public void LC297_Codecs_HandleEmptyTree_ReturnsNull()
    {
        var codec = new TreeCodecSolver();

        codec.DeserializeDfs(codec.SerializeDfs(null)).Should().BeNull();
        codec.DeserializeBfs(codec.SerializeBfs(null)).Should().BeNull();
    }

    [Fact]
    public void LC297_Codecs_HandleSingleNode_ReturnsFidelity()
    {
        var codec = new TreeCodecSolver();
        var single = new TreeNode(42);

        var resDfs = codec.DeserializeDfs(codec.SerializeDfs(single));
        resDfs.Should().NotBeNull();
        resDfs!.val.Should().Be(42);
        resDfs.left.Should().BeNull();
        resDfs.right.Should().BeNull();

        var resBfs = codec.DeserializeBfs(codec.SerializeBfs(single));
        resBfs.Should().NotBeNull();
        resBfs!.val.Should().Be(42);
        resBfs.left.Should().BeNull();
        resBfs.right.Should().BeNull();
    }

    [Fact]
    public void LC297_Codecs_HandleLeftSkewedTree_MaintainsHierarchy()
    {
        var codec = new TreeCodecSolver();
        // 3 -> 2 -> 1
        var leftSkewed = new TreeNode(3, new TreeNode(2, new TreeNode(1), null), null);

        var restored = codec.DeserializeDfs(codec.SerializeDfs(leftSkewed));
        restored!.val.Should().Be(3);
        restored.left!.val.Should().Be(2);
        restored.left.left!.val.Should().Be(1);
        restored.right.Should().BeNull();
    }

    [Fact]
    public void LC297_Codecs_HandleRightSkewedTree_MaintainsHierarchy()
    {
        var codec = new TreeCodecSolver();
        // 1 -> 2 -> 3
        var rightSkewed = new TreeNode(1, null, new TreeNode(2, null, new TreeNode(3)));

        var restored = codec.DeserializeBfs(codec.SerializeBfs(rightSkewed));
        restored!.val.Should().Be(1);
        restored.left.Should().BeNull();
        restored.right!.val.Should().Be(2);
        restored.right.right!.val.Should().Be(3);
    }

    [Fact]
    public void LC297_Codecs_HandleNegativeValues_WithoutTokenCorruption()
    {
        var codec = new TreeCodecSolver();
        // Root: -10, Left: -25, Right: 50
        var negTree = new TreeNode(-10, new TreeNode(-25), new TreeNode(50));

        var restoredDfs = codec.DeserializeDfs(codec.SerializeDfs(negTree));
        restoredDfs!.val.Should().Be(-10);
        restoredDfs.left!.val.Should().Be(-25);
        restoredDfs.right!.val.Should().Be(50);

        var restoredBfs = codec.DeserializeBfs(codec.SerializeBfs(negTree));
        restoredBfs!.val.Should().Be(-10);
        restoredBfs.left!.val.Should().Be(-25);
        restoredBfs.right!.val.Should().Be(50);
    }
}
