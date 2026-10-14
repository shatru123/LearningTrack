# Day 28: Senior & Staff Engineer Interview Questions & Answers

### 1. Why is blindly adding retries to microservices dangerous, and how do Retry Storms cause cascading catastrophic failures?
**Answer:**
When a downstream microservice experiences high CPU load or database lock contention, its latency increases. If callers have short timeouts and immediately retry failed or timed-out requests:
1. **Traffic Multiplication**: If each request retries 3 times, a struggling service experiencing 1,000 requests/sec suddenly receives up to 4,000 requests/sec.
2. **Work Duplication**: The downstream service may still be processing the original request while the caller retries, doubling the CPU and I/O consumption.
3. **Cascading Collapse**: As the downstream service crashes completely, caller thread pools saturate waiting on timeouts, causing callers to crash and propagating the failure up the call chain to the API Gateway and frontend.

**Defensive Architecture:**
- Use **Exponential Backoff with Jitter** to de-synchronize retries.
- Implement **Circuit Breakers** to fail fast when downstream services are degraded.
- Enforce strict **Total Timeouts** encompassing all retry attempts.
- Only retry **idempotent operations** or transient network errors (HTTP 429, 503; never 400, 401, 403, 404, or 422).

---

### 2. How do Idempotency Keys enable safe retries for non-idempotent HTTP methods (such as `POST /payments`)?
**Answer:**
Under HTTP semantics, `GET`, `PUT`, and `DELETE` are naturally idempotent (re-executing yields the same side effects). `POST` operations (e.g. charging a credit card or creating an order) are non-idempotent; retrying a timed-out `POST` could result in duplicate charges or multiple orders.

**Idempotency Key Pattern:**
1. The client generates a unique UUID (e.g. `Idempotency-Key: f47ac10b-58cc-4372-a567-0e02b2c3d479`) before making the initial `POST`.
2. The downstream service checks an ACID database or Redis cache:
   - If the key is new: Store the key with status `PROCESSING` and execute the transaction. Once complete, save the final HTTP response payload and status code alongside the key.
   - If the key exists and status is `PROCESSING`: Return HTTP 409 Conflict or wait for completion.
   - If the key exists and status is `COMPLETED`: Immediately return the cached response without re-executing the business logic.
3. Callers can safely retry failed or timed-out `POST` requests without fear of duplicate execution.

---

### 3. What architectural enhancements make Polly v8 significantly faster and more memory-efficient than Polly v7?
**Answer:**
Polly v7 was designed around `IAsyncPolicy` and relied heavily on closures, object allocations (`Context`), and `Task<T>` instantiations on the managed heap for every policy execution.

**Polly v8 Enhancements (`Polly.Core`):**
1. **Zero-Allocation Execution Path**: Uses `ValueTask` and non-allocating structs (`ResilienceContext`). When an operation succeeds on the first attempt (the 99% happy path), Polly v8 introduces **0 bytes of heap allocation**.
2. **Unified Pipeline Architecture**: Replaced the cumbersome `PolicyWrap` with `ResiliencePipelineBuilder`, providing a linear, single-allocation builder that compiles into a highly optimized strategy chain.
3. **Native OpenTelemetry Metrics**: Emits standardized `System.Diagnostics.Metrics` (e.g., retry attempts, circuit breaker state changes, execution durations) natively without needing custom diagnostic listeners.
4. **First-Class Timeouts & Cancellation**: Every strategy natively accepts and forwards `CancellationToken`.

---

### 4. What is the mathematical difference between Full Jitter and Equal Jitter in Exponential Backoff?
**Answer:**
Given base delay $b$, attempt $i$, and ceiling delay $c$:
- Standard Exponential Backoff without jitter:
  $$t_{\text{sleep}} = \min(c, b \times 2^i)$$
- **Equal Jitter**: Divides the delay into a deterministic half and a randomized half:
  $$t_{\text{sleep}} = \frac{\min(c, b \times 2^i)}{2} + \text{Random}\left(0, \frac{\min(c, b \times 2^i)}{2}\right)$$
  Ensures some minimum wait while adding jitter.
- **Full Jitter (AWS Recommended)**: Distributes the wait time uniformly between 0 and the full exponential ceiling:
  $$t_{\text{sleep}} = \text{Random}(0, \min(c, b \times 2^i))$$
  *Advantage*: Maximally flattens client request aggregation, completely neutralizing thundering herd synchronizations and reducing total system completion time.

---

### 5. How should an architect tune Circuit Breaker parameters for high-throughput vs low-throughput microservices?
**Answer:**
- **High-Throughput Services (e.g., 5,000 RPS Payment/Catalog)**:
  - **SamplingDuration**: Short (e.g., 5–10 seconds).
  - **MinimumThroughput**: High (e.g., 50–100 requests). An isolated hiccup of 5 failures should not trip a high-volume service.
  - **FailureRatio**: 0.3–0.5 (30% to 50% failure rate).
  - **BreakDuration**: Moderate (e.g., 5–15 seconds) with gradual canary recovery.
