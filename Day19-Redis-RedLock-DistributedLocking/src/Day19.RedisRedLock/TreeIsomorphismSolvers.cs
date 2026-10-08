using System;
using System.Collections.Generic;

namespace Day19.RedisRedLock;

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
/// Tree isomorphism and subtree matching algorithms:
/// - LeetCode #100: Same Tree
/// - LeetCode #572: Subtree of Another Tree (Standard DFS & O(N+M) Merkle Tree Hashing)
/// </summary>
public class TreeIsomorphismSolvers
{
    /// <summary>
    /// Checks if two binary trees are structurally identical and have identical node values (LeetCode #100).
    /// Time Complexity: O(min(N, M))
    /// Space Complexity: O(min(Hp, Hq)) recursion stack space.
    /// </summary>
    public bool IsSameTree(TreeNode? p, TreeNode? q)
    {
        if (p == null && q == null) return true;
        if (p == null || q == null) return false;
        if (p.val != q.val) return false;

        return IsSameTree(p.left, q.left) && IsSameTree(p.right, q.right);
    }

    /// <summary>
    /// Determines whether 'subRoot' is a subtree of 'root' using classic recursive DFS (LeetCode #572).
    /// Time Complexity: O(N * M) worst-case (e.g. skewed repetitive trees).
    /// Space Complexity: O(H_root) recursion stack space.
    /// </summary>
    public bool IsSubtree(TreeNode? root, TreeNode? subRoot)
    {
        if (subRoot == null) return true;
        if (root == null) return false;

        if (IsSameTree(root, subRoot)) return true;

        return IsSubtree(root.left, subRoot) || IsSubtree(root.right, subRoot);
    }

    /// <summary>
    /// Staff-level linear-time solution for LeetCode #572 using Merkle Tree Hashing.
    /// Assigns a unique cryptographic/polynomial hash to every subtree during a single bottom-up pass.
    /// Time Complexity: O(N + M)
    /// Space Complexity: O(N + M)
    /// </summary>
    public bool IsSubtreeMerkle(TreeNode? root, TreeNode? subRoot)
    {
        if (subRoot == null) return true;
        if (root == null) return false;

        var subtreeHashes = new HashSet<long>();

        // 1. Compute target subRoot hash
        long targetHash = ComputeMerkleHash(subRoot, null);

        // 2. Compute all subtree hashes in root
        ComputeMerkleHash(root, subtreeHashes);

        return subtreeHashes.Contains(targetHash);
    }

    private static long ComputeMerkleHash(TreeNode? node, HashSet<long>? collectedHashes)
    {
        if (node == null)
        {
            // Sentinel null marker hash
            return unchecked((long)0x9e3779b97f4a7c15UL);
        }

        long leftHash = ComputeMerkleHash(node.left, collectedHashes);
        long rightHash = ComputeMerkleHash(node.right, collectedHashes);

        // Murmur/SplitMix64-style mixing function:
        // Hash = Mix(node.val, leftHash, rightHash)
        long h = unchecked(node.val * 31L + leftHash * 1000003L + rightHash * 1000033L);
        h ^= (h >> 33);
        h = unchecked(h * -49064778989728563L);
        h ^= (h >> 33);

        collectedHashes?.Add(h);
        return h;
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
