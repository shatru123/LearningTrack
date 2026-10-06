# Day 17: Senior / Staff-Level Interview Questions & Deep Answers

### Q1: Why should `ConnectionMultiplexer` in `StackExchange.Redis` be registered as a Singleton?
- **Short answer**: It multiplexes concurrent operations over a shared persistent TCP connection and is expensive to construct; creating per-request instances exhausts socket descriptors and ThreadPool workers.
- **Deeper answer**:
  `ConnectionMultiplexer` does not use a traditional connection pool where each thread takes a socket. Instead, it pipelines multiple commands over a single full-duplex TCP socket, tagging each response with an internal message ID.
  Constructing a multiplexer involves DNS resolution, SSL handshakes, node topology discovery, and background heartbeat timers. If created transiently per HTTP request, the operating system runs out of ephemeral TCP ports (TIME_WAIT socket exhaustion) and the ThreadPool becomes saturated handling connection handshakes.

---

### Q2: What causes Redis timeouts in .NET applications when Redis CPU utilization is low (<10%)?
- **Short answer**: Client-side ThreadPool starvation or synchronous blocking on async calls, which delays the completion of Redis response callbacks.
- **Deeper answer**:
  `StackExchange.Redis` relies on .NET I/O Completion Ports (IOCP) to dispatch completed socket responses. When client application code calls `.Result` or `.Wait()` on asynchronous tasks, worker threads become blocked.
  When the thread pool experiences a burst, the .NET thread pool thread-injection heuristic only adds ~1–2 new worker threads every 500ms. If no threads are available to process incoming Redis socket packets, `StackExchange.Redis` triggers a timeout exception:
  `Timeout performing GET (5000ms), inst: 1, mgr: 10, err: none, qs: 50, in: 50, in-pipe: 0, aw: True`.
  The `qs` (queue size) and `in` parameters reveal that the response arrived from Redis over the wire, but sat queued in the client buffer waiting for an available ThreadPool thread.

---

### Q3: What is the "Cache Stampede" (Thundering Herd) problem and how do you mitigate it?
- **Short answer**: When a heavily accessed key expires, thousands of concurrent requests miss simultaneously, inundating the database with duplicate expensive queries.
- **Deeper answer**:
  In a system processing 10,000 requests/sec, if a product catalog cache entry expires, thousands of threads immediately query the database for the exact same record.
  **Mitigations**:
  1. **Mutex Locking (Single-Flight Pattern)**: The first thread acquires an in-process `SemaphoreSlim` or Redis lock (`SET lock:key token NX PX 5000`). Only the lock winner queries the DB; others wait and read the repopulated cache.
  2. **Probabilistic Early Expiration (XFetch)**: Background workers evaluate `delta * beta * -ln(rand) > expiry - now` to asynchronously refresh keys before they officially expire.
  3. **Stale-While-Revalidate**: Serve slightly stale data from cache while triggering a background task to refresh the value.

---

### Q4: How does the XFetch algorithm prevent cache stampedes without manual background timers?
- **Short answer**: It uses a randomized mathematical threshold to probabilistically trigger a background cache refresh as the key nears expiration, with probability increasing as time runs out.
- **Deeper answer**:
  The formula is:
  $$\Delta \cdot \beta \cdot (-\ln(\text{rand})) > \text{timeRemaining}$$
  - $\Delta$ is the computation time taken to generate the value.
  - $\beta > 0$ is an aggressiveness tuning multiplier.
  - $\text{rand} \in (0, 1]$ is a uniform random number.
  When a key is accessed, if the formula evaluates to true, the reader triggers an immediate asynchronous refresh while returning the current cached value. As `timeRemaining` shrinks toward 0, the probability of at least one request triggering a refresh approaches 1.0, ensuring the key is renewed seamlessly without a cache miss.

---

### Q5: Compare Cache-Aside vs Write-Through vs Write-Behind caching strategies.
- **Short answer**:
  - **Cache-Aside**: Application manages reads from cache and writes directly to database; lazy loading on miss.
  - **Write-Through**: Application writes to cache, which synchronously persists to database; strong consistency.
  - **Write-Behind**: Application writes to cache; writes to database are batched and flushed asynchronously; maximum write speed, risk of data loss.
