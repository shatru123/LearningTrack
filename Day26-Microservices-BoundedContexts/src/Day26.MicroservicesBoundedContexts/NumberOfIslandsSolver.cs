namespace Day26.MicroservicesBoundedContexts;

/// <summary>
/// Solves LeetCode #200: Number of Islands.
/// Given an m x n 2D binary grid grid which represents a map of '1's (land) and '0's (water),
/// return the number of islands. An island is surrounded by water and is formed by connecting
/// adjacent lands horizontally or vertically.
/// </summary>
public class NumberOfIslandsSolver
{
    private static readonly int[] RowOffsets = { -1, 1, 0, 0 };
    private static readonly int[] ColOffsets = { 0, 0, -1, 1 };

    /// <summary>
    /// Solves Number of Islands using Depth-First Search (DFS) with in-place sinking.
    /// Time Complexity: O(M * N)
    /// Space Complexity: O(M * N) in worst case (recursion call stack for all land).
    /// </summary>
    public int NumIslandsDfs(char[][]? grid)
    {
        if (grid == null || grid.Length == 0 || grid[0].Length == 0)
            return 0;

        // Clone grid to preserve caller input
        var m = grid.Length;
        var n = grid[0].Length;
        var workingGrid = new char[m][];
        for (var i = 0; i < m; i++)
        {
            workingGrid[i] = (char[])grid[i].Clone();
        }

        var islandCount = 0;
        for (var r = 0; r < m; r++)
        {
            for (var c = 0; c < n; c++)
            {
                if (workingGrid[r][c] == '1')
                {
                    islandCount++;
                    DfsSink(workingGrid, r, c, m, n);
                }
            }
        }

        return islandCount;
    }

    private static void DfsSink(char[][] grid, int r, int c, int m, int n)
    {
        if (r < 0 || r >= m || c < 0 || c >= n || grid[r][c] != '1')
            return;

        // Sink land to water ('0') to prevent revisit
        grid[r][c] = '0';

        for (var i = 0; i < 4; i++)
        {
            DfsSink(grid, r + RowOffsets[i], c + ColOffsets[i], m, n);
        }
    }

    /// <summary>
    /// Solves Number of Islands using Breadth-First Search (BFS) with FIFO queue.
    /// Time Complexity: O(M * N)
    /// Space Complexity: O(min(M, N)) queue memory.
    /// </summary>
    public int NumIslandsBfs(char[][]? grid)
    {
        if (grid == null || grid.Length == 0 || grid[0].Length == 0)
            return 0;

        var m = grid.Length;
        var n = grid[0].Length;
        var workingGrid = new char[m][];
        for (var i = 0; i < m; i++)
        {
            workingGrid[i] = (char[])grid[i].Clone();
        }

        var islandCount = 0;
        var queue = new Queue<(int r, int c)>();

        for (var r = 0; r < m; r++)
        {
            for (var c = 0; c < n; c++)
            {
                if (workingGrid[r][c] == '1')
                {
                    islandCount++;
                    workingGrid[r][c] = '0'; // mark visited immediately
                    queue.Enqueue((r, c));

                    while (queue.Count > 0)
                    {
                        var (currR, currC) = queue.Dequeue();

                        for (var i = 0; i < 4; i++)
                        {
                            var nr = currR + RowOffsets[i];
                            var nc = currC + ColOffsets[i];

                            if (nr >= 0 && nr < m && nc >= 0 && nc < n && workingGrid[nr][nc] == '1')
                            {
                                workingGrid[nr][nc] = '0'; // mark visited on enqueue to prevent duplicates
                                queue.Enqueue((nr, nc));
                            }
                        }
                    }
                }
            }
        }

        return islandCount;
    }
}
