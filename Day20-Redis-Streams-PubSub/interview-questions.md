# Day 20: Senior / Staff-Level Interview Questions & Deep Answers

### Q1: What is the architectural difference between Redis Pub/Sub and Redis Streams, and when should you choose one over the other?
- **Short answer**: Redis Pub/Sub is an ephemeral fire-and-forget broadcast mechanism with zero disk persistence; Redis Streams is an append-only, disk-persisted log supporting consumer groups, offsets, acknowledgments, and replayability.
- **Deeper answer**:
  - **Pub/Sub**: Operates entirely in memory. When a publisher emits a message to a channel, Redis pushes it to currently connected subscriber TCP sockets. If a subscriber is disconnected or restarting, the message is permanently lost. All subscribers receive identical copies. Best for real-time notifications, chat broadcasts, and ephemeral cache invalidation signals.
  - **Streams**: Messages are appended to a persistent Radix tree on disk. Messages persist regardless of whether consumers are active. Consumer groups partition messages across worker pools so that each event is processed by only one worker in the group. Supports `XACK` and historical replay via `XRANGE`. Best for reliable event-driven microservices, order processing pipelines, and audit event logs.

---

### Q2: How does Redis Streams work partitioning compare to Apache Kafka?
- **Short answer**: Kafka binds physical partitions 1:1 to consumer group workers, limiting concurrency to partition count; Redis Streams dynamically distributes messages across any number of workers from a single logical stream.
- **Deeper answer**:
  In Kafka, if a topic has 8 partitions, a consumer group can have at most 8 active consumers; adding a 9th consumer leaves it idle.
  In Redis Streams:
  - Any number of consumers can join a consumer group dynamically.
  - Calling `XREADGROUP GROUP group worker_N COUNT 10 STREAMS stream >` pops the next 10 unassigned messages regardless of which worker requests them.
  - This provides elastic, fine-grained load leveling without requiring upfront physical topic partitioning. However, total write throughput is bounded by a single Redis node's CPU, whereas Kafka distributes writes across multiple broker nodes.

---

### Q3: What is the Pending Entries List (PEL) in Redis Streams, and what is the "Forgotten XACK" hazard?
- **Short answer**: The PEL tracks in-flight messages delivered to consumers that have not yet been acknowledged; failing to execute `XACK` causes unbounded memory growth because PEL entries are retained in RAM indefinitely.
- **Deeper answer**:
  When a consumer reads messages via `XREADGROUP >`, Redis inserts an entry into the group's internal PEL storing the message ID, consumer name, delivery count, and last delivery timestamp.
  Even if the stream is capped with `MAXLEN ~ 10000`, Redis cannot delete PEL metadata for unacknowledged messages. If consumer code catches exceptions or finishes without calling `XACK`, millions of unacknowledged entries accumulate in the PEL, gradually consuming all available Redis RAM and triggering an Out-Of-Memory (OOM) crash.

---

### Q4: How does dead-letter and worker crash recovery work in Redis Streams using `XPENDING` and `XCLAIM`?
- **Short answer**: Workers periodically query `XPENDING` to find messages whose idle time exceeds a threshold (e.g., 30s) from dead workers, then use `XCLAIM` to transfer ownership to an active worker.
- **Deeper answer**:
  1. A background watchdog thread in a surviving worker executes:
     `XPENDING stream group - + 50` or `XPENDING stream group IDLE 30000 - + 50`
     to detect messages that have been sitting unacknowledged longer than 30 seconds.
  2. The worker calls:
     `XCLAIM stream group active_worker_2 30000 <message_id>`
  3. Redis verifies the message has been idle for at least 30,000 ms, reassigns its owner to `active_worker_2`, increments its `delivery_count`, and returns the message payload.
  4. If `delivery_count` exceeds a poison-pill limit (e.g., 5 attempts), the worker routes the message to a Dead Letter Queue (DLQ) and calls `XACK` to prevent infinite crash loops.

---

### Q5: Why is approximate trimming (`MAXLEN ~ N`) recommended over exact trimming (`MAXLEN N`)?
- **Short answer**: Approximate trimming prunes entire ListPack macro-nodes in $O(1)$ time, while exact trimming requires CPU-intensive parsing and reallocation of memory buffers to delete exact counts.
- **Deeper answer**:
  Redis Streams group dozens of entries inside contiguous ListPack buffers.
  - When exact `MAXLEN N` is specified, if the cutoff falls in the middle of a ListPack, Redis must allocate a new memory buffer, copy remaining items, and free the old buffer. Under 50,000 writes/sec, this creates severe CPU spikes and memory fragmentation.
  - Adding the tilde `MAXLEN ~ N` tells Redis: "Only evict when a full ListPack can be released." Redis simply drops the pointer to the old ListPack block in constant $O(1)$ time with zero memory repacking.

