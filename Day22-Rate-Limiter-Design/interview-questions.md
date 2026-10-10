# Senior & Staff Engineering Interview Questions: Day 22 — Rate Limiter Design & BST Traversal

### Q1: How would you design a distributed rate limiter for a high-traffic microservices architecture (100k+ QPS)?
**Answer:**
A production-grade distributed rate limiter requires a multi-tier defense architecture:
1. **Tier 1 (Edge / API Gateway)**: Envoy, Cloudflare, or AWS API Gateway enforces coarse rate limits per IP or client subnet to absorb volumetric DDoS attacks before reaching origin servers.
2. **Tier 2 (Service Mesh / In-Process Local Token Bucket)**: Each ASP.NET Core pod runs an in-memory token bucket that batches token synchronizations with a shared store, reducing network I/O.
3. **Tier 3 (Centralized Redis Cluster)**:
   - Utilizes Redis as a low-latency (sub-millisecond) shared data store.
   - Executes atomic Lua scripts (`SlidingWindowCounter` or `redis-cell`) to avoid distributed race conditions.
   - Keys are sharded across Redis nodes by client identifier: `rl:{client_id}:{window_epoch}`.
   - Emits standard RFC 6585 headers: `X-RateLimit-Limit`, `X-RateLimit-Remaining`, `X-RateLimit-Reset`, and `Retry-After`.

---

### Q2: Compare Token Bucket vs. Leaky Bucket. When would you strictly pick one over the other?
**Answer:**
- **Token Bucket**: Accumulates tokens up to capacity $C$ at rate $R$.
  - *Behavior*: Permits instantaneous bursts up to $C$, then strictly caps throughput at rate $R$.
  - *Pick when*: Facing customer-facing APIs (e.g. Stripe, GitHub). Real-world user interactions are inherently bursty (e.g. loading a dashboard triggers 15 concurrent REST calls). Penalizing natural bursts damages user experience.
- **Leaky Bucket**: Queues incoming requests in a FIFO buffer and discharges them at a steady, constant frequency $R$.
  - *Behavior*: Completely eliminates bursts; outputs a perfectly uniform stream.
  - *Pick when*: Egress rate limiting and traffic shaping—such as dispatching webhooks to external third-party services whose servers cannot tolerate bursts, or background batch DB writers where sustained constant disk throughput is required.

---

### Q3: What is the Boundary Burst problem in Fixed Window rate limiters, and how does the Sliding Window Counter solve it?
**Answer:**
- **Problem**: In a Fixed Window limiter with a limit of $N$ per minute, if an attacker fires $N$ requests at second 59 and another $N$ requests at second 01 of the next minute, $2N$ requests are processed within a 2-second interval, violating SLA guarantees and risking downstream service collapse.
- **Solution (Sliding Window Counter / Cloudflare Hybrid)**:
  Instead of tracking thousands of timestamps in a sorted set ($O(M)$ RAM), it maintains only the current window count and previous window count ($O(1)$ RAM).
  $$\text{Estimated Count} = \text{CurrentCount} + \text{PreviousCount} \times \left(1 - \frac{\text{ElapsedInCurrent}}{\text{WindowDuration}}\right)$$
  In the scenario above, at second 01 of the new minute, the weight of the previous window is $59/60 \approx 98.3\%$. The calculated count is $0 + N \times 0.983 = 0.983N$, immediately rejecting subsequent requests until sufficient time has elapsed.

---

### Q4: How do you handle race conditions in a distributed rate limiter without introducing massive lock contention?
**Answer:**
Naive `GET` followed by `SET` in Redis causes race conditions: two concurrent threads both read counter `99`, both see limit `100`, and both increment to `100`, allowing 101 requests.
- **Anti-Pattern**: Using distributed locks (e.g., RedLock) on every API request adds 5–15ms of latency and creates extreme lock contention at 100k QPS.
- **Production Solution**: Atomic Lua Scripts in Redis. Redis executes Lua scripts as single atomic operations within its single-threaded event loop. No other command can interleave.
```lua
local current = redis.call('incr', KEYS[1])
if current == 1 then
    redis.call('expire', KEYS[1], ARGV[1])
end
if current > tonumber(ARGV[2]) then
    return 0 -- Rejected
end
return 1 -- Allowed
```

---

### Q5: What client identification strategy should you use, and how do you prevent IP spoofing attacks?
**Answer:**
Client identity resolution must follow a strict priority ladder:
1. **Authenticated Users**: `Bearer` token subject (`sub` claim) or user ID.
2. **API Integrations**: Cryptographic API Key (`X-Api-Key` or `Authorization: ApiKey ...`).
3. **Unauthenticated Public Endpoints**: Remote Client IP.
- **IP Spoofing Vulnerability**: Attackers can forge `X-Forwarded-For: 1.2.3.4` headers.
- **Defense**: Never trust raw `X-Forwarded-For` from the open internet. The edge proxy (Cloudflare, AWS ALB, NGINX) must be configured to strip untrusted incoming headers and append the true socket IP (`RemoteIpAddress`) to the header chain. In ASP.NET Core, use `app.UseForwardedHeaders()` with explicitly configured `KnownNetworks` and `KnownProxies`.

