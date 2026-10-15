# Day 30: Senior & Staff Engineer Interview Scenarios & Answers
## Theme: RabbitMQ Dead-Letter Queues, Progressive Retries & MassTransit Sagas

---

### Q1: What is the exact mechanical difference between `basic.reject(requeue: true)` and `basic.reject(requeue: false)` with an AMQP Dead-Letter Exchange configured?
**Answer:**
- When a consumer calls `basic.reject(deliveryTag, requeue: true)`, RabbitMQ immediately re-inserts the unacknowledged message back into the **head of the same queue** (or its original position). If the consumer failure was caused by a persistent software bug or malformed payload, this triggers an infinite immediate retry loop (retry storm) that consumes 100% CPU and completely blocks all subsequent messages from processing (Head-of-Line blocking).
- When a consumer calls `basic.reject(deliveryTag, requeue: false)`, RabbitMQ checks if the queue has an `x-dead-letter-exchange` argument declared. If yes, the broker intercepts the message, injects an entry into its `x-death` diagnostic header, modifies its exchange/routing key as configured, and publishes it to the dead-letter exchange. If no DLX is configured, the message is permanently destroyed and discarded. In production, unhandled exceptions must always reject with `requeue: false` towards a configured DLX.

---

### Q2: Explain Head-of-Line blocking in RabbitMQ when using per-message TTL for delayed retries. How do you prevent it?
**Answer:**
RabbitMQ evaluates message expiration only when a message reaches the **head** of the queue. If a worker queue publishes message A with a 60-second TTL (`expiration: "60000"`) followed immediately by message B with a 5-second TTL (`expiration: "5000"`), message B will NOT dead-letter after 5 seconds. It is physically blocked behind message A. Only when message A's 60 seconds expire will message A dead-letter, at which point the broker inspects message B and discovers it has already expired.
To prevent this:
1. **Discrete Queue-Level TTL**: Do NOT use per-message TTL on a shared queue. Declare distinct queues with fixed queue-level `x-message-ttl` arguments (`retry.5s`, `retry.30s`, `retry.2m`). All messages in a given queue share the identical TTL and therefore expire in exact FIFO order.
2. **RabbitMQ Delayed Message Plugin**: Use the `x-delayed-message` exchange plugin, which stores delayed messages in an internal Erlang Mnesia database table and routes them only when their specific timer elapses.

---

### Q3: What is the operational difference between the RabbitMQ Delayed Message Exchange plugin and the DLX/TTL loop pattern?
**Answer:**
- **DLX/TTL Loop (Native AMQP)**: Requires zero plugins and runs on standard, unmodified RabbitMQ clusters. Messages sit in regular queues with no consumers. When TTL expires, the broker moves them via DLX. However, it requires predefined discrete delay intervals (e.g. 5s, 30s, 5m), and creating arbitrary per-message delay intervals is impossible without risking Head-of-Line blocking.
- **Delayed Message Plugin (`x-delayed-message`)**: Allows arbitrary per-message delay via the `x-delay` header (in milliseconds). The exchange stores messages in an internal Mnesia database on disk, bypassing queues entirely until expiration. However, because it relies on Mnesia, it does not scale well past ~100,000 delayed messages, does not support Quorum Queues directly at the exchange level, and complicates node replication and cluster upgrades.

---

### Q4: How does MassTransit differentiate between transient exceptions and poison messages? Explain `UseMessageRetry` vs `UseDelayedRedelivery`.
**Answer:**
- **`UseMessageRetry` (In-Memory Retry)**: Catches exceptions inside the consumer's executing scope without acknowledging or rejecting the message on the broker. The thread or task waits using in-memory timers (e.g. exponential backoff: 100ms, 200ms, 500ms). This is ideal for short, transient glitches (e.g., brief database deadlocks or network socket resets). However, holding the message in memory locks the consumer thread/prefetch slot.
- **`UseDelayedRedelivery` (Broker Delayed Retry)**: When in-memory retries are exhausted, `UseDelayedRedelivery` acknowledges or moves the message out of the main queue into a delayed queue (via RabbitMQ delayed-message exchange or quartz/hangfire). This releases the consumer thread and prefetch buffer to process subsequent messages. The message is republished to the consumer queue minutes or hours later.
- If delayed redelivery also exhausts its maximum attempts, MassTransit routes the poison message to a designated `_error` queue with serialized exception details in message headers.

---

