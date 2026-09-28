# Day 04 - .NET Garbage Collection Internals & Memory Laboratory

## Objective
Analyze .NET Garbage Collection generational architecture (Gen 0, Gen 1, Gen 2, Large Object Heap, Pinned Object Heap), contrast Server GC vs Workstation GC and Background GC, inspect `GCSettings`, and benchmark the latency and allocation impact of naive string manipulation against optimized memory buffers.

## Original Learning Tasks
1. Analyze Server GC vs Workstation GC, Background GC, GCSettings
2. DSA: Product of Array Except Self

## Practical Problem
In high-throughput .NET microservices, allocation rates directly dictate garbage collection frequency. High volumes of short-lived allocations trigger frequent Gen 0/Gen 1 sweeps, while allocations exceeding 85,000 bytes land directly on the Large Object Heap (LOH), which requires expensive Gen 2 collections to clean up. In worst-case scenarios, uncompacted LOH leads to memory fragmentation, high pause times (Stop-The-World), and degraded p99 API latencies.

## What I Implemented
1. **GcLaboratory**:
   - Runtime configuration inspector (`GCSettings.IsServerGC`, `GCSettings.LatencyMode`, `GC.MaxGeneration`).
   - Generational promotion laboratory (`DemonstrateGenerationalPromotion`): verifies object progression from Gen 0 to Gen 1 and Gen 2 through surviving collections.
   - Large Object Heap laboratory (`DemonstrateLohAllocation`): verifies that 85,000+ byte arrays bypass Gen 0/1 and land immediately in Gen 2 (LOH).
   - Controlled allocation benchmark (`CompareAllocationImpact`): measures per-thread allocation deltas with `GC.GetAllocatedBytesForCurrentThread()` across naive string concatenation, pre-sized `StringBuilder`, and rented `ArrayPool` buffers.
2. **DSA Solution**:
   - `ProductExceptSelf`: Two-pass prefix/suffix product array solution in $O(n)$ time and $O(1)$ auxiliary space without using division.

## Architecture
```text
                          Managed Heap Layout
┌───────────────────────────────────────┬───────────────────────────────┐
│           Ephemeral Segment           │       Large Object Heap       │
├───────────────┬───────────────┬───────┼───────────────────────────────┤
│     Gen 0     │     Gen 1     │ Gen 2 │              LOH              │
│ (Short-lived) │ (Surviving G0)│(Long) │  (>= 85,000 bytes / objects)  │
└───────────────┴───────────────┴───────┴───────────────────────────────┘
       ▲               ▲            ▲                   ▲
   Allocations      Promoted     Promoted         Allocated Direct
   (new byte[64])                                 (new byte[85000])
```

## Key Concepts
- **Generations (0, 1, 2)**:
  - **Gen 0**: Ephemeral generation holding newly created objects. Fast, non-blocking sweeps.
  - **Gen 1**: Serves as a buffer between ephemeral and long-lived objects.
  - **Gen 2**: Long-lived objects (static singletons, caches, LOH). Collections here are "full GCs" and carry significant pause time overhead.
- **Large Object Heap (LOH)**: Objects $\ge 85,000$ bytes. Not compacted by default (to avoid copying large memory blocks), leading to fragmentation unless configured with `GCSettings.LargeObjectHeapCompactionMode`.
- **Server GC vs Workstation GC**:
  - *Workstation GC*: 1 managed heap, GC runs on the allocating thread (or a single background GC thread). Optimized for desktop UI responsiveness and low memory footprint.
  - *Server GC*: 1 heap and 1 dedicated GC thread per logical CPU core. Heaps operate in parallel, maximizing throughput for multi-core server workloads at the cost of higher base memory consumption.
- **Background GC**: Runs concurrent Gen 2 collections in the background while allowing ephemeral (Gen 0/1) collections to occur concurrently, preventing long Stop-the-World pauses.

## Testing
- Automated xUnit test suite (`GcLaboratoryTests`, `DsaExercisesTests`).
- Verifies generational promotion through forced generation collections.
- Verifies LOH threshold ($85,000$ bytes landing in Gen 2).
- Validates order-of-magnitude allocation reductions between naive concatenation and pre-allocated buffers.
- Validates prefix/suffix product logic across positive, negative, and zero arrays.

## Performance Observations
- Naive string concatenation allocated over $30,000+$ bytes for 50 operations due to intermediate immutable string allocations ($O(N^2)$ byte churn).
- Pre-sized `StringBuilder` and `ArrayPool` reduced heap allocations by $>90\%$, eliminating ephemeral GC churn completely.

## Production Relevance
- Tuning GC mode in `runtimeconfig.json` (`ServerGarbageCollection: true`) is essential for containerized microservices in Kubernetes. However, setting inappropriate CPU limits can lead to CPU throttling if Server GC spins up threads for each host core instead of the assigned container quota.

## Common Mistakes
1. Forcing `GC.Collect()` in production request pipelines, which suspends all execution threads and ruins throughput.
2. Allocating short-lived byte arrays $\ge 85,000$ bytes (e.g. reading full file uploads into memory) instead of streaming with buffers $\le 81,920$ bytes.
3. Not pre-sizing `StringBuilder` or `List<T>` when the capacity is known, triggering repeated heap buffer resizes.
4. Ignoring container memory limits when running Server GC.

## Senior Interview Questions
See [interview-questions.md](file:///Users/shatrughnaambhore/Shatru/Learning/Projects/LearningTrack/Day04-Garbage-Collection/interview-questions.md) for 10 in-depth architectural questions.

## What I Practically Understood
- How the generational hypothesis holds in practice: most objects die young in Gen 0.
- Why objects $\ge 85\text{KB}$ jump directly into Gen 2 and why streaming via `ArrayPool` buffers ($< 85\text{KB}$) protects the LOH.
- How to accurately measure allocation differences using `GC.GetAllocatedBytesForCurrentThread()`.

## What I Need to Read Later
- Pinned Object Heap (POH) behavior introduced in .NET 5+.
- CoreCLR DATAS (Dynamic Adaptation To Application Sizes) GC in .NET 8/9.

## Key Takeaways
Garbage collection pause times are directly driven by allocation volume and object survival rates. Writing high-throughput .NET microservices requires designing allocation-conscious code that minimizes Gen 0 churn and avoids LOH allocations.