---

### Q6: How do you size memory requirements for a Redis cluster backing 50 million active users on a sliding window rate limiter?
**Answer:**
Using the Cloudflare Sliding Window Counter:
- **Keys per user**: 1 key per window (with previous window stored as a hash or separate key).
- **Data size**: Key string (`rl:usr:{uuid}:epoch` ~ 36 bytes) + Redis integer value (8 bytes) + dict entry overhead (~32 bytes) + jemalloc allocation padding $\approx 100\text{ bytes}$ per entry.
- **Two windows per user**: $100\text{ bytes} \times 2 = 200\text{ bytes}$.
- **50 Million Active Users**:
  $$50,000,000 \times 200\text{ bytes} \approx 10,000,000,000\text{ bytes} \approx 10\text{ GB RAM}$$
- **TTL Pruning**: Keys have `EXPIRE = window_size * 2`, ensuring inactive users consume zero memory. 10 GB of Redis RAM comfortably fits on a single small Redis cluster node or a 3-shard cluster for high availability.
- *Contrast*: A Sliding Window Log at 100 req/min would require $50\text{M} \times 100 \times 16\text{ bytes} \approx 80\text{ GB RAM}$.

---

### Q7: What headers and HTTP response status code should be returned according to RFC specifications when rate limiting?
**Answer:**
1. **Status Code**: `HTTP 429 Too Many Requests` (RFC 6585).
2. **Headers**:
   - `Retry-After`: Number of seconds (or HTTP-date) the client must wait before retrying (RFC 7231).
   - `X-RateLimit-Limit`: Maximum requests permitted in the current period.
   - `X-RateLimit-Remaining`: Remaining request quota.
   - `X-RateLimit-Reset`: Unix epoch timestamp indicating when the current window resets.
3. **Response Body**: RFC 7807 `application/problem+json` providing machine-readable error details, helping client libraries implement automated exponential backoff with jitter.

---

### Q8: What is the impact of clock drift in distributed rate limiting, and how do you mitigate it?
**Answer:**
- **Problem**: In a distributed cluster, if App Server A has clock time $T_0$, App Server B has $T_0 + 2\text{s}$, and Redis has $T_0 - 1\text{s}$, calculating window boundaries locally leads to inconsistent window IDs and premature or delayed resets.
- **Mitigation**:
  1. **Redis Server-Side Time**: Never pass application server local timestamps to Redis for window calculations. Call `redis.call('TIME')` inside the Lua script or use Redis relative expiration `PEXPIRE`.
  2. **NTP / PTP Synchronization**: Run AWS Time Sync Service or Chrony on all infrastructure instances to maintain clock drift below 1 millisecond.
  3. **Relative Time**: In Token Bucket algorithms, track elapsed time relative to monotonically increasing system ticks (`Stopwatch.GetTimestamp()` in .NET) rather than wall-clock `DateTime.UtcNow`.

---

### Q9: Explain LeetCode #98 (Validate BST). Why is checking `left < root < right` locally a critical pitfall?
**Answer:**
- **The Pitfall**: Local verification only validates direct parent-child relationships. Consider the tree:
  ```
       10
      /  \
     5    15
         /  \
        6    20
  ```
  Locally, $6 < 15 < 20$ is valid, and $5 < 10 < 15$ is valid. However, the node $6$ resides in the right subtree of $10$, which violates the fundamental BST invariant: **every** node in the right subtree must be strictly greater than $10$!
- **Optimal Solution**: Maintain valid intervals $(\text{min}, \text{max})$ down the call tree:
  - Left child inherits $(\text{min}, \text{node.val})$.
  - Right child inherits $(\text{node.val}, \text{max})$.
  - Use `long?` for bounds to handle boundary nodes containing `int.MinValue` and `int.MaxValue`.
  - Alternatively, perform an in-order traversal verifying that each visited node is strictly greater than the preceding node: `prev.HasValue && curr.val <= prev.Value => return false`.
  - Time: $O(N)$, Space: $O(H)$.

---

### Q10: In LeetCode #230 (Kth Smallest Element in BST), how do you achieve early termination in $O(H + k)$ time instead of $O(N)$?
**Answer:**
- **Naive Approach**: Traverse the whole tree ($O(N)$), store values in a sorted list or min-heap, and retrieve the $k$-th element. This wastes $O(N)$ time and memory when $k$ is small.
- **Optimal Stack Approach**:
  Simulate in-order traversal using an explicit stack:
  ```csharp
  var stack = new Stack<TreeNode>();
  var curr = root;
  while (curr != null || stack.Count > 0)
  {
      while (curr != null)
      {
          stack.Push(curr);
          curr = curr.left; // Drill down to leftmost leaf
      }
      curr = stack.Pop();
      if (--k == 0) return curr.val; // Early exit!
      curr = curr.right;
  }
  ```
  - **Complexity**: It drills down the left branch of height $H$, then pops and traverses only $k$ nodes. Runtime is $O(H + k)$ rather than $O(N)$. For a balanced BST with $H = \log N$ and small $k$, this executes in nearly logarithmic time with $O(H)$ auxiliary space.
