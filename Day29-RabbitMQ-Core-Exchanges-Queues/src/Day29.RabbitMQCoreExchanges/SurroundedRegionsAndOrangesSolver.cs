namespace Day29.RabbitMQCoreExchanges;

/// <summary>
/// Solvers for Graph / Grid algorithms:
/// 1. LeetCode #130: Surrounded Regions (Medium)
/// 2. LeetCode #994: Rotting Oranges (Medium)
/// </summary>
public class SurroundedRegionsAndOrangesSolver
{
    private static readonly int[] RowOffsets = { -1, 1, 0, 0 };
    private static readonly int[] ColOffsets = { 0, 0, -1, 1 };

    /// <summary>
    /// Solves LeetCode #130: Surrounded Regions using reverse boundary DFS.
    /// Captures all regions surrounded by 'X' by preserving boundary-connected 'O's.
    /// Time Complexity: O(M * N)
    /// Space Complexity: O(M * N) recursion stack.
    /// </summary>
    public void SolveSurroundedRegions(char[][]? board)
    {
        if (board == null || board.Length == 0 || board[0].Length == 0)
            return;

        int m = board.Length;
        int n = board[0].Length;

        // 1. Mark boundary 'O's and their connected component as '#' (protected)
        for (int r = 0; r < m; r++)
        {
            if (board[r][0] == 'O') DfsMarkSafe(board, r, 0, m, n);
            if (board[r][n - 1] == 'O') DfsMarkSafe(board, r, n - 1, m, n);
        }

        for (int c = 0; c < n; c++)
        {
            if (board[0][c] == 'O') DfsMarkSafe(board, 0, c, m, n);
            if (board[m - 1][c] == 'O') DfsMarkSafe(board, m - 1, c, m, n);
        }

        // 2. Flip all unprotected 'O's to 'X', and restore '#' back to 'O'
        for (int r = 0; r < m; r++)
        {
            for (int c = 0; c < n; c++)
            {
                if (board[r][c] == 'O')
                {
                    board[r][c] = 'X'; // Captured
                }
                else if (board[r][c] == '#')
                {
                    board[r][c] = 'O'; // Restored
                }
            }
        }
    }

    private void DfsMarkSafe(char[][] board, int r, int c, int m, int n)
    {
        if (r < 0 || r >= m || c < 0 || c >= n || board[r][c] != 'O')
            return;

        board[r][c] = '#'; // Mark safe

        for (int i = 0; i < 4; i++)
        {
            DfsMarkSafe(board, r + RowOffsets[i], c + ColOffsets[i], m, n);
        }
    }

    /// <summary>
    /// Solves LeetCode #994: Rotting Oranges using level-by-level Multi-Source Breadth-First Search (BFS).
    /// Time Complexity: O(M * N)
    /// Space Complexity: O(M * N)
    /// </summary>
    public int OrangesRotting(int[][]? grid)
    {
        if (grid == null || grid.Length == 0 || grid[0].Length == 0)
            return 0;

        int m = grid.Length;
        int n = grid[0].Length;

        var queue = new Queue<(int r, int c)>();
        int freshCount = 0;

        // Clone grid to preserve caller input
        var workingGrid = new int[m][];
        for (int i = 0; i < m; i++)
        {
            workingGrid[i] = (int[])grid[i].Clone();
            for (int j = 0; j < n; j++)
            {
                if (workingGrid[i][j] == 2)
                {
                    queue.Enqueue((i, j));
                }
                else if (workingGrid[i][j] == 1)
                {
                    freshCount++;
                }
            }
        }

        if (freshCount == 0) return 0;

        int minutesElapsed = 0;

        while (queue.Count > 0 && freshCount > 0)
        {
            int currentBatchSize = queue.Count;
            minutesElapsed++;

            for (int i = 0; i < currentBatchSize; i++)
            {
                var (r, c) = queue.Dequeue();

                for (int d = 0; d < 4; d++)
                {
                    int nr = r + RowOffsets[d];
                    int nc = c + ColOffsets[d];

                    if (nr >= 0 && nr < m && nc >= 0 && nc < n && workingGrid[nr][nc] == 1)
                    {
                        workingGrid[nr][nc] = 2; // Infect to rotten
                        freshCount--;
                        queue.Enqueue((nr, nc));
                    }
                }
            }
        }

        return freshCount == 0 ? minutesElapsed : -1;
    }
}
