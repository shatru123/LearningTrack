# Day 16: Distributed Locking & Synchronization Primitives

## Overview
Comparing in-process thread synchronization with distributed locking across multi-node clusters. This module explores atomic lock-free coordination via `Interlocked`, asynchronous resource throttling using `SemaphoreSlim`, read-biased concurrency with `ReaderWriterLockSlim`, and enterprise distributed locking architectures (Redis Redlock, fencing tokens, zombie process mitigation). We also master two pointer-based linked list problems: Reorder List (LeetCode #143) and Remove Nth Node From End of List (LeetCode #19).

---

## Key Architectural Concepts

### 1. In-Process Synchronization Primitives Hierarchy
- **`Interlocked`**: Hardware-level atomic CPU instructions (`LOCK CMPXCHG` on x86/x64). Zero kernel transitions, zero thread blocking, ultra-low latency (<10ns). Ideal for lock-free counters, sequence numbers, and optimistic state machines.
- **`SemaphoreSlim`**: Lightweight asynchronous semaphore supporting `WaitAsync(timeout, ct)`. Essential for rate-limiting concurrent outbound HTTP connections, database connection pool access, and parallel batch processing.
- **`ReaderWriterLockSlim`**: Grants concurrent shared access to multiple reader threads while enforcing exclusive access for writers. Drastically reduces contention in read-heavy in-memory caches.

### 2. Distributed Locking & The Zombie Process Hazard
In a distributed cloud architecture (Kubernetes), process memory is isolated. In-process locks cannot protect shared databases. Distributed locks (e.g., Redis `SET resource_name my_random_token NX PX 30000`) provide mutual exclusion across nodes.
- **The Martin Kleppmann Hazard (GC Pause / Zombie Writes)**: A node acquires a lock with a 10s TTL. The node experiences an 11s full GC pause or network stall. The lock expires in Redis. A second node acquires the lock. The first node resumes, unaware that its lock has expired, and writes stale data to the database, corrupting state!
- **Fencing Tokens**: Every lock acquisition returns a strictly monotonically increasing 64-bit integer (`FencingToken`). The database validates that incoming writes must have a higher fencing token than the last committed transaction, safely discarding stale writes.

---

## LeetCode Problems Solved

### LeetCode #143: Reorder List
- **Algorithm**: Three-step in-place transformation:
  1. Find middle of list using fast and slow pointers.
  2. Reverse the second half in-place.
  3. Interleave nodes from first and second halves.
- **Complexity**: Time: $O(N)$, Space: $O(1)$.

### LeetCode #19: Remove Nth Node From End of List
- **Algorithm**: One-pass two-pointer gap strategy using a sentinel dummy node. Advance `fast` by $n + 1$ steps, then advance both until `fast == null`. `slow.next` is the target node to delete.
- **Complexity**: Time: $O(N)$, Space: $O(1)$.
