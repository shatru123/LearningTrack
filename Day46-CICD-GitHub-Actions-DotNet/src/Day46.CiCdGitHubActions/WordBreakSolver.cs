using System;
using System.Collections.Generic;

namespace Day46.CiCdGitHubActions;

/// <summary>
/// Solves LeetCode #139: Word Break.
/// 1D Dynamic Programming with Dictionary HashSet Lookup.
/// Time Complexity: O(N^2), Space Complexity: O(N + Words).
/// </summary>
public static class WordBreakSolver
{
    public static bool WordBreak(string? s, IList<string>? wordDict)
    {
        if (string.IsNullOrEmpty(s) || wordDict == null || wordDict.Count == 0)
        {
            return false;
        }

        var wordSet = new HashSet<string>(wordDict);
        int n = s.Length;

        // dp[i] represents if s[0 .. i-1] can be segmented into words from wordDict
        bool[] dp = new bool[n + 1];
        dp[0] = true; // empty string is always valid base case

        int maxWordLen = 0;
        foreach (var w in wordDict)
        {
            maxWordLen = Math.Max(maxWordLen, w.Length);
        }

        for (int i = 1; i <= n; i++)
        {
            // Prune inner loop: word length cannot exceed maxWordLen
            for (int j = i - 1; j >= Math.Max(0, i - maxWordLen); j--)
            {
                if (dp[j])
                {
                    string sub = s.Substring(j, i - j);
                    if (wordSet.Contains(sub))
                    {
                        dp[i] = true;
                        break;
                    }
                }
            }
        }

        return dp[n];
    }
}
