# Day 21: Senior / Staff-Level Interview Questions & Deep Answers

### Q1: What is the fatal flaw of traditional modular hashing (`key % N`) in distributed caching, and how does Consistent Hashing solve it?
- **Short answer**: Modular hashing redistributes almost all keys ($1 - \frac{1}{N} \approx 80\text{--}90\%$) whenever a node is added or removed, causing a massive cache storm; consistent hashing restricts key movement to only $\approx \frac{1}{N}$ keys.
- **Deeper answer**:
  In a cluster of $N = 10$ nodes, a key's destination is given by $\text{hash}(key) \pmod{10}$.
  If one node fails and the cluster drops to $N = 9$, the formula becomes $\text{hash}(key) \pmod 9$. Because modulo arithmetic produces entirely different remainders for different divisors, roughly $90\%$ of keys immediately map to wrong machines. The entire cache tier effectively misses at the same second, overwhelming the primary database.
  Consistent Hashing maps both keys and nodes to a fixed circular ring ($[0, 2^{32}-1]$). When a node is removed, only the keys previously owned by that specific node are reassigned to its immediate clockwise neighbor. All other keys on all other nodes remain completely undisturbed.

---

### Q2: Why are Virtual Nodes (VNodes) essential in a consistent hash ring, and what happens if you omit them?
- **Short answer**: Without virtual nodes, physical servers end up clustered non-uniformly on the ring, causing severe load imbalance (hotspots); virtual nodes distribute physical capacity evenly across hundreds of points.
- **Deeper answer**:
  If 5 physical machines are hashed once onto a circle, the distance between adjacent nodes varies wildly by chance. One node might be responsible for $60\%$ of the ring arc, while another owns only $5\%$.
  Furthermore, if a node fails, its entire load cascades onto a single neighbor, creating a domino effect of server crashes.
  By assigning 100–200 virtual nodes per physical machine (e.g., `server1#vn_0` ... `server1#vn_199`), each server owns hundreds of small arcs dispersed throughout the ring.
  When a node fails, its virtual nodes are scattered across the ring, distributing its load evenly across all surviving machines.

---

### Q3: What is the "Hot Key" problem in distributed caching, and how do you mitigate it when a key receives 1,000,000 requests/sec?
- **Short answer**: Consistent hashing routes all requests for a single key to the same shard node, which saturates its CPU and NIC; mitigation requires L1 local in-process memory caching and suffix scattering.
- **Deeper answer**:
  Even in a 1,000-node cluster, a single viral key (e.g. world cup score, celebrity post) hashes to exactly one shard. That shard node crashes from network interface card (NIC) saturation.
  **Mitigations**:
  1. **L1 In-Process Caching**: The client application stores the hot key in process RAM (`IMemoryCache`) with a 2-second TTL. 99.9% of reads are served in nanoseconds without leaving the web server.
  2. **Read Suffix Scattering**: Replicate the key across $M$ suffixes: `key:#0`, `key:#1`, `key:#2`, `key:#3`. The consistent hash ring maps each suffix to a different shard. Readers pick a random suffix, distributing the 1,000,000 req/sec evenly across $M$ physical machines.
  3. **Client-Side Caching (Redis 6+)**: Redis invalidates local client copies over a sideband connection (`CLIENT TRACKING on`), guaranteeing sub-millisecond reads with immediate invalidation.

---

### Q4: How does Suffix Scattering work for hot keys, and what is its trade-off?
- **Short answer**: Replicating a key across $M$ suffixes scales read capacity by $M\times$, but increases write overhead ($M$ writes) and risks transient consistency windows between copies.
- **Deeper answer**:
  When storing a hot key:
  - The writer computes $M$ keys: `product:101:#0`, `product:101:#1`, ... `product:101:#M-1`.
  - The writer updates all $M$ keys concurrently across the cluster.
  - Readers randomly choose a suffix: `product:101:#{rand(0, M-1)}`.
  - **Trade-offs**:
    - **Write Amplification**: Every update requires $M$ separate network operations.
    - **Consistency Delays**: If one shard update lags or fails, some readers may observe stale data until all replicas sync.
    - Suffix scattering should therefore only be applied selectively to verified hot keys, not the entire keyspace.

---

### Q5: How do you maintain cache coherency between an L1 in-process memory cache and an L2 distributed cluster cache?
- **Short answer**: Use very short L1 TTLs (1–3 seconds), pub/sub cache invalidation broadcasts, or Redis 6+ Client-Side Caching.
- **Deeper answer**:
  Multi-tier caching creates a coherence challenge: when Pod A updates a product price in the L2 Redis cluster, Pod B might still serve the old price from its local L1 memory.
  **Architectural patterns**:
  1. **Short TTL + Eventual Consistency**: Setting an L1 TTL of 1–2 seconds bounds stale reads to 2 seconds, which is acceptable for catalog reads.
  2. **Pub/Sub Invalidation**: When any service mutates a record, it publishes an invalidation event (`PUBLISH cache:invalidations key`). All subscriber pods receive the message and evict the key from their local `IMemoryCache`.
  3. **Redis RESP3 Client Tracking**: The Redis server tracks which keys the client has read and pushes an out-of-band invalidation message over the TCP connection when the key is mutated by any client.