- **Low-Throughput / Sensitive Services (e.g., 2 RPS Batch Processing)**:
  - **SamplingDuration**: Long (e.g., 30–60 seconds).
  - **MinimumThroughput**: Low (e.g., 2–4 requests). Otherwise, the circuit might never gather enough data to trip.
  - **FailureRatio**: Higher tolerance or consecutive failure counts (e.g., 3 consecutive failures).
  - **BreakDuration**: Longer (e.g., 30–60 seconds) to give external downstream dependencies adequate time to reboot.

---

### 6. When should an engineer use Hedging over Retries, and what are the trade-offs?
**Answer:**
- **Retry**:
  - Waits for the primary attempt to fail or time out completely before issuing an alternative attempt.
  - *Trade-off*: Increases total latency by at least the timeout duration of the failed request.
- **Hedging (Speculative Parallel Execution)**:
  - Does *not* wait for failure; instead, if the primary request exceeds a latency threshold (e.g. 50ms), it fires a second concurrent request to another server replica while keeping the first request alive. Whichever returns first wins.
  - *Best Suited For*: Idempotent read operations where tail latency (p99/p99.9) is unacceptable (e.g., search queries, cache lookups, financial quote lookups).
  - *Cost/Trade-off*: Increases downstream server load and network bandwidth by issuing duplicate requests during latency spikes. Must never be used on non-idempotent mutating endpoints.

---

### 7. How do you integrate Refit with `IHttpClientFactory` and Polly v8 in .NET 8 Dependency Injection?
**Answer:**
Using `Microsoft.Extensions.Http.Resilience`:
```csharp
builder.Services
    .AddRefitClient<IInventoryServiceClient>()
    .ConfigureHttpClient(c => c.BaseAddress = new Uri("https://inventory-svc"))
    .AddResilienceHandler("inventory-pipeline", pipelineBuilder =>
    {
        pipelineBuilder.AddTimeout(TimeSpan.FromSeconds(5))
            .AddRetry(new HttpRetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true
            })
            .AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
            {
                FailureRatio = 0.5,
                SamplingDuration = TimeSpan.FromSeconds(10)
            });
    });
```
Refit automatically binds to the resilient `HttpClient` configured with the Polly pipeline, sharing connection pools and telemetry.

---

### 8. How does cancellation token propagation prevent "Ghost Requests" in microservices call chains?
**Answer:**
When an external client (e.g. mobile app) cancels a request or closes the connection:
- If microservices ignore the `CancellationToken`, backend servers continue executing expensive database queries, cryptographic operations, and downstream HTTP calls that will ultimately be discarded when attempting to write the HTTP response (Ghost Requests).
- **Correct Propagation**:
  1. Receive `HttpContext.RequestAborted` in the API endpoint.
  2. Pass `cancellationToken` into Refit method calls: `await _client.GetItemAsync(sku, ct)`.
  3. Polly v8 strategies wrap and link this token with internal attempt timeouts using `CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutToken)`.
  4. Sockets immediately abort active TCP streams, freeing up downstream worker threads and connection pool sockets.

---

### 9. Why does reverse multi-source traversal in LeetCode #417 (*Pacific Atlantic Water Flow*) reduce time complexity from $\mathcal{O}((MN)^2)$ to $\mathcal{O}(MN)$?
**Answer:**
- **Forward Approach**:
  - For every cell $(r, c)$ in the $M \times N$ matrix, we run a search to see if it can reach the Pacific and Atlantic boundaries.
  - In the worst case (e.g. flat plateau), each search visits $\mathcal{O}(M \times N)$ cells.
  - Total time: $(M \times N) \times \mathcal{O}(M \times N) = \mathcal{O}((M \times N)^2)$. For a $200 \times 200$ grid, this requires $\approx 1.6 \times 10^9$ operations, triggering Time Limit Exceeded (TLE).
- **Reverse Multi-Source Approach**:
  - We seed the ocean boundaries (perimeter cells) and search uphill:
    - Pacific: $M + N$ boundary cells.
    - Atlantic: $M + N$ boundary cells.
  - Because we maintain a `visited` matrix for each ocean, each cell is evaluated at most once per ocean.
  - Total time: $2 \times \mathcal{O}(M \times N) = \mathcal{O}(M \times N)$ linear operations ($\approx 8 \times 10^4$ operations, completing in milliseconds).

---

### 10. Compare multi-source DFS vs multi-source BFS for grid graph problems in terms of memory and stack safety.
**Answer:**
- **Multi-Source DFS**:
  - *Mechanism*: Uses the thread call stack (or explicit recursion stack).
  - *Memory Footprint*: On deep grids (e.g. spiral or descending corridors), recursion depth can equal $M \times N$. In .NET, a 1MB default thread stack can overflow on grids larger than $1000 \times 1000$.
- **Multi-Source BFS**:
  - *Mechanism*: Uses a FIFO `Queue<(int, int)>` on the managed heap.
  - *Memory Footprint*: The queue holds at most the frontier perimeter of the search wave at any given step, bounded by $\mathcal{O}(\min(M, N))$ for balanced waves (or $\mathcal{O}(M \times N)$ worst case).
  - *Stack Safety*: Completely stack-safe; cannot cause `StackOverflowException`.
  - *Level-by-Level*: BFS naturally processes cells in order of increasing distance/elevation, which is ideal for shortest path or contagion spreading problems (e.g. Rotting Oranges).
