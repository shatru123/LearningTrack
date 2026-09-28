namespace Day03.AsyncStateMachine;

public static class DsaExercises
{
    /// <summary>
    /// LeetCode 347 - Top K Frequent Elements
    /// Given an integer array nums and an integer k, return the k most frequent elements.
    /// Time Complexity: O(N log K) using a min-heap of size K via PriorityQueue&lt;int, int&gt;.
    /// Space Complexity: O(N) for frequency map + O(K) for priority queue.
    /// </summary>
    public static int[] TopKFrequent(int[] nums, int k)
    {
        if (nums == null || nums.Length == 0 || k <= 0)
            return Array.Empty<int>();

        Dictionary<int, int> frequencies = new();
        foreach (int num in nums)
        {
            frequencies[num] = frequencies.GetValueOrDefault(num, 0) + 1;
        }

        // Min-heap where priority is frequency
        PriorityQueue<int, int> minHeap = new();

        foreach (var (num, freq) in frequencies)
        {
            minHeap.Enqueue(num, freq);
            if (minHeap.Count > k)
            {
                minHeap.Dequeue();
            }
        }

        int[] result = new int[k];
        for (int i = 0; i < k; i++)
        {
            result[i] = minHeap.Dequeue();
        }

        return result;
    }
}
