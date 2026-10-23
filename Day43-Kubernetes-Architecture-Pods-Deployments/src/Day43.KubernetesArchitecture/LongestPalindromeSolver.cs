using System;

namespace Day43.KubernetesArchitecture;

/// <summary>
/// Solves LeetCode #5: Longest Palindromic Substring.
/// Uses Expand Around Center algorithm with O(N^2) time and O(1) auxiliary space.
/// </summary>
public static class LongestPalindromeSolver
{
    public static string LongestPalindrome(string? s)
    {
        if (string.IsNullOrEmpty(s) || s.Length <= 1) return s ?? "";

        int start = 0;
        int maxLength = 1;

        for (int i = 0; i < s.Length; i++)
        {
            // Odd length palindrome (center is s[i])
            ExpandAroundCenter(s, i, i, ref start, ref maxLength);

            // Even length palindrome (center is between s[i] and s[i+1])
            ExpandAroundCenter(s, i, i + 1, ref start, ref maxLength);
        }

        return s.Substring(start, maxLength);
    }

    private static void ExpandAroundCenter(string s, int left, int right, ref int start, ref int maxLength)
    {
        while (left >= 0 && right < s.Length && s[left] == s[right])
        {
            int currentLen = right - left + 1;
            if (currentLen > maxLength)
            {
                start = left;
                maxLength = currentLen;
            }
            left--;
            right++;
        }
    }

    public static string LongestPalindromeDp(string? s)
    {
        if (string.IsNullOrEmpty(s) || s.Length <= 1) return s ?? "";

        int n = s.Length;
        bool[,] dp = new bool[n, n];
        int start = 0;
        int maxLen = 1;

        for (int i = 0; i < n; i++)
        {
            dp[i, i] = true;
        }

        for (int len = 2; len <= n; len++)
        {
            for (int i = 0; i <= n - len; i++)
            {
                int j = i + len - 1;

                if (s[i] == s[j])
                {
                    if (len == 2 || dp[i + 1, j - 1])
                    {
                        dp[i, j] = true;
                        if (len > maxLen)
                        {
                            start = i;
                            maxLen = len;
                        }
                    }
                }
            }
        }

        return s.Substring(start, maxLen);
    }
}
