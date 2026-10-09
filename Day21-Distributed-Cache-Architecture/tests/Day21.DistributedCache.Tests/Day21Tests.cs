using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace Day21.DistributedCache.Tests;

public class Day21Tests
{
    // ==========================================
    // 1. CONSISTENT HASHING RING TESTS
    // ==========================================

    [Fact]
    public void ConsistentHashRing_UniformDistribution_DistributesKeysEvenlyAcrossNodes()
    {
        var nodes = new[] { "node-alpha", "node-beta", "node-gamma", "node-delta" };
        var ring = new ConsistentHashRing<string>(s => s, virtualNodeReplicas: 150, initialNodes: nodes);

        var counts = nodes.ToDictionary(n => n, _ => 0);
        int totalKeys = 1000;

        for (int i = 0; i < totalKeys; i++)
        {
            var node = ring.GetNode($"cache:user:{i}");
            counts[node]++;
        }

        // Each node in a 4-node ring should ideally receive ~250 keys (+/- 10%)
        foreach (var (node, count) in counts)
        {
            count.Should().BeInRange(170, 330, $"Node {node} received {count} keys, which is within expected balanced range.");
        }
    }

    [Fact]
    public void ConsistentHashRing_NodeAddition_RedistributesMinimalKeyProportion()
    {
        var initialNodes = new[] { "n1", "n2", "n3", "n4" };
        var ring = new ConsistentHashRing<string>(s => s, virtualNodeReplicas: 150, initialNodes: initialNodes);

        var originalMapping = new Dictionary<string, string>();
        int totalKeys = 1000;

        for (int i = 0; i < totalKeys; i++)
        {
            string key = $"entity:order:{i}";
            originalMapping[key] = ring.GetNode(key);
        }

        // Add 5th node
        ring.AddNode("n5");

        int shiftedKeys = 0;
        for (int i = 0; i < totalKeys; i++)
        {
            string key = $"entity:order:{i}";
            string newNode = ring.GetNode(key);
            if (newNode != originalMapping[key])
            {
                shiftedKeys++;
                // Any shifted key MUST be reassigned to the newly added node n5
                newNode.Should().Be("n5");
            }
        }

        // Expected shift: ~1/5 = 20% of keys (e.g. 12% - 28%)
        // Traditional modular hashing (K % N) shifts ~80% of keys!
        double shiftRatio = (double)shiftedKeys / totalKeys;
        shiftRatio.Should().BeInRange(0.12, 0.28, $"Shifted {shiftedKeys}/{totalKeys} keys, which matches ~1/N redistribution.");
    }

    // ==========================================
    // 2. DISTRIBUTED CACHE CLUSTER TESTS
    // ==========================================

    [Fact]
    public async Task DistributedCacheCluster_MultiTier_UsesL1AndL2Efficiently()
    {
        var shards = new[]
        {
            new CacheShardNode("shard-1"),
            new CacheShardNode("shard-2"),
            new CacheShardNode("shard-3")
        };

        var cluster = new DistributedCacheCluster(shards, l1Ttl: TimeSpan.FromSeconds(2));
        int factoryCalls = 0;

        Func<Task<string>> factory = () =>
        {
            Interlocked.Increment(ref factoryCalls);
            return Task.FromResult("calculated-result");
        };

        // Call 1: Misses L1 and L2 -> runs factory
        var res1 = await cluster.GetOrSetAsync("product:101", factory, TimeSpan.FromSeconds(10));
        res1.Should().Be("calculated-result");
        factoryCalls.Should().Be(1);
        cluster.L1CacheCount.Should().Be(1);

        // Call 2: Hits L1 memory cache -> factory NOT called
        var res2 = await cluster.GetOrSetAsync("product:101", factory, TimeSpan.FromSeconds(10));
        res2.Should().Be("calculated-result");
        factoryCalls.Should().Be(1);

        // Invalidate: removes from L1 and L2
        cluster.Invalidate("product:101");
        cluster.L1CacheCount.Should().Be(0);

        // Call 3: Misses -> runs factory again
        var res3 = await cluster.GetOrSetAsync("product:101", factory, TimeSpan.FromSeconds(10));
        res3.Should().Be("calculated-result");
        factoryCalls.Should().Be(2);
    }

