using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace Day16.SynchronizationLocks.Tests;

public class Day16Tests
{
    [Fact]
    public void LeetCode143_ReorderList_EvenAndOddLengths()
    {
        // Odd length: [1, 2, 3, 4, 5] -> [1, 5, 2, 4, 3]
        var list1 = ListNode.FromArray(new[] { 1, 2, 3, 4, 5 });
        AdvancedLinkedListSolvers.ReorderList(list1);
        list1!.ToArray().Should().Equal(new[] { 1, 5, 2, 4, 3 });

        // Even length: [1, 2, 3, 4] -> [1, 4, 2, 3]
        var list2 = ListNode.FromArray(new[] { 1, 2, 3, 4 });
        AdvancedLinkedListSolvers.ReorderList(list2);
        list2!.ToArray().Should().Equal(new[] { 1, 4, 2, 3 });

        // Short length: [1, 2] -> [1, 2]
        var list3 = ListNode.FromArray(new[] { 1, 2 });
        AdvancedLinkedListSolvers.ReorderList(list3);
        list3!.ToArray().Should().Equal(new[] { 1, 2 });
    }

    [Fact]
    public void LeetCode19_RemoveNthFromEnd_VariousPositions()
    {
        // Remove 2nd from end: [1, 2, 3, 4, 5], n=2 -> [1, 2, 3, 5]
        var list1 = ListNode.FromArray(new[] { 1, 2, 3, 4, 5 });
        var res1 = AdvancedLinkedListSolvers.RemoveNthFromEnd(list1, 2);
        res1!.ToArray().Should().Equal(new[] { 1, 2, 3, 5 });

        // Remove only node: [1], n=1 -> null
        var list2 = ListNode.FromArray(new[] { 1 });
        var res2 = AdvancedLinkedListSolvers.RemoveNthFromEnd(list2, 1);
        res2.Should().BeNull();

        // Remove head: [1, 2], n=2 -> [2]
        var list3 = ListNode.FromArray(new[] { 1, 2 });
        var res3 = AdvancedLinkedListSolvers.RemoveNthFromEnd(list3, 2);
        res3!.ToArray().Should().Equal(new[] { 2 });
    }

    [Fact]
    public async Task LockFreeCounter_ConcurrentIncrements_NoDataRace()
    {
        var counter = new LockFreeCounter();
        const int tasksCount = 10;
        const int incPerTask = 1000;

        var tasks = new List<Task>();
        for (int i = 0; i < tasksCount; i++)
        {
            tasks.Add(Task.Run(() =>
            {
                for (int j = 0; j < incPerTask; j++)
                {
                    counter.Increment();
                }
            }));
        }

        await Task.WhenAll(tasks);
        counter.Value.Should().Be(tasksCount * incPerTask);

        // Update if greater
        counter.UpdateIfGreaterThan(5000).Should().BeFalse();
        counter.UpdateIfGreaterThan(20000).Should().BeTrue();
        counter.Value.Should().Be(20000);
    }

    [Fact]
    public async Task AsyncResourceThrottle_LimitsConcurrencyAndHonorsTimeouts()
    {
        var throttle = new AsyncResourceThrottle(maxConcurrency: 2);
        throttle.CurrentAvailable.Should().Be(2);

        var lease1 = await throttle.TryAcquireLeaseAsync(TimeSpan.FromMilliseconds(50));
        lease1.Should().NotBeNull();
        throttle.CurrentAvailable.Should().Be(1);

        var lease2 = await throttle.TryAcquireLeaseAsync(TimeSpan.FromMilliseconds(50));
        lease2.Should().NotBeNull();
        throttle.CurrentAvailable.Should().Be(0);

        // Third lease should time out
        var lease3 = await throttle.TryAcquireLeaseAsync(TimeSpan.FromMilliseconds(20));
        lease3.Should().BeNull();

        // Release first lease
        lease1!.Dispose();
        throttle.CurrentAvailable.Should().Be(1);

        // Now third can acquire
        var lease3Retry = await throttle.TryAcquireLeaseAsync(TimeSpan.FromMilliseconds(50));
        lease3Retry.Should().NotBeNull();

        lease2!.Dispose();
        lease3Retry!.Dispose();
        throttle.CurrentAvailable.Should().Be(2);
    }

    [Fact]
    public void ReadHeavyCache_GetOrAdd_ThreadSafe()
    {
        using var cache = new ReadHeavyCache<string, string>();
        int factoryInvocations = 0;

        Parallel.For(0, 50, _ =>
        {
            cache.GetOrAdd("key1", k =>
            {
                System.Threading.Interlocked.Increment(ref factoryInvocations);
                return $"computed-{k}";
            });
        });

        factoryInvocations.Should().Be(1);
        cache.TryGet("key1", out var val).Should().BeTrue();
        val.Should().Be("computed-key1");
    }

    [Fact]
    public void DistributedLockSimulator_MutualExclusion_AndFencingRejection()
    {
        var sim = new DistributedLockSimulator();

        // 1. Worker A acquires lock
        var resA = sim.AcquireLock("invoice:100", "worker-A", TimeSpan.FromSeconds(5));
        resA.Success.Should().BeTrue();
        resA.Token.Should().NotBeNull();
        var tokenA = resA.Token!;

        // 2. Worker B fails to acquire
        var resB = sim.AcquireLock("invoice:100", "worker-B", TimeSpan.FromSeconds(5));
        resB.Success.Should().BeFalse();

        // 3. Worker A commits write to storage with valid fencing token
        sim.ValidateFencingTokenForStorageWrite(tokenA.FencingToken).Should().BeTrue();

        // 4. Worker A releases lock
        sim.ReleaseLock(tokenA).Should().BeTrue();

        // 5. Worker B now succeeds with newer fencing token
        var resB2 = sim.AcquireLock("invoice:100", "worker-B", TimeSpan.FromSeconds(5));
        resB2.Success.Should().BeTrue();
        var tokenB = resB2.Token!;
        tokenB.FencingToken.Should().BeGreaterThan(tokenA.FencingToken);

        // 6. Worker B commits
        sim.ValidateFencingTokenForStorageWrite(tokenB.FencingToken).Should().BeTrue();

        // 7. Stale Worker A attempts late write with old fencing token -> REJECTED!
        sim.ValidateFencingTokenForStorageWrite(tokenA.FencingToken).Should().BeFalse();
    }
}
