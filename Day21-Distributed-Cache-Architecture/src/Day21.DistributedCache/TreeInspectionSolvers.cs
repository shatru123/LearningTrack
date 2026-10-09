using System;
using System.Collections.Generic;

namespace Day21.DistributedCache;

/// <summary>
/// Definition for a binary tree node.
/// </summary>
public class TreeNode
{
    public int val;
    public TreeNode? left;
    public TreeNode? right;

    public TreeNode(int val = 0, TreeNode? left = null, TreeNode? right = null)
    {
        this.val = val;
        this.left = left;
        this.right = right;
    }
}

/// <summary>
/// Tree inspection and view algorithms:
/// - LeetCode #199: Binary Tree Right Side View
/// - LeetCode #1448: Count Good Nodes in Binary Tree
/// </summary>
public class TreeInspectionSolvers
{
    /// <summary>
    /// Computes the right-side view of a binary tree from top to bottom (LeetCode #199).
    /// Uses right-first recursive DFS: visits right children before left children and records
    /// the first node encountered at each depth level.
    /// Time Complexity: O(N) visiting each node once.
    /// Space Complexity: O(H) recursion stack space.
    /// </summary>
    public IList<int> RightSideView(TreeNode? root)
    {
        var result = new List<int>();

        void Dfs(TreeNode? node, int depth)
        {
            if (node == null) return;

            // If this is the first node encountered at this depth level, it must be the rightmost
            if (depth == result.Count)
            {
                result.Add(node.val);
            }

            // Prioritize right subtree
            Dfs(node.right, depth + 1);
            Dfs(node.left, depth + 1);
        }

        Dfs(root, 0);
        return result;
    }

    /// <summary>
    /// Counts the number of "good" nodes in a binary tree (LeetCode #1448).
    /// A node X is good if in the path from the root to X there are no nodes with a value greater than X.
    /// Time Complexity: O(N)
    /// Space Complexity: O(H) recursion stack space.
    /// </summary>
    public int GoodNodes(TreeNode? root)
    {
        if (root == null) return 0;

        int Dfs(TreeNode? node, int maxSoFar)
        {
            if (node == null) return 0;

            int isGood = node.val >= maxSoFar ? 1 : 0;
            int newMax = Math.Max(maxSoFar, node.val);

            return isGood + Dfs(node.left, newMax) + Dfs(node.right, newMax);
        }

        return Dfs(root, root.val);
    }

    /// <summary>
    /// Helper to deserialize a tree from a level-order array representation.
    /// </summary>
    public static TreeNode? BuildTree(int?[] values)
    {
        if (values == null || values.Length == 0 || values[0] == null) return null;

        var root = new TreeNode(values[0]!.Value);
        var queue = new Queue<TreeNode>();
        queue.Enqueue(root);

        int i = 1;
        while (queue.Count > 0 && i < values.Length)
        {
            var curr = queue.Dequeue();

            if (i < values.Length && values[i] != null)
            {
                curr.left = new TreeNode(values[i]!.Value);
                queue.Enqueue(curr.left);
            }
            i++;

            if (i < values.Length && values[i] != null)
            {
                curr.right = new TreeNode(values[i]!.Value);
                queue.Enqueue(curr.right);
            }
            i++;
        }

        return root;
    }
}
