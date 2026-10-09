# Day 20 - Engineering Notes: Redis Streams Internals & Messaging Architecture

## 1. Redis Streams Internal Memory Layout: Radix Tree of ListPacks

Redis Streams are implemented internally using a **Radix Tree** (`rax.c`) where each leaf node stores a **ListPack** containing multiple message entries:

### Why Radix Tree + ListPack?
1. **Memory Compactness**: In a stream receiving 10,000,000 events, storing each event as a standalone Redis Object (`robj`) would incur 16 bytes of object header + 8 bytes of pointer metadata per entry (240 MB purely for pointers).
2. **Batching inside ListPack**: Multiple sequential messages are packed contiguously into a single ListPack memory buffer. Field keys (e.g. `"userId"`, `"timestamp"`) are deduplicated across messages within the same ListPack block.
3. **Approximate Trimming (`MAXLEN ~ N`)**:
   - `XADD stream MAXLEN 1000 ...` (exact trimming) forces Redis to prune exact entries, requiring expensive splitting and repacking of ListPacks.
   - `XADD stream MAXLEN ~ 1000 ...` (approximate trimming) allows Redis to drop entire ListPack blocks once all their entries fall outside the threshold, turning an $O(N)$ operation into an $O(1)$ memory release.

---

## 2. The Pending Entries List (PEL) & Memory Leak Hazards

### How PEL Operates Under the Hood
Each Consumer Group maintains two primary data structures:
1. **`last_id`**: The cursor pointing to the last message dispatched via `XREADGROUP >`.
2. **`PEL`**: A radix tree mapping `MessageID -> { ConsumerName, DeliveryTime, DeliveryCount }`.

### The "Forgotten XACK" Memory Leak
- Every message read by a consumer is placed in the PEL.
- Messages remain in the PEL indefinitely until an explicit **`XACK`** command is received.
- **Production Anti-Pattern**: If worker services consume messages, process them, and forget to call `XACK` (or crash before ACK), the stream data may be trimmed by `MAXLEN`, but the PEL entries are retained in RAM forever.
- In high-throughput ingestion pipelines (50,000 msgs/sec), forgotten ACKs will exhaust gigabytes of Redis memory within hours.

---

## 3. Redis Streams vs Kafka vs RabbitMQ

| Dimension | Redis Streams | Apache Kafka | RabbitMQ |
|---|---|---|---|
| **Primary Architecture** | In-memory append log (Radix Tree) | Distributed disk-backed commit log | Erlang AMQP message broker / exchange |
| **P99 Read/Write Latency** | **< 1 millisecond** (In-RAM) | 5–15 milliseconds (OS page cache) | 2–10 milliseconds |
| **Partitioning Model** | Key-level / Logical Consumer Groups | Physical Topic Partitions | Exchange / Queue routing bindings |
| **Max Scale / Retention** | Bounded by RAM (Gigabytes) | Bounded by Disk / S3 (Terabytes/Petabytes) | Bounded by Queue size |
| **Best Used For** | Real-time event streams, user activity feeds, microservice event bus | Large-scale telemetry, audit logs, analytics pipes | Complex transactional task routing & RPC |

---

## 4. DSA Invariant: BST LCA vs General Binary Tree LCA

### Why BST LCA is $O(H)$ and $O(1)$ Space
In a Binary Search Tree:
$$\forall x \in \text{Left}(root): x.val < root.val, \quad \forall y \in \text{Right}(root): y.val > root.val$$
Therefore:
- If both $p.val < root.val$ and $q.val < root.val$, the lowest common ancestor **must** lie exclusively in the left subtree.
- If both $p.val > root.val$ and $q.val > root.val$, the lowest common ancestor **must** lie exclusively in the right subtree.
- The very first node where $p$ and $q$ diverge (or when $root.val \in \{p.val, q.val\}$) is mathematically guaranteed to be their Lowest Common Ancestor.
This enables a zero-allocation single while-loop traversal in $O(H)$ time and $O(1)$ extra space, unlike general binary trees (LeetCode #236) which require post-order recursive tree scanning in $O(N)$ time.