---

### Q6: How do you achieve exactly-once semantics when consuming from Redis Streams?
- **Short answer**: Redis Streams provides at-least-once delivery; exactly-once semantics requires an idempotent consumer or transactional deduplication table in the downstream data store.
- **Deeper answer**:
  Because network failures or consumer crashes can occur after a database write but before the `XACK` is sent to Redis, messages can be redelivered via `XCLAIM`.
  To ensure exactly-once processing:
  1. **Idempotent mutations**: Design operations such that repeating them produces identical state (e.g. `SET balance = 100` rather than `INCR balance 10`).
  2. **Deduplication Key Table**: When writing to the relational database, write the `StreamMessage.Id` to a `processed_events` table within the same SQL transaction:
     `INSERT INTO processed_events (event_id) VALUES (@streamId);`
     If the write throws a unique constraint violation, the consumer immediately calls `XACK` and safely skips re-executing the business logic.

---

### Q7: What happens when a slow subscriber cannot keep up with high-frequency Redis Pub/Sub traffic?
- **Short answer**: Redis buffers unconsumed messages in the client's output buffer until `client-output-buffer-limit` is exceeded, at which point Redis abruptly kills the client socket.
- **Deeper answer**:
  In Redis Pub/Sub, the broker does not pause producers for slow consumers.
  Instead, pending messages accumulate in the client's memory output buffer on the Redis server. Redis enforces buffer safety via:
  `client-output-buffer-limit pubsub 32mb 8mb 60`
  If a subscriber's buffer exceeds 32 MB hard limit, or stays above 8 MB for 60 seconds continuously, Redis unilaterally severs the TCP connection. The subscriber disconnects, misses all subsequently broadcasted events, and reconnects in an un-synced state.

---

### Q8: What internal data structure powers Redis Streams and why?
- **Short answer**: A Radix Tree (`rax`) containing ListPacks; it enables logarithmic ID seeks by timestamp, sequential iteration, and compact memory utilization via field deduplication.
- **Deeper answer**:
  A pure doubly-linked list would waste 24 bytes of pointer RAM per entry. A hash table does not support range scans (`XRANGE`).
  Redis uses a Radix Tree whose keys are stream IDs (timestamps). Each leaf node is a contiguous byte buffer called a **ListPack**.
  Field keys (like `"sensorId"`, `"temperature"`) that repeat across consecutive events are stored once in the ListPack header and referenced by offset, saving up to 70% RAM compared to standard JSON or hash table storage.

---

### Q9: In LeetCode #235 (Lowest Common Ancestor of a BST), why does the iterative solution use $O(1)$ space and $O(H)$ time?
- **Short answer**: The BST ordering invariant lets you determine whether the LCA is in the left or right subtree with simple value comparisons, eliminating recursive call stack overhead.
- **Deeper answer**:
  ```csharp
  public TreeNode LowestCommonAncestor(TreeNode root, TreeNode p, TreeNode q) {
      TreeNode curr = root;
      while (curr != null) {
          if (p.val < curr.val && q.val < curr.val) curr = curr.left;
          else if (p.val > curr.val && q.val > curr.val) curr = curr.right;
          else return curr;
      }
      return null;
  }
  ```
  At each node:
  - If both values are smaller, LCA must be in left subtree.
  - If both are larger, LCA must be in right subtree.
  - The first node where $p$ and $q$ split is the LCA.
  No recursion stack is allocated, achieving strict $O(1)$ auxiliary space and $O(H)$ time ($O(\log N)$ on balanced trees).

---

### Q10: In LeetCode #102 (Binary Tree Level Order Traversal), how do you partition nodes into discrete level groups?
- **Short answer**: In each BFS queue iteration, capture `int levelSize = queue.Count` before the inner loop; dequeue exactly that many nodes to process the current level completely before moving to the next.
- **Deeper answer**:
  ```csharp
  public IList<IList<int>> LevelOrder(TreeNode root) {
      var result = new List<IList<int>>();
      if (root == null) return result;
      var queue = new Queue<TreeNode>();
      queue.Enqueue(root);
      while (queue.Count > 0) {
          int levelSize = queue.Count;
          var level = new List<int>(levelSize);
          for (int i = 0; i < levelSize; i++) {
              var node = queue.Dequeue();
              level.Add(node.val);
              if (node.left != null) queue.Enqueue(node.left);
              if (node.right != null) queue.Enqueue(node.right);
          }
          result.Add(level);
      }
      return result;
  }
  ```
  Taking a snapshot of `queue.Count` decouples the nodes belonging to the current level from their children being enqueued for the subsequent level, guaranteeing clean level partitioning in $O(N)$ time and $O(W)$ space.
