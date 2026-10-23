namespace Day34.OpenTelemetryTracing;

public static class TaskSchedulerSolver
{
    /// <summary>
    /// LeetCode #621: Task Scheduler (Medium).
    /// Mathematical greedy approach:
    /// Time Complexity: O(N) where N = tasks.Length, Space Complexity: O(1) [26 characters].
    /// </summary>
    public static int LeastIntervalMath(char[] tasks, int n)
    {
        if (tasks == null || tasks.Length == 0) return 0;
        if (n <= 0) return tasks.Length;

        Span<int> frequencies = stackalloc int[26];
        foreach (char t in tasks)
        {
            frequencies[t - 'A']++;
        }

        int maxFreq = 0;
        int maxCount = 0;

        for (int i = 0; i < 26; i++)
        {
            if (frequencies[i] > maxFreq)
            {
                maxFreq = frequencies[i];
                maxCount = 1;
            }
            else if (frequencies[i] == maxFreq && maxFreq > 0)
            {
                maxCount++;
            }
        }

        // Formula: (maxFreq - 1) * (n + 1) + maxCount
        int calculatedTime = (maxFreq - 1) * (n + 1) + maxCount;
        return Math.Max(tasks.Length, calculatedTime);
    }

    /// <summary>
    /// Priority Queue (Max-Heap) + Cooldown Queue simulation.
    /// Time Complexity: O(N * log 26) = O(N), Space Complexity: O(26) = O(1).
    /// Simulates actual clock cycles and CPU idle periods.
    /// </summary>
    public static int LeastIntervalSimulation(char[] tasks, int n)
    {
        if (tasks == null || tasks.Length == 0) return 0;
        if (n <= 0) return tasks.Length;

        var freqMap = new Dictionary<char, int>();
        foreach (var t in tasks)
        {
            freqMap[t] = freqMap.GetValueOrDefault(t, 0) + 1;
        }

        // Max-heap by frequency (in .NET PriorityQueue, lower priority dequeues first, so invert priority to negative)
        var maxHeap = new PriorityQueue<char, int>();
        foreach (var (task, count) in freqMap)
        {
            maxHeap.Enqueue(task, -count);
        }

        // Queue of (task, remainingCount, availableAtTime)
        var cooldownQueue = new Queue<(char Task, int Count, int AvailableTime)>();
        int time = 0;

        while (maxHeap.Count > 0 || cooldownQueue.Count > 0)
        {
            time++;

            if (maxHeap.Count > 0)
            {
                maxHeap.TryDequeue(out var task, out var negCount);
                int count = -negCount;
                count--;
                if (count > 0)
                {
                    cooldownQueue.Enqueue((task, count, time + n));
                }
            }

            if (cooldownQueue.Count > 0 && cooldownQueue.Peek().AvailableTime <= time)
            {
                var ready = cooldownQueue.Dequeue();
                maxHeap.Enqueue(ready.Task, -ready.Count);
            }
        }

        return time;
    }
}
