namespace Day47.TerraformIaC;

using System;
using System.Collections.Generic;

public class LongestIncreasingSubsequenceSolver
{
    /// <summary>
    /// Classic O(N^2) Dynamic Programming solution.
    /// </summary>
    public static int SolveDp(int[] nums)
    {
        if (nums == null || nums.Length == 0) return 0;

        int n = nums.Length;
        int[] dp = new int[n];
        Array.Fill(dp, 1);
        int maxLen = 1;

        for (int i = 1; i < n; i++)
        {
            for (int j = 0; j < i; j++)
            {
                if (nums[j] < nums[i])
                {
                    dp[i] = Math.Max(dp[i], dp[j] + 1);
                }
            }
            maxLen = Math.Max(maxLen, dp[i]);
        }

        return maxLen;
    }

    /// <summary>
    /// Optimal O(N log N) Patience Sorting with Binary Search.
    /// </summary>
    public static int SolvePatience(int[] nums)
    {
        if (nums == null || nums.Length == 0) return 0;

        var tails = new List<int>();

        foreach (int x in nums)
        {
            int idx = tails.BinarySearch(x);
            if (idx < 0) idx = ~idx;

            if (idx == tails.Count)
            {
                tails.Add(x);
            }
            else
            {
                tails[idx] = x;
            }
        }

        return tails.Count;
    }

    /// <summary>
    /// Reconstructs the actual sequence of Longest Increasing Subsequence.
    /// </summary>
    public static int[] GetActualLis(int[] nums)
    {
        if (nums == null || nums.Length == 0) return Array.Empty<int>();

        int n = nums.Length;
        int[] dp = new int[n];
        int[] prev = new int[n];
        Array.Fill(dp, 1);
        Array.Fill(prev, -1);

        int maxLen = 1;
        int bestEndIndex = 0;

        for (int i = 1; i < n; i++)
        {
            for (int j = 0; j < i; j++)
            {
                if (nums[j] < nums[i] && dp[j] + 1 > dp[i])
                {
                    dp[i] = dp[j] + 1;
                    prev[i] = j;
                }
            }

            if (dp[i] > maxLen)
            {
                maxLen = dp[i];
                bestEndIndex = i;
            }
        }

        var result = new List<int>();
        int curr = bestEndIndex;
        while (curr != -1)
        {
            result.Add(nums[curr]);
            curr = prev[curr];
        }

        result.Reverse();
        return result.ToArray();
    }
}
