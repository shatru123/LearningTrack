namespace Day01.MemoryLab;

public static class DsaExercises
{
    /// <summary>
    /// LeetCode 1 - Two Sum
    /// Given an array of integers nums and an integer target, return indices of the two numbers such that they add up to target.
    /// Time Complexity: O(n) using a single pass hash map.
    /// Space Complexity: O(n).
    /// </summary>
    public static int[] TwoSum(int[] nums, int target)
    {
        if (nums == null || nums.Length < 2)
            throw new ArgumentException("Array must contain at least two elements.", nameof(nums));

        Dictionary<int, int> seen = new(nums.Length);
        for (int i = 0; i < nums.Length; i++)
        {
            int complement = target - nums[i];
            if (seen.TryGetValue(complement, out int index))
            {
                return new[] { index, i };
            }
            seen[nums[i]] = i;
        }

        throw new InvalidOperationException("No two sum solution found.");
    }

    /// <summary>
    /// LeetCode 217 - Contains Duplicate
    /// Given an integer array nums, return true if any value appears at least twice in the array, and false if every element is distinct.
    /// Time Complexity: O(n).
    /// Space Complexity: O(n).
    /// </summary>
    public static bool ContainsDuplicate(int[] nums)
    {
        if (nums == null || nums.Length <= 1)
            return false;

        HashSet<int> seen = new(nums.Length);
        foreach (int num in nums)
        {
            if (!seen.Add(num))
            {
                return true;
            }
        }
        return false;
    }
}
