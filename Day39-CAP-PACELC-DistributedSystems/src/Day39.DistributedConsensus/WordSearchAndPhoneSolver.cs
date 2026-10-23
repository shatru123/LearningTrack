using System.Text;

namespace Day39.DistributedConsensus;

public static class WordSearchAndPhoneSolver
{
    #region LeetCode #79: Word Search

    /// <summary>
    /// LeetCode #79: Word Search (Medium).
    /// DFS Backtracking on 2D character grid with in-place cell marking.
    /// Time Complexity: O(M * N * 3^L) where L = word.Length. Space: O(L) recursion stack.
    /// </summary>
    public static bool Exist(char[][] board, string word)
    {
        if (board == null || board.Length == 0 || string.IsNullOrEmpty(word))
            return false;

        int rows = board.Length;
        int cols = board[0].Length;

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                if (board[r][c] == word[0] && DfsWordSearch(board, word, r, c, 0))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool DfsWordSearch(char[][] board, string word, int r, int c, int index)
    {
        if (index == word.Length)
        {
            return true;
        }

        if (r < 0 || r >= board.Length || c < 0 || c >= board[0].Length || board[r][c] != word[index])
        {
            return false;
        }

        char original = board[r][c];
        board[r][c] = '#'; // Mark cell as visited in-place

        bool found = DfsWordSearch(board, word, r + 1, c, index + 1) ||
                     DfsWordSearch(board, word, r - 1, c, index + 1) ||
                     DfsWordSearch(board, word, r, c + 1, index + 1) ||
                     DfsWordSearch(board, word, r, c - 1, index + 1);

        board[r][c] = original; // Backtrack / unmark
        return found;
    }

    #endregion

    #region LeetCode #17: Letter Combinations of a Phone Number

    private static readonly string[] DigitMap =
    [
        "",     // 0
        "",     // 1
        "abc",  // 2
        "def",  // 3
        "ghi",  // 4
        "jkl",  // 5
        "mno",  // 6
        "pqrs", // 7
        "tuv",  // 8
        "wxyz"  // 9
    ];

    /// <summary>
    /// LeetCode #17: Letter Combinations of a Phone Number (Medium).
    /// Time Complexity: O(4^N * N), Space Complexity: O(N) stack.
    /// </summary>
    public static IList<string> LetterCombinations(string digits)
    {
        var result = new List<string>();
        if (string.IsNullOrEmpty(digits)) return result;

        var current = new StringBuilder();
        BacktrackLetters(digits, 0, current, result);
        return result;
    }

    private static void BacktrackLetters(string digits, int index, StringBuilder current, List<string> result)
    {
        if (index == digits.Length)
        {
            result.Add(current.ToString());
            return;
        }

        int digit = digits[index] - '0';
        if (digit < 2 || digit > 9) return;

        string letters = DigitMap[digit];
        foreach (char ch in letters)
        {
            current.Append(ch);
            BacktrackLetters(digits, index + 1, current, result);
            current.Length--; // Backtrack
        }
    }

    #endregion
}
