namespace Day27.ApiGatewayYarp;

/// <summary>
/// Node definition for LeetCode #133: Clone Graph.
/// </summary>
public class Node
{
    public int val;
    public IList<Node> neighbors;

    public Node()
    {
        val = 0;
        neighbors = new List<Node>();
    }

    public Node(int _val)
    {
        val = _val;
        neighbors = new List<Node>();
    }

    public Node(int _val, IList<Node> _neighbors)
    {
        val = _val;
        neighbors = _neighbors;
    }
}

/// <summary>
/// Graph and 2D Grid Solvers:
/// 1. LeetCode #133: Clone Graph (Medium)
/// 2. LeetCode #695: Max Area of Island (Medium)
/// </summary>
public class GraphAndIslandSolver
{
    private static readonly int[] RowOffsets = { -1, 1, 0, 0 };
    private static readonly int[] ColOffsets = { 0, 0, -1, 1 };

    /// <summary>
    /// Clones an undirected graph using Depth-First Search (DFS) with a visited lookup map.
    /// Time Complexity: O(V + E) where V is vertices and E is edges.
    /// Space Complexity: O(V) for clone map and recursion stack.
    /// </summary>
    public Node? CloneGraphDfs(Node? node)
    {
        if (node == null) return null;
        var visited = new Dictionary<Node, Node>();
        return DfsClone(node, visited);
    }

    private Node DfsClone(Node node, Dictionary<Node, Node> visited)
    {
        if (visited.TryGetValue(node, out var existingClone))
        {
            return existingClone;
        }

        var clone = new Node(node.val);
        visited[node] = clone;

        foreach (var neighbor in node.neighbors)
        {
            clone.neighbors.Add(DfsClone(neighbor, visited));
        }

        return clone;
    }

    /// <summary>
    /// Clones an undirected graph using Breadth-First Search (BFS) with a FIFO queue.
    /// Time Complexity: O(V + E)
    /// Space Complexity: O(V)
    /// </summary>
    public Node? CloneGraphBfs(Node? node)
    {
        if (node == null) return null;

        var visited = new Dictionary<Node, Node>();
        var queue = new Queue<Node>();

        var cloneRoot = new Node(node.val);
        visited[node] = cloneRoot;
        queue.Enqueue(node);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();

            foreach (var neighbor in current.neighbors)
            {
                if (!visited.TryGetValue(neighbor, out var neighborClone))
                {
                    neighborClone = new Node(neighbor.val);
                    visited[neighbor] = neighborClone;
                    queue.Enqueue(neighbor);
                }

                visited[current].neighbors.Add(neighborClone);
            }
        }

        return cloneRoot;
    }

    /// <summary>
    /// Computes the maximum area of an island using Depth-First Search (DFS).
    /// Time Complexity: O(M * N)
    /// Space Complexity: O(M * N) recursion stack.
    /// </summary>
    public int MaxAreaOfIslandDfs(int[][]? grid)
    {
        if (grid == null || grid.Length == 0 || grid[0].Length == 0) return 0;

        var m = grid.Length;
        var n = grid[0].Length;
        var workingGrid = new int[m][];
        for (var i = 0; i < m; i++)
        {
            workingGrid[i] = (int[])grid[i].Clone();
        }

        var maxArea = 0;
        for (var r = 0; r < m; r++)
        {
            for (var c = 0; c < n; c++)
            {
                if (workingGrid[r][c] == 1)
                {
                    var area = DfsSinkArea(workingGrid, r, c, m, n);
                    maxArea = Math.Max(maxArea, area);
                }
            }
        }

        return maxArea;
    }

    private int DfsSinkArea(int[][] grid, int r, int c, int m, int n)
    {
        if (r < 0 || r >= m || c < 0 || c >= n || grid[r][c] != 1)
            return 0;

        // Sink cell to 0 to prevent re-counting
        grid[r][c] = 0;
        var area = 1;

        for (var i = 0; i < 4; i++)
        {
            area += DfsSinkArea(grid, r + RowOffsets[i], c + ColOffsets[i], m, n);
        }

        return area;
    }

    /// <summary>
    /// Computes the maximum area of an island using Breadth-First Search (BFS).
    /// Time Complexity: O(M * N)
    /// Space Complexity: O(min(M, N))
    /// </summary>
    public int MaxAreaOfIslandBfs(int[][]? grid)
    {
        if (grid == null || grid.Length == 0 || grid[0].Length == 0) return 0;

        var m = grid.Length;
        var n = grid[0].Length;
        var workingGrid = new int[m][];
        for (var i = 0; i < m; i++)
        {
            workingGrid[i] = (int[])grid[i].Clone();
        }

        var maxArea = 0;
        var queue = new Queue<(int r, int c)>();

        for (var r = 0; r < m; r++)
        {
            for (var c = 0; c < n; c++)
            {
                if (workingGrid[r][c] == 1)
                {
                    workingGrid[r][c] = 0; // sink immediately
                    queue.Enqueue((r, c));
                    var currentArea = 0;

                    while (queue.Count > 0)
                    {
                        var (currR, currC) = queue.Dequeue();
                        currentArea++;

                        for (var i = 0; i < 4; i++)
                        {
                            var nr = currR + RowOffsets[i];
                            var nc = currC + ColOffsets[i];

                            if (nr >= 0 && nr < m && nc >= 0 && nc < n && workingGrid[nr][nc] == 1)
                            {
                                workingGrid[nr][nc] = 0;
                                queue.Enqueue((nr, nc));
                            }
                        }
                    }

                    maxArea = Math.Max(maxArea, currentArea);
                }
            }
        }

        return maxArea;
    }
}
