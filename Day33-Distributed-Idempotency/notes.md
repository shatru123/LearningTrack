# Deep Dive Notes: Distributed Idempotency & De-duplication

## 1. Idempotency Key Semantics in Distributed REST & Messaging
In high-concurrency microservices, idempotency guarantees that making the same request multiple times has the exact same side effects on system state as making it once:
$$f(f(x)) = f(x)$$

### The IETF Idempotency-Key HTTP Header Specification
The IETF draft standard defines the `Idempotency-Key` header sent by HTTP clients. When processing:
1. **Fingerprint Validation**: Compute a cryptographic digest (SHA-256) of the HTTP method, request path, and body. If a request arrives with an existing key but a different digest, the server MUST return HTTP `422 Unprocessable Entity` or `400 Bad Request`.
2. **Lock State**: If the key is currently locked (processing) and the lock has not expired, return HTTP `409 Conflict` with a `Retry-After` header.
3. **Completed State**: If the key has completed, replay the exact response payload, headers, and status code (typically HTTP 200/201) along with an `Idempotent-Replay: true` header.

## 2. Distributed Storage & Lease Strategies
- **Relational DB with Unique Constraints**:
  - `INSERT INTO idempotency_keys (key, hash, status, locked_until) VALUES (...) ON CONFLICT DO NOTHING;`
  - Guarantees ACID atomicity across application clusters.
- **Redis with Redlock or SET NX PX**:
  - `SET key:idempotency lock_token NX PX 30000`
  - High throughput (sub-millisecond), but requires fallback or write-through persistence to a permanent store for long-lived cached results.
- **Worker Crash Handling & Lease Expiration**:
  - Never hold a lock without a finite lease timeout (TTL). If node crashes mid-operation, the unreleased lock must expire after a configurable duration (e.g. 30–60s) allowing safe automated recovery.

## 3. LeetCode #215: Quickselect vs Min-Heap Analysis
- **Quickselect**:
  - Partition array around a random pivot.
  - If pivot index equals target index $N - k$, return value.
  - If pivot index $< N - k$, recurse/iterate on the right subarray.
  - If pivot index $> N - k$, recurse/iterate on the left subarray.
  - **Recurrence**: $T(N) = T(N/2) + O(N) = O(N)$ average time.
  - Worst case: $O(N^2)$ if worst-case pivot selection occurs repeatedly, mitigated via randomized pivot.
- **Min-Heap**:
  - Uses `System.Collections.Generic.PriorityQueue<int, int>` in .NET.
  - Push each element with value as priority. When size exceeds $k$, dequeue smallest element.
  - The heap retains the $k$ largest elements seen so far; top of heap is the $k$-th largest.
  - Time: $O(N \log K)$, Space: $O(K)$. Ideal for streaming data where total size $N$ is unknown or unbounded.
