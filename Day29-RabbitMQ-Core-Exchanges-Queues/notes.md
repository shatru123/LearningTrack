# Day 29: Deep Dive Notes — RabbitMQ Core Concepts: Exchanges & Queues

## 1. AMQP 0-9-1 Architectural Model

RabbitMQ implements the **Advanced Message Queuing Protocol (AMQP 0-9-1)**, an open standard wire-level protocol for enterprise asynchronous messaging.

```
+----------------------------------------------------------------------------------------------------+
|                                    AMQP 0-9-1 MESSAGING TOPOLOGY                                   |
+----------------------------------------------------------------------------------------------------+
|                                                                                                    |
|    [ Producer ]                                                                                    |
|         |                                                                                          |
|    (Publish with RoutingKey)                                                                       |
|         v                                                                                          |
|    +------------------------------------------------------------------------------------------+    |
|    |                                        EXCHANGE                                          |    |
|    |------------------------------------------------------------------------------------------|    |
|    |  • Direct: Exact match                                                                   |    |
|    |  • Topic: Wildcard patterns (* = 1 word, # = 0+ words)                                   |    |
|    |  • Fanout: Unconditional broadcast                                                       |    |
|    |  • Headers: x-match all/any metadata                                                     |    |
|    +------------------------------------------------------------------------------------------+    |
|               |                                      |                                             |
|          (Binding 1)                            (Binding 2)                                        |
|               v                                      v                                             |
|    +---------------------+                +---------------------+                                  |
|    |     Work Queue      |                |    Audit Log Queue  |                                  |
|    | (PrefetchCount = 1) |                | (PrefetchCount = 5) |                                  |
|    +---------------------+                +---------------------+                                  |
|         |          |                                 |                                             |
|      (Ack)      (Nack + requeue=false)            (Ack)                                            |
|         |          v                                 |                                             |
|         |     +-------------------------+            |                                             |
|         |     | Dead Letter Exchange    |            |                                             |
|         |     | (DLX -> Poison Queue)   |            |                                             |
|         |     +-------------------------+            |                                             |
|         v                                            v                                             |
|    [ Consumer 1 ]                             [ Consumer 2 ]                                       |
|                                                                                                    |
+----------------------------------------------------------------------------------------------------+
```

### Connection vs. Channel
- **Connection**: A persistent TCP/IP socket between the client application and RabbitMQ broker. Establishing a connection involves a full TCP handshake, TLS negotiation, and authentication challenge (expensive).
- **Channel**: A lightweight virtual connection multiplexed inside a single physical TCP connection. Applications open and close hundreds of channels concurrently for publishing and consuming, completely avoiding TCP handshake overhead.

---

## 2. The 4 AMQP Exchange Types

An exchange acts as an internal message router. Producers **never** send messages directly to queues; they publish to an Exchange with an optional **Routing Key**.

### 1. Direct Exchange
Routes messages to queues whose **Binding Key** exactly matches the message's **Routing Key**.
- Example:
  - Queue A bound with `orders.created`
  - Queue B bound with `orders.cancelled`
  - Publishing with `orders.created` goes exclusively to Queue A.

### 2. Fanout Exchange
Routinely ignores routing keys and duplicates incoming messages to **all queues** bound to the exchange.
- Blazingly fast ($\mathcal{O}(1)$ routing table lookup).
- Ideal for publish-subscribe broadcast patterns (e.g., distributed cache invalidation, live price feeds).

### 3. Topic Exchange
Performs routing based on dot-delimited tokens in the routing key using wildcard tokens:
- `*` (asterisk): Matches **exactly one** word.
- `#` (hash): Matches **zero or more** words.
- Examples:
  - `orders.eu.*`: Matches `orders.eu.created`, `orders.eu.paid`; does NOT match `orders.eu.b2b.created`.
  - `orders.#`: Matches `orders`, `orders.us`, `orders.us.california.expedited`.
  - `#.critical`: Matches any routing key ending with `.critical`.

### 4. Headers Exchange
Routes messages based on message header attributes rather than the routing key string.
- Uses `x-match`:
  - `x-match = all`: Every header key/value pair specified in the binding must be present in the message.
  - `x-match = any`: At least one matching header key/value pair routes the message.

---

## 3. Reliability, Confirmations & DLX

