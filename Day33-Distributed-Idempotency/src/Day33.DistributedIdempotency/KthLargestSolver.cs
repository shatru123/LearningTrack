namespace Day33.DistributedIdempotency;

public static class KthLargestSolver
{
    private static readonly Random _random = new();

    /// <summary>
    /// LeetCode #215: Kth Largest Element in an Array (Medium).
    /// Quickselect Algorithm (Hoare's selection algorithm) with randomized pivot.
    /// Average Time Complexity: O(N), Worst Case: O(N^2), Space Complexity: O(1) iterative.
    /// </summary>
    public static int FindKthLargestQuickselect(int[] nums, int k)
    {
        if (nums == null || nums.Length == 0 || k <= 0 || k > nums.Length)
            throw new ArgumentException("Invalid arguments for Quickselect");

        // The Kth largest element is at index (nums.Length - k) in sorted ascending order
        int targetIndex = nums.Length - k;
        int left = 0;
        int right = nums.Length - 1;

        while (left <= right)
        {
            int pivotIndex = Partition(nums, left, right);

            if (pivotIndex == targetIndex)
            {
                return nums[pivotIndex];
            }
            else if (pivotIndex < targetIndex)
            {
                left = pivotIndex + 1;
            }
            else
            {
                right = pivotIndex - 1;
            }
        }

        return nums[targetIndex];
    }

    private static int Partition(int[] nums, int left, int right)
    {
        // Randomized pivot to avoid worst-case O(N^2) on sorted/reverse-sorted inputs
        int randomIndex = _random.Next(left, right + 1);
        Swap(nums, randomIndex, right);

        int pivotValue = nums[right];
        int storeIndex = left;

        for (int i = left; i < right; i++)
        {
            if (nums[i] <= pivotValue)
            {
                Swap(nums, storeIndex, i);
                storeIndex++;
            }
        }

        Swap(nums, storeIndex, right);
        return storeIndex;
    }

    private static void Swap(int[] nums, int i, int j)
    {
        int temp = nums[i];
        nums[i] = nums[j];
        nums[j] = temp;
    }

    /// <summary>
    /// LeetCode #215: Kth Largest Element using a Min-Heap (PriorityQueue in .NET 6+).
    /// Time Complexity: O(N * log K), Space Complexity: O(K).
    /// </summary>
    public static int FindKthLargestMinHeap(int[] nums, int k)
    {
        if (nums == null || nums.Length == 0 || k <= 0 || k > nums.Length)
            throw new ArgumentException("Invalid arguments for Min-Heap");

        var minHeap = new PriorityQueue<int, int>();

        foreach (var num in nums)
        {
            minHeap.Enqueue(num, num);
            if (minHeap.Count > k)
            {
                minHeap.Dequeue();
            }
        }

        return minHeap.Peek();
    }
}
