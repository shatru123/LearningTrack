using System;
using System.Collections.Generic;

namespace Day18.RedisDataStructures;

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
/// High-performance tree metric algorithms:
/// - LeetCode #543: Diameter of Binary Tree
/// - LeetCode #110: Balanced Binary Tree
/// Both solved in optimal O(N) time and O(H) auxiliary stack space using bottom-up post-order traversal.
/// </summary>
public class BinaryTreeMetricsSolvers
{
    /// <summary>
    /// Computes the diameter of a binary tree (LeetCode #543).
    /// The diameter is the length of the longest path between any two nodes in a tree (measured in edges).
    /// Time Complexity: O(N) visiting each node exactly once.
    /// Space Complexity: O(H) recursion stack where H is the height of the tree.
    /// </summary>
    public int DiameterOfBinaryTree(TreeNode? root)
    {
        int maxDiameter = 0;

        int GetHeight(TreeNode? node)
        {
            if (node == null) return 0;

            int leftHeight = GetHeight(node.left);
            int rightHeight = GetHeight(node.right);

            // Path through this node in edges is leftHeight + rightHeight
            maxDiameter = Math.Max(maxDiameter, leftHeight + rightHeight);

            // Height is 1 + max child height
            return 1 + Math.Max(leftHeight, rightHeight);
        }

        GetHeight(root);
        return maxDiameter;
    }

    /// <summary>
    /// Determines if a binary tree is height-balanced (LeetCode #110).
    /// A binary tree is balanced if the depth of the two subtrees of every node never differs by more than 1.
    /// Uses bottom-up post-order recursion returning -1 early if any subtree is unbalanced.
    /// Time Complexity: O(N) - short circuits immediately upon detecting imbalance.
    /// Space Complexity: O(H) recursion stack space.
    /// </summary>
    public bool IsBalanced(TreeNode? root)
    {
        return CheckHeight(root) != -1;
    }

    private static int CheckHeight(TreeNode? node)
    {
        if (node == null) return 0;

        int leftHeight = CheckHeight(node.left);
        if (leftHeight == -1) return -1; // Left subtree is already unbalanced

        int rightHeight = CheckHeight(node.right);
        if (rightHeight == -1) return -1; // Right subtree is already unbalanced

        if (Math.Abs(leftHeight - rightHeight) > 1)
        {
            return -1; // Current node violates balance property
        }

        return 1 + Math.Max(leftHeight, rightHeight);
    }

    /// <summary>
    /// Helper to construct a binary tree from level-order array representation (LeetCode test format).
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
            var current = queue.Dequeue();

            if (i < values.Length && values[i] != null)
            {
                current.left = new TreeNode(values[i]!.Value);
                queue.Enqueue(current.left);
            }
            i++;

            if (i < values.Length && values[i] != null)
            {
                current.right = new TreeNode(values[i]!.Value);
                queue.Enqueue(current.right);
            }
            i++;
        }

        return root;
    }
}
