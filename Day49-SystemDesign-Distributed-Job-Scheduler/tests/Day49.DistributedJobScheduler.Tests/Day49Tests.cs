namespace Day49.DistributedJobScheduler.Tests;

using System;
using Day49.DistributedJobScheduler;
using Xunit;

public class Day49Tests
{
    [Fact]
    public void Test1_LeaderElection_AcquiresLeaseAndFencingToken()
    {
        var election = new SchedulerLeaderElection();
        var now = DateTime.UtcNow;

        bool success = election.TryAcquireOrRenewLease("node-1", TimeSpan.FromSeconds(10), now, out long token1);

        Assert.True(success);
        Assert.Equal("node-1", election.CurrentLeaderId);
        Assert.Equal(1, token1);
    }

    [Fact]
    public void Test2_LeaderElection_PreventsSplitBrainDuringActiveLease()
    {
        var election = new SchedulerLeaderElection();
        var now = DateTime.UtcNow;

        election.TryAcquireOrRenewLease("node-1", TimeSpan.FromSeconds(10), now, out _);
        bool candidateAcquire = election.TryAcquireOrRenewLease("node-2", TimeSpan.FromSeconds(10), now.AddSeconds(2), out _);

        Assert.False(candidateAcquire);
        Assert.Equal("node-1", election.CurrentLeaderId);
    }

    [Fact]
    public void Test3_LeaderElection_FailsOverWithMonotonicFencingTokenWhenExpired()
    {
        var election = new SchedulerLeaderElection();
        var now = DateTime.UtcNow;

        election.TryAcquireOrRenewLease("node-1", TimeSpan.FromSeconds(5), now, out _);
        bool newLeaderAcquired = election.TryAcquireOrRenewLease("node-2", TimeSpan.FromSeconds(5), now.AddSeconds(6), out long token2);

        Assert.True(newLeaderAcquired);
        Assert.Equal("node-2", election.CurrentLeaderId);
        Assert.Equal(2, token2); // Incremented fencing token
    }

    [Fact]
    public void Test4_WorkerRegistry_HeartbeatAndDeadWorkerEviction()
    {
        var registry = new WorkerNodeRegistry();
        var now = DateTime.UtcNow;

        registry.RegisterOrHeartbeat("worker-A", capacity: 4, now);
        registry.RegisterOrHeartbeat("worker-B", capacity: 4, now);
        registry.AssignTask("task-101", "worker-A");
        registry.AssignTask("task-102", "worker-B");

        // Advance clock by 15s; worker-B heartbeats at +12s, worker-A misses
        registry.RegisterOrHeartbeat("worker-B", capacity: 4, now.AddSeconds(12));

        var reallocated = registry.EvictDeadWorkers(TimeSpan.FromSeconds(10), now.AddSeconds(15));

        Assert.Contains("task-101", reallocated);
        Assert.DoesNotContain("task-102", reallocated);
        Assert.False(registry.IsWorkerAlive("worker-A"));
        Assert.True(registry.IsWorkerAlive("worker-B"));
    }

    [Fact]
    public void Test5_TaskScheduler_PollsDueTasksAccurately()
    {
        var scheduler = new DistributedTaskScheduler();
        var now = DateTime.UtcNow;

        scheduler.EnqueueTask(new ScheduledTask("task-past", now.AddMinutes(-5)));
        scheduler.EnqueueTask(new ScheduledTask("task-future", now.AddMinutes(10)));

        var due = scheduler.PollDueTasks(now);

        Assert.Single(due);
        Assert.Equal("task-past", due[0].TaskId);
    }

    [Fact]
    public void Test6_TaskScheduler_IdempotencyTokenRejectsDuplicateExecution()
    {
        var scheduler = new DistributedTaskScheduler();
        string token = "token-job-99-attempt-1";

        bool firstRun = scheduler.TryExecuteWithIdempotencyToken(token);
        bool duplicateRun = scheduler.TryExecuteWithIdempotencyToken(token);

        Assert.True(firstRun);
        Assert.False(duplicateRun);
    }

    [Fact]
    public void Test7_TaskScheduler_ExponentialBackoffWithJitter()
    {
        var delay0 = DistributedTaskScheduler.CalculateBackoffWithJitter(0, 100);
        var delay1 = DistributedTaskScheduler.CalculateBackoffWithJitter(1, 100);
        var delay2 = DistributedTaskScheduler.CalculateBackoffWithJitter(2, 100);

        Assert.True(delay1 > delay0);
        Assert.True(delay2 > delay1);
        Assert.Equal(110.0, delay0.TotalMilliseconds); // 100 * 1 * 1.1
        Assert.Equal(220.0, delay1.TotalMilliseconds); // 100 * 2 * 1.1
    }

    [Theory]
    [InlineData(new int[] { 0, 1, 0, 2, 1, 0, 1, 3, 2, 1, 2, 1 }, 6)]
    [InlineData(new int[] { 4, 2, 0, 3, 2, 5 }, 9)]
    [InlineData(new int[] { 3, 0, 0, 2, 0, 4 }, 10)]
    [InlineData(new int[] { 2, 0, 2 }, 2)]
    public void Test8_TrappingRainWater_TwoPointersAndStackMatch(int[] height, int expected)
    {
        int resTwoPointers = TrappingRainWaterSolver.TrapTwoPointers(height);
        int resStack = TrappingRainWaterSolver.TrapMonotonicStack(height);
        int resDp = TrappingRainWaterSolver.TrapDp(height);

        Assert.Equal(expected, resTwoPointers);
        Assert.Equal(expected, resStack);
        Assert.Equal(expected, resDp);
    }

    [Fact]
    public void Test9_TrappingRainWater_FlatAndMonotonicTerrain_ReturnsZero()
    {
        Assert.Equal(0, TrappingRainWaterSolver.TrapTwoPointers(new int[] { 5, 5, 5, 5 }));
        Assert.Equal(0, TrappingRainWaterSolver.TrapTwoPointers(new int[] { 1, 2, 3, 4, 5 }));
        Assert.Equal(0, TrappingRainWaterSolver.TrapTwoPointers(new int[] { 5, 4, 3, 2, 1 }));
    }

    [Fact]
    public void Test10_TrappingRainWater_EdgeCasesHandled()
    {
        Assert.Equal(0, TrappingRainWaterSolver.TrapTwoPointers(null!));
        Assert.Equal(0, TrappingRainWaterSolver.TrapTwoPointers(Array.Empty<int>()));
        Assert.Equal(0, TrappingRainWaterSolver.TrapTwoPointers(new int[] { 1 }));
        Assert.Equal(0, TrappingRainWaterSolver.TrapTwoPointers(new int[] { 1, 2 }));
    }
}
