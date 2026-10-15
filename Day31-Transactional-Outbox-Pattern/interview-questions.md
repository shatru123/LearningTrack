# Day 31: Senior & Staff Engineer Interview Scenarios & Answers
## Theme: The Transactional Outbox Pattern, Polling, CDC & Union-Find

---

### Q1: What is the Dual-Write Problem in microservices, and why can't it be solved by wrapping database commits and message publication in a standard `try-catch` block?
**Answer:**
The Dual-Write Problem arises whenever an application attempts to update two separate stateful systems (e.g., commit an SQL database transaction and publish an AMQP/Kafka message) without a distributed consensus coordinator.
Wrapping them in a `try-catch` block fails due to the physical realities of network failures:
1. **Database first, broker second**: If `context.SaveChangesAsync()` succeeds, the local database state is durable. If the process crashes or network partitions before `bus.PublishAsync()` succeeds, the `try-catch` may catch an error or crash completely, but the database transaction cannot be rolled back because it has already committed and other transactions may have built on it. The event is permanently lost.
2. **Broker first, database second**: If `bus.PublishAsync()` succeeds, the broker delivers the event to downstream consumers immediately. If `context.SaveChangesAsync()` subsequently throws an optimistic concurrency violation, unique constraint error, or disk failure, downstream services have already reacted to phantom data that never existed.
Distributed transactions (2PC) are not supported by modern cloud brokers; therefore, dual-writes cannot be solved with application-level try-catch blocks.

---

### Q2: Explain the Transactional Outbox Pattern. What exact guarantees does it provide, and what trade-offs does it introduce?
**Answer:**
The Transactional Outbox Pattern eliminates dual-writes by leveraging the local ACID transaction boundary of the database.
- **Mechanism**: Instead of publishing directly to the broker, the service writes both the business entity (e.g. `Order`) and an `OutboxMessage` record inside the **same local database transaction**. A separate background process (polling worker or CDC log tailer) reads the outbox table and publishes events to the broker.
- **Guarantees**:
  - **Atomicity**: An event is guaranteed to be saved if and only if the business data is saved.
  - **At-Least-Once Delivery**: Every event will eventually be published to the broker at least once.
- **Trade-offs**:
  - **Eventual Consistency**: There is a latency gap (milliseconds to seconds) between database commit and event consumption.
  - **Duplicate Messages**: The publisher may crash after sending to the broker but before updating the outbox table, requiring downstream consumers to be idempotent.
  - **Database Storage Growth**: The outbox table requires indexing and continuous cleanup/purging.

---

### Q3: How do you implement high-throughput outbox message polling without running into database deadlocks and worker contention?
**Answer:**
When scaling out microservice instances (e.g. 10 Kubernetes pods running the same outbox background worker), multiple workers polling the same `OutboxMessages` table will compete for the same rows.
To eliminate contention:
1. **`SELECT ... FOR UPDATE SKIP LOCKED`**: In PostgreSQL, MySQL 8+, and Oracle (or `WITH (UPDLOCK, READPAST)` in SQL Server), workers query:
   ```sql
   SELECT * FROM OutboxMessages 
   WHERE ProcessedOnUtc IS NULL 
   ORDER BY OccurredOnUtc ASC 
   LIMIT @BatchSize 
   FOR UPDATE SKIP LOCKED;
   ```
   `SKIP LOCKED` instructs the engine to immediately bypass any rows currently locked by another worker. Workers never wait for each other, preventing lock contention and eliminating deadlocks completely.
2. **Lease/Heartbeat Pattern**: For database engines without native `SKIP LOCKED`, use an optimistic lease: update `LockedUntilUtc = NOW() + INTERVAL '30 SECONDS', LockId = @WorkerId` with an atomic `UPDATE ... WHERE LockedUntilUtc < NOW()`.

---