### Q5: How does MassTransit solve concurrent saga state transitions when two asynchronous events arrive simultaneously for the same `CorrelationId`?
**Answer:**
In an asynchronous distributed architecture, multiple events (such as `InventoryReserved` and `PaymentCompleted`) may arrive concurrently at different consumer instances for the exact same saga instance.
MassTransit solves this via:
1. **Optimistic Concurrency Control**: The saga instance table includes a `Version` or `RowVersion` integer column. MassTransit issues an atomic update: `UPDATE SagaTable SET State = @NewState, Version = Version + 1 WHERE Id = @Id AND Version = @ExpectedVersion`. If a concurrent worker updated the version first, the second worker's update affects 0 rows, throwing a `DbUpdateConcurrencyException`. MassTransit's saga repository middleware catches this concurrency exception and automatically retries loading the fresh saga state and applying the second event.
2. **Pessimistic Locking**: Configured via `UseRowLock()` in EF Core (e.g. `SELECT ... FOR UPDATE`), which serializes event processing at the database transaction boundary.

---

### Q6: What is the structure of the `x-death` AMQP header in RabbitMQ, and how do you extract the root failure reason?
**Answer:**
When RabbitMQ dead-letters a message, it adds an `x-death` array of dictionary objects to the message's AMQP headers. Each element contains:
- `queue`: Name of the queue where the message died.
- `reason`: String enum: `"rejected"`, `"expired"`, or `"maxlen"`.
- `count`: How many times the message died in that specific queue.
- `exchange`: The original exchange the message was published to.
- `time`: Unix timestamp when the dead-lettering occurred.

If a message travels through multiple retry queues, `x-death` contains multiple entries. To find the root queue where failure first started, inspect the last element in the array or match the non-retry queue name. To get the total number of retries across a specific queue, read the `count` property.

---

### Q7: When should you choose Orchestration (Saga State Machine) over Choreography in microservices?
**Answer:**
- **Choose Choreography** when:
  - The workflow is short and simple (2 to 3 bounded contexts).
  - There are no complex branching logic, timeout deadlines, or multi-step compensating actions.
  - Services are loosely coupled and publish generic domain events (`OrderPlaced`, `CustomerBilled`).
- **Choose Orchestration (State Machine)** when:
  - The business process involves 4+ services, conditional branching, or time-based escalation (e.g., "if payment not received within 15 minutes, cancel order and release inventory").
  - Compensating transactions are required upon failure (rolling back steps 2 and 1 if step 3 fails).
  - Centralized visibility, auditability, and operational tracking of workflow state are mandatory for compliance or customer support.

---

### Q8: How do you safely implement DLQ re-drive (replaying dead-lettered messages back to production) without triggering duplicate processing?
**Answer:**
1. **Fix Root Cause First**: Never re-drive messages while the underlying bug, schema incompatibility, or downstream outage exists.
2. **Consumer Idempotency**: All consumers must implement the Idempotent Consumer / Inbox pattern using a unique `MessageId` or business deduplication key stored in an atomic database table.
3. **Strip Diagnostic Headers**: When transferring messages from `orders.dlq` back to `orders.work`, strip or reset the `x-death` and retry count headers so the message starts with a clean retry budget.
4. **Controlled Rate / Shovel**: Use RabbitMQ Shovel or a rate-limited re-drive worker rather than a bulk instantaneous dump to prevent overwhelming downstream databases.

---

### Q9: Explain Kahn's algorithm for topological sorting. Why does an unprocessed vertex count prove the existence of a directed cycle?
**Answer:**
Kahn's algorithm operates on the topological property that in any Directed Acyclic Graph (DAG), there must be at least one vertex with an in-degree of 0 (no dependencies).
1. Compute the in-degree of all vertices.
2. Enqueue all vertices with `inDegree == 0`.
3. Repeatedly dequeue a vertex $u$, add it to the topological order, and decrement the in-degree of all its outbound neighbors $(u, v)$.
4. Whenever a neighbor's in-degree drops to 0, enqueue it.
5. **Cycle Proof**: If the graph contains a directed cycle (e.g. $A \to B \to C \to A$), every node in that cycle has an in-degree $\ge 1$ from within the cycle. Therefore, none of the vertices in the cycle can ever reach an in-degree of 0, and none will ever be enqueued. If the total count of processed vertices is less than the total vertices $|V|$, the graph strictly contains at least one directed cycle.

---

### Q10: How do you handle a partial failure during a Saga compensation (e.g. inventory rollback succeeds, but payment refund API fails)?
**Answer:**
This is the fundamental reality of distributed systems: compensating actions can themselves fail.
1. **Exponential Retry with DLQ**: The compensation command (`RefundPayment`) is published through the same reliable messaging pipeline with exponential backoff and a dedicated dead-letter queue.
2. **Idempotent Compensations**: Compensation endpoints must be idempotent. If the compensation retry executes three times, the customer must only be refunded once.
3. **Escalation to Human Workflow (Manual Triage)**: If automated compensation fails after reaching maximum retries, the saga instance enters a `NeedsManualIntervention` state, publishes a critical P1 telemetry alert to Ops, and logs the saga `CorrelationId` and transaction IDs to an administrative console for human reconciliation. Distributed systems cannot guarantee 100% automated recovery for non-idempotent external third-party failures; automated retries combined with human escalation queues ensure no funds or data are silently lost.
