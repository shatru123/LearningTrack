# Day 32: Senior & Staff Engineer Interview Scenarios & Answers
## Theme: Saga Pattern (Orchestration vs Choreography), Compensation & Bidirectional BFS

---

### Q1: Explain the difference between Orchestration and Choreography in the Saga pattern. When does choreography become an anti-pattern?
**Answer:**
- **Choreography (Decentralized)**: Services publish domain events and react to events emitted by other services without a centralized coordinator.
- **Orchestration (Centralized)**: A dedicated saga orchestrator (`OrderStateMachine`) explicitly invokes commands on participant services and transitions between states based on incoming responses.
- **When Choreography becomes an anti-pattern**:
  1. *Pinball Machine Effect*: Tracing business flow requires tracing across multiple disparate telemetry streams without a single place to inspect current order status.
  2. *Compensating Cascade Complexity*: When a rollback involves 4+ services with branching conditions, coordinating reverse-order rollbacks via broadcast events creates fragile circular dependency loops.
  3. *Unbounded Event Flooding*: A single domain event can accidentally trigger unexpected cascading transactions across loosely coupled teams.

---

### Q2: What is a "Pivot Transaction" in a saga workflow, and how does it determine whether a step requires a compensating action?
**Answer:**
In a saga, the **Pivot Transaction** is the architectural point of no return:
- Every step **before** the pivot is a *Compensatable Transaction* (e.g., `ReserveInventory`, `HoldSeats`). If any pre-pivot step fails, all prior steps must execute compensating transactions in reverse order.
- The Pivot itself (e.g., `CaptureCreditCardPayment`) determines whether the saga aborts or completes. If payment fails, compensation begins. If payment succeeds, the saga is committed to completion.
- Every step **after** the pivot is a *Retryable Transaction* (e.g., `GenerateInvoice`, `NotifyWarehouse`). These steps **never have compensating actions**; they are guaranteed to eventually succeed through exponential retries, idempotent workers, and dead-letter queues.

---

### Q3: What is the fundamental difference between an ACID database rollback and a distributed compensating transaction?
**Answer:**
- **ACID Rollback**: A technical, physical rollback. The database engine reverts disk pages using write-ahead logs, rendering the transaction as if it never occurred. No intermediate state was ever exposed outside `SERIALIZABLE` or `READ COMMITTED` isolation.
- **Compensating Transaction**: A semantic, forward-moving business action. The intermediate state was already committed to an external database and was visible to users. A compensating action applies a *new* forward mutation that neutralizes the business impact (e.g. issuing a financial credit note, restoring an inventory count, or sending a cancellation email), preserving an immutable audit log.

---

### Q4: How do you handle a scenario where a compensating transaction itself fails permanently?
**Answer:**
1. **Exponential Retry with DLQ**: The compensation command (`ReleaseInventory`) must be retryable with exponential backoff and dead-letter queues. Most failures are transient network glitches.
2. **Idempotent Compensations**: The compensation handler must be strictly idempotent so repeated retries do not double-refund or double-increment inventory.
3. **Escalation to Human Intervention**: If automated retries exhaust their threshold, the saga enters a `NeedsManualIntervention` state, raises a P1 alert to SRE/Ops, and records the `CorrelationId` and transaction IDs in an operations dashboard for human financial reconciliation.

---

### Q5: How does MassTransit correlate asynchronous, out-of-order messages to a saga instance?
**Answer:**
MassTransit correlates messages using a common identifier declared via `Event(() => EventName, x => x.CorrelateById(m => m.Message.CorrelationId))`.
- When an event arrives, MassTransit queries the saga repository (e.g. PostgreSQL EF Core) by `CorrelationId`.
- If the event is an `Initially` event, it instantiates a new saga instance.
- If it is a subsequent event, MassTransit checks `During(CurrentState)`. If an event arrives out-of-order (e.g. `PaymentAuthorized` arrives before `InventoryReserved` due to message broker partition parallelism), MassTransit can be configured to buffer the message, ignore it, or declare lenient transitions across composite states.

