# Day 18 - Engineering Notes: Redis Memory Internals & Eviction Architectures

## 1. Redis Internal Data Structure Implementations

### SDS (Simple Dynamic String)
Redis strings are backed by SDS rather than raw C null-terminated strings (`char*`):
- **Structure**: Header contains `len` (current length), `alloc` (allocated capacity), `flags` (header type: `sdshdr8`, `sdshdr16`, `sdshdr32`), and `buf[]`.
- **Advantages**:
  1. **$O(1)$ Length Lookups**: Returns `len` in constant time without scanning for `\0`.
  2. **Binary Safe**: Can store arbitrary binary data (images, Protobuf, gzipped bytes) containing embedded null characters (`\0`).
  3. **Zero Buffer Overflows**: SDS automatically reallocates memory via buffer pre-allocation (`alloc` doubled up to 1 MB).
  4. **Memory Optimization (`embstr` vs `raw`)**: For strings $\le 44$ bytes, Redis allocates the `redisObject` header and SDS buffer in a single contiguous memory chunk (`embstr`), reducing memory fragmentation and CPU cache misses.

### Redis Hashes: ListPack / ZipList vs Hash Table (Dict)
- When a hash has few fields ($\le 512$) and small values ($\le 64$ bytes), Redis stores it as an encoded **ListPack** (or legacy **ZipList**): a single contiguous block of memory without pointers.
- Once limits (`hash-max-ziplist-entries` or `hash-max-ziplist-value`) are exceeded, Redis automatically converts it to a standard **Dict** (two hash tables supporting progressive re-hashing).
- Memory savings: ListPack eliminates 64-bit forward/backward pointers, reducing RAM consumption by up to 80% for small objects.

### Sorted Sets (ZSET): SkipList vs Red-Black Trees
Redis implements ZSETs using a combination of a **SkipList** and a **Hash Table**:
- **Why SkipList over Red-Black Tree?**:
  1. **Range Queries**: SkipLists support range scans (`ZRANGEBYSCORE`) with trivial pointer traversals along level 0 forward pointers.
  2. **Simpler Implementation & Concurrency**: SkipLists require no complex tree rotations or node rebalancing.
  3. **Memory Tunability**: Node level distribution follows a geometric distribution with probability $p = 0.25$, yielding an average of only 1.33 pointers per node.

### HyperLogLog: 12 KB Fixed-Memory Cardinality Estimation
- **Problem**: Counting millions of unique IP addresses with a Redis Set of 100,000,000 strings requires $\approx 4\text{ GB}$ of RAM.
- **HyperLogLog Solution**: Requires exactly **12 KB** of memory regardless of whether counting 1,000 or 1,000,000,000 unique elements.
- **Algorithm**:
  1. Hash incoming element with a 64-bit uniform hash function (MurmurHash64A).
  2. Use first 14 bits as the register index ($2^{14} = 16,384$ registers).
  3. Count leading zeros in the remaining 50 bits ($\rho$).
  4. Store $\max(\rho, \text{current\_register\_value})$ in an 6-bit register ($2^6 = 64$ max value).
  5. Estimate cardinality via the harmonic mean of all 16,384 registers:
     $$E = \alpha_m \cdot m^2 \cdot \left(\sum_{j=1}^{m} 2^{-M[j]}\right)^{-1}$$
  6. Standard error is bounded at $\frac{1.04}{\sqrt{m}} = \frac{1.04}{\sqrt{16384}} \approx 0.81\%$.

---

## 2. Memory Eviction Algorithms Under the Hood

### Approximate LRU (Sample Pool Algorithm)
- Redis does **NOT** maintain a global doubly-linked list of all keys. In a database with 50,000,000 keys, maintaining linked list pointers would consume over **1.2 GB** of RAM purely for LRU metadata.
- **Redis Approximation**:
  1. Each `redisObject` contains a 24-bit field storing the current LRU clock timestamp.
  2. When memory exceeds `maxmemory`, Redis randomly samples $K$ keys (default `maxmemory-samples 5`, or 10 in high-precision mode).
  3. Keys are inserted into an **Eviction Pool** of size 16, kept sorted by idle time (`idle = current_clock - lru_clock`).
  4. The key with the highest idle time is evicted.
  5. With `maxmemory-samples 10`, Redis LRU approximation matches true LRU behavior within 99.5% accuracy.

### LFU (Least Frequently Used) with Morris Counter
In LFU mode, the 24-bit `lru` field is split into:
- **16 bits**: Last Decrement Time (`ldt`) in minutes modulo $2^{16}$.
- **8 bits**: Logistic Counter (`log_cnt`), initialized to 5.
- **Decay Phase**: If elapsed minutes $> 0$, decrement counter by `minutes_passed * lfu_decay_time`.
- **Increment Phase**: Increment is probabilistic using Morris logarithmic counting:
  $$P = \frac{1}{\text{counter} \cdot \text{lfu\_log\_factor} + 1}$$
  This allows an 8-bit counter (0–255) to represent frequencies up to 1,000,000+ requests.

---

## 3. High-Throughput Sliding Window Rate Limiting Pattern

```csharp
// Distributed sliding window rate limiter via Redis ZSET
public async Task<bool> IsRateLimitedAsync(IDatabase db, string clientKey, int maxRequests, TimeSpan window)
{
    var key = $"ratelimit:{clientKey}";
    long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    long windowStartMs = nowMs - (long)window.TotalMilliseconds;
    string memberId = Guid.NewGuid().ToString("N");

    var tran = db.CreateTransaction();
    // 1. Remove expired timestamps outside the rolling window
    _ = tran.SortedSetRemoveRangeByScoreAsync(key, double.NegativeInfinity, windowStartMs);
    // 2. Count requests remaining in current window
    var countTask = tran.SortedSetLengthAsync(key);
    // 3. Add current request timestamp
    _ = tran.SortedSetAddAsync(key, memberId, nowMs);
    // 4. Reset TTL so inactive keys expire
    _ = tran.KeyExpireAsync(key, window);

    bool committed = await tran.ExecuteAsync();
    if (!committed) return true;

    long requestCount = await countTask;
    return requestCount >= maxRequests;
}
```

---

## 4. DSA Tree Algorithms: Top-Down vs Bottom-Up

### Why Top-Down is $O(N^2)$ (Anti-Pattern)
Evaluating tree balance by calling a standalone `GetDepth(node)` on each node recursively visits descendants repeatedly:
$$T(N) = 2 \cdot T(N/2) + O(N) \implies O(N \log N) \text{ (best case)}, \quad O(N^2) \text{ (worst case degenerate)}$$

### Optimal Bottom-Up Post-Order DFS ($O(N)$)
By computing height and detecting balance/diameter during the **post-order return path** (ascent), each node is visited exactly once:
- **Balanced Tree (LeetCode #110)**: Returns `-1` as soon as $|height_{left} - height_{right}| > 1$, immediately terminating remaining recursion.
- **Diameter (LeetCode #543)**: Tracks max diameter across all subtrees while returning standard height $1 + \max(h_{left}, h_{right})$.
