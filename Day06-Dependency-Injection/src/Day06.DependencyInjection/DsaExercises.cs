namespace Day06.DependencyInjection;

public static class DsaExercises
{
    /// <summary>
    /// LeetCode 15 - 3Sum
    /// Given an integer array nums, return all the triplets [nums[i], nums[j], nums[k]] such that
    /// i != j, i != k, and j != k, and nums[i] + nums[j] + nums[k] == 0.
    /// Notice that the solution set must not contain duplicate triplets.
    /// Time Complexity: O(n^2) with sorting and two pointers.
    /// Space Complexity: O(1) auxiliary (excluding output).
    /// </summary>
    public static IList<IList<int>> ThreeSum(int[] nums)
    {
        var result = new List<IList<int>>();
        if (nums == null || nums.Length < 3)
            return result;

        Array.Sort(nums);

        for (int i = 0; i < nums.Length - 2; i++)
        {
            // Skip duplicate values for the first element
            if (i > 0 && nums[i] == nums[i - 1])
                continue;

            // Early exit if the smallest remaining number is greater than zero
            if (nums[i] > 0)
                break;

            int left = i + 1;
            int right = nums.Length - 1;

            while (left < right)
            {
                int sum = nums[i] + nums[left] + nums[right];
                if (sum == 0)
                {
                    result.Add(new List<int> { nums[i], nums[left], nums[right] });

                    // Skip duplicates for left and right
                    while (left < right && nums[left] == nums[left + 1]) left++;
                    while (left < right && nums[right] == nums[right - 1]) right--;

                    left++;
                    right--;
                }
                else if (sum < 0)
                {
                    left++;
                }
                else
                {
                    right--;
                }
            }
        }

        return result;
    }

    /// <summary>
    /// LeetCode 11 - Container With Most Water
    /// You are given an integer array height of length n. There are n vertical lines drawn such that
    /// the two endpoints of the ith line are (i, 0) and (i, height[i]).
    /// Find two lines that together with the x-axis form a container, such that the container contains the most water.
    /// Time Complexity: O(n) using two pointers.
    /// Space Complexity: O(1).
    /// </summary>
    public static int MaxArea(int[] height)
    {
        if (height == null || height.Length < 2)
            return 0;

        int left = 0;
        int right = height.Length - 1;
        int maxWater = 0;

        while (left < right)
        {
            int h = Math.Min(height[left], height[right]);
            int width = right - left;
            int area = h * width;

            if (area > maxWater)
            {
                maxWater = area;
            }

            // Always move the pointer with the smaller height to search for taller boundaries
            if (height[left] < height[right])
            {
                left++;
            }
            else
            {
                right--;
            }
        }

        return maxWater;
    }
}
