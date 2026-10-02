namespace Day09.SqlExecutionPlans;

public static class CharacterReplacementSolver
{
    /// <summary>
    /// LeetCode #424: Longest Repeating Character Replacement
    /// Finds the length of the longest substring containing the same letter you can get after performing at most k operations.
    /// Time Complexity: O(N) single-pass sliding window
    /// Space Complexity: O(1) fixed 26-element frequency table
    /// </summary>
    public static int CharacterReplacement(string s, int k)
    {
        if (string.IsNullOrEmpty(s)) return 0;
        if (s.Length <= k) return s.Length;

        Span<int> charCounts = stackalloc int[26];
        int left = 0;
        int maxFreq = 0;
        int maxLen = 0;

        for (int right = 0; right < s.Length; right++)
        {
            int index = s[right] - 'A';
            charCounts[index]++;
            if (charCounts[index] > maxFreq)
            {
                maxFreq = charCounts[index];
            }

            // Window length = (right - left + 1)
            // Number of characters needing replacement = window length - maxFreq
            while ((right - left + 1) - maxFreq > k)
            {
                charCounts[s[left] - 'A']--;
                left++;
                // Note: maxFreq does not need to be decremented because a smaller maxFreq
                // will never produce a larger valid window length than what we've already seen.
            }

            int currentWindow = right - left + 1;
            if (currentWindow > maxLen)
            {
                maxLen = currentWindow;
            }
        }

        return maxLen;
    }
}
