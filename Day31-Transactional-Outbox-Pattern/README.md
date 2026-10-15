# Day 31: The Transactional Outbox Pattern

## Architectural Overview

In distributed architectures, microservices must frequently perform two critical actions:
1. Mutate operational state in an ACID relational database (e.g. insert an `Order`).
2. Publish an event to an asynchronous message broker (e.g. `OrderCreatedEvent` to RabbitMQ/Kafka).

Executing these two operations as separate, uncoordinated steps leads to the classic **Dual-Write Problem**:
- If the database commits but the network call to the message broker fails or times out, downstream consumers never learn of the new order (silent data inconsistency).
- If the message broker publication succeeds first but the subsequent database transaction rolls back, downstream services process phantom events for state that was never committed.

Two-Phase Commit (2PC / XA Transactions) across heterogeneous storage engines is non-viable in cloud microservices due to extreme latency overhead, locking, and lack of support in modern brokers.

The **Transactional Outbox Pattern** solves this problem by guaranteeing **At-Least-Once event delivery** without distributed transactions, relying strictly on the local database's native ACID transaction boundary.

---

## Key Architectural Concepts

### 1. Atomic Local Database Persistence
Instead of publishing directly to the broker inside the business transaction:
1. The business mutation (`Order`) and the outgoing message (`OutboxMessage`) are written to the database **within the same local ACID transaction**.
2. If the transaction commits, both records persist. If the transaction aborts, both roll back. Dual-write inconsistency is mathematically eliminated.

```
+--------------------------------------------------------------+
|                     Local ACID Transaction                   |
|                                                              |
|   1. INSERT INTO Orders (...)                                |
|   2. INSERT INTO OutboxMessages (Payload, EventType, ...)    |
|   COMMIT                                                     |
+--------------------------------------------------------------+
```

### 2. Outbox Dispatch Strategies: Polling vs Change Data Capture (CDC)

| Characteristic | Polling Publisher | Change Data Capture (CDC / Debezium) |
|---|---|---|
| **Mechanism** | Background worker queries `OutboxMessages` with leasing | Tails database write-ahead log (WAL) |
| **Database Overhead** | Regular `SELECT ... FOR UPDATE SKIP LOCKED` polling queries | Minimal; reads the sequential append-only WAL stream |
| **Latency** | Polling interval delay (e.g., 200ms–2s) | Real-time / sub-second streaming |
| **Operational Complexity** | Low; runs as standard ASP.NET Core `IHostedService` | High; requires Kafka Connect, Debezium, ZooKeeper/KRaft |

### 3. Worker Leasing & Concurrency
When multiple outbox background workers run across scale-out instances:
- A worker leases pending rows using an expiration timestamp (`LockedUntilUtc`) and instance ID (`LockId`).
- Relational engines utilize `SELECT ... FOR UPDATE SKIP LOCKED` to allow parallel workers to process non-overlapping partitions without contention or deadlock.

### 4. At-Least-Once Delivery & The Idempotent Consumer (Inbox Pattern)
Because outbox publishers may crash after dispatching a message to the broker but before marking `ProcessedOnUtc` in the database, message re-delivery is guaranteed to happen occasionally.
Downstream consumers must implement the **Inbox Pattern**:
1. Check `InboxMessages` table for the unique `MessageId`.
2. If found, safely ACK and discard without re-running business side-effects.
3. If not found, execute the side-effect and record the `MessageId` atomically.

---

## Data Structures & Algorithms: Disjoint Set Union (Union-Find)

### LeetCode #684: Redundant Connection (Medium)
- **Problem**: Given a graph that started as a tree of $N$ nodes labeled $1$ to $N$, with one additional edge added, find the edge that can be removed so the resulting graph is a tree. If multiple answers, return the edge that occurs last in the input.
- **Algorithm (Disjoint Set Union with Path Compression and Union by Rank)**:
  - Initialize each node as its own parent with rank 1.
  - For each edge $(u, v)$:
    - Find root representatives: $R_u = \text{Find}(u)$ and $R_v = \text{Find}(v)$.
    - If $R_u == R_v$, vertices $u$ and $v$ are already connected in the same component. Adding this edge closes a cycle; return $(u, v)$ immediately.
    - Otherwise, merge sets using $\text{Union}(u, v)$ by attaching the smaller rank tree under the larger rank tree.
- **Complexity**:
  - **Time Complexity**: $\mathcal{O}(N \cdot \alpha(N)) \approx \mathcal{O}(N)$, where $\alpha$ is the Inverse Ackermann function ($\alpha(N) < 5$).
  - **Space Complexity**: $\mathcal{O}(N)$ for the parent and rank arrays.

---

## Test Suite Summary

All 10 unit tests pass with 100% coverage:
1. `OrderService_AtomicCreate_PersistsOrderAndOutboxMessage`
2. `OrderService_SimulatedRollback_PreventsDualWriteHazard`
3. `OutboxPublisher_ProcessesPendingMessages_AndMarksThemProcessed`
4. `OutboxPublisher_RespectsBatchSizeLimit`
5. `OutboxPublisher_WorkerLeasing_PreventsConcurrentDoubleProcessing`
6. `OutboxPublisher_TransientBrokerFailure_IncrementsRetryAndAppliesBackoff`
7. `OutboxPublisher_ExceedingMaxRetries_DeadLettersOutboxMessage`
8. `IdempotentConsumer_ProcessesNewMessage_AndRecordsInInbox`
9. `IdempotentConsumer_IgnoresDuplicateDelivery_GuaranteesExactlyOnceSideEffects`
10. `RedundantConnection_LC684_FindsCycleEdgeUsingUnionFind`
