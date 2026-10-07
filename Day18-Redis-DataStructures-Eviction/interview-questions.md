# Day 18: Senior / Staff-Level Interview Questions & Deep Answers

### Q1: How does Redis achieve $O(1)$ operations on Strings and prevent buffer overflows internally compared to standard C strings?
- **Short answer**: Redis uses SDS (Simple Dynamic String) headers containing length, capacity, and flags, providing constant-time length lookups, automatic buffer reallocation, and binary safety.
- **Deeper answer**:
  In standard C, strings are null-terminated (`\0`), meaning `strlen()` runs in $O(N)$ time and functions like `strcat()` are prone to buffer overflows.
  Redis wraps strings in SDS structures (`sdshdr8`, `sdshdr16`, `sdshdr32`):
  - **`len`**: Current byte length, allowing $O(1)$ length queries without scanning.
  - **`alloc`**: Total buffer capacity, allowing Redis to verify free space before appending and dynamically reallocate buffer memory (buffer preallocation doubles size up to 1 MB).
  - **Binary safety**: Because length is determined by `len` rather than `\0`, Redis can safely store arbitrary binary payloads like JPEG images, Gzip streams, or Protobuf blobs containing null bytes.
  - **`embstr` optimization**: For strings under 44 bytes, Redis allocates the `redisObject` metadata and the SDS payload within a single contiguous `malloc` block, eliminating a second memory allocation and maximizing CPU L1/L2 cache locality.

---

### Q2: Why does Redis use SkipLists instead of Red-Black Trees or AVL trees for Sorted Sets (ZSET)?
- **Short answer**: SkipLists provide simpler range query traversals, easier lock-free / low-overhead concurrent implementations, and tuneable memory usage without complex tree rebalancing rotations.
- **Deeper answer**:
  Redis creator Salvatore Sanfilippo chose SkipLists over balanced BSTs for three critical engineering reasons:
  1. **Range Scans (`ZRANGEBYSCORE`)**: In a SkipList, finding the lower bound takes $O(\log N)$, after which scanning subsequent items simply traverses the level-0 linked list pointers ($O(1)$ per subsequent element). In a BST, in-order traversal requires stack allocation or parent pointer reversals.
  2. **Rebalancing Overhead**: Red-Black trees require complex rotations and color flips upon insertion or deletion, which can cascade across multiple levels. A SkipList only modifies local pointer references of adjacent nodes.
  3. **Memory Tunability**: The probability $p$ of a node being elevated to the next level is parameterized. With $p = 0.25$, nodes average only 1.33 pointers each, consuming less memory than a 3-pointer binary tree node (left, right, parent + color bit).

---

### Q3: How does HyperLogLog estimate unique cardinalities up to billions of elements using only 12 KB of memory?
- **Short answer**: By hashing elements into 16,384 registers and recording the maximum number of leading zeros in the hash values, cardinality is probabilistically estimated using the harmonic mean with ~0.81% standard error.
- **Deeper answer**:
  HyperLogLog exploits the mathematical property that in a uniformly distributed binary stream, a sequence of $k$ consecutive leading zeros occurs with probability $2^{-k}$.
  1. Incoming elements are hashed with 64-bit MurmurHash.
  2. The first 14 bits determine the register index ($2^{14} = 16,384$ registers).
  3. The remaining 50 bits are analyzed for leading zeros ($\rho$).
  4. Each register stores $\max(\text{existing\_val}, \rho)$ in a 6-bit integer ($16384 \times 6 \text{ bits} / 8 = 12,288 \text{ bytes} = 12 \text{ KB}$).
  5. Redis calculates the harmonic mean across all registers to prevent statistical skew from single high-value outliers:
     $$E = \alpha_m \cdot m^2 \cdot \left(\sum_{j=1}^{m} 2^{-M[j]}\right)^{-1}$$
  6. For low cardinalities ($E < 2.5 \cdot m$), Redis applies Linear Counting bias correction based on the number of zero registers.

