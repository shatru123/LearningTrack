using System;
using System.Collections.Generic;

namespace Day20.RedisStreams;

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
/// Tree traversal and ancestor algorithms:
/// - LeetCode #235: Lowest Common Ancestor of a Binary Search Tree (BST)
/// - LeetCode #102: Binary Tree Level Order Traversal (BFS)
/// </summary>
public class TreeTraversalSolvers
{
    /// <summary>
    /// Finds the Lowest Common Ancestor (LCA) of two given nodes in a BST (LeetCode #235).
    /// Exploits BST ordering invariant: all left values &lt; node.val &lt; all right values.
    /// Time Complexity: O(H) where H is tree height (O(log N) balanced, O(N) skewed).
    /// Space Complexity: O(1) auxiliary memory using iterative traversal.
    /// </summary>
    public TreeNode? LowestCommonAncestor(TreeNode? root, TreeNode? p, TreeNode? q)
    {
        if (root == null || p == null || q == null) return null;

        var curr = root;
        while (curr != null)
        {
            if (p.val < curr.val && q.val < curr.val)
            {
                // Both nodes reside in left subtree
                curr = curr.left;
            }
            else if (p.val > curr.val && q.val > curr.val)
            {
                // Both nodes reside in right subtree
                curr = curr.right;
            }
            else
            {
                // Split point found: one is on left, one on right, or curr is p or q
                return curr;
            }
        }

        return null;
    }

    /// <summary>
    /// Returns the level order traversal of a binary tree's node values (LeetCode #102).
    /// Uses iterative BFS level-by-level queue batching.
    /// Time Complexity: O(N) visiting each node exactly once.
    /// Space Complexity: O(W) where W is maximum width of tree (up to N/2 nodes at leaf level).
    /// </summary>
    public IList<IList<int>> LevelOrder(TreeNode? root)
    {
        var result = new List<IList<int>>();
        if (root == null) return result;

        var queue = new Queue<TreeNode>();
        queue.Enqueue(root);

        while (queue.Count > 0)
        {
            int levelSize = queue.Count;
            var currentLevel = new List<int>(levelSize);

            for (int i = 0; i < levelSize; i++)
            {
                var node = queue.Dequeue();
                currentLevel.Add(node.val);

                if (node.left != null) queue.Enqueue(node.left);
                if (node.right != null) queue.Enqueue(node.right);
            }

            result.Add(currentLevel);
        }

        return result;
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

    /// <summary>
    /// Helper to find a specific node by value in the tree.
    /// </summary>
    public static TreeNode? FindNode(TreeNode? root, int targetVal)
    {
        if (root == null) return null;
        if (root.val == targetVal) return root;

        return FindNode(root.left, targetVal) ?? FindNode(root.right, targetVal);
    }
}
