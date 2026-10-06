using System;
using System.Collections.Generic;

namespace Day17.RedisCachePatterns;

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

    public static TreeNode? FromLevelOrder(int?[] values)
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

    public List<int?> ToLevelOrder()
    {
        var result = new List<int?>();
        var queue = new Queue<TreeNode?>();
        queue.Enqueue(this);

        while (queue.Count > 0)
        {
            var node = queue.Dequeue();
            if (node != null)
            {
                result.Add(node.val);
                queue.Enqueue(node.left);
                queue.Enqueue(node.right);
            }
            else
            {
                result.Add(null);
            }
        }

        // Trim trailing nulls
        for (int i = result.Count - 1; i >= 0; i--)
        {
            if (result[i] == null) result.RemoveAt(i);
            else break;
        }

        return result;
    }
}

public static class BinaryTreeSolvers
{
    /// <summary>
    /// LeetCode #226: Invert Binary Tree (Recursive DFS).
    /// Time: O(N), Space: O(H) where H is height of the tree.
    /// </summary>
    public static TreeNode? InvertTree(TreeNode? root)
    {
        if (root == null) return null;

        var temp = root.left;
        root.left = InvertTree(root.right);
        root.right = InvertTree(temp);

        return root;
    }

    /// <summary>
    /// LeetCode #226: Invert Binary Tree (Iterative BFS Queue).
    /// Time: O(N), Space: O(W) where W is maximum tree width.
    /// </summary>
    public static TreeNode? InvertTreeIterative(TreeNode? root)
    {
        if (root == null) return null;

        var queue = new Queue<TreeNode>();
        queue.Enqueue(root);

        while (queue.Count > 0)
        {
            var node = queue.Dequeue();

            var temp = node.left;
            node.left = node.right;
            node.right = temp;

            if (node.left != null) queue.Enqueue(node.left);
            if (node.right != null) queue.Enqueue(node.right);
        }

        return root;
    }

    /// <summary>
    /// LeetCode #104: Maximum Depth of Binary Tree (Recursive DFS).
    /// Time: O(N), Space: O(H).
    /// </summary>
    public static int MaxDepth(TreeNode? root)
    {
        if (root == null) return 0;

        int leftDepth = MaxDepth(root.left);
        int rightDepth = MaxDepth(root.right);

        return Math.Max(leftDepth, rightDepth) + 1;
    }

    /// <summary>
    /// LeetCode #104: Maximum Depth of Binary Tree (Iterative BFS Level-Order).
    /// Time: O(N), Space: O(W).
    /// </summary>
    public static int MaxDepthIterative(TreeNode? root)
    {
        if (root == null) return 0;

        var queue = new Queue<TreeNode>();
        queue.Enqueue(root);
        int depth = 0;

        while (queue.Count > 0)
        {
            int levelSize = queue.Count;
            depth++;

            for (int i = 0; i < levelSize; i++)
            {
                var curr = queue.Dequeue();
                if (curr.left != null) queue.Enqueue(curr.left);
                if (curr.right != null) queue.Enqueue(curr.right);
            }
        }

        return depth;
    }
}
