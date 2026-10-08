# Day 19 - Engineering Notes: RedLock Distributed Locking & Consensus

## 1. The Great RedLock Debate: Martin Kleppmann vs Salvatore Sanfilippo (antirez)

In 2016, distributed systems researcher Martin Kleppmann published a critique of the RedLock algorithm, prompting an extensive architectural debate with Redis creator Salvatore Sanfilippo.

### Kleppmann's Critique ("How to do distributed locking"):
1. **Asynchronous Network & Process Pauses**:
   - In an asynchronous distributed system with unbounded network delays and process pauses (such as .NET garbage collection stop-the-world pauses), no algorithm relying purely on wall-clock time can guarantee mutual exclusion.
   - *The GC Pause Scenario*: Client 1 acquires RedLock with a 10s TTL. Client 1 immediately hits a 15-second Gen 2 GC pause. The lock expires on Redis. Client 2 acquires the lock and begins writing to storage. Client 1 resumes, unaware that its lock expired, and also writes to storage, causing silent data corruption.
2. **Clock Skew & NTP Jumps**:
   - If one of the 5 Redis nodes experiences an unsynchronized NTP step forward, its keys will expire prematurely, breaking quorum assumptions.
3. **Kleppmann's Solution**:
   - Locks are insufficient for correctness without **Fencing Tokens**: monotonically increasing numbers issued with each lock acquisition that the storage tier verifies and rejects if out-of-order.

### Antirez's Rebuttal ("Is Redlock safe?"):
1. **Monotonic Clocks Mitigate NTP Steps**:
   - RedLock does not use wall-clock time (`gettimeofday`); it uses monotonic clocks (`CLOCK_MONOTONIC`), which never jump backwards or forwards due to NTP adjustments.
2. **Bounded Drift Model**:
   - RedLock explicitly accounts for maximum clock drift by calculating:
     $$\text{validity\_time} = \text{TTL} - \Delta T - \text{drift}$$
   - If elapsed time plus drift exceeds the TTL, the lock is immediately aborted.
3. **Consensus in Practice**:
   - While Raft/Paxos-based systems (like etcd or ZooKeeper) provide strict CP consensus with formal leadership election, RedLock provides a pragmatic, highly available distributed locking layer across independent Redis instances without requiring complex consensus state machines.

---

## 2. Clock Drift Mechanics in High-Performance .NET

```csharp
// Anti-Pattern: DateTime.UtcNow can jump due to NTP sync
DateTime start = DateTime.UtcNow;
...
TimeSpan elapsed = DateTime.UtcNow - start; // Can be negative or skewed!

// Production Standard: Monotonic Clock via Stopwatch
long startTick = Stopwatch.GetTimestamp();
...
TimeSpan elapsed = Stopwatch.GetElapsedTime(startTick); // Guaranteed strictly monotonic
```

- In .NET 8, `Stopwatch.GetTimestamp()` maps to `mach_absolute_time()` on macOS, `clock_gettime(CLOCK_MONOTONIC)` on Linux, and `QueryPerformanceCounter` on Windows.
- It operates at sub-microsecond precision and is completely unaffected by operating system clock corrections.

---

## 3. Atomic Lua Scripts in Redis Distributed Locks

### Safe Unlock Script
```lua
if redis.call("get", KEYS[1]) == ARGV[1] then
    return redis.call("del", KEYS[1])
else
    return 0
end
```
- **Why it matters**: If client A acquired the lock and took 11 seconds on a 10s TTL, client B will acquire the lock after second 10. If client A blindly called `DEL lock_key`, it would delete client B's active lock! The Lua script guarantees that client A only deletes the lock if the stored UUID still matches client A's token.

### Safe Extend (Heartbeat) Script
```lua
if redis.call("get", KEYS[1]) == ARGV[1] then
    return redis.call("pexpire", KEYS[1], ARGV[2])
else
    return 0
end
```
- Extends the lease only if the current client remains the rightful lock holder.

---

## 4. DSA: Merkle Tree Hashing vs Recursive Isomorphism

| Metric | Classic Recursive DFS | Merkle Tree Hashing |
|---|---|---|
| **Time Complexity** | $O(N \cdot M)$ worst-case | $O(N + M)$ linear time |
| **Space Complexity** | $O(H)$ recursion stack | $O(N + M)$ hash set storage |
| **Algorithmic Idea** | Brute force check at each node | Bottom-up subtree hash fingerprinting |
| **Collision Probability** | 0% (Exact comparison) | $< 10^{-9}$ with 64-bit hash mixing |

### Merkle Tree Hashing Formula
$$H(node) = \text{SplitMix64}(node.val \times 31 + H(left) \times 1000003 + H(right) \times 1000033)$$
By encoding both the node value and child structural hashes into a 64-bit integer, two subtrees produce the identical hash if and only if they are isomorphic.
