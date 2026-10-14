namespace Day28.ResilientHttpRefitPolly;

/// <summary>
/// Solves LeetCode #417: Pacific Atlantic Water Flow (Medium).
/// Uses reverse multi-source traversal flowing uphill from ocean boundaries.
/// </summary>
public class PacificAtlanticSolver
{
    private static readonly int[] RowOffsets = { -1, 1, 0, 0 };
    private static readonly int[] ColOffsets = { 0, 0, -1, 1 };

    /// <summary>
    /// Computes intersection coordinates reachable by both Pacific and Atlantic using Depth-First Search (DFS).
    /// Time Complexity: O(M * N)
    /// Space Complexity: O(M * N) for reachability matrices and recursion call stack.
    /// </summary>
    public IList<IList<int>> PacificAtlanticDfs(int[][]? heights)
    {
        if (heights == null || heights.Length == 0 || heights[0].Length == 0)
            return new List<IList<int>>();

        int m = heights.Length;
        int n = heights[0].Length;

        bool[,] pacific = new bool[m, n];
        bool[,] atlantic = new bool[m, n];

        // 1. Pacific borders: Top row (r=0) and Left col (c=0)
        for (int c = 0; c < n; c++) DfsFlow(heights, 0, c, pacific, m, n);
        for (int r = 0; r < m; r++) DfsFlow(heights, r, 0, pacific, m, n);

        // 2. Atlantic borders: Bottom row (r=m-1) and Right col (c=n-1)
        for (int c = 0; c < n; c++) DfsFlow(heights, m - 1, c, atlantic, m, n);
        for (int r = 0; r < m; r++) DfsFlow(heights, r, n - 1, atlantic, m, n);

        // 3. Find intersection coordinates
        var result = new List<IList<int>>();
        for (int r = 0; r < m; r++)
        {
            for (int c = 0; c < n; c++)
            {
                if (pacific[r, c] && atlantic[r, c])
                {
                    result.Add(new List<int> { r, c });
                }
            }
        }

        return result;
    }

    private void DfsFlow(int[][] heights, int r, int c, bool[,] oceanReachable, int m, int n)
    {
        oceanReachable[r, c] = true;

        for (int i = 0; i < 4; i++)
        {
            int nr = r + RowOffsets[i];
            int nc = c + ColOffsets[i];

            // Reverse flow condition: neighbor must be >= current cell height
            if (nr >= 0 && nr < m && nc >= 0 && nc < n &&
                !oceanReachable[nr, nc] &&
                heights[nr][nc] >= heights[r][c])
            {
                DfsFlow(heights, nr, nc, oceanReachable, m, n);
            }
        }
    }

    /// <summary>
    /// Computes intersection coordinates using Breadth-First Search (BFS) with queues.
    /// Time Complexity: O(M * N)
    /// Space Complexity: O(M * N)
    /// </summary>
    public IList<IList<int>> PacificAtlanticBfs(int[][]? heights)
    {
        if (heights == null || heights.Length == 0 || heights[0].Length == 0)
            return new List<IList<int>>();

        int m = heights.Length;
        int n = heights[0].Length;

        bool[,] pacific = new bool[m, n];
        bool[,] atlantic = new bool[m, n];

        var pacQueue = new Queue<(int r, int c)>();
        var atlQueue = new Queue<(int r, int c)>();

        // Seed Pacific borders
        for (int c = 0; c < n; c++) { pacific[0, c] = true; pacQueue.Enqueue((0, c)); }
        for (int r = 0; r < m; r++) { if (!pacific[r, 0]) { pacific[r, 0] = true; pacQueue.Enqueue((r, 0)); } }

        // Seed Atlantic borders
        for (int c = 0; c < n; c++) { atlantic[m - 1, c] = true; atlQueue.Enqueue((m - 1, c)); }
        for (int r = 0; r < m; r++) { if (!atlantic[r, n - 1]) { atlantic[r, n - 1] = true; atlQueue.Enqueue((r, n - 1)); } }

        BfsExplore(heights, pacQueue, pacific, m, n);
        BfsExplore(heights, atlQueue, atlantic, m, n);

        var result = new List<IList<int>>();
        for (int r = 0; r < m; r++)
        {
            for (int c = 0; c < n; c++)
            {
                if (pacific[r, c] && atlantic[r, c])
                {
                    result.Add(new List<int> { r, c });
                }
            }
        }

        return result;
    }

    private void BfsExplore(int[][] heights, Queue<(int r, int c)> queue, bool[,] visited, int m, int n)
    {
        while (queue.Count > 0)
        {
            var (r, c) = queue.Dequeue();

            for (int i = 0; i < 4; i++)
            {
                int nr = r + RowOffsets[i];
                int nc = c + ColOffsets[i];

                if (nr >= 0 && nr < m && nc >= 0 && nc < n &&
                    !visited[nr, nc] &&
                    heights[nr][nc] >= heights[r][c])
                {
                    visited[nr, nc] = true;
                    queue.Enqueue((nr, nc));
                }
            }
        }
    }
}