### Q4: How does Change Data Capture (CDC) via Debezium compare to application-level outbox polling? When should you switch?
**Answer:**
- **Polling Publisher**:
  - An application background worker queries the database every $N$ milliseconds.
  - *Pros*: Zero infrastructure overhead; pure C#/.NET code; easy to maintain and test.
  - *Cons*: Periodic polling adds CPU/IO load to the database; introduces poll interval latency ($200\text{ms} - 2\text{s}$).
- **Change Data Capture (CDC / Debezium)**:
  - Debezium connects as a replication follower to the database Write-Ahead Log (PostgreSQL WAL or MySQL binlog). It streams committed transactions to Kafka in real time.
  - *Pros*: Sub-second latency ($< 10\text{ms}$); zero polling queries against database indexes; eliminates all lock contention.
  - *Cons*: High operational complexity; requires Kafka Connect, Kafka cluster, schema registries, and replication slot monitoring to prevent WAL disk blowouts.
- **When to switch**: Stay with application polling for modest throughput ($< 500 \text{ msgs/sec}$). Switch to CDC when outbox polling overhead degrades primary database performance or when sub-100ms end-to-end event propagation is mandatory.

---

### Q5: Why does the Transactional Outbox Pattern guarantee At-Least-Once delivery instead of Exactly-Once delivery?
**Answer:**
In any distributed system, the **Two Generals' Problem** dictates that reliable message delivery across an unreliable network cannot achieve exactly-once execution natively at the transport layer.
Consider an outbox publisher:
1. It fetches an outbox message.
2. It calls `broker.PublishAsync(msg)`.
3. The broker receives and persists the message, and sends an ACK over TCP.
4. **Failure point**: A network hiccup drops the TCP ACK, or the worker process crashes (OOM, SIGKILL) right before executing `UPDATE OutboxMessages SET ProcessedOnUtc = NOW()`.
When the worker restarts or another worker acquires the lease, it re-reads the un-processed outbox row and publishes it to the broker a second time.
Therefore, the outbox pattern guarantees **At-Least-Once Delivery**. Exactly-Once processing can only be achieved at the consumer end through **Idempotent Consumers** (Inbox Pattern).

---

### Q6: Describe the Idempotent Consumer (Inbox Pattern). How do you design it for high-throughput scale?
**Answer:**
The Inbox Pattern guarantees that re-delivered messages do not trigger duplicate side-effects (e.g. charging a credit card twice or double-incrementing inventory).
- **Mechanism**:
  1. The consumer receives a message containing a globally unique `MessageId` (UUID) or business deduplication key.
  2. Inside the consumer's local database transaction, it queries the `InboxMessages` table.
  3. If `MessageId` exists, the message has already been processed: the consumer immediately ACKs the broker and exits without executing side-effects.
  4. If `MessageId` does not exist, the consumer executes the business logic and inserts `INSERT INTO InboxMessages (Id, ProcessedAtUtc)` within the same database transaction.
- **High-Throughput Optimization**:
  - Insert a high-speed Redis filter (e.g., `SETNX message:{id} 1 EX 86400`) before querying the database to absorb 99.9% of duplicate checks without hitting disk.
  - Ensure the database `InboxMessages` table has a primary key on `MessageId` and is partitioned by date with automated partition truncation.

---

### Q7: How should you manage long-term data cleanup for the `OutboxMessages` table in high-throughput production environments?
**Answer:**
In systems publishing millions of events daily, an outbox table will rapidly bloat to hundreds of gigabytes, severely degrading index efficiency (`VACUUM` overhead in PostgreSQL).
Three production cleanup strategies:
1. **Immediate Delete on Publish**: Instead of `UPDATE OutboxMessages SET ProcessedOnUtc = NOW()`, execute `DELETE FROM OutboxMessages WHERE Id = @Id`. This keeps the table size near zero. However, it removes historical auditing.
2. **Range Partitioning by Day/Month**: Partition the outbox table by `OccurredOnUtc`. When a day's partition has all messages processed, drop the entire table partition (`DROP TABLE outbox_messages_2026_10_14`). Dropping a partition is an $\mathcal{O}(1)$ metadata operation with zero WAL bloat and zero vacuum lock contention.
3. **Background Chunked Purge**: Run an asynchronous scheduled worker executing `DELETE FROM OutboxMessages WHERE ProcessedOnUtc < NOW() - INTERVAL '7 DAYS' LIMIT 5000` to prevent holding long table locks.

