using System;
using System.Collections.Generic;

namespace Day23.TinyUrl;

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
/// LeetCode #105: Construct Binary Tree from Preorder and Inorder Traversal.
/// Uses a hash map to achieve O(1) partition lookups in divide-and-conquer recursion.
/// Time Complexity: O(N) | Space Complexity: O(N)
/// </summary>
public class TreeReconstructionSolvers
{
    public TreeNode? BuildTree(int[] preorder, int[] inorder)
    {
        if (preorder == null || inorder == null || preorder.Length != inorder.Length || preorder.Length == 0)
        {
            return null;
        }

        // Build value-to-index map for O(1) split index retrieval in inorder array
        var inorderMap = new Dictionary<int, int>(inorder.Length);
        for (int i = 0; i < inorder.Length; i++)
        {
            inorderMap[inorder[i]] = i;
        }

        int preIndex = 0;

        TreeNode? BuildSubtree(int inStart, int inEnd)
        {
            if (inStart > inEnd) return null;

            int rootVal = preorder[preIndex++];
            var root = new TreeNode(rootVal);

            int inRootIndex = inorderMap[rootVal];

            // In preorder, all left subtree nodes appear immediately after root,
            // so left child must be built before right child.
            root.left = BuildSubtree(inStart, inRootIndex - 1);
            root.right = BuildSubtree(inRootIndex + 1, inEnd);

            return root;
        }

        return BuildSubtree(0, inorder.Length - 1);
    }
}
