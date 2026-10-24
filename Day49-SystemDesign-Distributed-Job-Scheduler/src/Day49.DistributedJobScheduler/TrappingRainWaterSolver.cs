namespace Day49.DistributedJobScheduler;

using System;
using System.Collections.Generic;

public class TrappingRainWaterSolver
{
    /// <summary>
    /// LeetCode #42: Trapping Rain Water.
    /// Optimal Two-Pointer approach: O(N) time, O(1) space.
    /// </summary>
    public static int TrapTwoPointers(int[] height)
    {
        if (height == null || height.Length < 3) return 0;

        int left = 0;
        int right = height.Length - 1;
        int leftMax = 0;
        int rightMax = 0;
        int totalWater = 0;

        while (left < right)
        {
            if (height[left] < height[right])
            {
                if (height[left] >= leftMax)
                {
                    leftMax = height[left];
                }
                else
                {
                    totalWater += leftMax - height[left];
                }
                left++;
            }
            else
            {
                if (height[right] >= rightMax)
                {
                    rightMax = height[right];
                }
                else
                {
                    totalWater += rightMax - height[right];
                }
                right--;
            }
        }

        return totalWater;
    }

    /// <summary>
    /// Monotonic Stack approach: O(N) time, O(N) space.
    /// Computes trapped water bounded horizontally between left and right walls.
    /// </summary>
    public static int TrapMonotonicStack(int[] height)
    {
        if (height == null || height.Length < 3) return 0;

        var stack = new Stack<int>(); // Stores indices of decreasing bars
        int totalWater = 0;

        for (int i = 0; i < height.Length; i++)
        {
            while (stack.Count > 0 && height[i] > height[stack.Peek()])
            {
                int bottomIdx = stack.Pop();

                if (stack.Count == 0) break; // No left boundary

                int leftIdx = stack.Peek();
                int boundedHeight = Math.Min(height[leftIdx], height[i]) - height[bottomIdx];
                int distance = i - leftIdx - 1;

                totalWater += boundedHeight * distance;
            }

            stack.Push(i);
        }

        return totalWater;
    }

    /// <summary>
    /// Dynamic Programming prefix/suffix max approach: O(N) time, O(N) space.
    /// </summary>
    public static int TrapDp(int[] height)
    {
        if (height == null || height.Length < 3) return 0;

        int n = height.Length;
        int[] leftMax = new int[n];
        int[] rightMax = new int[n];

        leftMax[0] = height[0];
        for (int i = 1; i < n; i++)
        {
            leftMax[i] = Math.Max(leftMax[i - 1], height[i]);
        }

        rightMax[n - 1] = height[n - 1];
        for (int i = n - 2; i >= 0; i--)
        {
            rightMax[i] = Math.Max(rightMax[i + 1], height[i]);
        }

        int totalWater = 0;
        for (int i = 0; i < n; i++)
        {
            int trapped = Math.Min(leftMax[i], rightMax[i]) - height[i];
            if (trapped > 0)
            {
                totalWater += trapped;
            }
        }

        return totalWater;
    }
}
