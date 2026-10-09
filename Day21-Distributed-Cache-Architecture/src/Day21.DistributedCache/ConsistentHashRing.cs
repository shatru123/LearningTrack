using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace Day21.DistributedCache;

/// <summary>
/// Production Consistent Hash Ring with Virtual Nodes (Ketama-style).
/// Distributes keys uniformly across cache nodes in a 32-bit circular hash space.
/// </summary>
public class ConsistentHashRing<TNode> where TNode : notnull
{
    private readonly int _virtualNodeReplicas;
    private readonly Func<TNode, string> _nodeKeySelector;
    private readonly List<uint> _sortedRingKeys = new();
    private readonly Dictionary<uint, TNode> _ring = new();
    private readonly HashSet<TNode> _physicalNodes = new();
    private readonly object _syncLock = new();

    public int PhysicalNodeCount
    {
        get { lock (_syncLock) return _physicalNodes.Count; }
    }

    public int VirtualNodeCount
    {
        get { lock (_syncLock) return _sortedRingKeys.Count; }
    }

    public IReadOnlyCollection<TNode> PhysicalNodes
    {
        get { lock (_syncLock) return _physicalNodes.ToList(); }
    }

    public ConsistentHashRing(
        Func<TNode, string> nodeKeySelector,
        int virtualNodeReplicas = 100,
        IEnumerable<TNode>? initialNodes = null)
    {
        _nodeKeySelector = nodeKeySelector ?? throw new ArgumentNullException(nameof(nodeKeySelector));
        _virtualNodeReplicas = virtualNodeReplicas > 0 ? virtualNodeReplicas : 100;

        if (initialNodes != null)
        {
            foreach (var node in initialNodes)
            {
                AddNode(node);
            }
        }
    }

    /// <summary>
    /// Adds a physical node to the ring, allocating _virtualNodeReplicas positions on the hash ring.
    /// </summary>
    public bool AddNode(TNode node)
    {
        lock (_syncLock)
        {
            if (!_physicalNodes.Add(node)) return false;

            string baseNodeKey = _nodeKeySelector(node);
            for (int i = 0; i < _virtualNodeReplicas; i++)
            {
                string vNodeKey = $"{baseNodeKey}#vn_{i}";
                uint hash = ComputeHash(vNodeKey);

                _ring[hash] = node;
                int idx = _sortedRingKeys.BinarySearch(hash);
                if (idx < 0)
                {
                    _sortedRingKeys.Insert(~idx, hash);
                }
            }

            return true;
        }
    }

    /// <summary>
    /// Removes a physical node and all its virtual nodes from the ring.
    /// </summary>
    public bool RemoveNode(TNode node)
    {
        lock (_syncLock)
        {
            if (!_physicalNodes.Remove(node)) return false;

            string baseNodeKey = _nodeKeySelector(node);
            for (int i = 0; i < _virtualNodeReplicas; i++)
            {
                string vNodeKey = $"{baseNodeKey}#vn_{i}";
                uint hash = ComputeHash(vNodeKey);

                _ring.Remove(hash);
                int idx = _sortedRingKeys.BinarySearch(hash);
                if (idx >= 0)
                {
                    _sortedRingKeys.RemoveAt(idx);
                }
            }

            return true;
        }
    }

    /// <summary>
    /// Finds the owning cache node for a given cache key by walking clockwise on the ring.
    /// Complexity: O(log(N * V)) via binary search.
    /// </summary>
    public TNode GetNode(string key)
    {
        lock (_syncLock)
        {
            if (_sortedRingKeys.Count == 0)
            {
                throw new InvalidOperationException("Cannot resolve key: Consistent hash ring is empty.");
            }

            uint hash = ComputeHash(key);
            int idx = _sortedRingKeys.BinarySearch(hash);

            if (idx < 0)
            {
                idx = ~idx;
                // If hash is greater than all points on the ring, wrap around to the first point (circular ring)
                if (idx == _sortedRingKeys.Count)
                {
                    idx = 0;
                }
            }

            uint ringHash = _sortedRingKeys[idx];
            return _ring[ringHash];
        }
    }

    /// <summary>
    /// Computes a uniform 32-bit hash using MD5 bytes truncated to uint.
    /// MD5 provides superior distribution uniformity over standard string.GetHashCode() across virtual nodes.
    /// </summary>
    public static uint ComputeHash(string input)
    {
        byte[] inputBytes = Encoding.UTF8.GetBytes(input);
        byte[] hashBytes = MD5.HashData(inputBytes);

        // Take first 4 bytes as 32-bit integer
        return BitConverter.ToUInt32(hashBytes, 0);
    }
}
