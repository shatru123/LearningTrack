# Day 33: Senior & Staff Interview Questions & Answers

### Q1: What is the semantic difference between safe, idempotent, and non-idempotent HTTP methods according to RFC 9110?
**Answer**:
- **Safe Methods** (`GET`, `HEAD`, `OPTIONS`, `TRACE`): Do not modify server resource state. A client can execute them without expectation of mutation or side effects.
- **Idempotent Methods** (`PUT`, `DELETE`, `GET`, `HEAD`): Executing the operation once yields the exact same server resource state as executing it multiple times ($f(f(x)) = f(x)$). For example, `DELETE /orders/42` sets the status or deletes the record; subsequent calls yield resource not found or already deleted, but resource state remains deleted.
- **Non-Idempotent Methods** (`POST`, `PATCH` in some contexts): Submitting repeated requests produces cumulative side effects (e.g. repeated `POST /payments` creates multiple charge records and bills the credit card multiple times). Applying distributed idempotency mechanisms converts non-idempotent operations into functionally idempotent operations.

---

### Q2: How does an Idempotency Key architecture prevent double-charging in payment gateways like Stripe or Adyen?
**Answer**:
When an end-user submits a payment checkout:
1. The frontend or client generates an unguessable UUIDv4 or client-derived transaction token as the `Idempotency-Key` HTTP header.
2. The payment gateway intercepts the request and verifies the key in an atomic fast-path datastore (e.g. PostgreSQL or Redis with `SET NX EX`).
3. If the key exists in `Processing` state and lock is unexpired, the gateway returns HTTP `409 Conflict` (or waits on a distributed promise/future), preventing concurrent race charges.
4. If the key exists in `Completed` state, the stored receipt is returned without touching the card network rails.
5. If the key is new, the gateway creates a `Processing` record, contacts Visa/Mastercard, records `Completed` with the transaction result, and returns HTTP 200/201.

---

### Q3: Why is request payload hashing (SHA-256) critical when using client-supplied idempotency keys?
**Answer**:
A client bug or malicious actor might reuse an idempotency key across different semantic operations (e.g., re-using key `tx-001` originally used to purchase \$10 goods for a \$1,000 withdrawal). Without payload validation:
- The system would treat the \$1,000 request as a duplicate replay of the \$10 request, returning the old cached response and ignoring the new intent, or vice-versa.
Computing a SHA-256 fingerprint over the method, URL path, and serialized request body allows the server to verify whether the incoming payload matches the original. If mismatched, the server rejects the request with HTTP `422 Unprocessable Entity` or `400 Bad Request` ("Idempotency key payload mismatch").

---

### Q4: How do you handle distributed worker crashes when processing an idempotent request under a lease lock?
**Answer**:
If a worker acquires a processing lock (`Status = "Processing"`) and dies due to an OOM crash or unhandled SIGKILL, the key could remain permanently stuck. To solve this:
1. **Time-Bound Leases (TTL)**: Processing records include a `LockedUntilUtc` timestamp (e.g. `DateTime.UtcNow.AddSeconds(30)`).
2. **Heartbeating**: For long operations, the worker periodically extends the lease.
3. **Automated Recovery**: When another request arrives and finds `Status == "Processing"` but `LockedUntilUtc < DateTime.UtcNow`, it detects that the previous worker crashed. It atomically updates `LockedUntilUtc` to a new expiration and re-executes the operation safely.

---

### Q5: How do you implement distributed idempotency in asynchronous message-driven architectures (e.g. RabbitMQ, Kafka)?
**Answer**:
Message brokers deliver messages with *at-least-once* semantics. To ensure idempotency:
1. **Message Deduplication Table**: The consumer inserts the incoming `MessageId` or domain deduplication key into a relational table within the same database transaction as the business state mutation.
2. **Outbox / Inbox Pattern**: Use the transactional Inbox pattern. If the unique constraint on `(ConsumerName, MessageId)` throws a unique constraint violation, the message is recognized as duplicate and acknowledged (`ack`) immediately without re-executing domain actions.
3. **Natural Idempotency**: Design message mutations to be commutative or state-based (`UPDATE account SET balance = 500 WHERE account_id = 1 AND version = 2`) rather than incremental delta-based (`UPDATE account SET balance = balance + 100`).

