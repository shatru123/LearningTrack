# Day 22: System Design — Rate Limiter Design & DSA BST In-Order Traversal

Comprehensive systems design lab implementing core rate limiting algorithms, production-grade ASP.NET Core distributed rate limiting middleware, and BST traversal verification.

---

## 🎯 Architecture & Objectives

1. **Token Bucket Algorithm**:
   - Manages bursty traffic with fixed maximum capacity $C$ and constant token generation rate $R$.
   - Allows requests consuming $K$ tokens if available; returns exact `Retry-After` calculation when tokens are depleted.
2. **Leaky Bucket Algorithm**:
   - Smooths output traffic to a constant rate $R$ via a bounded FIFO buffer queue.
   - Drops incoming requests immediately if buffer capacity is exceeded.
3. **Fixed Window Counter**:
   - Discrete time-window counter illustrating the classical $2\times$ boundary burst vulnerability at window transitions.
4. **Sliding Window Log**:
   - Accurate timestamp log (equivalent to Redis Sorted Set `ZADD`/`ZREMRANGEBYSCORE`) with $O(M)$ memory trade-off.
5. **Sliding Window Counter (Cloudflare Hybrid)**:
   - High-throughput $O(1)$ memory algorithm combining current and previous window counts using weighted approximation:
     $$\text{Estimated Count} = \text{CurrentCount} + \text{PreviousCount} \times \left(1 - \frac{\text{Elapsed}}{\text{Window}}\right)$$
6. **ASP.NET Core Middleware (`DistributedRateLimiterMiddleware`)**:
   - Injects RFC 6585 standard headers (`X-RateLimit-Limit`, `X-RateLimit-Remaining`, `X-RateLimit-Reset`, `Retry-After`).
   - Returns RFC 7807 `ProblemDetails` on HTTP 429 Too Many Requests.
7. **DSA BST Traversal Solvers**:
   - **LeetCode #98 (Validate Binary Search Tree)**: Range-bounded DFS and in-order strictly-increasing validation in $O(N)$ time.
   - **LeetCode #230 (Kth Smallest Element in a BST)**: Iterative in-order stack traversal with early termination at step $k$ in $O(H + k)$ time.

---

## 📁 Project Structure

```
Day22-Rate-Limiter-Design/
├── src/
│   └── Day22.RateLimiter/
│       ├── Day22.RateLimiter.csproj
│       ├── RateLimiterModels.cs             # RateLimitResult, Rule & IRateLimiter
│       ├── Algorithms.cs                    # 5 Rate limiting algorithm implementations
│       ├── DistributedRateLimiterMiddleware.cs # ASP.NET Core middleware
│       └── BstValidationSolvers.cs          # LC #98 & LC #230 BST solvers
├── tests/
│   └── Day22.RateLimiter.Tests/
│       ├── Day22.RateLimiter.Tests.csproj
│       └── Day22Tests.cs                    # 10 unit tests covering algorithms & DSA
├── README.md
├── notes.md                                 # Deep dive architectural notes
├── interview-questions.md                   # 10 Senior/Staff interview Q&As
└── index.html                               # Standalone offline web guide
```

---

## 🧪 Running Tests

```bash
dotnet test tests/Day22.RateLimiter.Tests/Day22.RateLimiter.Tests.csproj
```
