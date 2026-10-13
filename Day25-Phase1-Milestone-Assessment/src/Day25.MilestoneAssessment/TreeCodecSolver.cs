using System;
using System.Collections.Generic;
using System.Text;

namespace Day25.MilestoneAssessment;

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
/// LeetCode #297: Serialize and Deserialize Binary Tree (Hard).
/// Provides both Preorder DFS and Level-Order BFS codecs.
/// Time Complexity: O(N) | Space Complexity: O(N)
/// </summary>
public class TreeCodecSolver
{
    private const string NullSentinel = "#";
    private const char Delimiter = ',';

    /// <summary>
    /// Serializes a binary tree into a single compact string using Preorder DFS.
    /// Format: "1,2,#,#,3,4,#,#,5,#,#"
    /// </summary>
    public string SerializeDfs(TreeNode? root)
    {
        var sb = new StringBuilder();
        BuildDfsString(root, sb);
        return sb.ToString();
    }

    private void BuildDfsString(TreeNode? node, StringBuilder sb)
    {
        if (node == null)
        {
            sb.Append(NullSentinel).Append(Delimiter);
            return;
        }

        sb.Append(node.val).Append(Delimiter);
        BuildDfsString(node.left, sb);
        BuildDfsString(node.right, sb);
    }

    /// <summary>
    /// Deserializes encoded Preorder DFS data back to the original binary tree.
    /// </summary>
    public TreeNode? DeserializeDfs(string data)
    {
        if (string.IsNullOrWhiteSpace(data)) return null;

        var tokens = data.Split(Delimiter, StringSplitOptions.RemoveEmptyEntries);
        var queue = new Queue<string>(tokens);

        return BuildDfsTree(queue);
    }

    private TreeNode? BuildDfsTree(Queue<string> queue)
    {
        if (queue.Count == 0) return null;

        var token = queue.Dequeue();
        if (token == NullSentinel)
        {
            return null;
        }

        int val = int.Parse(token);
        var node = new TreeNode(val)
        {
            left = BuildDfsTree(queue),
            right = BuildDfsTree(queue)
        };

        return node;
    }

    /// <summary>
    /// Serializes a binary tree using Level-Order BFS (Canonical LeetCode format).
    /// Format: "1,2,3,null,null,4,5"
    /// </summary>
    public string SerializeBfs(TreeNode? root)
    {
        if (root == null) return string.Empty;

        var sb = new StringBuilder();
        var queue = new Queue<TreeNode?>();
        queue.Enqueue(root);

        while (queue.Count > 0)
        {
            var curr = queue.Dequeue();
            if (curr != null)
            {
                sb.Append(curr.val).Append(Delimiter);
                queue.Enqueue(curr.left);
                queue.Enqueue(curr.right);
            }
            else
            {
                sb.Append(NullSentinel).Append(Delimiter);
            }
        }

        // Trim trailing null sentinels for cleaner representation
        return sb.ToString().TrimEnd(Delimiter);
    }

    /// <summary>
    /// Deserializes Level-Order BFS string back to original binary tree.
    /// </summary>
    public TreeNode? DeserializeBfs(string data)
    {
        if (string.IsNullOrWhiteSpace(data)) return null;

        var tokens = data.Split(Delimiter, StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 0 || tokens[0] == NullSentinel) return null;

        var root = new TreeNode(int.Parse(tokens[0]));
        var queue = new Queue<TreeNode>();
        queue.Enqueue(root);

        int index = 1;
        while (queue.Count > 0 && index < tokens.Length)
        {
            var parent = queue.Dequeue();

            // Left child
            if (index < tokens.Length)
            {
                var leftToken = tokens[index++];
                if (leftToken != NullSentinel)
                {
                    parent.left = new TreeNode(int.Parse(leftToken));
                    queue.Enqueue(parent.left);
                }
            }

            // Right child
            if (index < tokens.Length)
            {
                var rightToken = tokens[index++];
                if (rightToken != NullSentinel)
                {
                    parent.right = new TreeNode(int.Parse(rightToken));
                    queue.Enqueue(parent.right);
                }
            }
        }

        return root;
    }
}
