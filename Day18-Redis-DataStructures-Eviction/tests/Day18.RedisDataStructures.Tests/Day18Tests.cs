using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace Day18.RedisDataStructures.Tests;

public class Day18Tests
{
    // ==========================================
    // 1. REDIS DATA STRUCTURES TESTS
    // ==========================================

    [Fact]
    public void RedisStrings_AtomicIncrement_IncrementsConcurrentlyAndAccurately()
    {
        var service = new RedisDataStructuresService();
        service.StringSet("views:article:101", "100");

        var val1 = service.StringIncrement("views:article:101", 5);
        val1.Should().Be(105);

        // Concurrent atomic increments
        Parallel.For(0, 50, _ =>
        {
            service.StringIncrement("views:article:101", 1);
        });

        long finalVal = long.Parse(service.StringGet("views:article:101")!);
        finalVal.Should().Be(155);
    }

    [Fact]
    public void RedisHashes_FieldGranularity_AllowsDiscreteFieldMutation()
    {
        var service = new RedisDataStructuresService();
        service.HashSetField("user:42", "name", "Alice");
        service.HashSetField("user:42", "email", "alice@example.com");
        service.HashSetField("user:42", "logins", "3");

        service.HashGetField("user:42", "name").Should().Be("Alice");
        service.HashGetField("user:42", "email").Should().Be("alice@example.com");

        var newLogins = service.HashIncrementField("user:42", "logins", 1);
        newLogins.Should().Be(4);

        var allFields = service.HashGetAll("user:42");
        allFields.Should().NotBeNull();
        allFields!["name"].Should().Be("Alice");
        allFields["logins"].Should().Be("4");
    }

    [Fact]
    public void RedisSets_UnorderedDeduplicationAndIntersection_CalculatesCorrectOverlap()
    {
        var service = new RedisDataStructuresService();
        service.SetAdd("user:1:interests", "dotnet");
        service.SetAdd("user:1:interests", "distributed_systems");
        service.SetAdd("user:1:interests", "redis");

        service.SetAdd("user:2:interests", "redis");
        service.SetAdd("user:2:interests", "golang");
        service.SetAdd("user:2:interests", "dotnet");

        service.SetIsMember("user:1:interests", "dotnet").Should().BeTrue();
        service.SetIsMember("user:1:interests", "python").Should().BeFalse();

        var common = service.SetIntersect("user:1:interests", "user:2:interests");
        common.Should().BeEquivalentTo(new[] { "dotnet", "redis" });
    }

    [Fact]
    public void RedisZSet_LeaderboardRanking_MaintainsSortedOrder()
    {
        var service = new RedisDataStructuresService();
        service.ZSetAdd("leaderboard:season1", "PlayerAlice", 1500.0);
        service.ZSetAdd("leaderboard:season1", "PlayerBob", 2200.5);
        service.ZSetAdd("leaderboard:season1", "PlayerCharlie", 1850.0);
        service.ZSetAdd("leaderboard:season1", "PlayerDavid", 900.0);

        var top3 = service.ZSetGetTop("leaderboard:season1", 3);
        top3.Should().HaveCount(3);
        top3[0].PlayerId.Should().Be("PlayerBob");
        top3[0].Score.Should().Be(2200.5);
        top3[0].Rank.Should().Be(1);

        top3[1].PlayerId.Should().Be("PlayerCharlie");
        top3[1].Score.Should().Be(1850.0);
        top3[1].Rank.Should().Be(2);

        top3[2].PlayerId.Should().Be("PlayerAlice");
        top3[2].Score.Should().Be(1500.0);
        top3[2].Rank.Should().Be(3);
    }

    [Fact]
    public void RedisZSet_SlidingWindowRateLimiter_ThrottlesExcessiveRequests()
    {
        var service = new RedisDataStructuresService();
        var clientIp = "192.168.1.100";
        var window = TimeSpan.FromSeconds(10);
        var baseTime = DateTime.UtcNow;

        // Allow up to 3 requests per 10-second window
        service.IsRateLimited(clientIp, 3, window, baseTime.AddSeconds(1)).Should().BeFalse();
        service.IsRateLimited(clientIp, 3, window, baseTime.AddSeconds(2)).Should().BeFalse();
        service.IsRateLimited(clientIp, 3, window, baseTime.AddSeconds(3)).Should().BeFalse();

        // 4th request inside window should be rate-limited
        service.IsRateLimited(clientIp, 3, window, baseTime.AddSeconds(4)).Should().BeTrue();

        // 5th request 11 seconds later (outside sliding window) should be accepted
        service.IsRateLimited(clientIp, 3, window, baseTime.AddSeconds(15)).Should().BeFalse();
    }

