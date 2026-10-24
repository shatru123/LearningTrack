namespace Day48.CloudArchitecture;

using System;
using System.Linq;

public class PartitionEqualSubsetSumSolver
{
    /// <summary>
    /// LeetCode #416: Partition Equal Subset Sum.
    /// Classic 2D Dynamic Programming solution O(N * Target).
    /// </summary>
    public static bool CanPartition2D(int[] nums)
    {
        if (nums == null || nums.Length <= 1) return false;

        int totalSum = nums.Sum();
        if (totalSum % 2 != 0) return false; // Odd sum cannot be partitioned equally

        int target = totalSum / 2;
        int n = nums.Length;
        bool[,] dp = new bool[n + 1, target + 1];

        for (int i = 0; i <= n; i++)
        {
            dp[i, 0] = true; // sum 0 is always achievable with empty subset
        }

        for (int i = 1; i <= n; i++)
        {
            int num = nums[i - 1];
            for (int j = 1; j <= target; j++)
            {
                if (j >= num)
                {
                    dp[i, j] = dp[i - 1, j] || dp[i - 1, j - num];
                }
                else
                {
                    dp[i, j] = dp[i - 1, j];
                }
            }
        }

        return dp[n, target];
    }

    /// <summary>
    /// Space-optimized 1D boolean array DP running right-to-left O(Target) space.
    /// </summary>
    public static bool CanPartition1D(int[] nums)
    {
        if (nums == null || nums.Length <= 1) return false;

        int totalSum = nums.Sum();
        if (totalSum % 2 != 0) return false;

        int target = totalSum / 2;
        bool[] dp = new bool[target + 1];
        dp[0] = true;

        foreach (int num in nums)
        {
            for (int j = target; j >= num; j--)
            {
                if (dp[j - num])
                {
                    dp[j] = true;
                }
            }
            if (dp[target]) return true; // Early exit
        }

        return dp[target];
    }
}
