namespace Day37.ChatSystem;

public static class CombinationSumSolver
{
    /// <summary>
    /// LeetCode #39: Combination Sum (Medium).
    /// Candidates are distinct. Elements can be chosen unlimited number of times.
    /// Time Complexity: O(2^T) where T = target / min(candidates). Space: O(T) stack.
    /// </summary>
    public static IList<IList<int>> CombinationSum(int[] candidates, int target)
    {
        var result = new List<IList<int>>();
        if (candidates == null || candidates.Length == 0) return result;

        Array.Sort(candidates);
        var current = new List<int>();
        BacktrackSum1(candidates, target, 0, current, result);
        return result;
    }

    private static void BacktrackSum1(int[] candidates, int remain, int start, List<int> current, List<IList<int>> result)
    {
        if (remain == 0)
        {
            result.Add(new List<int>(current));
            return;
        }

        for (int i = start; i < candidates.Length; i++)
        {
            if (candidates[i] > remain) break; // Prune: sorted array, no subsequent elements can fit

            current.Add(candidates[i]);
            // Pass 'i' instead of 'i + 1' to allow reuse of current element
            BacktrackSum1(candidates, remain - candidates[i], i, current, result);
            current.RemoveAt(current.Count - 1);
        }
    }

    /// <summary>
    /// LeetCode #40: Combination Sum II (Medium).
    /// Candidates may contain duplicates. Each element can be used AT MOST ONCE.
    /// Result must not contain duplicate combinations.
    /// </summary>
    public static IList<IList<int>> CombinationSum2(int[] candidates, int target)
    {
        var result = new List<IList<int>>();
        if (candidates == null || candidates.Length == 0) return result;

        Array.Sort(candidates);
        var current = new List<int>();
        BacktrackSum2(candidates, target, 0, current, result);
        return result;
    }

    private static void BacktrackSum2(int[] candidates, int remain, int start, List<int> current, List<IList<int>> result)
    {
        if (remain == 0)
        {
            result.Add(new List<int>(current));
            return;
        }

        for (int i = start; i < candidates.Length; i++)
        {
            if (candidates[i] > remain) break; // Prune

            // Skip duplicate sibling branches at the same tree depth
            if (i > start && candidates[i] == candidates[i - 1])
            {
                continue;
            }

            current.Add(candidates[i]);
            // Pass 'i + 1' so each element is only used once
            BacktrackSum2(candidates, remain - candidates[i], i + 1, current, result);
            current.RemoveAt(current.Count - 1);
        }
    }
}
