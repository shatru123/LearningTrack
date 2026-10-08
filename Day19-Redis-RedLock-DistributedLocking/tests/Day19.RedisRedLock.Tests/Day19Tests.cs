using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace Day19.RedisRedLock.Tests;

public class Day19Tests
{
    // ==========================================
    // 1. REDLOCK DISTRIBUTED LOCKING TESTS
    // ==========================================

    [Fact]
    public async Task RedLock_FullClusterQuorum_AcquiresLockWithHighValidityTime()
    {
        var nodes = Enumerable.Range(1, 5)
            .Select(i => new SimulatedRedisNode($"node-{i}"))
            .ToList();

        var engine = new RedLockEngine(nodes, new RedLockOptions
        {
            LockTtl = TimeSpan.FromSeconds(5),
            NodeTimeout = TimeSpan.FromMilliseconds(50)
        });

        var result = await engine.TryAcquireLockAsync("order:checkout:1001");

        result.Success.Should().BeTrue();
        result.AcquiredNodes.Should().Be(5);
        result.TotalNodes.Should().Be(5);
        result.ValidityTime.Should().BeGreaterThan(TimeSpan.FromSeconds(4.5));
        result.FencingToken.Should().BeGreaterThan(0);

        // Verify keys exist on all 5 nodes with same lock value
        foreach (var node in nodes)
        {
            var storedVal = await node.GetLockValueAsync("order:checkout:1001");
            storedVal.Should().Be(result.LockValue);
        }

        // Release
        int released = await engine.ReleaseLockAsync("order:checkout:1001", result.LockValue);
        released.Should().Be(5);
    }

    [Fact]
    public async Task RedLock_PartialClusterFault_AcquiresLockWhenQuorumIsMet()
    {
        var nodes = Enumerable.Range(1, 5)
            .Select(i => new SimulatedRedisNode($"node-{i}"))
            .ToList();

        // Simulate 2 failed/offline nodes out of 5
        nodes[0].IsHealthy = false;
        nodes[1].IsHealthy = false;

        var engine = new RedLockEngine(nodes, new RedLockOptions
        {
            LockTtl = TimeSpan.FromSeconds(5),
            NodeTimeout = TimeSpan.FromMilliseconds(50)
        });

        // 3 of 5 nodes available => Quorum (5/2 + 1 = 3) is met
        var result = await engine.TryAcquireLockAsync("inventory:sku:404");

        result.Success.Should().BeTrue();
        result.AcquiredNodes.Should().Be(3);
        result.TotalNodes.Should().Be(5);
        result.ValidityTime.Should().BeGreaterThan(TimeSpan.Zero);

        // Cleanup
        int released = await engine.ReleaseLockAsync("inventory:sku:404", result.LockValue);
        released.Should().Be(3);
    }

    [Fact]
    public async Task RedLock_QuorumFailure_CompensatesAndRollsBackAllAcquiredNodes()
    {
        var nodes = Enumerable.Range(1, 5)
            .Select(i => new SimulatedRedisNode($"node-{i}"))
            .ToList();

        // 3 nodes offline => only 2 can acquire, failing quorum of 3
        nodes[0].IsHealthy = false;
        nodes[1].IsHealthy = false;
        nodes[2].IsHealthy = false;

        var engine = new RedLockEngine(nodes, new RedLockOptions
        {
            LockTtl = TimeSpan.FromSeconds(5),
            NodeTimeout = TimeSpan.FromMilliseconds(50)
        });

        var result = await engine.TryAcquireLockAsync("account:transfer:99");

        result.Success.Should().BeFalse();
        result.AcquiredNodes.Should().Be(2);
        result.ErrorMessage.Should().Contain("Quorum not met");

        // Verify rollback: even nodes 3 and 4 that succeeded have been released
        (await nodes[3].GetLockValueAsync("account:transfer:99")).Should().BeNull();
        (await nodes[4].GetLockValueAsync("account:transfer:99")).Should().BeNull();
    }

