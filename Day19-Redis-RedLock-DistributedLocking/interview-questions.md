# Day 19: Senior / Staff-Level Interview Questions & Deep Answers

### Q1: Why is a single Redis master node with asynchronous replication inadequate for strong distributed locking?
- **Short answer**: Asynchronous replication creates a race condition during master failure; if the master crashes before replicating the lock to replicas, a newly promoted replica will grant the same lock to a second client, resulting in dual-master concurrent execution.
- **Deeper answer**:
  In a standard Redis master-replica topology, writes to the master are acknowledged immediately before being asynchronously sent to the replication stream.
  Consider this timeline:
  1. Client A acquires lock `SET lock:order 123 NX PX 10000` on the Master.
  2. The Master acknowledges success to Client A, but crashes before the write reaches Replica 1.
  3. Sentinel or Redis Cluster promotes Replica 1 to be the new Master.
  4. Client B attempts to acquire `SET lock:order 456 NX PX 10000` on the new Master. Because the key does not exist on Replica 1, the lock is granted.
  5. Both Client A and Client B now concurrently hold the exact same lock and execute mutating operations, violating mutual exclusion.
  RedLock eliminates this failure mode by removing master-replica replication dependencies entirely and operating across $N$ independent, un-replicated master instances.

---

### Q2: Walk through the RedLock algorithm step-by-step. How does it determine if a lock was successfully acquired?
- **Short answer**: A client attempts to acquire the lock across $N$ independent nodes in parallel with a short timeout; it succeeds if at least $\lfloor N/2 \rfloor + 1$ nodes respond successfully and the total elapsed time plus clock drift is less than the lock TTL.
- **Deeper answer**:
  For an $N = 5$ cluster:
  1. Record the current monotonic timestamp $T_1$.
  2. Concurrently attempt `SET resource random_token NX PX ttl` on all 5 nodes with a per-node timeout of 5–50 ms.
  3. Record completion monotonic timestamp $T_2$ and compute elapsed time $\Delta T = T_2 - T_1$.
  4. Calculate clock drift: $\text{Drift} = (\text{TTL} \times 0.01) + \text{ClockSkewTolerance}$.
  5. Compute remaining validity time: $\text{ValidityTime} = \text{TTL} - \Delta T - \text{Drift}$.
  6. **Quorum check**: If at least 3 nodes accepted the lock AND $\text{ValidityTime} > 0$, the lock is successfully acquired.
  7. **Rollback**: If quorum is not met (e.g. only 2 nodes responded) or $\text{ValidityTime} \le 0$, the client sends an unlock command to all 5 nodes, ensuring partially acquired keys are cleaned up.

---

### Q3: What was Martin Kleppmann's critique of RedLock, and what is the engineering takeaway?
- **Short answer**: Kleppmann argued that in asynchronous systems with stop-the-world GC pauses and clock jumps, RedLock cannot guarantee mutual exclusion without fencing tokens; antirez clarified that RedLock uses monotonic clocks and bounded drift, but agreed fencing tokens provide storage-level safety.
- **Deeper answer**:
  Kleppmann pointed out that physical clocks and process execution in managed runtimes (.NET, JVM) are unpredictable:
  - If a .NET thread acquires a 10-second lock and immediately experiences a 12-second Gen 2 GC pause, its lock expires on Redis. Another client acquires the lock. When the GC pause completes, the first thread resumes execution unaware that its lock has expired and writes to storage, corrupting data.
  - **Engineering takeaway**: A distributed lock cannot guarantee safety on its own if the protected resource allows unvalidated writes. Strict safety requires that the downstream storage engine (e.g., PostgreSQL or S3) validates a monotonically increasing **Fencing Token** or version number.

---

### Q4: What is a Fencing Token and how does it prevent zombie writes from GC-paused clients?
- **Short answer**: A fencing token is a strictly monotonically increasing number issued with each lock lease; downstream data stores reject any write whose token is less than or equal to the highest token previously seen.
- **Deeper answer**:
  When Client 1 acquires the lock, it receives fencing token 33.
  If Client 1 pauses for 15 seconds during a GC pause, the lock expires.
  Client 2 acquires the lock and receives fencing token 34. Client 2 writes to the database: `UPDATE accounts SET balance = balance + 100, lock_token = 34 WHERE id = 1 AND lock_token < 34;` (Success).
  Client 1 wakes up and attempts its delayed write with token 33:
  `UPDATE accounts SET balance = balance + 50, lock_token = 33 WHERE id = 1 AND lock_token < 33;`
  Because the database already recorded token 34, the conditional check fails and 0 rows are affected. The zombie write is safely rejected.

---

### Q5: Why is releasing a Redis distributed lock without a Lua script considered a severe bug?
- **Short answer**: A simple `DEL` command can accidentally delete another client's active lock if the current client's TTL expired while executing.
- **Deeper answer**:
  Suppose Client A acquires `lock:user_123` with token `uuid_A` and a 5-second TTL.
  Client A's operation encounters unexpected database latency and takes 7 seconds.
  At second 5, the key expires in Redis.
  At second 6, Client B acquires `lock:user_123` with token `uuid_B`.
  At second 7, Client A finishes and executes `DEL lock:user_123`.
  Client A just deleted Client B's lock! Client C can now acquire the lock, causing concurrent execution between B and C.
  Using an atomic Lua script:
  `if redis.call('get', KEYS[1]) == ARGV[1] then return redis.call('del', KEYS[1]) else return 0 end`
  ensures Client A only deletes the lock if the stored token still equals `uuid_A`.

