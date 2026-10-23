using System;

namespace Day44.KubernetesConfigIngress;

/// <summary>
/// Solves LeetCode #647 (Palindromic Substrings) and LeetCode #91 (Decode Ways).
/// </summary>
public static class PalindromicSubstringsAndDecodeWaysSolver
{
    /// <summary>
    /// LC #647: Count total palindromic substrings.
    /// Time Complexity: O(N^2), Space Complexity: O(1).
    /// </summary>
    public static int CountSubstrings(string? s)
    {
        if (string.IsNullOrEmpty(s)) return 0;

        int totalCount = 0;
        for (int i = 0; i < s.Length; i++)
        {
            // Odd length palindromes centered at i
            totalCount += ExpandAndCount(s, i, i);

            // Even length palindromes centered between i and i+1
            totalCount += ExpandAndCount(s, i, i + 1);
        }

        return totalCount;
    }

    private static int ExpandAndCount(string s, int left, int right)
    {
        int count = 0;
        while (left >= 0 && right < s.Length && s[left] == s[right])
        {
            count++;
            left--;
            right++;
        }
        return count;
    }

    /// <summary>
    /// LC #91: Decode Ways ('1'-'26' mapping).
    /// Time Complexity: O(N), Space Complexity: O(1).
    /// </summary>
    public static int NumDecodings(string? s)
    {
        if (string.IsNullOrEmpty(s) || s[0] == '0') return 0;

        int n = s.Length;
        int prev2 = 1; // dp[i-2]
        int prev1 = 1; // dp[i-1]

        for (int i = 1; i < n; i++)
        {
            int current = 0;
            int singleDigit = s[i] - '0';
            int twoDigits = (s[i - 1] - '0') * 10 + singleDigit;

            // Single digit valid transition
            if (singleDigit >= 1 && singleDigit <= 9)
            {
                current += prev1;
            }

            // Two digits valid transition
            if (twoDigits >= 10 && twoDigits <= 26)
            {
                current += prev2;
            }

            prev2 = prev1;
            prev1 = current;
        }

        return prev1;
    }
}
