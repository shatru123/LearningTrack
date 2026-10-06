using System;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace Day17.RedisCachePatterns.Tests;

public class Day17Tests
{
    [Fact]
    public void LeetCode226_InvertTree_InvertsLeftAndRightNodes()
    {
        // Tree: [4, 2, 7, 1, 3, 6, 9] -> Inverted: [4, 7, 2, 9, 6, 3, 1]
        var root = TreeNode.FromLevelOrder(new int?[] { 4, 2, 7, 1, 3, 6, 9 });
        var inverted = BinaryTreeSolvers.InvertTree(root);

        inverted.Should().NotBeNull();
        inverted!.ToLevelOrder().Should().Equal(new int?[] { 4, 7, 2, 9, 6, 3, 1 });

        // Null tree
        BinaryTreeSolvers.InvertTree(null).Should().BeNull();
    }

    [Fact]
    public void LeetCode226_InvertTreeIterative_MatchesRecursive()
    {
        var root = TreeNode.FromLevelOrder(new int?[] { 2, 1, 3 });
        var inverted = BinaryTreeSolvers.InvertTreeIterative(root);

        inverted!.ToLevelOrder().Should().Equal(new int?[] { 2, 3, 1 });
    }

    [Fact]
    public void LeetCode104_MaxDepth_CalculatesCorrectDepth()
    {
        // Tree: [3, 9, 20, null, null, 15, 7] -> depth 3
        var root = TreeNode.FromLevelOrder(new int?[] { 3, 9, 20, null, null, 15, 7 });
        BinaryTreeSolvers.MaxDepth(root).Should().Be(3);
        BinaryTreeSolvers.MaxDepthIterative(root).Should().Be(3);

        // Linear degenerate tree: 1 -> 2 -> 3
        var root2 = TreeNode.FromLevelOrder(new int?[] { 1, null, 2, null, 3 });
        BinaryTreeSolvers.MaxDepth(root2).Should().Be(3);

        // Null tree
        BinaryTreeSolvers.MaxDepth(null).Should().Be(0);
    }

    [Fact]
    public async Task CacheAside_PopulatesCacheOnMiss_AndAvoidsDbOnHit()
    {
        var cache = new MockRedisCacheProvider();
        var manager = new CacheAsideManager(cache);
        int dbQueryCount = 0;

        Task<string> LoadFromDb()
        {
            dbQueryCount++;
            return Task.FromResult("user-profile-data");
        }

        // First call: Cache Miss -> loads DB
        var res1 = await manager.GetOrCreateAsync("user:42", LoadFromDb, TimeSpan.FromMinutes(5));
        res1.Should().Be("user-profile-data");
        dbQueryCount.Should().Be(1);

        // Second call: Cache Hit -> skips DB
        var res2 = await manager.GetOrCreateAsync("user:42", LoadFromDb, TimeSpan.FromMinutes(5));
        res2.Should().Be("user-profile-data");
        dbQueryCount.Should().Be(1);

        var metrics = manager.GetMetrics();
        metrics.Hits.Should().Be(1);
        metrics.Misses.Should().Be(1);
        metrics.DatabaseLoads.Should().Be(1);
    }

    [Fact]
    public async Task CacheStampedeMutex_PreventsThunderingHerd()
    {
        var cache = new MockRedisCacheProvider();
        var manager = new CacheAsideManager(cache);
        int dbQueryCount = 0;

        async Task<string> SlowDbQuery()
        {
            await Task.Delay(50);
            System.Threading.Interlocked.Increment(ref dbQueryCount);
            return "expensive-report";
        }

        // Fire 10 concurrent requests for the exact same un-cached key
        var tasks = new Task<string>[10];
        for (int i = 0; i < 10; i++)
        {
            tasks[i] = manager.GetOrCreateAsync("report:monthly", SlowDbQuery, TimeSpan.FromMinutes(5));
        }

        var results = await Task.WhenAll(tasks);

        // All 10 received the result
        foreach (var r in results) r.Should().Be("expensive-report");

        // BUT the expensive DB query executed exactly ONCE!
        dbQueryCount.Should().Be(1);
    }

    [Fact]
    public async Task WriteThrough_UpdatesCacheAndStoreSimultaneously()
    {
        var cache = new MockRedisCacheProvider();
        var manager = new WriteThroughManager(cache);

        var prod = new Product(101, "Mechanical Keyboard", 129.99m, 50, DateTime.UtcNow);
        await manager.SaveProductAsync(prod, TimeSpan.FromMinutes(10));

        // Read verifies cache is populated immediately
        var fetched = await manager.GetProductAsync(101);
        fetched.Should().NotBeNull();
        fetched!.Name.Should().Be("Mechanical Keyboard");
        fetched.Price.Should().Be(129.99m);
    }

    [Fact]
    public async Task WriteBehind_EnqueuesAndFlushesBatch()
    {
        var cache = new MockRedisCacheProvider();
        var manager = new WriteBehindManager(cache, queueCapacity: 100);

        var prod1 = new Product(1, "Mouse", 49.99m, 100, DateTime.UtcNow);
        var prod2 = new Product(2, "Monitor", 399.99m, 20, DateTime.UtcNow);

        await manager.WriteAsync(prod1, TimeSpan.FromMinutes(5));
        await manager.WriteAsync(prod2, TimeSpan.FromMinutes(5));

        // Immediately available in cache
        var cached = await cache.GetAsync<Product>("product:1");
        cached.Should().NotBeNull();
        cached!.Name.Should().Be("Mouse");

        // Before flush, database does not have them yet
        manager.GetFromDatabaseDirectly(1).Should().BeNull();

        // Flush batch to database
        var flushed = await manager.FlushBatchAsync(10);
        flushed.Should().Be(2);

        // Now database has them
        manager.GetFromDatabaseDirectly(1).Should().NotBeNull();
        manager.GetFromDatabaseDirectly(2).Should().NotBeNull();
    }

    [Fact]
    public void XFetch_EvaluatesProbabilisticEarlyRefresh()
    {
        // Case 1: Already expired -> MUST refresh
        XFetchProtector.ShouldRefreshEarly(
            expiresAtUtc: DateTime.UtcNow.AddSeconds(-1),
            computeDuration: TimeSpan.FromSeconds(1)
        ).Should().BeTrue();

        // Case 2: Far future expiry with tiny compute duration -> should almost certainly NOT refresh
        bool refreshed = XFetchProtector.ShouldRefreshEarly(
            expiresAtUtc: DateTime.UtcNow.AddDays(10),
            computeDuration: TimeSpan.FromMilliseconds(1)
        );
        refreshed.Should().BeFalse();
    }
}
