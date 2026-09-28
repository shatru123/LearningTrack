namespace Day04.GarbageCollection;

public static class DsaExercises
{
    /// <summary>
    /// LeetCode 238 - Product of Array Except Self
    /// Given an integer array nums, return an array answer such that answer[i] is equal to
    /// the product of all the elements of nums except nums[i].
    /// Must run in O(n) time and without using the division operation.
    /// Auxiliary space: O(1) beyond the output array.
    /// </summary>
    public static int[] ProductExceptSelf(int[] nums)
    {
        if (nums == null || nums.Length == 0)
            return Array.Empty<int>();

        int n = nums.Length;
        int[] result = new int[n];

        // Step 1: Compute prefix products directly in result array
        result[0] = 1;
        for (int i = 1; i < n; i++)
        {
            result[i] = result[i - 1] * nums[i - 1];
        }

        // Step 2: Multiply by running suffix product from the right
        int suffix = 1;
        for (int i = n - 1; i >= 0; i--)
        {
            result[i] *= suffix;
            suffix *= nums[i];
        }

        return result;
    }
}