---

### Q4: How does Redis approximate LRU eviction without the prohibitive memory overhead of a true doubly linked list?
- **Short answer**: Instead of maintaining a global linked list of all keys (which would waste 24+ bytes of pointer RAM per key), Redis stores a 24-bit clock in each object header and samples a configurable pool of keys to evict the oldest.
- **Deeper answer**:
  In a database with 50 million keys, maintaining an exact LRU doubly linked list would require 24 bytes per key in pointer overhead (1.2 GB of RAM solely for eviction pointers).
  **Redis Approximated LRU**:
  - Each `redisObject` header contains a 24-bit `lru` field recording the system clock timestamp (in seconds, wrapping every 194 days).
  - When `maxmemory` is reached, Redis randomly selects $K$ keys (default `maxmemory-samples 5`, configurable to 10).
  - Redis populates an internal 16-key **Eviction Pool** sorted by idle time (`current_timestamp - lru_timestamp`).
  - The key with the highest idle time is evicted.
  - Benchmarks demonstrate that with `maxmemory-samples 10`, the approximate LRU curve is virtually indistinguishable from a strict mathematical LRU list while consuming zero pointer memory.

---

### Q5: Explain how Redis implements LFU (Least Frequently Used) using an 8-bit Morris counter and time decay.
- **Short answer**: The 24-bit `lru` field is partitioned into a 16-bit decay timestamp and an 8-bit logarithmic Morris counter that increments probabilistically and decays per elapsed minute.
- **Deeper answer**:
  LRU suffers from cache pollution: a one-time batch scan can flush hot items that have been accessed thousands of times. LFU solves this.
  Redis splits the 24-bit field into:
  1. **16-bit Last Decrement Time (`ldt`)**: Unix timestamp in minutes modulo $2^{16}$.
  2. **8-bit Logistic Counter (`log_cnt`)**: Initialized to 5 to protect newly created keys.
  - **Decay Step**: When accessed, if $(now - ldt) > 0$, the counter is decremented by $(now - ldt) \times \text{lfu-decay-time}$.
  - **Probabilistic Increment Step**: An 8-bit counter can only count to 255. Redis uses Morris logarithmic counting where the probability of increment is:
    $$P = \frac{1}{\text{counter} \cdot \text{lfu\_log\_factor} + 1}$$
  With default factor 10, a counter value of 255 represents approximately 1,000,000 accesses, enabling wide dynamic range within a single byte.

---

### Q6: Compare `volatile-*` vs `allkeys-*` eviction policies. Under what architectural circumstances can `volatile-lru` or `volatile-ttl` cause an Out-Of-Memory (OOM) error?
- **Short answer**: `volatile-*` policies only evict keys configured with an explicit TTL; if memory is saturated by persistent keys without TTL, Redis cannot evict anything and throws an OOM exception.
- **Deeper answer**:
  - `allkeys-lru` / `allkeys-lfu`: Treats every key as eligible for eviction. Recommended for pure caching tiers.
  - `volatile-lru` / `volatile-lfu` / `volatile-ttl`: Restricts eviction candidates to keys with an expiration (`EXPIRE`, `SETEX`).
  **The OOM Failure Scenario**:
  If an application uses Redis both for caching (with TTL) and for persistent state (e.g., user sessions or background job queues without TTL), and persistent data grows until `maxmemory` is reached while all volatile keys have already been evicted, Redis cannot free any memory.
  Any subsequent write command returns:
  `(error) OOM command not allowed when used memory > 'maxmemory'`.
  **Rule of Thumb**: Use `allkeys-lru` when Redis is a dedicated cache; use `volatile-lru` only when persistent keys are guaranteed to stay strictly within a known memory budget.

---

