using System;
using System.Collections.Generic;

namespace Day22.RateLimiter;

/// <summary>
/// Standard Binary Tree Node definition.
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
/// High-performance solvers for BST Validation and Kth Smallest Element.
/// </summary>
public class BstValidationSolvers
{
    /// <summary>
    /// LeetCode #98: Validate Binary Search Tree.
    /// Uses bounded range constraints [min, max] to verify that all nodes in left subtrees
    /// are strictly smaller than ancestors, and all in right subtrees are strictly larger.
    /// Time Complexity: O(N) | Space Complexity: O(H) call stack.
    /// </summary>
    public bool IsValidBST(TreeNode? root)
    {
        return ValidateRange(root, null, null);
    }

    private static bool ValidateRange(TreeNode? node, long? min, long? max)
    {
        if (node == null) return true;

        if (min.HasValue && node.val <= min.Value) return false;
        if (max.HasValue && node.val >= max.Value) return false;

        return ValidateRange(node.left, min, node.val) &&
               ValidateRange(node.right, node.val, max);
    }

    /// <summary>
    /// Alternative validation using In-Order Traversal invariant.
    /// In a valid BST, in-order traversal (Left, Root, Right) strictly yields an increasing sequence.
    /// Time Complexity: O(N) | Space Complexity: O(H).
    /// </summary>
    public bool IsValidBstInOrder(TreeNode? root)
    {
        long? prev = null;
        var stack = new Stack<TreeNode>();
        var curr = root;

        while (curr != null || stack.Count > 0)
        {
            while (curr != null)
            {
                stack.Push(curr);
                curr = curr.left;
            }

            curr = stack.Pop();
            if (prev.HasValue && curr.val <= prev.Value)
            {
                return false;
            }
            prev = curr.val;
            curr = curr.right;
        }

        return true;
    }

    /// <summary>
    /// LeetCode #230: Kth Smallest Element in a BST.
    /// Executes an iterative In-Order Traversal with early-exit termination when k steps have been popped.
    /// Time Complexity: O(H + k) | Space Complexity: O(H).
    /// </summary>
    public int KthSmallest(TreeNode? root, int k)
    {
        if (root == null) throw new ArgumentNullException(nameof(root));
        if (k <= 0) throw new ArgumentOutOfRangeException(nameof(k), "k must be positive.");

        var stack = new Stack<TreeNode>();
        var curr = root;

        while (curr != null || stack.Count > 0)
        {
            while (curr != null)
            {
                stack.Push(curr);
                curr = curr.left;
            }

            curr = stack.Pop();
            k--;
            if (k == 0)
            {
                return curr.val;
            }

            curr = curr.right;
        }

        throw new ArgumentException("k is larger than the number of nodes in the BST.", nameof(k));
    }
}
