# Day 39: Senior & Staff Interview Questions & Answers

### Q1: What is the formal difference between CAP Consistency and ACID Consistency?
**Answer**:
They describe completely different properties despite sharing the same word "Consistency":
- **CAP Consistency ($C$)**: Means **Linearizability** / Single-copy consistency. It is a distributed systems property ensuring that once a write completes, all subsequent reads across any node in the cluster will observe that write or a later one.
- **ACID Consistency ($C$)**: Means **Application Invariant Preservation**. It is a database transaction property ensuring state transitions obey all domain business rules, schema constraints, foreign keys, and unique indices (e.g. account balances cannot drop below zero).

---

### Q2: Why is "CA" (Consistent + Available without Partition Tolerance) an impossible architectural choice in distributed systems?
**Answer**:
A network partition is not a design choice; it is an inevitable physical reality of distributed infrastructure (cables cut, switch failures, cross-datacenter fiber cuts, GC pauses causing heartbeat timeouts).
- If a partition occurs, nodes in partition group 1 cannot communicate with nodes in partition group 2.
- If a client issues a write to group 1:
  - If group 1 accepts the write, group 2 becomes stale (sacrificing Consistency $C$).
  - If group 1 rejects the write or blocks forever waiting for group 2, the system stops serving requests (sacrificing Availability $A$).
- Therefore, every real distributed system MUST be partition-tolerant ($P$). The only choice is between CP and AP.

---

### Q3: Explain PACELC theorem and give real-world database examples for each quadrant.
**Answer**:
PACELC states: If **Partition** ($P$), trade off **Availability** ($A$) vs **Consistency** ($C$); **Else** (normal non-partitioned operation), trade off **Latency** ($L$) vs **Consistency** ($C$).
- **PC/EC** (Google Spanner, CockroachDB): Prefers consistency during partitions (fails un-elected nodes); prefers strong consistency during normal operations (requires multi-region Paxos/Raft consensus before acknowledging writes, incurring network roundtrip latency).
- **PA/EL** (Apache Cassandra, Amazon DynamoDB, Couchbase): Prefers availability during partitions (accepts writes on any node); prefers ultra-low latency during normal operation (writes to local node and replicates asynchronously, risking stale reads).
- **PC/EL** (MongoDB with default settings): Master node rejects writes if isolated from replica majority (PC), but does fast local replication with eventual consistency for secondaries (EL).

---

### Q4: Prove mathematically why $R + W > N$ guarantees strong consistency (Linearizability) in quorum systems.
**Answer**:
Let $N$ be the total number of replica nodes for a key.
- A write is acknowledged when written to a set of $W$ nodes: $S_w \subseteq \text{Nodes}, |S_w| = W$.
- A read contacts a set of $R$ nodes: $S_r \subseteq \text{Nodes}, |S_r| = R$.
- By the Pigeonhole Principle:
  $$|S_w \cap S_r| = |S_w| + |S_r| - |S_w \cup S_r| \ge W + R - N$$
- Since $W + R > N$, we have:
  $$|S_w \cap S_r| \ge 1$$
- This guarantees the read set and write set intersect by at least one common replica node. Because that common node has the latest write (tracked via version or timestamp), the read coordinator receives the latest version and returns it to the client.

---

### Q5: What is "Read Repair" and how does it maintain eventual consistency in leaderless distributed databases?
**Answer**:
In leaderless systems (Cassandra, Dynamo):
1. A client initiates a read with quorum $R=2$ on a cluster with $N=3$.
2. Node 1 returns `{value: "Active", version: 5}`.
3. Node 2 returns `{value: "Pending", version: 4}`.
4. The coordinator notices a version discrepancy. It returns version 5 ("Active") to the client.
5. In the background (asynchronously), the coordinator sends an update to Node 2 with version 5.
- Over time, frequently read data is self-healing, reducing entropy without expensive full-cluster scans.

---

### Q6: What are the risks of using Last-Write-Wins (LWW) conflict resolution, and what is the alternative?
**Answer**:
- **Risks of LWW**:
  - Relies on physical wall-clock timestamps (NTP).
  - Clock skew between servers can cause a write that occurred physically *earlier* to overwrite a write that occurred physically *later* if Server A's clock is running 50ms fast.
  - Concurrent writes at the same millisecond silently drop one update, leading to data loss.
- **Alternatives**:
  - **Vector Clocks / Version Vectors**: Track causal history per node (`[NodeA: 2, NodeB: 1]`). If vectors do not dominate, a true conflict is detected and surfaced to the application (or resolved via CRDTs).
  - **Conflict-Free Replicated Data Types (CRDTs)**: Mathematically structured data types (e.g. PN-Counters, Observed-Remove Sets) whose merge operations are associative, commutative, and idempotent ($A \cup B = B \cup A$).

---

### Q7: In LeetCode #79 (Word Search), why is in-place grid mutation (`board[r][c] = '#'`) superior to maintaining a `HashSet<(int, int)>` or `bool[][]`?
**Answer**:
- A `HashSet<(int, int)>` allocates heap memory for every visited cell, causes boxing, and incurs hash table lookup overhead on every step.
- A separate `bool[rows][cols]` matrix requires $O(M \times N)$ auxiliary memory allocation per test case or expensive zeroing resets.
- By mutating the character directly in `board[r][c] = '#'` and restoring it on backtrack (`board[r][c] = original`), the algorithm uses $O(1)$ auxiliary memory and stays completely cache-friendly within the L1 data cache.

---

### Q8: What is the time complexity of Word Search on an $M \times N$ board for a word of length $L$?
**Answer**:
- There are $M \times N$ possible starting positions on the board.
- From each cell, the DFS explores in 4 directions. However, since we cannot visit the cell we just came from, subsequent steps have at most 3 branches.
- For a word of length $L$, the maximum depth of the recursion tree is $L$.
- Total worst-case time complexity: $O(M \times N \times 3^L)$.
- Space complexity: $O(L)$ for the recursion stack depth.

---

### Q9: In LeetCode #17 (Letter Combinations of a Phone Number), what is the maximum number of combinations generated for an input of length $N$?
**Answer**:
- Digits 7 and 9 map to 4 letters each ('pqrs', 'wxyz'); digits 2, 3, 4, 5, 6, 8 map to 3 letters each.
- In the worst case where all $N$ digits are 7 or 9, the total number of combinations is $4^N$.
- Copying each combination of length $N$ into the output list takes $O(N)$ operations.
- Total time complexity: $O(4^N \times N)$.
- Space complexity: $O(N)$ auxiliary stack space.

---

### Q10: How does Google Spanner claim to achieve "both Consistency and Availability" despite the CAP theorem?
**Answer**:
Spanner does not violate the CAP theorem; it is technically a **CP** system.
- If a network partition isolates a replica from the Paxos quorum, Spanner chooses Consistency over Availability for that partition.
- However, Google minimizes partitions to near zero through:
  1. Private multi-redundant global fiber optic backbone with automated rerouting.
  2. **TrueTime API**: Uses synchronized atomic clocks and GPS receivers in every datacenter, guaranteeing clock uncertainty $\epsilon < 7\text{ms}$.
  3. By waiting out the small uncertainty window $2\epsilon$ before committing transactions, Spanner achieves external consistency (strict serializability) globally with five-nines (99.999%) availability in practice, appearing as "CA" to external users.