- **Deeper answer**:
  - *Cache-Aside*: Most common, decoupled, resilient against cache outages (falls back to DB), but suffers cold-start misses.
  - *Write-Through*: High write latency because each write incurs both cache and DB network overhead, but eliminates cache misses on newly written entities.
  - *Write-Behind*: Transforms individual DB `INSERT` statements into bulk SQL statements (`INSERT INTO ... VALUES (...)`), reducing DB I/O by 90%. However, if the cache server crashes before flushing, queued writes are lost.

---

### Q6: What is Cache Penetration and how do you protect against it?
- **Short answer**: When clients repeatedly query for keys that do not exist in either the cache or the database (e.g., malicious requests for non-existent IDs), bypassing the cache entirely.
- **Deeper answer**:
  Because the requested entity does not exist in the database, Cache-Aside never writes a cache entry. Every subsequent request hits the database directly.
  **Mitigations**:
  1. **Caching Null Values**: Store a sentinel `NULL` object in cache with a short TTL (e.g., 30–60 seconds). Subsequent requests hit the cache and receive a fast 404.
  2. **Bloom Filters**: Place a Bloom Filter in front of the cache containing all valid primary keys. If the Bloom filter returns `false`, reject the request immediately without touching cache or database.

---

### Q7: What is Cache Breakdown vs Cache Avalanche?
- **Short answer**:
  - **Cache Breakdown**: A single super-hot key expires, causing a stampede on the database.
  - **Cache Avalanche**: A massive batch of different keys expire at the exact same second, causing global database overload.
- **Deeper answer**:
  - *Cache Breakdown*: Addressed via mutex locking or XFetch early refresh for the hot key.
  - *Cache Avalanche*: Commonly caused by setting uniform TTLs (e.g., `SetAsync(key, val, TimeSpan.FromHours(1))`). When midnight passes or a service restarts, all cached items expire simultaneously.
  **Mitigation for Avalanche**: Add **TTL Jitter** (randomized offset):
  `TimeSpan ttl = TimeSpan.FromHours(1) + TimeSpan.FromSeconds(Random.Shared.Next(-300, 300));`.

---

### Q8: What serialization strategy should be used for Redis in high-performance .NET applications?
- **Short answer**: Binary serializers like MemoryPack, MessagePack, or Protobuf offer 5–10x faster serialization and 50–70% smaller memory footprints compared to standard JSON.
- **Deeper answer**:
  While `System.Text.Json` is readable in `redis-cli`, serializing complex object graphs to UTF-8 strings incurs high string allocations and CPU overhead.
  Binary formats like MessagePack or MemoryPack serialize directly into `byte[]` or `ReadOnlySpan<byte>`, bypassing intermediate string allocations. A smaller payload also cuts network serialization time and saves expensive Redis RAM, which is typically the most expensive infrastructure component.

---

### Q9: In LeetCode #226 (Invert Binary Tree), what is the difference between BFS and DFS traversal?
- **Short answer**: DFS recursively swaps left and right subtrees in post-order or pre-order; BFS iteratively swaps children level-by-level using a FIFO queue.
- **Deeper answer**:
  Both visit every node exactly once ($O(N)$ time).
  - DFS recursion space complexity is bounded by tree height $O(H)$. For balanced trees, $H = \log N$; for degenerate skewed trees, $H = N$.
  - BFS queue space complexity is bounded by tree width $O(W)$. For a complete binary tree, the maximum width is at the leaf level, requiring $O(N / 2) = O(N)$ memory.

---

### Q10: How does LeetCode #104 (Maximum Depth of Binary Tree) handle skewed vs balanced trees?
- **Short answer**: In a balanced tree, call stack depth is $O(\log N)$; in a skewed degenerate tree (resembling a linked list), call stack depth is $O(N)$.
- **Deeper answer**:
  The recursive relation is `1 + Math.Max(MaxDepth(root.left), MaxDepth(root.right))`.
  When a tree is balanced, recursion depth is logarithmically small ($N = 1,000,000 \implies \text{depth } 20$).
  However, if nodes only have right children, the call stack grows to $N$. On large datasets, recursive DFS can trigger a `StackOverflowException`. In production environments with uncontrolled tree depth, an iterative BFS level-order traversal using an explicit heap-allocated `Queue<TreeNode>` is safer because the heap can accommodate millions of node references without exhausting thread stack space.
