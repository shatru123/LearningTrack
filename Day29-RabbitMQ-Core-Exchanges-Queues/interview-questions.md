# Day 29: Senior & Staff Engineer Interview Questions & Answers

### 1. What are the core architectural trade-offs between RabbitMQ and Apache Kafka?
**Answer:**
| Architectural Dimension | RabbitMQ | Apache Kafka |
|---|---|---|
| **Architectural Model** | **Smart Broker / Dumb Consumer**: Broker tracks message state, delivery tags, queue indexes, and consumer acks. Deletes messages upon acknowledgment. | **Dumb Broker / Smart Consumer**: Broker is an immutable distributed commit log. Consumers track their own offsets. Messages persist until retention policy expires. |
| **Routing Flexibility** | Complex in-broker routing (Direct, Topic, Fanout, Headers, Dead-lettering). | Partition key hashing (`Hash(Key) % Partitions`). Filtering happens client-side. |
| **Ordering Guarantees** | FIFO per queue. Concurrency (`prefetch > 1` or multiple consumers) breaks strict order. | Strict FIFO order guaranteed within each partition. |
| **Throughput & Scale** | Tens to hundreds of thousands of messages/sec. Memory-intensive broker state. | Millions of messages/sec. Sequential disk I/O, zero-copy OS page cache (`sendfile`). |
| **Replayability** | No message replay (once acked, it is gone). | High replayability (consumers can reset offsets to reprocess historic logs). |
| **Best Suited For** | Complex task routing, microservices commands, RPC, dynamic work queues. | High-volume telemetry, clickstreams, event sourcing, CDC stream processing. |

---

### 2. How do modern RabbitMQ Quorum Queues (Raft Consensus) overcome the vulnerabilities of Classic Mirrored Queues?
**Answer:**
- **Classic Mirrored Queues (Deprecated)**:
  - Relied on a proprietary primary-mirror synchronization mechanism.
  - Vulnerable to network partitions (Split-Brain). When a partition healed, non-synchronized mirrors either dropped messages or blocked the primary while replicating the entire queue from scratch, causing cluster-wide latency pauses.
- **Quorum Queues (RabbitMQ 3.8+)**:
  - Implemented using the formal **Raft Consensus Algorithm**.
  - A quorum queue consists of a Raft leader and followers across cluster nodes.
  - State replication requires acknowledgment from a majority of nodes ($\lfloor N/2 \rfloor + 1$) before confirming publishes to clients.
  - Tolerates $F$ node failures in an $2F+1$ node cluster with zero message loss and deterministic leader election without cluster-wide stalling.

---

### 3. What is the impact of `basic.qos(prefetch_count)` on consumer throughput, latency, and memory footprint?
**Answer:**
`prefetch_count` dictates how many unacknowledged messages the broker can deliver to a consumer channel before waiting for an ACK.
- **Prefetch = 0 (Unlimited)**:
  - The broker pushes all available queue messages over the TCP connection into the consumer process's RAM buffer.
  - *Risk*: Consumer process experiences Out-Of-Memory (OOM) crashes under high volume. Fast workers starve while slow workers buffer thousands of messages.
- **Prefetch = 1 (Strict Fair Dispatch)**:
  - The broker delivers exactly one message and waits for an ACK before sending another.
  - *Advantage*: Maximum work balancing across uneven workloads.
  - *Disadvantage*: High network round-trip time (RTT) latency overhead. The consumer thread remains idle between ACKing message $N$ and receiving message $N+1$.
- **Production Sweet Spot (`prefetch_count = 50–200`)**:
  - Pipelining allows the consumer to process incoming messages continuously while prior ACKs travel back over the wire, optimizing throughput without risking memory saturation.

---

### 4. How do Publisher Confirms guarantee zero message loss during broker node crashes and disk flushes?
**Answer:**
When a producer publishes a message without confirms, the socket write succeeds the moment bytes leave the client OS network buffer, giving no guarantee that the broker received, routed, or stored the message.

**With Publisher Confirms:**
1. The client issues `confirm.select` on the AMQP channel.
2. The broker assigns an incrementing 64-bit integer sequence number to each incoming message on that channel.
3. The broker asynchronously returns `basic.ack(sequenceNumber)` to the publisher only after:
   - For a persistent message on a durable queue: The message has been written and fsynced to disk (or replicated to a Raft quorum).
   - If the queue is transient or already consumed: The message is accepted by the consumer.
4. If a disk write fails, the broker returns `basic.nack(sequenceNumber)`.
5. The publisher maintains a pending dictionary of unconfirmed sequence numbers and retries or alerts upon receiving Nack or a confirmation timeout.

---

### 5. What causes "Unacknowledged Message Creep" in RabbitMQ, and how does it manifest in production?
**Answer:**
- **The Cause**:
  When consumer applications consume messages with `autoAck = false` but fail to call `basic.ack` or `basic.nack` due to:
  1. An unhandled exception that bypasses the acknowledgment block.
  2. Infinite loops or asynchronous deadlocks in worker processing logic.
  3. Swallowing exceptions without invoking `basic.nack(requeue: false)`.
