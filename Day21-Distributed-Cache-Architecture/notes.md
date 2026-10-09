# Day 21 - Engineering Notes: Consistent Hashing & Distributed Cache Design

## 1. Mathematical Foundations of Consistent Hashing (Karger et al., 1997)

Traditional modular hashing determines node placement via:
$$\text{NodeIndex} = \text{Hash}(key) \pmod N$$
- When $N$ changes to $N+1$, nearly **every** key maps to a new index because $\text{Hash}(key) \pmod N \ne \text{Hash}(key) \pmod{N+1}$.
- In a cluster storing 10,000,000 keys, adding 1 node invalidates up to 9,000,000 cached records simultaneously, causing an instantaneous database outage.

### The Consistent Hashing Ring
Consistent Hashing maps both keys and servers to points on a circle $[0, 2^{32}-1]$:
1. **Smoothness Property**: When a node is added, it only acquires keys from its clockwise predecessor. The fraction of keys reassigned is mathematically bounded at:
   $$E[\text{Reassigned Keys}] = \frac{K}{N+1}$$
2. **Balance via Virtual Nodes (Ketama)**:
   - If physical nodes are simply placed at 1 hash location, random variance causes severe imbalance (one node can receive 60% of all traffic while another gets 5%).
   - By creating $V = 100\text{--}200$ virtual points per physical machine (`host:port#vn_0` to `host:port#vn_199`), the law of large numbers guarantees that each physical machine receives within $\pm 5\%$ of its fair share: $\frac{K}{N}$.

---

## 2. Hot-Key Scaling Patterns in Distributed Systems

A "hot key" is a single resource that experiences an extreme traffic anomaly (e.g. 500,000 requests/sec for a breaking news article). Even in a 50-node cluster, 100% of that traffic routes to a single shard, causing CPU saturation and packet drops.

### Mitigation Strategies:
1. **L1 Local In-Memory Cache (Multi-Tier)**:
   - Keep a tiny, short-lived L1 cache (1–3 seconds TTL) inside the client process memory (`IMemoryCache`).
   - If 10,000 HTTP requests hit the same web server pod in 1 second, only 1 request goes over the network to the Redis shard; 9,999 requests are served directly from RAM in 10 nanoseconds.
2. **Read Suffix Scattering (Key Replication)**:
   - For known hot keys, the publisher writes $M$ copies: `key:#0`, `key:#1`, `key:#2`, `key:#3`.
   - Each copy lands on a different consistent hash shard.
   - Readers pick a random shard index: `key:#{rand(0, 3)}`.
   - Divides network bandwidth and CPU consumption across $M$ distinct machines.
3. **Dynamic Hot-Key Detection (Heavy Hitters)**:
   - Use a streaming **Count-Min Sketch** or Redis LFU tracking to detect keys whose access rate exceeds 1,000 req/sec, automatically promoting them to L1 or applying suffix scattering dynamically.

---

## 3. Tree Traversal Mechanics: DFS Right-First vs BFS Level Order

### LeetCode #199: Binary Tree Right Side View
- **BFS Approach**:
  - Traverses the entire tree with a FIFO queue.
  - At each level, appends the last dequeued element to the result.
  - Memory: $O(W)$ where $W = \frac{N}{2}$ at the leaf level for a complete tree.
- **Right-First DFS Approach (Optimal)**:
  - Recursion order: `Right` child **before** `Left` child.
  - Pass `depth` down the recursion.
  - Invariant: At any depth, the very first node visited is mathematically guaranteed to be the rightmost visible node.
  - If `depth == result.Count`, add `node.val` to result.
  - Memory: $O(H)$ call stack space ($O(\log N)$ balanced). Zero queue object allocations.
