namespace Day31.TransactionalOutbox;

public class DisjointSetUnion
{
    private readonly int[] _parent;
    private readonly int[] _rank;

    public DisjointSetUnion(int size)
    {
        _parent = new int[size + 1];
        _rank = new int[size + 1];
        for (int i = 0; i <= size; i++)
        {
            _parent[i] = i;
            _rank[i] = 1;
        }
    }

    /// <summary>
    /// Finds the canonical representative of the set containing element x with Path Compression.
    /// Amortized Time Complexity: O(alpha(N))
    /// </summary>
    public int Find(int x)
    {
        if (_parent[x] != x)
        {
            _parent[x] = Find(_parent[x]); // Path compression
        }
        return _parent[x];
    }

    /// <summary>
    /// Unites the sets containing u and v using Union by Rank.
    /// Returns false if u and v are already in the same set (cycle detected); otherwise true.
    /// </summary>
    public bool Union(int u, int v)
    {
        int rootU = Find(u);
        int rootV = Find(v);

        if (rootU == rootV)
        {
            return false; // Cycle detected!
        }

        // Union by rank: attach smaller tree under root of larger tree
        if (_rank[rootU] < _rank[rootV])
        {
            _parent[rootU] = rootV;
        }
        else if (_rank[rootU] > _rank[rootV])
        {
            _parent[rootV] = rootU;
        }
        else
        {
            _parent[rootV] = rootU;
            _rank[rootU]++;
        }

        return true;
    }

    public bool IsConnected(int u, int v) => Find(u) == Find(v);
}

public static class RedundantConnectionSolver
{
    /// <summary>
    /// LeetCode #684: Redundant Connection (Medium).
    /// Identifies the edge that can be removed so the graph becomes a tree.
    /// Time Complexity: O(N * alpha(N)) approx O(N), Space Complexity: O(N)
    /// </summary>
    public static int[] FindRedundantConnection(int[][] edges)
    {
        if (edges == null || edges.Length == 0) return Array.Empty<int>();

        int n = edges.Length;
        var dsu = new DisjointSetUnion(n);

        foreach (var edge in edges)
        {
            int u = edge[0];
            int v = edge[1];

            // If u and v already share the same root, adding (u, v) creates a cycle
            if (!dsu.Union(u, v))
            {
                return edge;
            }
        }

        return Array.Empty<int>();
    }
}
