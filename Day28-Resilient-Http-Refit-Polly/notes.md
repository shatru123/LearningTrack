# Day 28: Deep Dive Notes — Resilient HTTP with Refit & Polly

## 1. Resilience in Distributed Systems

In monolithic applications, an in-memory function call either succeeds or throws an immediate exception in nanoseconds. In microservices architectures, service-to-service communication crosses network boundaries, introducing partial failures, transient latency spikes, and socket disconnects.

```
+----------------------------------------------------------------------------------------------------+
|                                POLLY v8 RESILIENCE PIPELINE TOPOLOGY                               |
+----------------------------------------------------------------------------------------------------+
|                                                                                                    |
|    Caller Request                                                                                  |
|          |                                                                                         |
|          v                                                                                         |
|    +------------------------------------------------------------------------------------------+    |
|    |                                  ResiliencePipeline                                      |    |
|    |------------------------------------------------------------------------------------------|    |
|    |  1. Total Timeout Strategy (e.g. 5s overall limit)                                       |    |
|    |        |                                                                                 |    |
|    |        v                                                                                 |    |
|    |  2. Retry Strategy (Exponential Backoff + Decorrelated Jitter)                           |    |
|    |        |                                                                                 |    |
|    |        v                                                                                 |    |
|    |  3. Circuit Breaker Strategy                                                             |    |
|    |     [ Closed: Normal ] ---> (Failures >= 50%) ---> [ Open: Fast-Fail with BrokenCircuit ]|    |
|    |            ^                                               |                             |    |
|    |            |                                        (Break Duration)                     |    |
|    |            +--- (Canary Succeeds) <--- [ Half-Open ] <-----+                             |    |
|    |        |                                                                                 |    |
|    |        v                                                                                 |    |
|    |  4. Attempt Timeout Strategy (e.g. 1.5s per HTTP attempt)                                |    |
|    +------------------------------------------------------------------------------------------+    |
|          |                                                                                         |
|          v                                                                                         |
|    [ Downstream Service (via Refit IInventoryServiceClient) ]                                      |
|                                                                                                    |
+----------------------------------------------------------------------------------------------------+
```

---

## 2. Refit: Type-Safe Declarative REST Clients

### Manual `HttpClient` vs. Refit
- **Manual `HttpClient`**:
  - Developers write boilerplate code: `client.GetAsync($"/api/inventory/{sku}")`, deserialize JSON streams, manually attach auth and correlation headers.
  - Runtime errors from misspelled route tokens or query parameters.
- **Refit (`Refit.HttpClientFactory`)**:
  - Developers declare an interface adorned with HTTP annotations (`[Get]`, `[Post]`, `[Header]`, `[Body]`).
  - During build, Refit uses Roslyn Source Generators to compile high-performance, non-allocating proxy implementations directly into the assembly.

```csharp
public interface IInventoryServiceClient
{
    [Get("/api/inventory/{sku}")]
    Task<ApiResponse<InventoryItemDto>> GetInventoryItemAsync(
        string sku,
        [Header("X-Correlation-ID")] string? correlationId = null,
        CancellationToken cancellationToken = default);
}
```

---

## 3. Polly v8: The Modern Resilience Engine

In .NET 8, Polly was redesigned from the ground up (`Polly.Core`) in partnership with Microsoft.

### Polly v7 vs. Polly v8
| Architectural Aspect | Polly v7 (`IAsyncPolicy`) | Polly v8 (`ResiliencePipeline`) |
|---|---|---|
| **Memory Allocation** | Allocates execution contexts and closures on heap | Zero-allocation using `ValueTask` and non-allocating structs |
| **Composition Model** | PolicyWrap with awkward nesting | Fluent sequential pipeline builder |
| **Telemetry** | Manual logging or third-party adapters | Native `System.Diagnostics.Metrics` and `Meter` integration |
| **CancellationToken** | Optional or passed manually through context | First-class citizen on every strategy |

### Core Resilience Strategies

#### 1. Retry with Decorrelated Jitter
When hundreds of concurrent requests encounter a temporary downstream hiccup, simple exponential retries cause all clients to retry at the exact same millisecond intervals ($2\text{s}, 4\text{s}, 8\text{s}$), overwhelming the recovering service (the "Thundering Herd" or retry storm).
- **Decorrelated Jitter** injects cryptographic or pseudorandom variation into the sleep duration:
  $$t_{\text{sleep}} = \min(t_{\text{max}}, \text{Uniform}(\text{low}, \text{prev\_delay} \times 3))$$
- Spreads retry spikes across a smooth time distribution.

#### 2. Advanced Circuit Breaker
A Circuit Breaker stops an application from repeatedly executing an operation that is almost certainly doomed to fail:
- **`Closed`**: Operations execute normally. Failures are recorded within a sliding time window (SamplingDuration).
- **`Open`**: When the failure ratio breaches a threshold (e.g. $\ge 50\%$) and the minimum throughput requirement is met (e.g. $\ge 4$ calls), the circuit opens immediately. All subsequent requests fail fast with `BrokenCircuitException`, preventing thread starvation and giving downstream services time to recover.
- **`Half-Open`**: When the break duration expires, the circuit enters Half-Open. A limited number of trial (canary) requests are permitted through. If successful, the circuit resets to `Closed`; if any fail, it resets to `Open` for another cooldown.

#### 3. Hedging (Speculative Parallel Execution)
For latency-sensitive read operations (e.g. searching a distributed catalog), 99% of requests complete in 15ms, but a tail 1% hang for 1500ms due to GC pauses or network retries.
- Hedging sets a delay threshold (e.g. 40ms). If the primary request does not respond within 40ms, the pipeline launches a second, concurrent request to an alternate replica. Whichever returns first is delivered to the caller, effectively eliminating tail latency (p99 smoothing).

---

## 4. Graph Algorithms: Pacific Atlantic Water Flow (LeetCode #417)

### Problem Formulation
Given an $m \times n$ matrix of elevations, rain water can flow from a cell to adjacent cells horizontally or vertically if the adjacent cell's height is **less than or equal** to the current cell's height.
- Pacific Ocean touches top ($r=0$) and left ($c=0$) edges.
- Atlantic Ocean touches bottom ($r=m-1$) and right ($c=n-1$) edges.
- Find all coordinates where water can reach **both** oceans.

### 1. Naive Direct Approach
Starting a search from every individual cell $(r, c)$ to check if it can reach both oceans requires checking up to $M \times N$ cells, leading to an unacceptably slow $\mathcal{O}((M \times N)^2)$ time complexity.

### 2. Optimal Reverse Multi-Source Traversal
Instead of flowing water downhill from the continent to the ocean, reverse the flow: **flow water uphill from the ocean boundaries into the continent**.
- A neighbor cell $(nr, nc)$ can be reached from $(r, c)$ if:
  $$\text{heights}[nr][nc] \ge \text{heights}[r][c]$$
- **Algorithm**:
  1. Initialize two boolean matrices: `pacific[m, n]` and `atlantic[m, n]`.
  2. Multi-source DFS/BFS for Pacific starting from all top and left perimeter cells. Mark reachable cells in `pacific`.
  3. Multi-source DFS/BFS for Atlantic starting from all bottom and right perimeter cells. Mark reachable cells in `atlantic`.
  4. Iterate through all $(r, c)$: any cell where `pacific[r, c] && atlantic[r, c]` is true belongs to the final intersection set.
- **Time Complexity**: $\mathcal{O}(M \times N)$ — each matrix cell is visited at most twice.
- **Space Complexity**: $\mathcal{O}(M \times N)$ for boolean reachability grids and recursion stack/queue.
