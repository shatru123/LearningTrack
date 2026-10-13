using System;

namespace Day24.AdvancedSql;

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
/// LeetCode #124: Binary Tree Maximum Path Sum (Hard).
/// Solved via Post-Order DFS with bottom-up dynamic programming.
/// Computes maximum contribution gain from child subtrees while tracking the optimal peak path sum.
/// Time Complexity: O(N) | Space Complexity: O(H) recursion stack.
/// </summary>
public class MaxPathSumSolver
{
    private int _maxPathSum;

    public int MaxPathSum(TreeNode? root)
    {
        if (root == null) return 0;

        _maxPathSum = int.MinValue;
        CalculateMaxGain(root);
        return _maxPathSum;
    }

    private int CalculateMaxGain(TreeNode? node)
    {
        if (node == null) return 0;

        // Post-order: Evaluate subtrees first. Ignore negative path branches (clamp to 0).
        int leftGain = Math.Max(0, CalculateMaxGain(node.left));
        int rightGain = Math.Max(0, CalculateMaxGain(node.right));

        // Path passing through current node as the peak anchor
        int currentPathSum = node.val + leftGain + rightGain;

        // Update global maximum path sum
        _maxPathSum = Math.Max(_maxPathSum, currentPathSum);

        // Return maximum branch gain current node can contribute to its parent
        return node.val + Math.Max(leftGain, rightGain);
    }
}
