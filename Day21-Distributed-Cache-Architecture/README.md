# Day 21: System Design: Distributed Cache Architecture

## Overview
Comprehensive system design and high-performance implementation of a distributed caching cluster in .NET. Covers **Consistent Hashing with Virtual Nodes** (Ketama-style ring topology), **Hot-Key Read Scattering**, **2-Tier (L1 Local + L2 Sharded) Caching**, and **Cache Stampede Protection** (Single-Flight Mutex + XFetch probabilistic early refresh). Additionally, solves binary tree visibility algorithms: **LeetCode #199 (Binary Tree Right Side View)** and **LeetCode #1448 (Count Good Nodes in Binary Tree)**.

---

## Architectural Breakdown

### 1. Consistent Hashing Ring with Virtual Nodes

Traditional modular hashing (`node = hash(key) % N`) causes a catastrophic **Cache Storm** when nodes are added or removed, because almost all keys ($1 - \frac{1}{N} \approx 80\text{–}90\%$) are abruptly remapped to different servers, crashing the underlying database.

**Consistent Hashing Solution**:
- Maps both keys and cache nodes onto a continuous 32-bit circular hash ring $[0, 2^{32}-1]$.
- A key is owned by the first cache node encountered walking clockwise along the ring.
- **Virtual Nodes (VNodes)**: Each physical node is duplicated across $V = 100\text{–}200$ positions (`nodeId#vn_0`, `nodeId#vn_1`, etc.).
  - Prevents non-uniform clustering and hash hotspots.
  - Adding a new node redistributes only $\approx \frac{1}{N_{\text{new}}}$ of keys, which are taken smoothly from all existing nodes.

```
                  [Node A #vn1] (Hash: 0x1200)
                     /                   \
      [Node C #vn2]                         [Node B #vn0]
            |                                     |
      key:order:42                                |
      (Hash: 0x6400)                              |
            |                                     |
      [Node B #vn1] <----------------------- [Node A #vn0]
                     \                   /
                  [Node C #vn0] (Hash: 0x9000)
```

---

### 2. Hot-Key Mitigation: Suffix Scattering

When a single key receives millions of requests per second (e.g., viral social post, flash sale inventory), a single cache shard machine saturates its network interface card (NIC).
- **Suffix Scattering Pattern**:
  1. The key is logically replicated across $M$ suffixes: `key:#hk_0`, `key:#hk_1`, ... `key:#hk_M-1`.
  2. Because each suffix hashes to a different ring position, the replicas are distributed across different physical shard machines.
  3. Readers randomly query `key:#hk_{random(0, M-1)}`, spreading network I/O and CPU load across $M$ distinct cluster nodes.

---

### 3. Multi-Tier & Stampede Protection

- **Tier 1 (L1 In-Process MemoryCache)**: Sub-microsecond local RAM access with short TTLs (1–5 seconds). Bypasses cluster network hops for ultra-hot keys.
- **Tier 2 (L2 Distributed Shards)**: Consistent hash-routed distributed cluster nodes.
- **Single-Flight Lock**: Only one thread acquires the lock to compute a missing key from the database; all other concurrent callers await the result.

---

### 4. Tree Visibility Algorithms

#### LeetCode #199: Binary Tree Right Side View
- **Problem**: Return values of nodes visible when looking at the tree from the right side.
- **Algorithm**: Right-first recursive DFS (`dfs(node.right, depth + 1)` before `dfs(node.left, depth + 1)`). If `depth == result.Count`, the current node is the rightmost at that depth.
- **Complexity**: Time: $O(N)$, Space: $O(H)$ recursion stack.

#### LeetCode #1448: Count Good Nodes in Binary Tree
- **Problem**: Node $X$ is good if on the path from root to $X$, no node has a value greater than $X$.
- **Algorithm**: Pre-order DFS tracking running ancestor maximum: `isGood = (node.val >= maxSoFar) ? 1 : 0`, update `newMax = max(maxSoFar, node.val)`, recurse left and right.
- **Complexity**: Time: $O(N)$, Space: $O(H)$.

---

## Running Unit Tests

```bash
dotnet test Day21-Distributed-Cache-Architecture/tests/Day21.DistributedCache.Tests/Day21.DistributedCache.Tests.csproj
```