---

### Q6: Compare Single-Flight Mutex vs Probabilistic Early Expiration (XFetch) for preventing cache stampedes.
- **Short answer**: Single-Flight locks concurrent requests on a cache miss so only one worker queries the database; XFetch probabilistically pre-refreshes hot keys before they expire so misses never occur.
- **Deeper answer**:
  - **Single-Flight (SingleFlight / Mutex)**: When a key expires, 1,000 concurrent threads miss. The first thread acquires a lock and executes the SQL query. The remaining 999 threads await the result and receive the computed value. Downside: readers experience latency equal to the SQL query time.
  - **XFetch Algorithm**: Readers evaluate a probabilistic threshold: $\Delta \cdot \beta \cdot (-\ln(\text{rand})) > \text{timeRemaining}$. As expiration approaches, a single background task refreshes the value *while* returning the existing cached value to readers. Readers experience zero latency spikes and the key never expires under load.

---

### Q7: How does a Consistent Hash Ring handle a dead or failing cache node?
- **Short answer**: Clockwise handoff naturally routes traffic to the dead node's clockwise successor; health checks formally remove the node and rebalance its virtual arcs.
- **Deeper answer**:
  When a node crashes:
  1. **Immediate Handoff**: Any key previously mapped to the dead node's position will simply fall through to the next available virtual node clockwise along the ring.
  2. **Health Check Pruning**: A cluster coordinator (or client-side heartbeat) detects the node outage, calls `ring.RemoveNode(deadNode)`, and removes all associated virtual node hashes from the sorted ring list via binary search.
  3. **Re-warming**: Keys falling on the successor nodes will experience cold misses and re-populate from the database without impacting keys on unaffected nodes.

---

### Q8: What hash functions should be chosen for consistent hashing rings?
- **Short answer**: Cryptographic hashes (MD5, SHA-256) or non-cryptographic hashes like MurmurHash3 and xxHash; never standard `string.GetHashCode()`.
- **Deeper answer**:
  In .NET, `string.GetHashCode()` uses randomized hash seeds per process instance for security against HashDoS attacks. This means two different application pods running the same code would produce different hash values for the same key, breaking ring consistency across the fleet!
  Furthermore, `string.GetHashCode()` lacks uniform distribution properties.
  Algorithms like **MurmurHash3**, **xxHash**, or **MD5** (used by Ketama) produce consistent, deterministic 32-bit or 128-bit integers with near-perfect uniform distribution across the entire ring circumference.

---

### Q9: In LeetCode #199 (Binary Tree Right Side View), why is right-first DFS generally preferred over BFS?
- **Short answer**: Right-first DFS visits rightmost nodes first and uses $O(H)$ stack space without heap allocations; BFS uses $O(W)$ queue memory where tree width can reach $\frac{N}{2}$.
- **Deeper answer**:
  ```csharp
  void Dfs(TreeNode node, int depth) {
      if (node == null) return;
      if (depth == result.Count) result.Add(node.val);
      Dfs(node.right, depth + 1);
      Dfs(node.left, depth + 1);
  }
  ```
  By recursing `node.right` before `node.left`, the very first node encountered at any depth level is guaranteed to be the rightmost visible node.
  - DFS space: Bounded by tree height $O(H)$ ($O(\log N)$ on balanced trees). Uses thread stack frames without heap queue node allocation.
  - BFS space: Bounded by tree width $O(W)$. A complete tree with 1,000,000 nodes stores up to 500,000 node references in the queue at the leaf level.

---

### Q10: How does LeetCode #1448 (Count Good Nodes in Binary Tree) maintain the ancestor maximum invariant during pre-order DFS?
- **Short answer**: It passes the maximum value seen so far down the recursion tree; if the current node's value is greater than or equal to that maximum, it is counted and the maximum is updated for its subtrees.
- **Deeper answer**:
  ```csharp
  int Dfs(TreeNode node, int maxSoFar) {
      if (node == null) return 0;
      int isGood = node.val >= maxSoFar ? 1 : 0;
      int newMax = Math.Max(maxSoFar, node.val);
      return isGood + Dfs(node.left, newMax) + Dfs(node.right, newMax);
  }
  ```
  Because the tree is traversed pre-order (root before children), each node compares its value against the absolute maximum of its ancestors along its unique path from the root.
  Left and right branches receive independent copies of `newMax` on the call stack, guaranteeing that sibling subtrees never interfere with each other's path histories in $O(N)$ time and $O(H)$ space.