    [Fact]
    public void RedisBitmaps_UserPresenceAndDau_TracksSingleBitsEfficiently()
    {
        var service = new RedisDataStructuresService();
        string key = "active_users:2026-10-08";

        // Mark user IDs 5, 42, 100, 1023 as active
        service.SetBit(key, 5, true);
        service.SetBit(key, 42, true);
        service.SetBit(key, 100, true);
        service.SetBit(key, 1023, true);

        service.GetBit(key, 5).Should().BeTrue();
        service.GetBit(key, 42).Should().BeTrue();
        service.GetBit(key, 7).Should().BeFalse();
        service.GetBit(key, 1023).Should().BeTrue();

        // BitCount should equal 4
        service.BitCount(key).Should().Be(4);
    }

    [Fact]
    public void RedisHyperLogLog_ApproximateUniqueVisitors_EstimatesCardinalityWithinBounds()
    {
        var service = new RedisDataStructuresService();
        string key = "unique_visitors:homepage";

        // Add 1,000 distinct items with duplicates
        for (int i = 0; i < 1000; i++)
        {
            service.HyperLogLogAdd(key, $"visitor_ip_{i}");
            if (i % 2 == 0)
            {
                service.HyperLogLogAdd(key, $"visitor_ip_{i}"); // duplicate
            }
        }

        long count = service.HyperLogLogCount(key);
        // HLL standard error for 16384 registers is ~1.04% / sqrt(m)
        // With 1000 items, estimate should be close to 1000 (+/- 5%)
        count.Should().BeInRange(950, 1050);
    }

    // ==========================================
    // 2. MEMORY EVICTION POLICIES TESTS
    // ==========================================

    [Fact]
    public void Eviction_AllKeysLru_EvictsOldestAccessedKey()
    {
        // Max size 300 bytes
        var cache = new MemoryEvictionSimulator(maxSizeBytes: 300, EvictionPolicy.AllKeysLru);
        var t0 = DateTime.UtcNow;

        cache.Put("key1", "val1", sizeBytes: 100, customAccessTime: t0);
        cache.Put("key2", "val2", sizeBytes: 100, customAccessTime: t0.AddSeconds(1));
        cache.Put("key3", "val3", sizeBytes: 100, customAccessTime: t0.AddSeconds(2));

        // Touch key1 at t0+3 so it becomes most recently used
        cache.Get("key1", accessTime: t0.AddSeconds(3));

        // Insert key4 (100 bytes) -> Cache full (300). Must evict key2 (accessed at t0+1 vs key3 at t0+2 and key1 at t0+3)
        cache.Put("key4", "val4", sizeBytes: 100, customAccessTime: t0.AddSeconds(4));

        cache.Get("key2").Should().BeNull(); // Evicted!
        cache.Get("key1").Should().Be("val1");
        cache.Get("key3").Should().Be("val3");
        cache.Get("key4").Should().Be("val4");
        cache.EvictedCount.Should().Be(1);
    }

    [Fact]
    public void Eviction_VolatileTtl_EvictsKeyWithShortestRemainingTtl()
    {
        var cache = new MemoryEvictionSimulator(maxSizeBytes: 200, EvictionPolicy.VolatileTtl);
        var t0 = DateTime.UtcNow;

        // key1 expires in 60s, key2 expires in 10s
        cache.Put("key1", "val1", sizeBytes: 100, ttl: TimeSpan.FromSeconds(60), customAccessTime: t0);
        cache.Put("key2", "val2", sizeBytes: 100, ttl: TimeSpan.FromSeconds(10), customAccessTime: t0);

        // Inserting key3 (100 bytes) requires evicting key2 (expires sooner)
        cache.Put("key3", "val3", sizeBytes: 100, ttl: TimeSpan.FromSeconds(120), customAccessTime: t0);

        cache.Get("key2").Should().BeNull();
        cache.Get("key1").Should().Be("val1");
        cache.Get("key3").Should().Be("val3");
    }

    [Fact]
    public void Eviction_NoEviction_ThrowsExceptionWhenMemoryExceeded()
    {
        var cache = new MemoryEvictionSimulator(maxSizeBytes: 150, EvictionPolicy.NoEviction);

        cache.Put("k1", "v1", sizeBytes: 100);
        
        Action act = () => cache.Put("k2", "v2", sizeBytes: 100);
        act.Should().Throw<InvalidOperationException>()
           .WithMessage("*OOM command not allowed*");
    }

