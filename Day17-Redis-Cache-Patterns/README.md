# Day 17: Introduction to Redis & Cache Patterns

## Overview
Architecting scalable enterprise caching tiers with Redis and .NET. This module covers essential cache patterns (Cache-Aside, Write-Through, Write-Behind, Refresh-Ahead), cache stampede / thundering herd mitigation (per-key mutex locks vs probabilistic early expiration via XFetch), and binary tree recursion algorithms: Invert Binary Tree (LeetCode #226) and Maximum Depth of Binary Tree (LeetCode #104).

---

## Key Architectural Concepts

### 1. Enterprise Caching Patterns Taxonomy

- **Cache-Aside (Lazy Loading)**: The application checks the cache. On miss, it queries the database, writes the result to cache with a TTL, and returns it. Best for read-heavy workloads with non-critical stale reads.
- **Write-Through**: Application writes data to the cache, which synchronously updates the backing database within the same operation. Guarantees strong cache-store consistency at the cost of higher write latency.
- **Write-Behind (Write-Back)**: Writes are immediately committed to cache and enqueued into an asynchronous background channel. A background worker periodically flushes batched writes to the database. Delivers ultra-high write throughput, but risks data loss if the cache node crashes before flushing.
- **Refresh-Ahead**: The caching tier proactively re-computes hot keys before their TTL expires based on access patterns or background schedules.

### 2. Cache Stampede (Thundering Herd) Protection
When a heavily accessed key expires (e.g., 5,000 requests/sec), thousands of concurrent threads experience a simultaneous cache miss. They all execute the expensive database query at once, causing CPU spikes and database outages.
- **Solution 1: Distributed Mutex Lock (Single-Flight)**: The first thread acquires an exclusive lock (`SET lock:key token NX PX 5000`) and executes the database query. All other threads await the lock or read the updated cache value once populated.
- **Solution 2: Probabilistic Early Expiration (XFetch Algorithm)**: Re-fetches the key before expiration based on computing duration and access probability:
  $$\Delta \cdot \beta \cdot (-\ln(\text{rand})) > \text{timeRemaining}$$
  As the key nears expiration, the mathematical probability of a single background worker refreshing the key approaches 1.0, guaranteeing the key never officially expires while under active load.

---

## LeetCode Problems Solved

### LeetCode #226: Invert Binary Tree
- **Algorithm**: Recursive Post-Order / Pre-Order DFS swapping `left` and `right` subtrees, or iterative BFS level-order queue swap.
- **Complexity**: Time: $O(N)$, Space: $O(H)$ recursive stack, $O(W)$ iterative queue.

### LeetCode #104: Maximum Depth of Binary Tree
- **Algorithm**: Recursive DFS $\max(\text{depth}(left), \text{depth}(right)) + 1$, or iterative BFS counting level traversals.
- **Complexity**: Time: $O(N)$, Space: $O(H)$.
