# Day 16: Senior / Staff-Level Interview Questions & Deep Answers

### Q1: Why can't you use C#'s `lock` (`Monitor`) statement with `await`?
- **Short answer**: `lock` is thread-affine (tied to the specific executing OS thread ID), whereas async continuations frequently resume on different ThreadPool threads.
- **Deeper answer**:
  In .NET, `Monitor.Enter(obj)` records the managed `Thread.CurrentThread.ManagedThreadId`. When code hits an `await` expression, the current thread is returned to the ThreadPool. When the asynchronous operation completes, the continuation is dispatched to an arbitrary available ThreadPool thread. If that continuation attempts `Monitor.Exit(obj)`, the runtime detects that the exiting thread ID differs from the entering thread ID, throwing an immediate `SynchronizationLockException`. For async synchronization, `SemaphoreSlim` must be used instead.

---

### Q2: What is the purpose of Fencing Tokens in distributed locks?
- **Short answer**: They prevent data corruption caused by "zombie processes" that resume execution after their distributed lock lease has expired (due to GC pauses or network partitions).
- **Deeper answer**:
  In any distributed system, a client holding a lease may pause (e.g., during a Gen 2 GC pause or VM migration) longer than the lock's Time-To-Live (TTL). During the pause, the lock expires and is granted to another node. When the first node wakes up, it assumes it still owns the lock and writes to the database.
  A fencing token is a strictly monotonically increasing counter issued with every lock acquisition. The storage service rejects any write whose fencing token is less than or equal to the highest token it has already processed, safely blocking stale writes.

---

### Q3: How does `SemaphoreSlim` differ internally from OS-level `Semaphore`?
- **Short answer**: `SemaphoreSlim` is a lightweight managed construct that bypasses Windows/Linux kernel primitives unless heavy contention occurs, and natively supports asynchronous waiting (`WaitAsync`).
- **Deeper answer**:
  The classic `System.Threading.Semaphore` wraps a kernel-mode Win32/POSIX synchronization handle. Entering and exiting requires expensive user-to-kernel mode context transitions (~1,000ns+).
  `SemaphoreSlim` uses a fast user-space spin-wait loop coupled with managed linked lists of `TaskCompletionSource` objects. It avoids kernel mode transitions for uncontended scenarios and allows threads to await cooperatively without blocking underlying OS threads.

---

### Q4: When should you use `ReaderWriterLockSlim` instead of a standard `lock`?
- **Short answer**: When read operations heavily outnumber write operations (e.g., 95% reads, 5% writes) and read operations take a non-trivial amount of time.
- **Deeper answer**:
  In a read-heavy system, `lock` serializes all reads, creating massive thread contention and latency bottlenecks. `ReaderWriterLockSlim` allows an unlimited number of concurrent reader threads to execute simultaneously.
  However, `ReaderWriterLockSlim` has higher tracking overhead than `Monitor`. If reads are instantaneous (e.g., a simple dictionary lookup taking 5ns), the lock overhead outweighs the concurrency benefits. It shines when read operations involve traversing collections, computing hashes, or serializing data.

---

### Q5: What is the Redlock algorithm and what are its key trade-offs?
- **Short answer**: A distributed consensus locking algorithm developed for multi-node Redis clusters where a client must acquire locks on a majority ($N/2 + 1$) of independent Redis master instances within a time window.
- **Deeper answer**:
  In standard Redis master-replica replication, replication is asynchronous. If the master crashes before replicating a lock key to the replica, the promoted replica will grant the same lock to a second client, violating mutual exclusion.
  Redlock deploys $N$ (e.g., 5) independent master nodes with no replication between them. A client attempts to acquire the lock on all 5 nodes with a small timeout. If it secures $\ge 3$ nodes and the elapsed time is less than the validity time, the lock is granted. The trade-off is reliance on physical clock drift synchronization across servers and sensitivity to network latency.

---

### Q6: How does `Interlocked.CompareExchange` enable lock-free algorithms?
- **Short answer**: It executes a hardware-level atomic Compare-And-Swap (CAS) instruction, updating a memory address if and only if it matches an expected current value.
- **Deeper answer**:
  In CPU architecture, `Interlocked.CompareExchange(ref location, newValue, expectedValue)` executes `LOCK CMPXCHG`.
  This allows optimistic concurrency loops:
  1. Read value $V_1$.
  2. Compute new state $V_2 = f(V_1)$.
  3. Attempt CAS: if memory still holds $V_1$, write $V_2$ atomically. If another core modified memory in between, the CAS fails, and the loop retries.
  This avoids thread blocking entirely, providing superior throughput under low to moderate contention.

---

### Q7: What is the risk of Lock Inversion and how do you prevent deadlocks?
- **Short answer**: Lock inversion (deadlock) occurs when two threads acquire multiple shared locks in differing orders (Thread 1 acquires A then B; Thread 2 acquires B then A).
- **Deeper answer**:
  To prevent deadlocks:
  1. **Strict Global Lock Ordering**: Always acquire locks in a predetermined lexicographical or hierarchical order (e.g., always acquire Lock A before Lock B).
  2. **Timeout Enforcement**: Never use indefinite waits (`Wait()`); always specify timeouts (`WaitAsync(TimeSpan.FromSeconds(5))`) and implement circuit breaking or transaction retries on failure.
  3. **Coarse-Grained Locking**: Consolidate fine-grained locks into a single higher-level lock when possible.

---

### Q8: How does an asynchronous Disposable Lease pattern enhance `SemaphoreSlim` usage?
- **Short answer**: It combines `await semaphore.WaitAsync()` with `using var lease = ...` to guarantee that `semaphore.Release()` is always called even if exceptions are thrown.
- **Deeper answer**:
  Without the pattern, developers must remember `try { ... } finally { semaphore.Release(); }`. If a developer forgets the `finally` block or returns early, the semaphore permanently leaks permits, eventually halting all incoming requests.
  With a wrapper struct/class implementing `IDisposable`, the `Dispose()` method internally calls `semaphore.Release()`. This makes scoping clean and immune to forgotten release bugs.

---

### Q9: In LeetCode #143 (Reorder List), how do we achieve $O(1)$ auxiliary space?
- **Short answer**: By modifying pointers in-place across three sequential steps: finding the midpoint with fast/slow pointers, reversing the second half in-place, and interleaving nodes.
- **Deeper answer**:
  Many candidates copy node references into an array or deque, which requires $O(N)$ auxiliary memory.
  To achieve $O(1)$ space:
  1. `slow` advances 1 step, `fast` advances 2 steps. When `fast` reaches the end, `slow` is at the midpoint.
  2. Reverse `slow.next` using the 3-pointer method (`prev`, `curr`, `nextTemp`).
  3. Break the list into two halves (`slow.next = null`) and interleave: connect `first.next` to `second`, and `second.next` to `firstOriginalNext`.

---

### Q10: In LeetCode #19 (Remove Nth Node From End of List), why is a dummy sentinel node critical?
- **Short answer**: It gracefully handles the edge case where the node to be removed is the head of the list.
- **Deeper answer**:
  If a list has 5 nodes and you are instructed to remove the 5th node from the end, you must remove the head. Without a dummy node pointing to `head`, the `slow` pointer cannot point to the node preceding the head.
  By initializing `dummy.next = head` and placing `slow = dummy` and `fast = dummy`, advancing `fast` by $n + 1$ positions ensures that when `fast` reaches null, `slow` lands exactly on the node immediately preceding the deletion target, allowing `slow.next = slow.next.next;` to execute universally.