---

### Q6: What are the trade-offs between Redis and a Relational Database (PostgreSQL) for storing idempotency state?
**Answer**:
- **Redis**:
  - *Pros*: Extremely high throughput (sub-millisecond latency), built-in TTL eviction, `SETNX` atomic primitives.
  - *Cons*: Memory capacity limits, potential data loss during cluster failover (asynchronous replication), lack of distributed two-phase commit across Redis and domain database.
- **Relational Database (PostgreSQL)**:
  - *Pros*: Full ACID compliance; the idempotency record can be inserted in the **exact same database transaction** as the entity updates, guaranteeing 100% consistency with zero dual-write vulnerabilities.
  - *Cons*: Higher storage overhead and latency; requires background cleanup jobs or table partitioning to purge expired records over time.
- *Best Practice*: Use PostgreSQL for critical financial/ledger operations; use Redis for high-throughput, low-risk telemetry/notification rate-limiting.

---

### Q7: In Quickselect, why is randomized pivot selection superior to deterministic pivot selection (e.g., choosing `nums[0]`)?
**Answer**:
If a deterministic pivot selection (like picking `nums[left]` or `nums[right]`) is used on an already sorted or reverse-sorted array, each partition step only eliminates a single element ($N - 1$ remaining).
- This degrades time complexity from the expected $O(N)$ to the worst-case $O(N^2)$, exceeding stack limits or timing out.
- By selecting a pivot uniformly at random from `[left, right]`, the probability of repeatedly choosing extremal elements becomes astronomically small, guaranteeing expected $O(N)$ runtime across all input distributions.

---

### Q8: Compare Quickselect and Min-Heap for finding the Kth largest element in terms of streaming data and memory constraints.
**Answer**:
- **Quickselect**:
  - In-place algorithm ($O(1)$ space).
  - Requires the entire dataset to reside in random-access memory (RAM).
  - Mutates the original array in place. Cannot process streaming or unbounded data.
- **Min-Heap (`PriorityQueue<TElement, TPriority>`)**:
  - Operates on streaming/unbounded data where total count $N$ is unknown in advance.
  - Maintains a sliding window of the top $k$ elements in $O(K)$ auxiliary memory.
  - Time complexity is $O(N \log K)$. When $K \ll N$, this is practically linear and highly cache-efficient.

---

### Q9: How should downstream API calls to third-party providers (e.g. Stripe, SendGrid) be coordinated within an idempotent workflow?
**Answer**:
A dual-write problem exists: the internal DB transaction can commit, but the external API call might fail or time out, or vice versa.
1. Generate a deterministic downstream idempotency key derived from the upstream key: `downstreamKey = Hash(upstreamKey + "-stripe")`.
2. Save the pending downstream intent before calling the third party.
3. Pass `downstreamKey` in the external HTTP request header.
4. If a network timeout occurs, retrying the operation with the same `downstreamKey` ensures the external provider does not duplicate the charge or email.

---

### Q10: What is the optimal retention and cleanup policy for an idempotency store?
**Answer**:
Indefinite retention causes unbound table growth. The retention window should match the maximum client retry window:
- For user-facing web/mobile APIs, clients typically stop retrying after 24 to 72 hours.
- A sliding TTL of 24–48 hours is standard.
- Implementation: Use PostgreSQL partitioned tables (e.g., daily partitions dropped via `pg_partman` or cron `DROP TABLE`) or Redis key TTLs. Dropping partitions is an $O(1)$ catalog metadata operation with zero table bloat compared to costly `DELETE FROM WHERE created_at < NOW() - INTERVAL '3 days'`.
