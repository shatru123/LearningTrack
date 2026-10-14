# Day 28: Resilient Service-to-Service HTTP with Refit & Polly

## Overview
**Day 28** explores robust service-to-service communication across microservices in .NET 8. We master declarative type-safe REST clients using **Refit** and integrate enterprise resilience patterns via the redesigned **Polly v8** engine. The resilience pipeline features Exponential Backoff with Decorrelated Jitter, advanced Circuit Breakers (Closed $\to$ Open $\to$ Half-Open), Total and Attempt Timeouts, and Hedging (speculative parallel execution for p99 latency smoothing). For DSA, we solve **LeetCode #417 (*Pacific Atlantic Water Flow*)** using multi-source reverse graph traversals (DFS and BFS).

---

## Architectural Topics Covered

1. **Type-Safe REST Contracts with Refit**:
   - Declarative interface-driven HTTP contracts (`[Get]`, `[Post]`, `[Header]`, `[Body]`).
   - Strong typing of request/response payloads (`InventoryItemDto`, `StockReservationRequest/Response`).
   - Elimination of raw string URLs and boilerplate `HttpClient` JSON serialization.
2. **Polly v8 Resilience Pipelines**:
   - Modern `ResiliencePipelineBuilder` architecture in `Polly.Core`.
   - **Retry with Exponential Backoff + Jitter**: $T_{\text{delay}} = 2^{\text{attempt}} \times \text{base} \pm \text{jitter}$ to prevent thundering herd retries on recovering backends.
   - **Circuit Breaker**: Failure ratio monitoring, minimum throughput guardrails, sampling windows, and automated recovery via probe half-open states.
   - **Timeouts**: Total pipeline timeout vs granular per-attempt cancellation.
   - **Hedging**: Proactive concurrent execution if primary attempt latency exceeds a tight threshold (e.g., 40ms), returning whichever succeeds first.
3. **Graph Traversal & Multi-Source Flows**:
   - **LeetCode #417: Pacific Atlantic Water Flow (Medium)**: Reverse flow exploration from dual ocean coastlines.
   - Multi-source DFS and FIFO queue BFS exploring uphill elevations ($h_{\text{neighbor}} \ge h_{\text{current}}$).
   - Bitwise/matrix intersection of Pacific-reachable and Atlantic-reachable continental cells.

---

## Project Structure

```
Day28-Resilient-Http-Refit-Polly/
├── src/
│   └── Day28.ResilientHttpRefitPolly/
│       ├── Day28.ResilientHttpRefitPolly.csproj
│       ├── RefitContractsAndServices.cs
│       ├── ResiliencePipelineBuilder.cs
│       └── PacificAtlanticSolver.cs
├── tests/
│   └── Day28.ResilientHttpRefitPolly.Tests/
│       ├── Day28.ResilientHttpRefitPolly.Tests.csproj
│       └── Day28Tests.cs
├── README.md
├── notes.md
└── interview-questions.md
```

---

## Running the Unit Tests

```bash
dotnet test Day28-Resilient-Http-Refit-Polly/tests/Day28.ResilientHttpRefitPolly.Tests/Day28.ResilientHttpRefitPolly.Tests.csproj
```

### Test Suite Coverage
1. `RefitContract_ModelSerializationAndInvariants`: Validates DTO properties, decimal values, and contract invariants.
2. `PollyPipeline_ExecutesSuccessfulCallWithoutRetries`: Validates baseline fast-path execution through pipeline with zero retries.
3. `PollyPipeline_RetriesOnTransientHttpExceptions_AndSucceeds`: Verifies retry strategy retries on transient exceptions and completes successfully.
4. `PollyPipeline_ExhaustsRetries_WhenFailurePersists`: Confirms retry exhaustion and terminal exception rethrowing.
5. `PollyPipeline_CircuitBreaker_OpensAfterThreshold`: Validates circuit breaker opening upon breaching failure ratios, failing fast with `BrokenCircuitException`.
6. `PollyPipeline_Timeout_AbortsSlowOperations`: Validates cancellation propagation and `TimeoutRejectedException`.
7. `PollyPipeline_Hedging_ExecutesSpeculativeParallelAttempt`: Confirms hedging strategy executes speculative parallel attempts when primary attempt stalls.
8. `PacificAtlantic_DfsAndBfs_StandardContinent_CalculatesCorrectIntersection`: Validates LC #417 standard 5x5 elevation grid under both DFS and BFS.
9. `PacificAtlantic_SingleCell_ReachesBothOceans`: Validates 1x1 boundary matrix.
10. `PacificAtlantic_HandlesEdgeCases`: Validates null/empty matrices and flat continental plateaus.
