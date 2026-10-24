namespace Day51.AiLlmFoundations;

using System;
using System.Text;

public class UniquePathsAndLcsSolver
{
    /// <summary>
    /// LeetCode #62: Unique Paths via 2D Dynamic Programming grid.
    /// </summary>
    public static int UniquePaths2D(int m, int n)
    {
        if (m <= 0 || n <= 0) return 0;
        int[,] dp = new int[m, n];

        for (int i = 0; i < m; i++) dp[i, 0] = 1;
        for (int j = 0; j < n; j++) dp[0, j] = 1;

        for (int i = 1; i < m; i++)
        {
            for (int j = 1; j < n; j++)
            {
                dp[i, j] = dp[i - 1, j] + dp[i, j - 1];
            }
        }

        return dp[m - 1, n - 1];
    }

    /// <summary>
    /// LeetCode #62: Unique Paths via 1D rolling array O(min(m, n)) space.
    /// </summary>
    public static int UniquePaths1D(int m, int n)
    {
        if (m <= 0 || n <= 0) return 0;
        if (m < n) (m, n) = (n, m); // Ensure n is the smaller dimension

        int[] dp = new int[n];
        Array.Fill(dp, 1);

        for (int i = 1; i < m; i++)
        {
            for (int j = 1; j < n; j++)
            {
                dp[j] += dp[j - 1];
            }
        }

        return dp[n - 1];
    }

    /// <summary>
    /// LeetCode #62: Combinatorics formula C(m+n-2, m-1).
    /// </summary>
    public static long UniquePathsCombinatorics(int m, int n)
    {
        if (m <= 0 || n <= 0) return 0;
        int totalMoves = m + n - 2;
        int k = Math.Min(m - 1, n - 1);

        long result = 1;
        for (int i = 1; i <= k; i++)
        {
            result = result * (totalMoves - i + 1) / i;
        }

        return result;
    }

    /// <summary>
    /// LeetCode #1143: Longest Common Subsequence length O(M * N).
    /// </summary>
    public static int LongestCommonSubsequenceLength(string text1, string text2)
    {
        if (string.IsNullOrEmpty(text1) || string.IsNullOrEmpty(text2)) return 0;

        int m = text1.Length;
        int n = text2.Length;
        int[,] dp = new int[m + 1, n + 1];

        for (int i = 1; i <= m; i++)
        {
            for (int j = 1; j <= n; j++)
            {
                if (text1[i - 1] == text2[j - 1])
                {
                    dp[i, j] = dp[i - 1, j - 1] + 1;
                }
                else
                {
                    dp[i, j] = Math.Max(dp[i - 1, j], dp[i, j - 1]);
                }
            }
        }

        return dp[m, n];
    }

    /// <summary>
    /// Reconstructs the actual LCS string via backtracking through the DP table.
    /// </summary>
    public static string GetLcsString(string text1, string text2)
    {
        if (string.IsNullOrEmpty(text1) || string.IsNullOrEmpty(text2)) return string.Empty;

        int m = text1.Length;
        int n = text2.Length;
        int[,] dp = new int[m + 1, n + 1];

        for (int i = 1; i <= m; i++)
        {
            for (int j = 1; j <= n; j++)
            {
                if (text1[i - 1] == text2[j - 1])
                {
                    dp[i, j] = dp[i - 1, j - 1] + 1;
                }
                else
                {
                    dp[i, j] = Math.Max(dp[i - 1, j], dp[i, j - 1]);
                }
            }
        }

        var sb = new StringBuilder();
        int r = m, c = n;
        while (r > 0 && c > 0)
        {
            if (text1[r - 1] == text2[c - 1])
            {
                sb.Append(text1[r - 1]);
                r--;
                c--;
            }
            else if (dp[r - 1, c] >= dp[r, c - 1])
            {
                r--;
            }
            else
            {
                c--;
            }
        }

        char[] arr = sb.ToString().ToCharArray();
        Array.Reverse(arr);
        return new string(arr);
    }
}
