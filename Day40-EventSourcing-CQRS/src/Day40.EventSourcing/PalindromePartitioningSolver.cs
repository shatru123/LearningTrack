namespace Day40.EventSourcing;

public static class PalindromePartitioningSolver
{
    /// <summary>
    /// LeetCode #131: Palindrome Partitioning (Medium).
    /// Uses DP precomputation table to check palindromes in O(1) time during backtracking.
    /// Time Complexity: O(N * 2^N), Space Complexity: O(N^2) for DP table + O(N) recursion stack.
    /// </summary>
    public static IList<IList<string>> Partition(string s)
    {
        var result = new List<IList<string>>();
        if (string.IsNullOrEmpty(s)) return result;

        int n = s.Length;
        // DP table: dp[i, j] is true if substring s[i..j] is a palindrome
        var dp = new bool[n, n];

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
                    dp[i, j] = len == 2 || dp[i + 1, j - 1];
                }
            }
        }

        var current = new List<string>();
        Backtrack(s, 0, dp, current, result);
        return result;
    }

    private static void Backtrack(string s, int start, bool[,] dp, List<string> current, List<IList<string>> result)
    {
        if (start == s.Length)
        {
            result.Add(new List<string>(current));
            return;
        }

        for (int end = start; end < s.Length; end++)
        {
            if (dp[start, end])
            {
                current.Add(s.Substring(start, end - start + 1));
                Backtrack(s, end + 1, dp, current, result);
                current.RemoveAt(current.Count - 1);
            }
        }
    }
}
