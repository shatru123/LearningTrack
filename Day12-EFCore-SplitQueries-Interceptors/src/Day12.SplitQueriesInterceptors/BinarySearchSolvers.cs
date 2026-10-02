namespace Day12.SplitQueriesInterceptors;

public static class BinarySearchSolvers
{
    /// <summary>
    /// LeetCode #704: Binary Search.
    /// Finds target in a sorted ascending array in O(log n) time and O(1) space.
    /// Returns 0-based index if found; otherwise -1.
    /// </summary>
    public static int Search(int[] nums, int target)
    {
        ArgumentNullException.ThrowIfNull(nums);
        if (nums.Length == 0) return -1;

        int left = 0;
        int right = nums.Length - 1;

        while (left <= right)
        {
            // Avoid potential integer overflow
            int mid = left + (right - left) / 2;

            if (nums[mid] == target)
            {
                return mid;
            }
            if (nums[mid] < target)
            {
                left = mid + 1;
            }
            else
            {
                right = mid - 1;
            }
        }

        return -1;
    }

    /// <summary>
    /// LeetCode #74: Search a 2D Matrix.
    /// Treats an m x n matrix as a contiguous 1D sorted array of length m * n.
    /// Maps virtual index 'mid' to 2D coordinates: row = mid / n, col = mid % n.
    /// Time Complexity: O(log(m * n)), Space Complexity: O(1).
    /// </summary>
    public static bool SearchMatrix(int[][] matrix, int target)
    {
        ArgumentNullException.ThrowIfNull(matrix);
        if (matrix.Length == 0 || matrix[0].Length == 0)
        {
            return false;
        }

        int m = matrix.Length;
        int n = matrix[0].Length;

        int left = 0;
        int right = m * n - 1;

        while (left <= right)
        {
            int mid = left + (right - left) / 2;
            int row = mid / n;
            int col = mid % n;

            int value = matrix[row][col];

            if (value == target)
            {
                return true;
            }
            if (value < target)
            {
                left = mid + 1;
            }
            else
            {
                right = mid - 1;
            }
        }

        return false;
    }
}
