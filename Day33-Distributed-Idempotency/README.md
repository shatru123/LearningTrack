# Day 33: Distributed Idempotency & De-duplication

## Architectural Overview
In distributed systems, network partitions, client retries, and message broker at-least-once delivery guarantees inevitably produce duplicate requests. Without strict idempotency controls, duplicate submissions result in double-billing, corrupted inventory reserves, or inconsistent ledger entries.

This implementation demonstrates an enterprise-grade distributed idempotency pattern in .NET 8 / C# featuring:
1. **Idempotency Key Protocol**: Clients supply a unique idempotency key (e.g., UUID or deterministic hash) via HTTP header or message metadata.
2. **Cryptographic Payload Verification**: Computes a SHA-256 digest of the request payload to prevent payload tampering under an existing idempotency key. If payloads differ, an `IdempotencyPayloadMismatchException` (HTTP 422 Unprocessable Entity) is thrown.
3. **Optimistic Locking & Lease Management**: Prevents race conditions during concurrent in-flight requests by reserving a lock with a lease expiration (`LockedUntilUtc`). If a second request arrives while the first is processing, it receives an `IdempotencyConflictException` (HTTP 409 Conflict).
4. **Crash Fault Recovery**: If a worker node crashes mid-flight, subsequent requests after the lease window automatically reclaim the processing lock instead of deadlocking indefinitely.
5. **Response Caching**: Successful responses are stored alongside their HTTP status codes and returned immediately with `FromCache = true` for subsequent idempotent replays.

```
       Client Request (Key: "tx-123", Body)
                    │
                    ▼
     ┌─────────────────────────────┐
     │ SHA-256 Hash of Request Body│
     └──────────────┬──────────────┘
                    │
                    ▼
          Query Idempotency Store
                    │
    ┌───────────────┴─────────────────┐
    │                                 │
[Record Exists]               [Record Not Found]
    │                                 │
    ├─ Hash != Payload Hash ──> 422 Payload Mismatch
    ├─ Status == Completed  ──> Return Cached 200/201 (FromCache: true)
    ├─ Status == Processing           │
    │   ├─ Lease Valid   ──> 409 Conflict (In Flight)
    │   └─ Lease Expired ──> Re-lease lock & Re-execute
    │                                 │
    └─────────────────┬───────────────┘
                      │
            Atomic Insert Record (Status: Processing, Lease: 30s)
                      │
            Execute Business Logic
                      │
            Update Record (Status: Completed, Result Body)
                      │
            Return Response (FromCache: false)
```

---

## LeetCode #215: Kth Largest Element in an Array (Medium)
Finding the $k$-th largest element in an unsorted array efficiently:
- **Quickselect (Hoare's Selection)**: $O(N)$ average time complexity, $O(1)$ auxiliary space. Uses randomized pivot partitioning to prune half the search space iteratively. Target index is $N - k$.
- **Min-Heap (`PriorityQueue<int, int>`)**: $O(N \log K)$ time complexity, $O(K)$ space complexity. Maintains a min-heap of size $k$; the root element is always the $k$-th largest element.

---

## Running Tests
```bash
dotnet test Day33-Distributed-Idempotency/tests/Day33.DistributedIdempotency.Tests/Day33.DistributedIdempotency.Tests.csproj
```
All 10 unit tests validate payload hashing, lease expiry, concurrent conflicts, cached replays, and Quickselect vs Min-Heap algorithms.
