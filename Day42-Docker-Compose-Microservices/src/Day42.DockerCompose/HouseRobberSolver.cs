using System;

namespace Day42.DockerCompose;

/// <summary>
/// Solves LeetCode #198 (House Robber) and LeetCode #213 (House Robber II).
/// </summary>
public static class HouseRobberSolver
{
    /// <summary>
    /// LC #198: Standard linear street of houses.
    /// Time Complexity: O(N), Space Complexity: O(1).
    /// </summary>
    public static int Rob(int[] nums)
    {
        if (nums == null || nums.Length == 0) return 0;
        if (nums.Length == 1) return nums[0];

        int prev2 = 0;
        int prev1 = 0;

        foreach (var val in nums)
        {
            int current = Math.Max(prev1, prev2 + val);
            prev2 = prev1;
            prev1 = current;
        }

        return prev1;
    }

    /// <summary>
    /// LC #213: Circular street of houses (first and last are adjacent).
    /// Breaks problem into two linear sub-ranges: [0 .. N-2] and [1 .. N-1].
    /// Time Complexity: O(N), Space Complexity: O(1).
    /// </summary>
    public static int RobCircular(int[] nums)
    {
        if (nums == null || nums.Length == 0) return 0;
        if (nums.Length == 1) return nums[0];
        if (nums.Length == 2) return Math.Max(nums[0], nums[1]);

        int n = nums.Length;
        int robRange1 = RobRange(nums, 0, n - 2);
        int robRange2 = RobRange(nums, 1, n - 1);

        return Math.Max(robRange1, robRange2);
    }

    private static int RobRange(int[] nums, int start, int end)
    {
        int prev2 = 0;
        int prev1 = 0;

        for (int i = start; i <= end; i++)
        {
            int current = Math.Max(prev1, prev2 + nums[i]);
            prev2 = prev1;
            prev1 = current;
        }

        return prev1;
    }
}