### Q7: How do you implement a scalable Sliding Window Rate Limiter using Redis Sorted Sets (ZSET), and what is its time and space complexity?
- **Short answer**: Use a ZSET where both the score and member represent unix millisecond timestamps; prune expired elements with `ZREMRANGEBYSCORE`, check `ZCARD`, and add the current request with `ZADD`.
- **Deeper answer**:
  ```redis
  MULTI
  ZREMRANGEBYSCORE ratelimit:user_123 -inf (now - window_size)
  ZCARD ratelimit:user_123
  ZADD ratelimit:user_123 now now:uuid
  EXPIRE ratelimit:user_123 window_size
  EXEC
  ```
  - **Precision**: Provides exact sliding window protection with zero boundary reset spikes (unlike fixed-window counters).
  - **Time Complexity**: $O(\log N + M)$ where $N$ is the number of requests in the window and $M$ is the number of removed old entries.
  - **Space Complexity**: $O(R)$ where $R$ is the number of requests in the active window. For ultra-high volume APIs (millions of req/sec), ZSET memory can grow high, making a sliding-window counter or token bucket preferable.

---

### Q8: When would you use Redis Hashes over serializing a JSON string into a Redis String key?
- **Short answer**: Hashes are superior when you need to read or mutate individual fields (`HGET`, `HSET`, `HINCRBY`) without deserializing and transferring the entire payload over the network.
- **Deeper answer**:
  1. **Bandwidth & CPU**: Mutating a user's `last_active` timestamp in a 20 KB user profile JSON string requires downloading 20 KB, deserializing in C#, updating the property, re-serializing, and uploading 20 KB. With Redis Hashes: `HSET user:1 last_active 1728392000` transfers $< 50$ bytes.
  2. **Race Conditions**: Two microservices updating different fields of a JSON string concurrently will overwrite each other's changes (lost updates). With Hashes, field updates are atomic and independent.
  3. **Memory Optimization**: Small Hashes are stored as contiguous ListPacks, which can be more compact in RAM than JSON string keys.

---

### Q9: In LeetCode #543 (Diameter of Binary Tree), why does the longest path not necessarily pass through the root node, and how does the bottom-up algorithm handle this?
- **Short answer**: The longest path can exist entirely within a dense, deep subtree; bottom-up recursion computes and updates the global maximum path at every sub-root while returning height upwards.
- **Deeper answer**:
  Consider a tree where the root has a right child with depth 1, but its left child is the root of two very deep subtrees of depth 10 each. The diameter of the left subtree is $10 + 10 = 20$ edges, whereas any path passing through the root node has length at most $10 + 1 = 11$ edges.
  **Bottom-Up Handling**:
  The recursive post-order helper returns the **height** of the current subtree: $1 + \max(h_{left}, h_{right})$.
  Concurrently, at each node, it computes the diameter of the path pivoting at that node: $h_{left} + h_{right}$, updating a global tracker `maxDiameter = max(maxDiameter, left + right)`.
  This guarantees that every possible path apex is evaluated in $O(N)$ time.

---

### Q10: How does LeetCode #110 (Balanced Binary Tree) optimize from $O(N^2)$ to $O(N)$ time complexity?
- **Short answer**: Top-down recursion re-computes heights repeatedly for subtrees ($O(N^2)$ worst-case); bottom-up checks balance during height calculation and immediately propagates `-1` to short-circuit.
- **Deeper answer**:
  - **Top-Down ($O(N^2)$)**:
    `IsBalanced(node)` calls `Height(node.left)` and `Height(node.right)`, then recursively calls `IsBalanced(node.left)` and `IsBalanced(node.right)`. On a degenerate linked-list tree, each level recounts all children, resulting in $\sum_{i=1}^N i = O(N^2)$ operations.
  - **Bottom-Up ($O(N)$)**:
    In post-order DFS, the function returns the subtree height if balanced, or `-1` if unbalanced.
    As soon as `CheckHeight(left)` returns `-1` or `|left - right| > 1`, the function immediately returns `-1` up the call stack without traversing the rest of the tree. Each node is visited at most once, achieving strict $O(N)$ time and $O(H)$ stack space.