---

### Q6: In high-throughput architectures, how do you prevent saga state persistence from becoming a database bottleneck?
**Answer:**
1. **Saga State Sharding**: Partition the saga database by `CorrelationId` hash to distribute write IO across database nodes.
2. **Redis Saga Persistence**: Use Redis key-value storage with optimistic locking (`WATCH` / Lua scripts) for ultra-fast, short-lived sagas.
3. **Optimistic Concurrency**: Avoid database table locks (`SELECT ... FOR UPDATE`); rely on row `Version` checks with automatic MassTransit retry on concurrency conflict.
4. **Finalized Saga Archival**: Configure `SetCompletedWhenFinalized()` to remove completed saga rows immediately from active operational tables and archive them asynchronously to cold storage.

---

### Q7: Explain the Routing Slip pattern in MassTransit. When is it preferred over a full State Machine Saga?
**Answer:**
- **Routing Slip (MassTransit Courier)**: An executable itinerary attached to a message that defines an ordered sequence of activities and compensating activities (e.g. Activity 1: Reserve, Compensation: Release). Each service executes its step, appends results, and forwards the routing slip to the next address without a centralized state database.
- **When preferred**: Linear, sequential workflows without complex branching, long waiting periods, or external user interaction. Routing slips require zero centralized database state mutations, maximizing throughput.

---

### Q8: How do you handle timeout escalation in long-running sagas?
**Answer:**
MassTransit integrates with scheduling engines (RabbitMQ delayed exchange plugin or Quartz.NET):
- Upon entering a state (`Submitted`), schedule a timeout event:
  ```csharp
  Schedule(() => OrderTimeout, instance => instance.TimeoutTokenId, x => x.Delay = TimeSpan.FromMinutes(15));
  ```
- If the happy-path event (`PaymentAuthorized`) arrives within 15 minutes, unschedule the timeout.
- If the timeout event fires first, transition to `TimedOut` and trigger compensating actions (`CancelOrder`, `ReleaseInventory`).

---

### Q9: Why is Bidirectional BFS mathematically more efficient than standard BFS in graph shortest-path problems?
**Answer:**
Let $b$ be the average branching factor and $d$ be the shortest path distance.
- **Unidirectional BFS**: Expands a single search tree to depth $d$. Total vertices evaluated $\approx \mathcal{O}(b^d)$.
- **Bidirectional BFS**: Simultaneously expands two trees from the source and destination until they meet at depth $d/2$. Total vertices evaluated $\approx \mathcal{O}(2 \times b^{d/2})$.
- For $b = 10$ and $d = 8$, unidirectional BFS visits $10^8 = 100,000,000$ vertices, while bidirectional BFS visits only $2 \times 10^4 = 20,000$ vertices—a $5,000\times$ reduction in time and memory.

---

### Q10: In Word Ladder (LeetCode #127), why is generating 26 character mutations per position faster than checking every word in the dictionary for 1-character edit distance?
**Answer:**
Let $N$ be the number of words in the dictionary (e.g. 10,000) and $L$ be the word length (e.g. 5).
- **Checking all dictionary words**: For each word, comparing edit distance with all $N$ words takes $\mathcal{O}(N \times L)$ operations per BFS step. Over all words, this takes $\mathcal{O}(N^2 \times L)$, which for $N = 10,000$ is $10^8 \times 5 = 500,000,000$ operations (Time Limit Exceeded).
- **Generating 26 mutations**: Replacing each character with 'a'..'z' generates $26 \times L$ candidates. Checking existence in a hash set is $\mathcal{O}(L)$ string hashing. Total cost per word is $\mathcal{O}(26 \times L^2)$. For $L = 5$, this is $26 \times 25 = 650$ operations per word, independent of $N$. This runs orders of magnitude faster.
