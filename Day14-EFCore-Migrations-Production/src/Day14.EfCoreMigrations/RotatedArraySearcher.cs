using System;

namespace Day14.EfCoreMigrations;

public static class RotatedArraySearcher
{
    /// <summary>
    /// LeetCode #33: Search in Rotated Sorted Array (All distinct elements).
    /// Returns 0-based index of target if found, or -1 if not found. Time: O(log N), Space: O(1).
    /// </summary>
    public static int Search(int[] nums, int target)
    {
        if (nums == null || nums.Length == 0) return -1;

        int left = 0;
        int right = nums.Length - 1;

        while (left <= right)
        {
            int mid = left + (right - left) / 2;

            if (nums[mid] == target) return mid;

            // Check if left half is sorted
            if (nums[left] <= nums[mid])
            {
                // Target is within the sorted left half
                if (nums[left] <= target && target < nums[mid])
                {
                    right = mid - 1;
                }
                else
                {
                    left = mid + 1;
                }
            }
            else // Right half is sorted
            {
                // Target is within the sorted right half
                if (nums[mid] < target && target <= nums[right])
                {
                    left = mid + 1;
                }
                else
                {
                    right = mid - 1;
                }
            }
        }

        return -1;
    }

    /// <summary>
    /// LeetCode #81: Search in Rotated Sorted Array II (May contain duplicates).
    /// Returns true if target exists, false otherwise.
    /// Average: O(log N), Worst-case: O(N) when elements are identical. Space: O(1).
    /// </summary>
    public static bool SearchWithDuplicates(int[] nums, int target)
    {
        if (nums == null || nums.Length == 0) return false;

        int left = 0;
        int right = nums.Length - 1;

        while (left <= right)
        {
            int mid = left + (right - left) / 2;

            if (nums[mid] == target) return true;

            // When left, mid, and right are equal, we cannot determine which half is sorted.
            // Safely shrink boundaries by 1.
            if (nums[left] == nums[mid] && nums[mid] == nums[right])
            {
                left++;
                right--;
            }
            else if (nums[left] <= nums[mid]) // Left half is sorted
            {
                if (nums[left] <= target && target < nums[mid])
                {
                    right = mid - 1;
                }
                else
                {
                    left = mid + 1;
                }
            }
            else // Right half is sorted
            {
                if (nums[mid] < target && target <= nums[right])
                {
                    left = mid + 1;
                }
                else
                {
                    right = mid - 1;
                }
            }
        }

        return false;
    }
}
