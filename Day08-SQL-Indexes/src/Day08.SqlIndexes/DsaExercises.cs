namespace Day08.SqlIndexes;

public static class DsaExercises
{
    /// <summary>
    /// LeetCode 3 - Longest Substring Without Repeating Characters
    /// Given a string s, find the length of the longest substring without repeating characters.
    /// Time Complexity: O(n) using an optimal sliding window.
    /// Space Complexity: O(min(m, n)) where m is charset size (using fixed 256 array for ASCII).
    /// </summary>
    public static int LengthOfLongestSubstring(string s)
    {
        if (string.IsNullOrEmpty(s))
            return 0;

        // Stores 1-based index of last seen position of each character
        Span<int> lastSeen = stackalloc int[256];
        lastSeen.Clear();

        int maxLength = 0;
        int windowStart = 0;

        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            int charCode = (int)c;

            if (charCode < 256 && lastSeen[charCode] > 0)
            {
                // Move window start to the right of the previous occurrence
                windowStart = Math.Max(windowStart, lastSeen[charCode]);
            }

            int currentLength = i - windowStart + 1;
            if (currentLength > maxLength)
            {
                maxLength = currentLength;
            }

            if (charCode < 256)
            {
                lastSeen[charCode] = i + 1; // 1-based index
            }
        }

        return maxLength;
    }
}
