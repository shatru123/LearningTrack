# Day 19: Redis Distributed Locking with RedLock & Tree Isomorphism

## Overview
Comprehensive implementation of multi-instance distributed mutual exclusion using the **RedLock algorithm** across independent Redis nodes, paired with tree isomorphism and recursive subtree matching algorithms: **LeetCode #100 (Same Tree)** and **LeetCode #572 (Subtree of Another Tree)**.

---

## Architectural Breakdown

### 1. The RedLock Consensus Algorithm
Traditional Redis distributed locking (`SET lock_name my_random_value NX PX 30000`) on a single master node suffers from a fatal single point of failure: if the master crashes before asynchronous replication flushes to replicas, a newly elected master will grant the same lock to another client, causing dual-master split-brain writes.

RedLock solves this by using **$N$ completely independent Redis master nodes** (typically $N = 5$):

```
       +---------------------------------------------+
       |         Client Lock Coordinator             |
       +---------------------------------------------+
          /         |         |         \          \
         v          v         v          v          v
     [Node 1]   [Node 2]  [Node 3]   [Node 4]   [Node 5]
     (Redis)    (Redis)   (Redis)    (Redis)    (Redis)
         \          |         |          /
          +---------+---------+---------+
                         |
           Quorum Achieved (>= 3 / 5)
```

#### Step-by-Step Acquisition Lifecycle:
1. **Timestamp Recording**: Client captures start time $T_1$ using a monotonic clock.
2. **Concurrent Multi-Node Requests**: Attempts to acquire lock on all $N$ instances using `SET key value NX PX ttl` with a small per-node timeout (5–50 ms) to prevent slow/hung nodes from delaying the overall attempt.
3. **Elapsed Time & Clock Drift Calculation**:
   $$\Delta T = T_2 - T_1$$
   $$\text{Drift} = (\text{TTL} \times \text{DriftFactor}) + \text{ClockSkewTolerance}$$
   $$\text{ValidityTime} = \text{TTL} - \Delta T - \text{Drift}$$
4. **Quorum Evaluation**:
   - The lock is acquired **if and only if** the client successfully sets the key in at least $\lfloor N/2 \rfloor + 1$ nodes (3 out of 5) **AND** $\text{ValidityTime} > 0$.
5. **Compensation / Rollback**:
   - If quorum fails or validity time expires, the client dispatches an unlock command to **all $N$ nodes** (including nodes where the acquisition request timed out or was not received).

---

### 2. Auto-Renewal Heartbeats & Fencing Tokens

#### Lease Auto-Renewal (`RedLockLeaseHandle`)
- For long-running operations, setting a static TTL risks premature lock expiration if task execution takes longer than anticipated.
- The `RedLockLeaseHandle` runs a periodic background heartbeat timer extending the lease across quorum nodes at $\frac{\text{TTL}}{3}$ intervals until explicitly disposed.

#### Monotonic Fencing Tokens
- Defends against Martin Kleppmann's critique of distributed locks: if a process experiences a stop-the-world GC pause or network partition, its lock may expire while it is paused. When it resumes, it might perform a zombie write over another client's data.
- The storage tier enforces monotonically increasing fencing tokens: any write presenting a token $\le$ the highest token observed so far is rejected.

---

### 3. Tree Isomorphism & Subtree Solvers

#### LeetCode #100: Same Tree
- **Problem**: Check whether two binary trees $p$ and $q$ are structurally identical and have the same node values.
- **Algorithm**: Recursive DFS checking base cases and verifying `p.val == q.val && IsSameTree(p.left, q.left) && IsSameTree(p.right, q.right)`.
- **Complexity**: Time: $O(\min(N, M))$, Space: $O(\min(H_p, H_q))$.

#### LeetCode #572: Subtree of Another Tree
- **Approach 1 (Classic DFS)**: Traverses each node in `root` and invokes `IsSameTree(curr, subRoot)`. Time: $O(N \cdot M)$.
- **Approach 2 (Merkle Tree Hashing)**: Computes a 64-bit cryptographic/polynomial subtree hash for each sub-root during a single post-order pass. Subtree matching is reduced to a set lookup in $O(N + M)$ time.

---

## Running Unit Tests

```bash
dotnet test Day19-Redis-RedLock-DistributedLocking/tests/Day19.RedisRedLock.Tests/Day19.RedisRedLock.Tests.csproj
```
