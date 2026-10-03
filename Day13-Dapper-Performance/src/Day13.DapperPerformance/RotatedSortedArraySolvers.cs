namespace Day13.DapperPerformance;

public static class RotatedSortedArraySolvers
{
    /// <summary>
    /// LeetCode #153: Find Minimum in Rotated Sorted Array.
    /// An ascending sorted array of unique elements was rotated between 1 and n times.
    /// Uses modified binary search with pivot detection to find the minimum element in O(log n) time and O(1) space.
    /// </summary>
    public static int FindMin(int[] nums)
    {
        ArgumentNullException.ThrowIfNull(nums);
        if (nums.Length == 0)
        {
            throw new ArgumentException("Array cannot be empty.", nameof(nums));
        }

        int left = 0;
        int right = nums.Length - 1;

        // If already completely sorted (0 rotations or n rotations)
        if (nums[left] <= nums[right])
        {
            return nums[left];
        }

        while (left < right)
        {
            int mid = left + (right - left) / 2;

            // If middle element is strictly greater than right element,
            // the pivot (minimum) MUST lie in the right half: [mid + 1, right]
            if (nums[mid] > nums[right])
            {
                left = mid + 1;
            }
            // Otherwise, the pivot lies at mid or in the left half: [left, mid]
            else
            {
                right = mid;
            }
        }

        return nums[left];
    }

    /// <summary>
    /// Finds the 0-based index of the minimum element (the rotation pivot point).
    /// </summary>
    public static int FindPivotIndex(int[] nums)
    {
        ArgumentNullException.ThrowIfNull(nums);
        if (nums.Length == 0)
        {
            throw new ArgumentException("Array cannot be empty.", nameof(nums));
        }

        int left = 0;
        int right = nums.Length - 1;

        if (nums[left] <= nums[right])
        {
            return left;
        }

        while (left < right)
        {
            int mid = left + (right - left) / 2;
            if (nums[mid] > nums[right])
            {
                left = mid + 1;
            }
            else
            {
                right = mid;
            }
        }

        return left;
    }

    /// <summary>
    /// LeetCode #154: Find Minimum in Rotated Sorted Array II (Handles Duplicates).
    /// When nums[mid] == nums[right], decrement right by 1 to shrink search space safely.
    /// Time Complexity: Average O(log n), Worst-case O(n). Space Complexity: O(1).
    /// </summary>
    public static int FindMinWithDuplicates(int[] nums)
    {
        ArgumentNullException.ThrowIfNull(nums);
        if (nums.Length == 0)
        {
            throw new ArgumentException("Array cannot be empty.", nameof(nums));
        }

        int left = 0;
        int right = nums.Length - 1;

        while (left < right)
        {
            int mid = left + (right - left) / 2;

            if (nums[mid] > nums[right])
            {
                left = mid + 1;
            }
            else if (nums[mid] < nums[right])
            {
                right = mid;
            }
            else
            {
                // nums[mid] == nums[right]: cannot determine which half contains minimum, safely decrement right
                right--;
            }
        }

        return nums[left];
    }
}
