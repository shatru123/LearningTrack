namespace Day36.NotificationService;

public static class SubsetsSolver
{
    /// <summary>
    /// LeetCode #78: Subsets (Medium) via Backtracking.
    /// Time Complexity: O(N * 2^N), Space Complexity: O(N) recursion stack.
    /// </summary>
    public static IList<IList<int>> Subsets(int[] nums)
    {
        var result = new List<IList<int>>();
        if (nums == null) return result;

        var current = new List<int>();
        BacktrackSubsets(nums, 0, current, result);
        return result;
    }

    private static void BacktrackSubsets(int[] nums, int start, List<int> current, List<IList<int>> result)
    {
        result.Add(new List<int>(current));

        for (int i = start; i < nums.Length; i++)
        {
            current.Add(nums[i]);
            BacktrackSubsets(nums, i + 1, current, result);
            current.RemoveAt(current.Count - 1);
        }
    }

    /// <summary>
    /// LeetCode #78: Subsets (Medium) via Bit Manipulation.
    /// Iterates through 2^N binary bitmasks.
    /// </summary>
    public static IList<IList<int>> SubsetsBitmask(int[] nums)
    {
        var result = new List<IList<int>>();
        if (nums == null) return result;

        int n = nums.Length;
        int totalSubsets = 1 << n; // 2^n

        for (int mask = 0; mask < totalSubsets; mask++)
        {
            var subset = new List<int>();
            for (int i = 0; i < n; i++)
            {
                if ((mask & (1 << i)) != 0)
                {
                    subset.Add(nums[i]);
                }
            }
            result.Add(subset);
        }

        return result;
    }

    /// <summary>
    /// LeetCode #90: Subsets II (Medium) - contains duplicates.
    /// Sorts input and skips duplicate elements at the same tree depth.
    /// Time Complexity: O(N * 2^N), Space Complexity: O(N).
    /// </summary>
    public static IList<IList<int>> SubsetsWithDup(int[] nums)
    {
        var result = new List<IList<int>>();
        if (nums == null) return result;

        Array.Sort(nums);
        var current = new List<int>();
        BacktrackSubsetsWithDup(nums, 0, current, result);
        return result;
    }

    private static void BacktrackSubsetsWithDup(int[] nums, int start, List<int> current, List<IList<int>> result)
    {
        result.Add(new List<int>(current));

        for (int i = start; i < nums.Length; i++)
        {
            // Skip duplicate sibling branches
            if (i > start && nums[i] == nums[i - 1])
            {
                continue;
            }

            current.Add(nums[i]);
            BacktrackSubsetsWithDup(nums, i + 1, current, result);
            current.RemoveAt(current.Count - 1);
        }
    }
}