    [Fact]
    public async Task RedLock_ClockDriftAndTimeout_RejectsLockWhenValidityExpires()
    {
        var nodes = Enumerable.Range(1, 5)
            .Select(i => new SimulatedRedisNode($"node-{i}")
            {
                // Inject 80ms latency on each node
                SimulatedLatency = TimeSpan.FromMilliseconds(80)
            })
            .ToList();

        // Short TTL of 50ms with high latency => validity will be negative
        var engine = new RedLockEngine(nodes, new RedLockOptions
        {
            LockTtl = TimeSpan.FromMilliseconds(50),
            NodeTimeout = TimeSpan.FromMilliseconds(150),
            DriftFactor = 0.05
        });

        var result = await engine.TryAcquireLockAsync("rate:job:sync");

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("validity expired");
    }

    [Fact]
    public async Task RedLock_SafeRelease_ForeignTokenCannotReleaseLock()
    {
        var nodes = Enumerable.Range(1, 3)
            .Select(i => new SimulatedRedisNode($"node-{i}"))
            .ToList();

        var engine = new RedLockEngine(nodes, new RedLockOptions { LockTtl = TimeSpan.FromSeconds(5) });

        var result = await engine.TryAcquireLockAsync("batch:process:1");
        result.Success.Should().BeTrue();

        // Another client attempts to release with different token
        int releasedFraudulent = await engine.ReleaseLockAsync("batch:process:1", "wrong-random-token");
        releasedFraudulent.Should().Be(0);

        // Lock should still be held
        (await nodes[0].GetLockValueAsync("batch:process:1")).Should().Be(result.LockValue);

        // Authorized client releases with valid token
        int releasedLegit = await engine.ReleaseLockAsync("batch:process:1", result.LockValue);
        releasedLegit.Should().Be(3);
    }

    [Fact]
    public async Task RedLock_AutoRenewalLease_ExtendsLockAndReleasesOnDispose()
    {
        var nodes = Enumerable.Range(1, 3)
            .Select(i => new SimulatedRedisNode($"node-{i}"))
            .ToList();

        var engine = new RedLockEngine(nodes, new RedLockOptions
        {
            LockTtl = TimeSpan.FromMilliseconds(400),
            AutoRenewalInterval = TimeSpan.FromMilliseconds(150)
        });

        var lease = await engine.AcquireLeaseAsync("long:computation");
        lease.Should().NotBeNull();
        lease!.IsActive.Should().BeTrue();

        // Wait 350ms (normally expired if not renewed at 150ms)
        await Task.Delay(350);

        // Lock should still be held on node-1 due to auto-renewal heartbeat
        var val = await nodes[0].GetLockValueAsync("long:computation");
        val.Should().Be(lease.LockInfo.LockValue);

        // Dispose lease
        await lease.DisposeAsync();
        lease.IsActive.Should().BeFalse();

        // Lock is now released
        (await nodes[0].GetLockValueAsync("long:computation")).Should().BeNull();
    }

    [Fact]
    public async Task RedLock_FencingTokens_ProduceStrictlyIncreasingSequence()
    {
        var nodes = Enumerable.Range(1, 3)
            .Select(i => new SimulatedRedisNode($"node-{i}"))
            .ToList();

        var engine = new RedLockEngine(nodes);

        var r1 = await engine.TryAcquireLockAsync("doc:edit");
        await engine.ReleaseLockAsync("doc:edit", r1.LockValue);

        var r2 = await engine.TryAcquireLockAsync("doc:edit");
        await engine.ReleaseLockAsync("doc:edit", r2.LockValue);

        var r3 = await engine.TryAcquireLockAsync("doc:edit");
        await engine.ReleaseLockAsync("doc:edit", r3.LockValue);

        r1.FencingToken.Should().BeLessThan(r2.FencingToken);
        r2.FencingToken.Should().BeLessThan(r3.FencingToken);
    }

