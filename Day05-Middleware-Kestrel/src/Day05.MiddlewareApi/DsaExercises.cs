namespace Day05.MiddlewareApi;

public static class DsaExercises
{
    /// <summary>
    /// LeetCode 125 - Valid Palindrome
    /// A phrase is a palindrome if, after converting all uppercase letters into lowercase letters
    /// and removing all non-alphanumeric characters, it reads the same forward and backward.
    /// Time Complexity: O(n) using two pointers.
    /// Space Complexity: O(1).
    /// </summary>
    public static bool IsPalindrome(string s)
    {
        if (s == null) return false;

        int left = 0;
        int right = s.Length - 1;

        while (left < right)
        {
            while (left < right && !char.IsLetterOrDigit(s[left]))
            {
                left++;
            }
            while (left < right && !char.IsLetterOrDigit(s[right]))
            {
                right--;
            }

            if (char.ToLowerInvariant(s[left]) != char.ToLowerInvariant(s[right]))
            {
                return false;
            }

            left++;
            right--;
        }

        return true;
    }

    /// <summary>
    /// LeetCode 167 - Two Sum II - Input Array Is Sorted
    /// Given a 1-indexed array of integers numbers that is already sorted in non-decreasing order,
    /// find two numbers such that they add up to a specific target number.
    /// Time Complexity: O(n) using two pointers.
    /// Space Complexity: O(1).
    /// </summary>
    public static int[] TwoSum(int[] numbers, int target)
    {
        if (numbers == null || numbers.Length < 2)
            throw new ArgumentException("Array must contain at least 2 numbers.", nameof(numbers));

        int left = 0;
        int right = numbers.Length - 1;

        while (left < right)
        {
            int sum = numbers[left] + numbers[right];
            if (sum == target)
            {
                // Return 1-based indices
                return new[] { left + 1, right + 1 };
            }
            if (sum < target)
            {
                left++;
            }
            else
            {
                right--;
            }
        }

        throw new InvalidOperationException("No two sum solution found.");
    }
}
