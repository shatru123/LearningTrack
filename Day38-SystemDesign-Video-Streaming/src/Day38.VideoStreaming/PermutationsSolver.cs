namespace Day38.VideoStreaming;

public static class PermutationsSolver
{
    /// <summary>
    /// LeetCode #46: Permutations (Medium).
    /// All elements in nums are distinct.
    /// Time Complexity: O(N * N!), Space Complexity: O(N) stack.
    /// </summary>
    public static IList<IList<int>> Permute(int[] nums)
    {
        var result = new List<IList<int>>();
        if (nums == null || nums.Length == 0) return result;

        var used = new bool[nums.Length];
        var current = new List<int>(nums.Length);
        BacktrackPermute(nums, used, current, result);
        return result;
    }

    private static void BacktrackPermute(int[] nums, bool[] used, List<int> current, List<IList<int>> result)
    {
        if (current.Count == nums.Length)
        {
            result.Add(new List<int>(current));
            return;
        }

        for (int i = 0; i < nums.Length; i++)
        {
            if (used[i]) continue;

            used[i] = true;
            current.Add(nums[i]);
            BacktrackPermute(nums, used, current, result);
            current.RemoveAt(current.Count - 1);
            used[i] = false;
        }
    }

    /// <summary>
    /// LeetCode #47: Permutations II (Medium).
    /// Elements in nums may contain duplicates. Output must contain unique permutations.
    /// Time Complexity: O(N * N!), Space Complexity: O(N).
    /// </summary>
    public static IList<IList<int>> PermuteUnique(int[] nums)
    {
        var result = new List<IList<int>>();
        if (nums == null || nums.Length == 0) return result;

        Array.Sort(nums);
        var used = new bool[nums.Length];
        var current = new List<int>(nums.Length);
        BacktrackPermuteUnique(nums, used, current, result);
        return result;
    }

    private static void BacktrackPermuteUnique(int[] nums, bool[] used, List<int> current, List<IList<int>> result)
    {
        if (current.Count == nums.Length)
        {
            result.Add(new List<int>(current));
            return;
        }

        for (int i = 0; i < nums.Length; i++)
        {
            if (used[i]) continue;

            // If current element is duplicate of previous element, and previous duplicate
            // was NOT used in current recursion branch, skip to avoid duplicate permutations.
            if (i > 0 && nums[i] == nums[i - 1] && !used[i - 1])
            {
                continue;
            }

            used[i] = true;
            current.Add(nums[i]);
            BacktrackPermuteUnique(nums, used, current, result);
            current.RemoveAt(current.Count - 1);
            used[i] = false;
        }
    }
}