---

### Q8: What happens if an outbox publisher crashes mid-batch? How does this impact message ordering?
**Answer:**
If a worker crashes while publishing a batch of 50 messages (e.g. after publishing message 20):
- Messages 1 to 20 are already in the broker.
- Messages 21 to 50 are not yet in the broker.
- When the worker lease expires, a new worker leases all 50 messages (since the database transaction marking them processed never committed).
- The new worker publishes messages 1 to 50.
- **Consequence**: Messages 1 to 20 are published **twice**, and can arrive interleaved with messages 21 to 50.
- **Strict Ordering Guarantee**: The Transactional Outbox cannot guarantee strict global message ordering across worker crashes without partitioning messages by aggregate key (e.g. `Kafka PartitionKey` / `RabbitMQ Consistent Hash`) and processing keys sequentially.

---

### Q9: Explain how Union-Find (Disjoint Set Union) detects cycles in an undirected graph. Why is it preferred over DFS/BFS for dynamic edge streams?
**Answer:**
- **Cycle Detection Mechanism**:
  A tree on $N$ vertices has exactly $N - 1$ edges and contains no cycles. For every edge $(u, v)$, we query $\text{Find}(u)$ and $\text{Find}(v)$. If they have different root representatives, they belong to disjoint connected components; we execute $\text{Union}(u, v)$ to connect them. If $\text{Find}(u) == \text{Find}(v)$, $u$ and $v$ are already connected via an existing path. Adding the edge $(u, v)$ creates an alternate path between them, which by definition forms a cycle.
- **Why DSU over DFS/BFS for Dynamic Streams**:
  - In a static graph, DFS/BFS can detect cycles in $\mathcal{O}(V + E)$.
  - In a dynamic graph where edges are streamed incrementally (online edge additions), running DFS/BFS after each added edge takes $\mathcal{O}(E \times (V + E)) = \mathcal{O}(E^2)$, which is $\mathcal{O}(N^2)$.
  - DSU processes each edge in near $\mathcal{O}(1)$ amortized time ($\mathcal{O}(\alpha(N))$), allowing the entire stream of $N$ edges to be evaluated in $\mathcal{O}(N \cdot \alpha(N)) \approx \mathcal{O}(N)$ total time.

---

### Q10: What is the difference between Union by Rank and Union by Size in DSU? Why does combining path compression with union by rank achieve $\mathcal{O}(\alpha(N))$ time?
**Answer:**
- **Union by Rank**: The `rank` represents an upper bound on the tree depth. When uniting roots $R_u$ and $R_v$, the root with smaller rank is attached as a child to the root with larger rank. If ranks are equal, one is chosen as parent and its rank is incremented by 1.
- **Union by Size**: The `size` represents the total count of elements in the subtree. The root with smaller size is always attached under the root with larger size.
- **Why $\mathcal{O}(\alpha(N))$**:
  - Without optimizations, a naive Union-Find can degenerate into a linear linked list of depth $N$, resulting in $\mathcal{O}(N)$ per operation ($\mathcal{O}(N^2)$ overall).
  - Union by rank alone guarantees the tree height never exceeds $\lfloor \log_2 N \rfloor$, ensuring $\mathcal{O}(\log N)$ per operation.
  - Path compression flattens the tree so that subsequent operations take $\mathcal{O}(1)$.
  - Combining both prevents pathological re-deepening of paths. Tarjan mathematically proved that doing both path compression and union by rank reduces the amortized cost per operation to $\mathcal{O}(\alpha(N))$, where $\alpha$ is the Inverse Ackermann function.