### 1. Publisher Confirms
In standard fire-and-forget publishing, a producer does not know if a message was successfully accepted, written to disk, or rejected by the broker.
- **Publisher Confirms**: Once enabled on a channel, the broker asynchronously sends an `Ack` back to the publisher with the message's sequence number once:
  1. The message is persisted to disk on all durable queues, or
  2. The message is consumed and acknowledged by downstream workers.
- If the broker encounters disk full errors or internal node crashes, it emits a `Nack`.

### 2. Consumer Acknowledgments (Ack / Nack)
- `autoAck = true`: The broker removes the message from the queue the instant it sends it over the socket. If the worker process crashes before processing the payload, the message is permanently lost.
- `autoAck = false` (Manual Ack):
  - `basic.ack(delivery_tag)`: Worker successfully processed the message; broker removes it from memory.
  - `basic.nack(delivery_tag, requeue = true)`: Worker encountered a transient failure (e.g., DB lock timeout); broker re-enqueues the message at the head of the queue.
  - `basic.nack(delivery_tag, requeue = false)`: Poison message; discarded or forwarded to a Dead Letter Exchange.

### 3. Dead Letter Exchange (DLX)
Queues configured with `x-dead-letter-exchange` automatically re-route messages to the DLX when:
1. The message is rejected via `basic.nack(requeue: false)` or `basic.reject(requeue: false)`.
2. The message's Time-To-Live (`x-message-ttl`) expires in the queue.
3. The queue reaches its maximum capacity (`x-max-length`).

---

## 4. Consumer QoS & Fair Dispatch

By default, RabbitMQ dispatches messages round-robin in large batches to all connected consumers without checking how many unacknowledged messages each consumer currently holds.
- **The Problem**: If Consumer A gets heavy, slow jobs while Consumer B gets quick jobs, Consumer A becomes saturated with hundreds of queued messages while Consumer B sits idle.
- **The Solution (`basic.qos(prefetch_count)`)**:
  - `prefetch_count = 1`: Tells RabbitMQ never to give more than 1 unacknowledged message to a worker at a time.
  - RabbitMQ only assigns a new message to that worker after receiving `basic.ack`.
  - Achieves optimal load leveling across heterogeneous workers.

---

## 5. Graph Algorithms: LeetCode #130 & #994

### LeetCode #130: Surrounded Regions (Medium)
- **Problem**: Given an $m \times n$ matrix containing `'X'` and `'O'`, flip all `'O'`s that are 4-directionally surrounded by `'X'` into `'X'`.
- **Key Insight**: Any `'O'` connected to the border can never be surrounded.
- **Reverse Algorithm**:
  1. Iterate along the 4 borders of the matrix (row $0$, row $m-1$, col $0$, col $n-1$).
  2. For every `'O'` on the boundary, run DFS/BFS to mark all connected `'O'`s as a safe sentinel character (`'#'`).
  3. Scan the entire grid:
     - Flip all remaining `'O'`s to `'X'` (these are truly surrounded).
     - Restore all `'#'` back to `'O'` (these were border-connected).
  - **Complexity**: $\mathcal{O}(M \times N)$ time, $\mathcal{O}(M \times N)$ space.

### LeetCode #994: Rotting Oranges (Medium)
- **Problem**: In an $m \times n$ grid of 0 (empty), 1 (fresh), and 2 (rotten), every minute any fresh orange adjacent to a rotten orange becomes rotten. Return minimum minutes until no fresh oranges remain, or -1.
- **Algorithm (Multi-Source BFS)**:
  1. Scan the grid to enqueue all initially rotten oranges (2) into a FIFO queue and count the total number of fresh oranges (1).
  2. If `freshCount == 0`, return `0` immediately.
  3. While queue is not empty and `freshCount > 0`:
     - Increment `minutes`.
     - Process the current level snapshot (size of queue at this minute).
     - For each rotten orange, inspect 4 cardinal neighbors. If neighbor is fresh (1):
       - Mark as rotten (2), decrement `freshCount`, and enqueue.
  4. If `freshCount == 0`, return `minutes`; else return `-1` (unreachable fresh oranges).
  - **Complexity**: $\mathcal{O}(M \times N)$ time, $\mathcal{O}(M \times N)$ space.
