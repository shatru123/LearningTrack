# Day 39: CAP Theorem & PACELC in Practice

## Architectural Overview
Distributed systems running on unreliable networks must navigate fundamental trade-offs between consistency, availability, latency, and fault tolerance.

This module implements and proves distributed consensus and quorum models in .NET 8:
- **CAP Theorem (Brewer)**:
  - In the presence of a network Partition ($P$), a distributed system must choose between Consistency ($C$) or Availability ($A$).
- **PACELC Theorem (Abadi)**:
  - Extends CAP to normal operational states:
  - If **Partition** ($P$): choose between **Availability** ($A$) and **Consistency** ($C$).
  - **Else** ($E$): choose between **Latency** ($L$) and **Consistency** ($C$).
  - Examples: Spanner (PC/EC), Cassandra/DynamoDB (PA/EL), MongoDB (PC/EC or PA/EL depending on read concern).
- **Tunable Quorums ($N, W, R$)**:
  - $N$: Replication factor (e.g. 3 or 5 replicas).
  - $W$: Write quorum (number of nodes that must acknowledge a write before success).
  - $R$: Read quorum (number of nodes contacted during read).
  - **Strong Consistency (Pigeonhole Principle)**: When $R + W > N$, the read quorum and write quorum are mathematically guaranteed to overlap by at least one node, ensuring the client always observes the latest state.
  - **Eventual Consistency**: When $R + W \le N$, reads may return stale data.
- **Conflict Resolution & Read Repair**: Resolves conflicting replica versions via Last-Write-Wins (LWW) and automatically repairs out-of-date replica nodes asynchronously.

```
                     Quorum Cluster (N=3, W=2, R=2)
                                 │
                     Write "Alice" (Version 1)
                     Dispatched to all 3 nodes
                                 │
             ┌───────────────────┼───────────────────┐
             ▼                   ▼                   ▼
        [Node 1]            [Node 2]            [Node 3]
       (Ack: v=1)          (Ack: v=1)        (Partitioned!)
             │                   │
             └─────────┬─────────┘
                       ▼
                 W=2 Achieved!
                       │
       Later: Read Request (R=2) Contacting Node 1 & Node 3
                       │
             ┌─────────┴─────────┐
             ▼                   ▼
         [Node 1]            [Node 3]
         (v=1: Alice)        (v=0: Stale)
             │                   │
             └─────────┬─────────┘
                       │
                       ├─ Conflict Resolution: v=1 wins (Alice)
                       └─ Read Repair: Write v=1 to Node 3!
```

---

## LeetCode #79 & #17: Word Search & Letter Combinations (Medium)
- **LeetCode #79 (Word Search)**:
  - 2D grid character matching via DFS backtracking.
  - Replaces visited cells with `#` in-place to achieve $O(1)$ auxiliary allocation (avoiding $O(M \times N)$ boolean matrix).
- **LeetCode #17 (Letter Combinations of a Phone Number)**:
  - Generates Cartesian product of phone keypad letter mappings using recursive backtracking.
  - Time Complexity: $O(4^N \times N)$.

---

## Running Tests
```bash
dotnet test Day39-CAP-PACELC-DistributedSystems/tests/Day39.DistributedConsensus.Tests/Day39.DistributedConsensus.Tests.csproj
```
10 unit tests validate strict quorum overlaps ($R+W>N$), partition tolerance, read repair healing, PACELC classifications, 2D word search, and phone combinations.
