# Deep Dive Notes: CAP, PACELC & Distributed Consensus

## 1. CAP Theorem Formalization (Gilbert & Lynch Proof)
- **Consistency ($C$)**: Linearizability. Every read receives the most recent write or an error. All nodes appear to see operations in a single global, atomic order.
- **Availability ($A$)**: Every non-failing node returns a non-error response for every received request (no guarantees on data freshness).
- **Partition Tolerance ($P$)**: The network can drop or delay an arbitrary number of messages between nodes.
- **Theorem**: On an asynchronous network where partitions $P$ can occur, a distributed datastore CANNOT achieve both $C$ and $A$. You MUST choose whether to preserve consistency by rejecting requests (CP) or preserve availability by accepting potentially conflicting writes (AP).

## 2. PACELC Theorem (Daniel Abadi)
CAP only considers behavior when the system is actively partitioned ($P$). However, partitions are relatively rare in healthy datacenters. PACELC explains system behavior during normal (non-partitioned) states:
- **If Partition ($P$)**:
  - Choose between **Availability** ($A$) or **Consistency** ($C$).
- **Else ($E$)**:
  - Choose between **Latency** ($L$) or **Consistency** ($C$).
- **Categorization Matrix**:
  - **PC/EC** (Google Spanner, CockroachDB): Prefers consistency during partitions; prefers multi-datacenter two-phase commit / Paxos during normal operation (paying higher latency for consistency).
  - **PA/EL** (Apache Cassandra, Amazon DynamoDB, Couchbase): Prefers availability during partitions; returns local replica reads immediately during normal operation (paying consistency for low latency).
  - **PC/EL** (MongoDB default primary read/write): Refuses writes if primary partitioned (PC), but does fast local primary replication (EL).

## 3. Tunable Consistency Math ($N, W, R$)
- By the **Pigeonhole Principle**:
  - Total nodes = $N$.
  - Number of write nodes = $W$.
  - Number of read nodes = $R$.
  - If $W + R > N$, the set of nodes containing writes and the set of nodes consulted on reads MUST intersect by at least one node:
    $$\text{Intersection} = (W + R) - N \ge 1$$
  - The intersecting node holds the latest version $V_{\max}$.
- **Read Repair vs Anti-Entropy**:
  - **Read Repair**: Opportunistic sync during client reads. When a read quorum returns mismatched versions, the coordinator writes the newest version back to the stale nodes.
  - **Anti-Entropy**: Background process using Merkle trees to compare replica ranges and stream differences without client intervention.
