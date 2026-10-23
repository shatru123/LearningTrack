namespace Day32.SagaOrchestration;

public static class WordLadderSolver
{
    /// <summary>
    /// LeetCode #127: Word Ladder (Hard).
    /// Finds the length of shortest transformation sequence from beginWord to endWord
    /// using Bidirectional Breadth-First Search (BFS).
    /// Time Complexity: O(N * L^2), Space Complexity: O(N * L) where N is number of words, L is word length.
    /// </summary>
    public static int LadderLength(string beginWord, string endWord, IList<string> wordList)
    {
        if (string.IsNullOrEmpty(beginWord) || string.IsNullOrEmpty(endWord) || wordList == null)
            return 0;

        var wordSet = new HashSet<string>(wordList);
        if (!wordSet.Contains(endWord))
            return 0;

        var beginSet = new HashSet<string> { beginWord };
        var endSet = new HashSet<string> { endWord };
        wordSet.Remove(beginWord);
        wordSet.Remove(endWord);

        int distance = 1;

        while (beginSet.Count > 0 && endSet.Count > 0)
        {
            // Bidirectional optimization: always expand the smaller frontier to minimize branch factor
            if (beginSet.Count > endSet.Count)
            {
                var temp = beginSet;
                beginSet = endSet;
                endSet = temp;
            }

            var nextSet = new HashSet<string>();

            foreach (var word in beginSet)
            {
                char[] chars = word.ToCharArray();

                for (int i = 0; i < chars.Length; i++)
                {
                    char originalChar = chars[i];

                    for (char c = 'a'; c <= 'z'; c++)
                    {
                        if (c == originalChar) continue;

                        chars[i] = c;
                        string mutated = new string(chars);

                        if (endSet.Contains(mutated))
                        {
                            return distance + 1; // Frontiers met!
                        }

                        if (wordSet.Contains(mutated))
                        {
                            nextSet.Add(mutated);
                            wordSet.Remove(mutated); // Visited
                        }
                    }

                    chars[i] = originalChar; // Backtrack character
                }
            }

            beginSet = nextSet;
            distance++;
        }

        return 0; // Unreachable
    }
}
