# Day 18: Redis Data Structures & Memory Eviction Policies

## Overview
Comprehensive deep-dive into advanced Redis data structures and memory eviction architectures in high-scale distributed systems. This module covers production patterns across Redis Strings, Hashes, Sets, Sorted Sets (ZSET), Bitmaps, and HyperLogLogs, paired with in-depth analysis of Redis memory management and eviction policies (`allkeys-lru`, `volatile-lru`, `allkeys-lfu`, `volatile-lfu`, `volatile-ttl`, and `noeviction`). Additionally, we solve tree metric algorithms: Diameter of Binary Tree (LeetCode #543) and Balanced Binary Tree (LeetCode #110) in optimal $O(N)$ time.

---

## Architectural Breakdown

### 1. Redis Core Data Structures & Production Use Cases

| Data Structure | Internal Representation | Primary Use Case | Time Complexity |
|---|---|---|---|
| **Strings** | SDS (Simple Dynamic String), `raw` or `embstr` | Distributed Atomic Counters (`INCRBY`), Session Caches | $O(1)$ |
| **Hashes** | `ziplist`/`listpack` or `hashtable` (dict) | User Profiles, Discrete Field Updates without Full Serialization | $O(1)$ field access |
| **Sets** | `intset` or `hashtable` | Unique Tags, Mutual Friends (`SINTER`), Deduplication | $O(1)$ add/check, $O(N \cdot M)$ intersect |
| **Sorted Sets (ZSET)** | `ziplist`/`listpack` or `skiplist` + `dict` | Real-time Leaderboards, Sliding-Window Rate Limiters | $O(\log N)$ add/rank, $O(\log N + M)$ range |
| **Bitmaps** | String offsets manipulated at bit level | Daily Active Users (DAU), Feature Flag Presence, Compact Flags | $O(1)$ get/set bit, $O(N)$ `BITCOUNT` |
| **HyperLogLog** | 16,384 6-bit registers (Dense/Sparse SDS) | Unique Visitor Cardinality Estimation with fixed 12 KB RAM | $O(1)$ add, $O(1)$ count ($\approx 0.81\%$ standard error) |

---

### 2. Redis Memory Eviction Policies Under Memory Pressure

When `used_memory` reaches `maxmemory`, Redis executes its configured eviction policy on write operations:

1. **`noeviction`**:
   - Redis refuses writes and returns an `OOM command not allowed when used memory > 'maxmemory'` error.
   - Read and delete commands continue to function normally. Essential for systems where Redis functions as a persistent primary database.
2. **`allkeys-lru`**:
   - Evicts least recently used keys across the entire keyspace using Redis's 24-bit approximated LRU clock sample pool.
   - Ideal when key access follows a Power-Law (80/20 rule) distribution.
3. **`volatile-lru`**:
   - Evicts least recently used keys only among keys with an active TTL (`expire` set).
4. **`allkeys-lfu`**:
   - Evicts least frequently used keys across the entire keyspace using an 8-bit Morris logarithmic frequency counter and time decay.
   - Prevents cache pollution where a newly added key is evicted despite high long-term utility.
5. **`volatile-lfu`**:
   - Evicts least frequently used keys only among keys configured with a TTL.
6. **`volatile-ttl`**:
   - Evaluates a random sample of keys with TTL and evicts the key with the shortest remaining time-to-live.

---

### 3. Tree Recursion Algorithms Solved

#### LeetCode #543: Diameter of Binary Tree
- **Problem**: Find the length of the longest path between any two nodes in a tree (counted in edges).
- **Algorithm**: Bottom-up post-order DFS. At each node, compute left and right subtree heights, update global diameter `max(diameter, leftHeight + rightHeight)`, and return `1 + max(leftHeight, rightHeight)`.
- **Complexity**: Time: $O(N)$, Space: $O(H)$ recursion stack space.

#### LeetCode #110: Balanced Binary Tree
- **Problem**: Determine if a binary tree is height-balanced (subtree heights differ by no more than 1 for all nodes).
- **Algorithm**: Bottom-up DFS returning subtree height. If any subtree is unbalanced, it immediately bubbles up `-1` to short-circuit redundant traversals.
- **Complexity**: Time: $O(N)$, Space: $O(H)$.

---

## Running Unit Tests

```bash
dotnet test Day18-Redis-DataStructures-Eviction/tests/Day18.RedisDataStructures.Tests/Day18.RedisDataStructures.Tests.csproj
```
