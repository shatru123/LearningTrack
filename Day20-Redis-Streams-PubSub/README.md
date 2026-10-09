# Day 20: Redis Pub/Sub & Redis Streams

## Overview
Comprehensive exploration and production implementation of Redis messaging patterns: comparing ephemeral fire-and-forget **Redis Pub/Sub** against persistent, log-structured **Redis Streams**. We implement end-to-end event ingestion pipelines using Consumer Groups (`XGROUP CREATE`), partitioned consumption (`XREADGROUP`), atomic acknowledgments (`XACK`), in-flight message state tracking via the Pending Entries List (`XPENDING`), and dead-letter / crashed consumer recovery (`XCLAIM`). Additionally, we solve tree algorithms: Lowest Common Ancestor of a BST (LeetCode #235) in $O(H)$ and Binary Tree Level Order Traversal (LeetCode #102) in $O(N)$ BFS.

---

## Architectural Breakdown

### 1. Redis Pub/Sub vs Redis Streams Comparison

| Feature | Redis Pub/Sub | Redis Streams |
|---|---|---|
| **Persistence** | None (in-memory fire-and-forget) | Append-only log on disk (RDB/AOF) |
| **Delivery Guarantee** | At-most-once (unconnected clients lose messages) | At-least-once (ACK + PEL tracking) |
| **Consumer Scaling** | Every subscriber gets every message (broadcast only) | Consumer Groups partition messages across workers |
| **Backpressure / Retention**| None (subscriber buffer overflows crash clients) | `MAXLEN ~` trims stream log to target capacity |
| **Replay / Time-Travel** | Impossible | Supported via `XRANGE` and entry IDs |
| **Underlying Data Structure**| In-memory channel subscriber dictionary | Radix Tree containing encoded ListPacks |

---

### 2. Redis Streams Ingestion Lifecycle

```
Producer                     Stream: orders_stream                Consumer Group: order_processors
+---------+                    +-----------------+                     +----------------------+
|  XADD   | -----------------> | 1728500000-0    | ------------------> | Worker A (reads msg) |
| (event) |                    | 1728500000-1    |                     |   -> Added to PEL    |
+---------+                    | 1728500000-2    |                     +----------------------+
                               +-----------------+                                |
                                        |                                          v (Crash / Timeout)
                                        |                              +----------------------+
                                        +----------------------------> | Worker B             |
                                                                       |   -> XCLAIM recovers |
                                                                       |   -> XACK completes  |
                                                                       +----------------------+
```

1. **`XADD key [MAXLEN ~ N] ID field value ...`**:
   - Appends message to the stream. By default, generates `{timestamp_ms}-{sequence}` ID.
2. **`XGROUP CREATE key groupname ID`**:
   - Creates a consumer group with a dedicated cursor and Pending Entries List (PEL).
3. **`XREADGROUP GROUP group consumer [COUNT n] STREAMS key >`**:
   - Reads unconsumed messages (`>`) and assigns them to the requesting consumer, logging them in the PEL.
4. **`XACK key group ID ...`**:
   - Acknowledges processing; removes the ID from the group's PEL.
5. **`XPENDING key group [[IDLE minIdle] start end count]`**:
   - Inspects in-flight unacknowledged messages, showing idle time and delivery count.
6. **`XCLAIM key group consumer minIdleTime ID ...`**:
   - Transfers ownership of unacknowledged messages whose idle time exceeds a threshold from a dead consumer to a healthy consumer.

---

### 3. Tree Algorithms Solved

#### LeetCode #235: Lowest Common Ancestor of a BST
- **Problem**: Find the Lowest Common Ancestor of nodes $p$ and $q$ in a Binary Search Tree.
- **Algorithm**: Leverage BST ordering: if both $p$ and $q$ are smaller than root, traverse left; if both are greater, traverse right; otherwise, current node is the LCA split point.
- **Complexity**: Time: $O(H)$ ($O(\log N)$ balanced, $O(N)$ skewed), Space: $O(1)$ iterative.

#### LeetCode #102: Binary Tree Level Order Traversal
- **Problem**: Return node values level-by-level from left to right.
- **Algorithm**: Standard BFS queue. At each level, capture `queue.Count`, dequeue and collect that many nodes, and enqueue their children.
- **Complexity**: Time: $O(N)$, Space: $O(W) \approx O(N)$ width of tree.

---

## Running Unit Tests

```bash
dotnet test Day20-Redis-Streams-PubSub/tests/Day20.RedisStreams.Tests/Day20.RedisStreams.Tests.csproj
```