---

### Q6: How does the RedLock lease auto-renewal (watchdog) pattern work in high-throughput .NET services?
- **Short answer**: A background timer runs periodically (e.g., at $\frac{\text{TTL}}{3}$) and executes an atomic Lua script (`PEXPIRE`) across quorum nodes to extend the lock duration as long as the workload task is still running.
- **Deeper answer**:
  In systems with variable task runtimes, developers often set unnecessarily long static TTLs (e.g., 5 minutes) to avoid timeouts. However, if the process crashes, the resource remains locked for the full 5 minutes.
  **Watchdog Pattern**:
  1. Acquire lock with a short TTL (e.g., 3 seconds).
  2. Spawn a background `PeriodicTimer` firing every 1 second.
  3. Each tick executes `PEXPIRE` if the client token still matches.
  4. If the process crashes or hangs, the heartbeat stops, and Redis naturally frees the lock within 3 seconds.
  5. The lease handle implements `IAsyncDisposable` to cancel the heartbeat loop and immediately unlock when work finishes.

---

### Q7: What is clock drift and why must it be accounted for in the RedLock validity calculation?
- **Short answer**: Clock drift is the rate difference between hardware clocks on different servers; RedLock deducts maximum possible drift from the TTL to guarantee the lock is released on the client before it expires on the server.
- **Deeper answer**:
  Even without NTP steps, hardware quartz crystals experience clock drift due to temperature and voltage fluctuations, typically around 1 millisecond per second (0.1%).
  Over a 10-second TTL, two servers can drift apart by tens of milliseconds.
  If Server A's clock runs 20ms faster than Server B's, Server A will expire the key 20ms before the client expects.
  RedLock guards against this by subtracting a drift allowance:
  $$\text{Drift} = (\text{TTL} \times \text{factor}) + \text{skewTolerance}$$
  This ensures that when the client considers the lock valid, it is guaranteed to still be held on all quorum Redis nodes.

---

### Q8: How does RedLock maintain mutual exclusion during network partitions (split-brain scenarios)?
- **Short answer**: Because acquiring the lock requires a majority quorum ($\lfloor N/2 \rfloor + 1$), at most one network partition can ever accumulate enough nodes to grant a lock.
- **Deeper answer**:
  In a 5-node cluster, suppose a network partition splits the nodes into two partitions:
  - Partition 1: 3 nodes
  - Partition 2: 2 nodes
  A client communicating with Partition 2 can only acquire at most 2 nodes. Since the quorum requirement is $\lfloor 5/2 \rfloor + 1 = 3$, Partition 2 will reject the lock and trigger an immediate compensation rollback.
  Only clients able to communicate with the majority partition (Partition 1) can achieve quorum. It is mathematically impossible for two clients to both acquire a quorum of nodes simultaneously.

---

### Q9: In LeetCode #100 (Same Tree), what are the edge cases and recursive mechanics?
- **Short answer**: If both nodes are null, return true; if exactly one is null or values differ, return false; otherwise recursively verify that both left subtrees and right subtrees match.
- **Deeper answer**:
  ```csharp
  public bool IsSameTree(TreeNode? p, TreeNode? q) {
      if (p == null && q == null) return true;
      if (p == null || q == null || p.val != q.val) return false;
      return IsSameTree(p.left, q.left) && IsSameTree(p.right, q.right);
  }
  ```
  - **Time Complexity**: $O(\min(N, M))$ because the traversal short-circuits at the first mismatch.
  - **Space Complexity**: $O(\min(H_p, H_q))$ auxiliary stack space.
  - Edge cases include asymmetrical tree branches, negative node values, and trees of differing heights.

---

### Q10: How does Merkle Tree Hashing achieve $O(N + M)$ linear time for LeetCode #572 (Subtree of Another Tree)?
- **Short answer**: Rather than re-running recursive subtree checks at every node ($O(N \cdot M)$), Merkle tree hashing computes a unique 64-bit cryptographic hash for each subtree bottom-up, reducing subtree matching to a $O(1)$ set lookup.
- **Deeper answer**:
  - In classic DFS, if both trees are degenerately skewed with identical node values (e.g. chains of 1s), checking whether `subRoot` matches at each node of `root` takes $M$ operations for each of the $N$ nodes, resulting in $O(N \cdot M)$ worst-case time.
  - With **Merkle Hashing**:
    1. During a single post-order traversal of `root`, compute:
       $$H(u) = \text{Mix}(u.val, H(u.left), H(u.right))$$
    2. Store all computed hashes in a `HashSet<long>`.
    3. Compute $H(subRoot)$ using the same mixing function.
    4. If $H(subRoot)$ exists in the HashSet, `subRoot` is isomorphic to a subtree of `root`.
    5. Visiting each node once yields strictly $O(N + M)$ time and $O(N + M)$ space.
