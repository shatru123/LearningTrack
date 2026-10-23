using System;

namespace Day45.KubernetesProbesHPA;

/// <summary>
/// Solves LeetCode #322 (Coin Change) and LeetCode #152 (Maximum Product Subarray).
/// </summary>
public static class CoinChangeAndMaxProductSolver
{
    /// <summary>
    /// LC #322: Fewest coins needed to make up amount.
    /// Bottom-up Unbounded Knapsack DP.
    /// Time: O(Amount * Coins.Length), Space: O(Amount).
    /// </summary>
    public static int CoinChange(int[] coins, int amount)
    {
        if (amount < 0) return -1;
        if (amount == 0) return 0;

        int[] dp = new int[amount + 1];
        Array.Fill(dp, amount + 1);
        dp[0] = 0;

        for (int a = 1; a <= amount; a++)
        {
            foreach (var coin in coins)
            {
                if (a - coin >= 0)
                {
                    dp[a] = Math.Min(dp[a], 1 + dp[a - coin]);
                }
            }
        }

        return dp[amount] > amount ? -1 : dp[amount];
    }

    /// <summary>
    /// LC #152: Maximum Product Subarray.
    /// Tracks max and min running products to handle negative numbers.
    /// Time: O(N), Space: O(1).
    /// </summary>
    public static int MaxProduct(int[] nums)
    {
        if (nums == null || nums.Length == 0) return 0;

        int globalMax = nums[0];
        int currentMax = nums[0];
        int currentMin = nums[0];

        for (int i = 1; i < nums.Length; i++)
        {
            int n = nums[i];

            // If n is negative, swapping max and min prepares for sign flip
            if (n < 0)
            {
                (currentMax, currentMin) = (currentMin, currentMax);
            }

            currentMax = Math.Max(n, currentMax * n);
            currentMin = Math.Min(n, currentMin * n);

            globalMax = Math.Max(globalMax, currentMax);
        }

        return globalMax;
    }
}