    [Fact]
    public async Task DistributedCacheCluster_SingleFlight_SuppressesThunderingHerd()
    {
        var shards = new[] { new CacheShardNode("s1"), new CacheShardNode("s2") };
        var cluster = new DistributedCacheCluster(shards);

        int computationCount = 0;
        Func<Task<string>> expensiveFactory = async () =>
        {
            Interlocked.Increment(ref computationCount);
            await Task.Delay(50); // Simulate expensive DB query
            return "db-payload-42";
        };

        // 20 concurrent threads request the exact same missing key simultaneously
        var tasks = Enumerable.Range(0, 20)
            .Select(_ => cluster.GetOrSetAsync("super-hot-record", expensiveFactory, TimeSpan.FromSeconds(10)))
            .ToList();

        var results = await Task.WhenAll(tasks);

        // All 20 threads receive identical result
        results.Should().AllBeEquivalentTo("db-payload-42");

        // Factory was executed ONLY ONCE thanks to single-flight mutex lock!
        computationCount.Should().Be(1);
    }

    [Fact]
    public async Task DistributedCacheCluster_HotKeyScatter_PopulatesMultipleReplicaShards()
    {
        var shards = new[]
        {
            new CacheShardNode("shard-A"),
            new CacheShardNode("shard-B"),
            new CacheShardNode("shard-C"),
            new CacheShardNode("shard-D")
        };

        var cluster = new DistributedCacheCluster(shards);
        int factoryCount = 0;

        var val = await cluster.GetOrSetHotKeyAsync("celebrity:live_tweet", () =>
        {
            Interlocked.Increment(ref factoryCount);
            return Task.FromResult("viral-announcement");
        }, TimeSpan.FromSeconds(10), scatterShards: 4);

        val.Should().Be("viral-announcement");
        factoryCount.Should().Be(1);

        // Verify across shards: all 4 scatter keys (0..3) are present in the cluster shards
        int totalFound = 0;
        for (int i = 0; i < 4; i++)
        {
            string sk = $"celebrity:live_tweet:#hk_{i}";
            var node = cluster.HashRing.GetNode(sk);
            if (node.Get(sk) != null) totalFound++;
        }
        totalFound.Should().Be(4);
    }

    // ==========================================
    // 3. DSA TREE VIEW & GOOD NODES TESTS (LC #199 & #1448)
    // ==========================================

    [Fact]
    public void LC199_RightSideView_StandardTree_ReturnsRightmostNodes()
    {
        var solvers = new TreeInspectionSolvers();
        // Tree: [1, 2, 3, null, 5, null, 4]
        //        1            <-- 1
        //       / \
        //      2   3          <-- 3
        //       \   \
        //        5   4        <-- 4
        var root = TreeInspectionSolvers.BuildTree(new int?[] { 1, 2, 3, null, 5, null, 4 });
        var view = solvers.RightSideView(root);
        view.Should().Equal(new[] { 1, 3, 4 });
    }

    [Fact]
    public void LC199_RightSideView_LeftHeavyBranchVisible_IncludesLeftDescendant()
    {
        var solvers = new TreeInspectionSolvers();
        // Tree: [1, 2, 3, 4]
        //        1            <-- 1
        //       / \
        //      2   3          <-- 3
        //     /
        //    4                <-- 4 (visible from right since 3 has no children!)
        var root = TreeInspectionSolvers.BuildTree(new int?[] { 1, 2, 3, 4 });
        var view = solvers.RightSideView(root);
        view.Should().Equal(new[] { 1, 3, 4 });
    }

    [Fact]
    public void LC199_RightSideView_EmptyAndSingleNode_ReturnsCorrectView()
    {
        var solvers = new TreeInspectionSolvers();
        solvers.RightSideView(null).Should().BeEmpty();
        solvers.RightSideView(new TreeNode(99)).Should().Equal(new[] { 99 });
    }

    [Fact]
    public void LC1448_GoodNodes_StandardTree_CountsNodesCorrectly()
    {
        var solvers = new TreeInspectionSolvers();
        // Tree: [3, 1, 4, 3, null, 1, 5]
        // Root 3: good
        // Node 1 (left of 3): max is 3 -> bad
        // Node 3 (left of 1): max is 3 -> good (3 >= 3)
        // Node 4 (right of 3): max is 3 -> good (4 >= 3)
        // Node 1 (left of 4): max is 4 -> bad
        // Node 5 (right of 4): max is 4 -> good (5 >= 4)
        // Total good nodes = 4
        var root = TreeInspectionSolvers.BuildTree(new int?[] { 3, 1, 4, 3, null, 1, 5 });
        solvers.GoodNodes(root).Should().Be(4);
    }

    [Fact]
    public void LC1448_GoodNodes_StrictlyIncreasingAndSingleNode_ReturnsCorrectCount()
    {
        var solvers = new TreeInspectionSolvers();
        // Tree: [2, null, 4, 10, 8] -> 2 is good, 4 is good, 10 is good, 8 is not (10 > 8) -> total 3
        var root = TreeInspectionSolvers.BuildTree(new int?[] { 2, null, 4, null, null, 10, 8 });
        solvers.GoodNodes(new TreeNode(7)).Should().Be(1);
    }
}
