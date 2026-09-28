namespace Day02.RefStructMemory;

public static class DsaExercises
{
    /// <summary>
    /// LeetCode 242 - Valid Anagram
    /// Given two strings s and t, return true if t is an anagram of s, and false otherwise.
    /// Time Complexity: O(n).
    /// Space Complexity: O(1) using fixed-size 26-element array for lowercase English letters.
    /// </summary>
    public static bool IsAnagram(string s, string t)
    {
        if (s == null || t == null || s.Length != t.Length)
            return false;

        Span<int> counts = stackalloc int[26];
        for (int i = 0; i < s.Length; i++)
        {
            counts[s[i] - 'a']++;
            counts[t[i] - 'a']--;
        }

        foreach (int count in counts)
        {
            if (count != 0) return false;
        }

        return true;
    }

    /// <summary>
    /// LeetCode 49 - Group Anagrams
    /// Given an array of strings strs, group the anagrams together.
    /// Time Complexity: O(N * K log K) or O(N * K) with character count hashing.
    /// Space Complexity: O(N * K).
    /// </summary>
    public static IList<IList<string>> GroupAnagrams(string[] strs)
    {
        if (strs == null || strs.Length == 0)
            return new List<IList<string>>();

        Dictionary<string, List<string>> groups = new();

        foreach (string str in strs)
        {
            char[] chars = str.ToCharArray();
            Array.Sort(chars);
            string key = new string(chars);

            if (!groups.TryGetValue(key, out var list))
            {
                list = new List<string>();
                groups[key] = list;
            }
            list.Add(str);
        }

        IList<IList<string>> result = new List<IList<string>>();
        foreach (var group in groups.Values)
        {
            result.Add(group);
        }
        return result;
    }
}
