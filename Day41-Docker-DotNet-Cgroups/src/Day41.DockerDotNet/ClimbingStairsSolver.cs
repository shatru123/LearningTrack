namespace Day41.DockerDotNet;

public static class ClimbingStairsSolver
{
    /// <summary>
    /// LeetCode #70: Climbing Stairs (Easy).
    /// Fibonacci recurrence: dp[i] = dp[i - 1] + dp[i - 2].
    /// Time Complexity: O(N), Space Complexity: O(1).
    /// </summary>
    public static int ClimbStairs(int n)
    {
        if (n <= 0) return 0;
        if (n == 1) return 1;
        if (n == 2) return 2;

        int prev2 = 1;
        int prev1 = 2;

        for (int i = 3; i <= n; i++)
        {
            int current = prev1 + prev2;
            prev2 = prev1;
            prev1 = current;
        }

        return prev1;
    }

    /// <summary>
    /// LeetCode #746: Min Cost Climbing Stairs (Easy).
    /// Recurrence: dp[i] = min(dp[i - 1] + cost[i - 1], dp[i - 2] + cost[i - 2]).
    /// Time Complexity: O(N), Space Complexity: O(1).
    /// </summary>
    public static int MinCostClimbingStairs(int[] cost)
    {
        if (cost == null || cost.Length < 2) return 0;

        int prev2 = 0; // min cost to reach step 0
        int prev1 = 0; // min cost to reach step 1

        for (int i = 2; i <= cost.Length; i++)
        {
            int current = Math.Min(prev1 + cost[i - 1], prev2 + cost[i - 2]);
            prev2 = prev1;
            prev1 = current;
        }

        return prev1;
    }
}