    // ==========================================
    // 2. DSA TREE ISOMORPHISM TESTS (LC #100 & #572)
    // ==========================================

    [Fact]
    public void LC100_IsSameTree_IdenticalTrees_ReturnsTrue()
    {
        var solvers = new TreeIsomorphismSolvers();
        var p = TreeIsomorphismSolvers.BuildTree(new int?[] { 1, 2, 3 });
        var q = TreeIsomorphismSolvers.BuildTree(new int?[] { 1, 2, 3 });

        solvers.IsSameTree(p, q).Should().BeTrue();
    }

    [Fact]
    public void LC100_IsSameTree_StructuralMismatch_ReturnsFalse()
    {
        var solvers = new TreeIsomorphismSolvers();
        // p = [1, 2], q = [1, null, 2]
        var p = TreeIsomorphismSolvers.BuildTree(new int?[] { 1, 2 });
        var q = TreeIsomorphismSolvers.BuildTree(new int?[] { 1, null, 2 });

        solvers.IsSameTree(p, q).Should().BeFalse();
    }

    [Fact]
    public void LC100_IsSameTree_ValueMismatch_ReturnsFalse()
    {
        var solvers = new TreeIsomorphismSolvers();
        // p = [1, 2, 1], q = [1, 1, 2]
        var p = TreeIsomorphismSolvers.BuildTree(new int?[] { 1, 2, 1 });
        var q = TreeIsomorphismSolvers.BuildTree(new int?[] { 1, 1, 2 });

        solvers.IsSameTree(p, q).Should().BeFalse();
    }

    [Fact]
    public void LC100_IsSameTree_EmptyAndNullTrees_ReturnsCorrectly()
    {
        var solvers = new TreeIsomorphismSolvers();
        solvers.IsSameTree(null, null).Should().BeTrue();
        solvers.IsSameTree(new TreeNode(1), null).Should().BeFalse();
        solvers.IsSameTree(null, new TreeNode(1)).Should().BeFalse();
    }

    [Fact]
    public void LC572_IsSubtree_ValidSubtree_ReturnsTrueForBothDfsAndMerkle()
    {
        var solvers = new TreeIsomorphismSolvers();
        // root = [3, 4, 5, 1, 2], subRoot = [4, 1, 2]
        var root = TreeIsomorphismSolvers.BuildTree(new int?[] { 3, 4, 5, 1, 2 });
        var subRoot = TreeIsomorphismSolvers.BuildTree(new int?[] { 4, 1, 2 });

        solvers.IsSubtree(root, subRoot).Should().BeTrue();
        solvers.IsSubtreeMerkle(root, subRoot).Should().BeTrue();
    }

    [Fact]
    public void LC572_IsSubtree_SubtreeHasExtraLeaf_ReturnsFalseForBothDfsAndMerkle()
    {
        var solvers = new TreeIsomorphismSolvers();
        // root = [3, 4, 5, 1, 2, null, null, null, null, 0]
        // subRoot = [4, 1, 2] -> 2 in root has child 0, so it is NOT an exact subtree match
        var root = TreeIsomorphismSolvers.BuildTree(new int?[] { 3, 4, 5, 1, 2, null, null, null, null, 0 });
        var subRoot = TreeIsomorphismSolvers.BuildTree(new int?[] { 4, 1, 2 });

        solvers.IsSubtree(root, subRoot).Should().BeFalse();
        solvers.IsSubtreeMerkle(root, subRoot).Should().BeFalse();
    }

    [Fact]
    public void LC572_IsSubtree_IdenticalTree_ReturnsTrue()
    {
        var solvers = new TreeIsomorphismSolvers();
        var root = TreeIsomorphismSolvers.BuildTree(new int?[] { 1, 2, 3 });
        var subRoot = TreeIsomorphismSolvers.BuildTree(new int?[] { 1, 2, 3 });

        solvers.IsSubtree(root, subRoot).Should().BeTrue();
        solvers.IsSubtreeMerkle(root, subRoot).Should().BeTrue();
    }
}