    [Fact]
    public void Eviction_VolatileLru_LeavesNonVolatileKeysIntact()
    {
        var cache = new MemoryEvictionSimulator(maxSizeBytes: 200, EvictionPolicy.VolatileLru);
        var t0 = DateTime.UtcNow;

        // key1 has no TTL (persistent)
        cache.Put("key1_persistent", "v1", sizeBytes: 100, ttl: null, customAccessTime: t0);
        // key2 has TTL (volatile)
        cache.Put("key2_volatile", "v2", sizeBytes: 100, ttl: TimeSpan.FromHours(1), customAccessTime: t0.AddSeconds(1));

        // Inserting key3 should evict key2 (volatile), never key1 (persistent)
        cache.Put("key3_volatile", "v3", sizeBytes: 100, ttl: TimeSpan.FromHours(2), customAccessTime: t0.AddSeconds(2));

        cache.Get("key1_persistent").Should().Be("v1");
        cache.Get("key2_volatile").Should().BeNull(); // Evicted!
        cache.Get("key3_volatile").Should().Be("v3");
    }

    // ==========================================
    // 3. DSA TREE METRICS TESTS (LC #543 & LC #110)
    // ==========================================

    [Fact]
    public void LC543_DiameterOfBinaryTree_StandardTree_ReturnsCorrectEdgeCount()
    {
        var solvers = new BinaryTreeMetricsSolvers();
        // Tree: [1, 2, 3, 4, 5]
        //        1
        //       / \
        //      2   3
        //     / \
        //    4   5
        // Longest path: 4 -> 2 -> 1 -> 3 or 5 -> 2 -> 1 -> 3 (length 3 edges)
        var root = BinaryTreeMetricsSolvers.BuildTree(new int?[] { 1, 2, 3, 4, 5 });
        solvers.DiameterOfBinaryTree(root).Should().Be(3);
    }

    [Fact]
    public void LC543_DiameterOfBinaryTree_PathNotPassingThroughRoot_ReturnsSubtreeDiameter()
    {
        var solvers = new BinaryTreeMetricsSolvers();
        // Construct tree where deepest path is completely in left subtree:
        //        1
        //       /
        //      2
        //     / \
        //    3   4
        //   /     \
        //  5       6
        // Longest path: 5 -> 3 -> 2 -> 4 -> 6 (length 4 edges), not passing through 1.
        var root = new TreeNode(1)
        {
            left = new TreeNode(2)
            {
                left = new TreeNode(3) { left = new TreeNode(5) },
                right = new TreeNode(4) { right = new TreeNode(6) }
            }
        };

        solvers.DiameterOfBinaryTree(root).Should().Be(4);
    }

    [Fact]
    public void LC543_DiameterOfBinaryTree_SingleNodeAndEmpty_ReturnsZero()
    {
        var solvers = new BinaryTreeMetricsSolvers();
        solvers.DiameterOfBinaryTree(null).Should().Be(0);
        solvers.DiameterOfBinaryTree(new TreeNode(42)).Should().Be(0);
    }

    [Fact]
    public void LC110_IsBalanced_BalancedTree_ReturnsTrue()
    {
        var solvers = new BinaryTreeMetricsSolvers();
        // Tree: [3, 9, 20, null, null, 15, 7]
        //        3
        //       / \
        //      9   20
        //         /  \
        //        15   7
        var root = BinaryTreeMetricsSolvers.BuildTree(new int?[] { 3, 9, 20, null, null, 15, 7 });
        solvers.IsBalanced(root).Should().BeTrue();
    }

    [Fact]
    public void LC110_IsBalanced_UnbalancedTree_ReturnsFalse()
    {
        var solvers = new BinaryTreeMetricsSolvers();
        // Tree: [1, 2, 2, 3, 3, null, null, 4, 4]
        // Left subtree has depth 4, right subtree has depth 2 -> Imbalance > 1
        var root = BinaryTreeMetricsSolvers.BuildTree(new int?[] { 1, 2, 2, 3, 3, null, null, 4, 4 });
        solvers.IsBalanced(root).Should().BeFalse();
    }

    [Fact]
    public void LC110_IsBalanced_SingleNodeAndNull_ReturnsTrue()
    {
        var solvers = new BinaryTreeMetricsSolvers();
        solvers.IsBalanced(null).Should().BeTrue();
        solvers.IsBalanced(new TreeNode(10)).Should().BeTrue();
    }
}