- **The Impact**:
  - The broker must keep unacknowledged messages stored in RAM and tracked in memory metadata in case the consumer disconnects and messages must be re-queued.
  - As unacked messages accumulate into the millions, broker RAM consumption hits the **High Memory Watermark** (`vm_memory_high_watermark`).
  - RabbitMQ initiates alarms, blocks all incoming publishers (TCP backpressure), and halts message throughput across the entire cluster.

---

### 6. How do you implement non-blocking exponential backoff retries using Dead Letter Exchanges (DLX) in RabbitMQ?
**Answer:**
Directly retrying failed messages with `basic.nack(requeue: true)` immediately puts the message at the front of the same queue. If the failure is persistent (e.g., database down), the worker immediately re-consumes it, causing a 100% CPU busy-spin loop that blocks all other legitimate queue messages.

**Dead-Letter Exponential Backoff Topology:**
1. **Main Queue** (`orders.queue`): Consumed by application workers.
2. When processing fails, worker calls `basic.nack(requeue: false)`.
3. The main queue's `x-dead-letter-exchange` forwards the message to a **Retry Exchange**.
4. The message routes to a series of delay queues with Message TTL (e.g. `retry.10s`, `retry.60s`, `retry.5m`):
   - Delay queues have **no consumers**.
   - Configured with `x-message-ttl: 10000` and `x-dead-letter-exchange: main.exchange`.
5. When the 10-second TTL expires, RabbitMQ automatically dead-letters the message *back* into the Main Queue for another processing attempt without blocking any concurrent traffic.
6. Messages exceeding maximum retry counts are forwarded to a permanent `orders.poison.dlq`.

---

### 7. How does a Topic Exchange match routing keys, and what is the difference between `*` and `#`?
**Answer:**
Routing keys in AMQP Topic Exchanges consist of dot-delimited words (e.g., `us.shipping.express`).
- `*` (asterisk): Substitutes for **exactly one** word.
  - Pattern: `*.shipping.*` matches `us.shipping.express` and `eu.shipping.standard`.
  - Does NOT match `shipping.express` or `us.west.shipping.express`.
- `#` (hash): Substitutes for **zero or more** words.
  - Pattern: `orders.#` matches `orders`, `orders.created`, `orders.us.california.b2b.priority`.
- **Internal Optimization**:
  RabbitMQ indexes topic bindings using a **Trie (Prefix Tree)** data structure. Incoming routing keys traverse the trie in $\mathcal{O}(K)$ time where $K$ is the number of words in the routing key, rather than performing slow sequential regex evaluations across thousands of queues.

---

### 8. When should an architect choose a Headers Exchange over a Topic Exchange?
**Answer:**
- **Topic Exchange Limitations**:
  - Requires a single linear dot-separated hierarchy (e.g., `format.vendor.region`).
  - Cannot cleanly express non-hierarchical, multi-dimensional queries. For example, if a message has format `json`, region `us`, vendor `acme`, and encrypted `true`, routing keys become combinatorial (`json.us.acme.true`).
- **Headers Exchange Advantages**:
  - Operates on arbitrary key-value metadata dictionaries.
  - Supports `x-match: all` (AND logic) and `x-match: any` (OR logic).
  - *Best Suited For*: Multi-tenant architectures where messages have disparate dimensions (e.g., `tenant_id`, `environment: staging`, `data_classification: pii`), or when the payload structure naturally maps to HTTP headers.

---

### 9. For LeetCode #130 (*Surrounded Regions*), why is reverse boundary flood fill superior to searching from every interior cell?
**Answer:**
- **Forward Interior Search**:
  - Testing each `'O'` cell requires checking whether its connected component can reach any boundary.
  - If a large connected component of size $K$ does not touch the boundary, we must track and flip all its cells. If it does touch the boundary, all explored paths must be retained without flipping.
  - Requires maintaining temporary component lists, tracking visited states, and handling multiple rollbacks.
- **Reverse Boundary Flood Fill**:
  - We observe that **only** cells connected to the boundary survive.
  - We initiate DFS/BFS strictly from the boundary `'O'` cells (at most $2M + 2N$ perimeter cells).
  - Any cell reached is marked with a safe sentinel (`'#'`).
  - A single linear $\mathcal{O}(M \times N)$ pass flips all remaining `'O'`s (which are guaranteed to be captured) to `'X'`, and restores `'#'` to `'O'`.
  - Zero rollback overhead and optimal cache-friendly linear memory access.

---

### 10. For LeetCode #994 (*Rotting Oranges*), why must the contagion simulation be solved with Multi-Source BFS rather than DFS?
**Answer:**
- **Multi-Source BFS**:
  - BFS explores a graph in concentric waves of equal distance (Level-by-Level).
  - In Rotting Oranges, all initially rotten oranges spread infection **simultaneously in parallel** at minute $1$, minute $2$, etc.
  - By enqueuing all initial rotten oranges into the BFS queue at minute $0$, the BFS naturally simulates this simultaneous parallel expansion. The number of BFS level expansions equals the exact minimum minutes required.
- **Why DFS Fails**:
  - DFS explores one deep path to its termination before backtracking.
  - A single rotten orange might take a 20-step path to rot an orange that was actually adjacent to another rotten orange that would have reached it in minute 1.
  - DFS cannot easily calculate the minimum parallel time without complex, expensive minimum timestamp relaxation across all cells.
