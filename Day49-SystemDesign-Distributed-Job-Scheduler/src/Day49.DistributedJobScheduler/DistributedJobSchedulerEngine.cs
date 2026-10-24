namespace Day49.DistributedJobScheduler;

using System;
using System.Collections.Generic;
using System.Linq;

public class SchedulerLeaderElection
{
    private readonly object _lock = new();
    public string? CurrentLeaderId { get; private set; }
    public long FencingToken { get; private set; }
    public DateTime LeaseExpiresAtUtc { get; private set; }

    public bool TryAcquireOrRenewLease(string candidateId, TimeSpan leaseDuration, DateTime nowUtc, out long grantedFencingToken)
    {
        lock (_lock)
        {
            if (CurrentLeaderId == null || nowUtc >= LeaseExpiresAtUtc || CurrentLeaderId == candidateId)
            {
                if (CurrentLeaderId != candidateId)
                {
                    FencingToken++; // Increment monotonic fencing token on leadership transition
                }
                CurrentLeaderId = candidateId;
                LeaseExpiresAtUtc = nowUtc.Add(leaseDuration);
                grantedFencingToken = FencingToken;
                return true;
            }

            grantedFencingToken = 0;
            return false;
        }
    }

    public void RelinquishLeadership(string leaderId)
    {
        lock (_lock)
        {
            if (CurrentLeaderId == leaderId)
            {
                CurrentLeaderId = null;
                LeaseExpiresAtUtc = DateTime.MinValue;
            }
        }
    }
}

public record WorkerNode(string WorkerId, int MaxCapacity, DateTime LastHeartbeatUtc);

public class WorkerNodeRegistry
{
    private readonly Dictionary<string, WorkerNode> _workers = new();
    private readonly Dictionary<string, string> _taskAssignments = new(); // TaskId -> WorkerId

    public void RegisterOrHeartbeat(string workerId, int capacity, DateTime nowUtc)
    {
        _workers[workerId] = new WorkerNode(workerId, capacity, nowUtc);
    }

    public void AssignTask(string taskId, string workerId)
    {
        _taskAssignments[taskId] = workerId;
    }

    public List<string> EvictDeadWorkers(TimeSpan timeout, DateTime nowUtc)
    {
        var deadWorkers = _workers.Values
            .Where(w => nowUtc - w.LastHeartbeatUtc > timeout)
            .Select(w => w.WorkerId)
            .ToList();

        var reallocatedTasks = new List<string>();

        foreach (var deadWorker in deadWorkers)
        {
            _workers.Remove(deadWorker);

            var tasksToReallocate = _taskAssignments
                .Where(kvp => kvp.Value == deadWorker)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var t in tasksToReallocate)
            {
                _taskAssignments.Remove(t);
                reallocatedTasks.Add(t);
            }
        }

        return reallocatedTasks;
    }

    public bool IsWorkerAlive(string workerId) => _workers.ContainsKey(workerId);
}

public record ScheduledTask(
    string TaskId,
    DateTime DueAtUtc,
    int RetryCount = 0,
    int MaxRetries = 3);

public class DistributedTaskScheduler
{
    private readonly List<ScheduledTask> _taskQueue = new();
    private readonly HashSet<string> _processedExecutionTokens = new();

    public void EnqueueTask(ScheduledTask task)
    {
        _taskQueue.Add(task);
    }

    public List<ScheduledTask> PollDueTasks(DateTime nowUtc)
    {
        var dueTasks = _taskQueue.Where(t => t.DueAtUtc <= nowUtc).ToList();
        foreach (var t in dueTasks)
        {
            _taskQueue.Remove(t);
        }
        return dueTasks;
    }

    public bool TryExecuteWithIdempotencyToken(string executionToken)
    {
        if (_processedExecutionTokens.Contains(executionToken))
        {
            return false; // Duplicate execution rejected
        }

        _processedExecutionTokens.Add(executionToken);
        return true;
    }

    public static TimeSpan CalculateBackoffWithJitter(int retryCount, int baseDelayMs = 100)
    {
        // Exponential backoff: base * 2^retry
        double delay = baseDelayMs * Math.Pow(2, Math.Min(retryCount, 6));
        // Deterministic jitter simulation (+ 10%)
        return TimeSpan.FromMilliseconds(delay * 1.1);
    }
}
