namespace Day30.RabbitMQDLQMassTransit;

public static class CourseScheduleSolver
{
    /// <summary>
    /// LeetCode #207: Course Schedule (Medium) using Kahn's Algorithm (BFS in-degree topological sort).
    /// Time Complexity: O(V + E), Space Complexity: O(V + E)
    /// </summary>
    public static bool CanFinishKahn(int numCourses, int[][] prerequisites)
    {
        if (numCourses <= 0) return true;

        var adj = new List<int>[numCourses];
        var inDegree = new int[numCourses];
        for (int i = 0; i < numCourses; i++)
        {
            adj[i] = new List<int>();
        }

        foreach (var pair in prerequisites)
        {
            int course = pair[0];
            int prereq = pair[1];
            adj[prereq].Add(course);
            inDegree[course]++;
        }

        var queue = new Queue<int>();
        for (int i = 0; i < numCourses; i++)
        {
            if (inDegree[i] == 0)
            {
                queue.Enqueue(i);
            }
        }

        int visitedCount = 0;
        while (queue.Count > 0)
        {
            int u = queue.Dequeue();
            visitedCount++;

            foreach (int v in adj[u])
            {
                inDegree[v]--;
                if (inDegree[v] == 0)
                {
                    queue.Enqueue(v);
                }
            }
        }

        return visitedCount == numCourses;
    }

    /// <summary>
    /// LeetCode #207: Course Schedule (Medium) using 3-Coloring DFS Cycle Detection.
    /// 0 = Unvisited, 1 = Visiting (in recursion stack), 2 = Visited.
    /// Time Complexity: O(V + E), Space Complexity: O(V + E)
    /// </summary>
    public static bool CanFinishDfs(int numCourses, int[][] prerequisites)
    {
        if (numCourses <= 0) return true;

        var adj = new List<int>[numCourses];
        for (int i = 0; i < numCourses; i++)
        {
            adj[i] = new List<int>();
        }

        foreach (var pair in prerequisites)
        {
            int course = pair[0];
            int prereq = pair[1];
            adj[prereq].Add(course);
        }

        var state = new int[numCourses]; // 0=White, 1=Gray, 2=Black

        for (int i = 0; i < numCourses; i++)
        {
            if (state[i] == 0)
            {
                if (HasCycleDfs(i, adj, state))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static bool HasCycleDfs(int u, List<int>[] adj, int[] state)
    {
        state[u] = 1; // Mark as visiting

        foreach (int v in adj[u])
        {
            if (state[v] == 1) return true; // Directed back-edge -> Cycle!
            if (state[v] == 0 && HasCycleDfs(v, adj, state)) return true;
        }

        state[u] = 2; // Mark as visited
        return false;
    }

    /// <summary>
    /// LeetCode #210: Course Schedule II (Medium) using Kahn's Algorithm.
    /// Returns valid topological order, or empty array if cycle detected.
    /// Time Complexity: O(V + E), Space Complexity: O(V + E)
    /// </summary>
    public static int[] FindOrderKahn(int numCourses, int[][] prerequisites)
    {
        if (numCourses <= 0) return Array.Empty<int>();

        var adj = new List<int>[numCourses];
        var inDegree = new int[numCourses];
        for (int i = 0; i < numCourses; i++)
        {
            adj[i] = new List<int>();
        }

        foreach (var pair in prerequisites)
        {
            int course = pair[0];
            int prereq = pair[1];
            adj[prereq].Add(course);
            inDegree[course]++;
        }

        var queue = new Queue<int>();
        for (int i = 0; i < numCourses; i++)
        {
            if (inDegree[i] == 0)
            {
                queue.Enqueue(i);
            }
        }

        var order = new int[numCourses];
        int index = 0;

        while (queue.Count > 0)
        {
            int u = queue.Dequeue();
            order[index++] = u;

            foreach (int v in adj[u])
            {
                inDegree[v]--;
                if (inDegree[v] == 0)
                {
                    queue.Enqueue(v);
                }
            }
        }

        return index == numCourses ? order : Array.Empty<int>();
    }

    /// <summary>
    /// LeetCode #210: Course Schedule II (Medium) using DFS Post-order reversal.
    /// Time Complexity: O(V + E), Space Complexity: O(V + E)
    /// </summary>
    public static int[] FindOrderDfs(int numCourses, int[][] prerequisites)
    {
        if (numCourses <= 0) return Array.Empty<int>();

        var adj = new List<int>[numCourses];
        for (int i = 0; i < numCourses; i++)
        {
            adj[i] = new List<int>();
        }

        foreach (var pair in prerequisites)
        {
            int course = pair[0];
            int prereq = pair[1];
            adj[prereq].Add(course);
        }

        var state = new int[numCourses];
        var orderStack = new Stack<int>();

        for (int i = 0; i < numCourses; i++)
        {
            if (state[i] == 0)
            {
                if (DfsTopological(i, adj, state, orderStack))
                {
                    return Array.Empty<int>(); // Cycle detected
                }
            }
        }

        var result = new int[numCourses];
        int index = 0;
        while (orderStack.Count > 0)
        {
            result[index++] = orderStack.Pop();
        }

        return result;
    }

    private static bool DfsTopological(int u, List<int>[] adj, int[] state, Stack<int> orderStack)
    {
        state[u] = 1;

        foreach (int v in adj[u])
        {
            if (state[v] == 1) return true; // Cycle!
            if (state[v] == 0 && DfsTopological(v, adj, state, orderStack)) return true;
        }

        state[u] = 2;
        orderStack.Push(u);
        return false;
    }
}
