# Day 30 Deep Dive Notes: RabbitMQ DLQ, TTL Retry Topology & MassTransit Sagas

## 1. AMQP Dead-Letter Protocol & Wire Semantics

### The AMQP Dead-Letter Specification
Under the AMQP 0-9-1 specification, a message entering an AMQP queue can be transitioned to dead-letter status under three exact conditions:
1. **Explicit Negative Acknowledgment**:
   - `basic.reject(deliveryTag, requeue = false)`
   - `basic.nack(deliveryTag, multiple = false, requeue = false)`
   When `requeue = true`, the broker places the message back into its original position in the queue. When `requeue = false`, if `x-dead-letter-exchange` is declared, the message is immediately routed to that exchange; otherwise, it is silently dropped into the ether.
2. **Time-To-Live (TTL) Expiration**:
   - Queue-level TTL (`x-message-ttl`): Automatically configured on queue declaration.
   - Message-level TTL (`expiration` header): Declared per publication.
3. **Queue Length Overflow**:
   - `x-max-length`: Maximum count of ready messages.
   - `x-max-length-bytes`: Maximum total payload size in bytes.
   - `x-overflow`: Determines behavior when full (`drop-head`, `reject-publish`, or `reject-publish-dlx`).

### The `x-death` Header Architecture
When RabbitMQ dead-letters a message, it modifies the message headers by prepending a hash to the `x-death` array:
```json
{
  "x-death": [
    {
      "count": 1,
      "exchange": "orders.direct",
      "queue": "orders.work",
      "reason": "rejected",
      "routing-keys": ["orders.work"],
      "time": 1729000000
    }
  ]
}
```
**Critical Production Gotcha**: If a message transitions through multiple dead-letter hops (e.g. `work` $\to$ `retry.5s` $\to$ `work` $\to$ `retry.30s` $\to$ `dlq`), RabbitMQ updates the count for existing queue entries or inserts a new element at index 0. To calculate total historical retries across multiple queues, your inspection tool must sum the `count` fields across all elements matching the root processing queue.

---

## 2. RabbitMQ TTL Retry Pitfalls: Head-of-Line Blocking

### Why Variable Per-Message TTL Causes Head-of-Line Blocking
A common architectural mistake is using a single retry queue with variable per-message `expiration` headers:
```csharp
// ANTI-PATTERN:
await channel.BasicPublishAsync(exchange: "", routingKey: "orders.retry", mandatory: true, basicProperties: new BasicProperties { Expiration = "30000" }, body: body1);
await channel.BasicPublishAsync(exchange: "", routingKey: "orders.retry", mandatory: true, basicProperties: new BasicProperties { Expiration = "5000" }, body: body2);
```
**Why this fails**: In standard RabbitMQ classic and quorum queues, messages are evaluated for expiration **only when they reach the head of the queue**. If message 1 has a 30-second TTL and message 2 has a 5-second TTL, message 2 will NOT expire and dead-letter after 5 seconds! It remains trapped behind message 1 for the full 30 seconds.

### The Correct Production Solution: Multi-Stage Discrete Delay Queues
Create discrete queues with fixed queue-level `x-message-ttl`:
- `orders.retry.5s`: `x-message-ttl = 5000`, DLX $\to$ `orders.work`
- `orders.retry.30s`: `x-message-ttl = 30000`, DLX $\to$ `orders.work`
- `orders.retry.2m`: `x-message-ttl = 120000`, DLX $\to$ `orders.work`
- `orders.dlq`: Final poison holding queue (retained for manual inspection and replay).

Because all messages in a given queue have identical TTL, messages always expire in strict FIFO order, preventing Head-of-Line blocking entirely.

---

## 3. MassTransit Architecture & Saga State Machines

### The MassTransit Pipeline Engine
MassTransit builds an asynchronous message pipeline inspired by the `Russian Doll` middleware pattern (GreenPipes):
```
Transport (RabbitMQ) 
  ---> DeserializationFilter 
    ---> ScopedFilter (DI Scope per message) 
      ---> RetryFilter (In-memory backoff) 
        ---> RedeliveryFilter (Delayed exchange) 
          ---> OutboxFilter (Transactional Outbox) 
            ---> Consumer / Saga State Machine
```

### Saga Orchestration vs Choreography
- **Choreography**: Each service listens to domain events and decides its own local action without centralized coordination. Works well for simple 2-3 step workflows, but becomes impossible to debug and trace in complex flows.
- **Orchestration (MassTransit Saga State Machine)**: A centralized state coordinator (`OrderStateMachine`) listens to events, tracks global state in a persistent instance table, and dispatches direct commands (`ReserveInventory`, `AuthorizePayment`).

### Concurrency & Locking in Sagas
When concurrent events arrive for the same saga instance (e.g. `PaymentCompleted` and `InventoryUnavailable` dispatched simultaneously):
1. **Optimistic Concurrency**: MassTransit tracks a `RowVersion` or `Version` property. If two threads attempt to commit state transitions simultaneously, one commits and the second throws `DbUpdateConcurrencyException`. MassTransit's retry middleware automatically catches the concurrency exception and re-reads the updated saga state.
2. **Pessimistic Concurrency**: Acquires a database row lock (`SELECT ... FOR UPDATE`) before executing saga activity.

---

## 4. DSA Deep Dive: Topological Sorting

### Directed Acyclic Graph (DAG) Foundations
A Topological Sort of a directed graph $G = (V, E)$ is a linear ordering of its vertices such that for every directed edge $(u, v)$, node $u$ comes before $v$ in the ordering.
A graph admits a topological sort **if and only if it is a Directed Acyclic Graph (DAG)**.

### Kahn's Algorithm vs DFS 3-Coloring

| Characteristic | Kahn's Algorithm (BFS) | 3-Coloring (DFS) |
|---|---|---|
| **Underlying Mechanism** | In-degree queue | Post-order reversal via recursion stack |
| **Cycle Detection** | Count of dequeued nodes $< \|V\|$ | Directed back-edge to a node currently in state `1` (Visiting) |
| **Memory Footprint** | In-degree array + FIFO queue | Recursion stack ($\mathcal{O}(H)$ call stack) |
| **Output Order** | Forward direct topological order | Reverse post-order (must reverse or stack pop) |
| **Cycle Path Extraction** | Harder to extract cycle loop | Trivial to extract cycle path by tracing gray nodes |

### Complexity Analysis
- **Time Complexity**: $\mathcal{O}(V + E)$ where $V$ is `numCourses` and $E$ is the number of prerequisites. Every vertex is enqueued/visited once, and every directed edge is decremented/traversed exactly once.
- **Space Complexity**: $\mathcal{O}(V + E)$ to store the adjacency list representation, plus $\mathcal{O}(V)$ for in-degree array / state array and queue/stack.
