# Day 25: Senior & Staff Engineer Interview Questions & Answers

### 1. In high-throughput .NET 8 services, how does the Garbage Collector distinguish between Ephemeral segments and the Large Object Heap (LOH), and how do you mitigate LOH fragmentation?
**Answer:**
The .NET runtime divides managed heap memory into Ephemeral segments (Generations 0 and 1), Generation 2 segments, and the Large Object Heap (LOH). Any object allocation requiring 85,000 bytes or more is allocated directly on the LOH. 

Unlike Ephemeral segments, the LOH is not compacted during standard Gen 2 GC passes because copying multi-megabyte memory blocks causes unacceptably long GC pause times (Stop-the-World). Consequently, frequently allocating and releasing temporary large objects (such as byte buffers or large strings) fragments LOH address space, triggering OutOfMemoryException even when total free memory is ample.

**Mitigation Strategies:**
1. **ArrayPool<T>.Shared**: Rent pooled arrays instead of allocating new buffers:
   ```csharp
   byte[] buffer = ArrayPool<byte>.Shared.Rent(128 * 1024);
   try { /* Process stream */ }
   finally { ArrayPool<byte>.Shared.Return(buffer); }
   ```
2. **Pinned Object Heap (POH)**: Allocate pinned buffers via `GC.AllocateArray<T>(length, pinned: true)` to avoid fragmentation on Gen 2/LOH.
3. **On-Demand LOH Compaction**: During scheduled maintenance or off-peak periods, set:
   ```csharp
   GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
   GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
   ```

---

### 2. Why are `Span<T>` and `ReadOnlySpan<T>` declared as `ref struct`, and what architectural constraints does this impose on asynchronous and generic code?
**Answer:**
`Span<T>` provides a type-safe and memory-safe representation of a contiguous region of arbitrary memory (managed heap, native heap via `Marshal.AllocHGlobal`, or execution stack via `stackalloc`). It is declared as a `ref struct` to guarantee that its instances live exclusively on the execution stack of the current thread.

If `Span<T>` could escape to the managed heap:
- The managed GC would not be able to track interior pointers into native memory or unpinned heap objects, causing wild pointer crashes during compaction.
- Pointers to stack frames could outlive the activation frame that created them, resulting in stack corruption.

**Architectural Constraints:**
1. Cannot be boxed or cast to `object`, `dynamic`, or any interface (e.g. `IEnumerable<T>`).
2. Cannot be fields of regular classes or non-ref structs.
3. Cannot be used across `await` boundaries in `async` methods because async state machines are compiled into heap-allocated classes.
4. Cannot be type arguments in generic classes or methods (e.g. `List<Span<byte>>` is illegal).

---

### 3. What are the antipatterns and catastrophic failure modes associated with `ValueTask<T>`, and when is it preferred over `Task<T>`?
**Answer:**
`ValueTask<T>` is a discriminated struct union designed to eliminate heap allocations when an asynchronous method frequently completes synchronously (e.g., cached reads, pre-validated payloads).

**Antipatterns & Failure Modes:**
1. **Awaiting Multiple Times**: A `ValueTask<T>` backed by an `IValueTaskSource<T>` recycles its underlying token upon the first `await`. Awaiting it a second time invokes undefined behavior, corrupts runtime state, or throws an `InvalidOperationException`.
2. **Concurrent Awaits**: Never pass a `ValueTask<T>` to multiple concurrent tasks (e.g., `Task.WhenAll` without converting via `.AsTask()`).
3. **Using `.Result` or `.GetAwaiter().GetResult()` Before Completion**: Blocking on a non-completed `ValueTask<T>` causes deadlocks or thread pool starvation.

**Selection Rule:**
Use `ValueTask<T>` when:
- The method completes synchronously $\ge 80\text{–}90\%$ of the time.
- The method is on a hot path where GC allocation of `Task<T>` causes measurable latency spikes.
Otherwise, default to `Task<T>`.

---

### 4. How does EF Core's Change Tracker create performance bottlenecks in read-heavy applications, and how do `AsNoTracking()` vs `AsNoTrackingWithIdentityResolution()` differ?
**Answer:**
By default, Entity Framework Core tracks all entities loaded by a `DbContext`. For every tracked entity, the `ChangeTracker`:
1. Maintains an Identity Map (mapping primary key to entity instance).
2. Creates snapshot copies of all scalar and navigation properties in memory.
3. Compares current property values against snapshot values during `SaveChanges()` (DetectChanges).

In read-heavy APIs or analytical queries loading thousands of rows, snapshot creation consumes massive RAM and increases Gen 0 collections.

**Comparison:**
- **`AsNoTracking()`**: Completely bypasses the Identity Map and snapshotting. Entities are instantiated and immediately detached. Zero tracking overhead. However, if the query returns multiple references to the same database row (e.g. Orders containing the same Customer), EF Core instantiates a *duplicate* C# instance for each reference.
- **`AsNoTrackingWithIdentityResolution()`**: Bypasses snapshotting and dirty checking, but maintains a temporary identity map for the duration of the query. Ensures that duplicate rows map to a single shared object reference in memory without overhead of `SaveChanges()` tracking.

---

### 5. What is the difference between an Index Seek, an Index Scan, and a Key Lookup, and how do Covering Indexes eliminate Key Lookups?
**Answer:**
- **Index Seek**: Navigates the B+ Tree from the root node to leaf pages using binary search ($\mathcal{O}(\log N)$ page accesses). Occurs when query predicates match the leading keys of an index.
- **Index Scan**: Traverses the entire doubly linked list of leaf pages in the B+ Tree ($\mathcal{O}(N)$ page reads). Occurs when predicates are non-sargable (e.g., `WHERE LOWER(Email) = '...'` or wildcards `LIKE '%xyz'`).
- **Key Lookup (Bookmark Lookup)**: When a non-clustered index satisfies a search predicate but does not contain all columns requested in the `SELECT` clause, the storage engine must execute an additional lookup into the clustered index (or RID heap) for *every* matching row.

**Covering Index Elimination:**
By appending non-key columns to the non-clustered index using the `INCLUDE` clause:
```sql
CREATE NONCLUSTERED INDEX IX_Orders_CustomerId
ON Orders (CustomerId)
INCLUDE (OrderDate, TotalAmount, Status);
```
All queried columns reside directly in the leaf pages of the non-clustered index. The query engine satisfies the query entirely via Index Seek without touching the clustered table pages, reducing I/O operations by orders of magnitude.

---

### 6. Explain how PostgreSQL Multiversion Concurrency Control (MVCC) handles updates without locking readers, and what phenomena are prevented under Serializable isolation.
**Answer:**
PostgreSQL implements MVCC by treating updates as a `DELETE` followed by an `INSERT`. Every table row (tuple) contains hidden header fields: `xmin` (the transaction ID that inserted the row) and `xmax` (the transaction ID that deleted/superseded the row).

- When a transaction updates a row, the existing row has its `xmax` set to the current transaction ID, and a new row version is inserted with `xmin` equal to the transaction ID.
- Readers inspect their transaction snapshot: any row version whose `xmin` is committed prior to the reader's snapshot and whose `xmax` is either uncommitted or committed after the snapshot is visible.
- Readers never block writers, and writers never block readers. Dead tuples are reclaimed asynchronously by `VACUUM`.

**Phenomena prevented under `Serializable`:**
1. **Dirty Reads**: Reading uncommitted mutations.
2. **Non-Repeatable Reads**: Re-reading a row within the same transaction yields altered values.
3. **Phantom Reads**: Re-executing a range query returns newly inserted rows.
4. **Write Skew**: Concurrent transactions read overlapping data sets, make mutually disjoint modifications, and commit states that violate business invariants (prevented via Serializable Snapshot Isolation / SSI tracking rw-antidependencies).

---

### 7. When should an enterprise architect choose Table Partitioning over Database Sharding, and what are the operational trade-offs?
**Answer:**
- **Table Partitioning (Vertical / Range within single RDBMS)**:
  - Divides a single logical table into physical sub-tables on the same database engine instance.
  - *Best suited for*: Managing time-series data lifecycle (e.g. dropping old monthly log partitions in $\mathcal{O}(1)$ via `ALTER TABLE ... DROP PARTITION`), query plan pruning for date-bounded queries, and storage tiering.
  - *Limitations*: Scaled vertically; constrained by single-node CPU, memory, and write I/O bandwidth.
- **Database Sharding (Horizontal Partitioning across multiple database nodes)**:
  - Distributes data across multiple distinct database servers partitioned by a Shard Key (e.g., TenantId or CustomerId hash).
  - *Best suited for*: Scaling beyond the write throughput or storage limit of the largest available single instance.
  - *Limitations*: Loss of foreign key referential integrity across shards, loss of distributed ACID transactions (requires two-phase commit or saga pattern), complex cross-shard aggregations, and challenging resharding/rebalancing operations.

---

### 8. How does `Interlocked.CompareExchange` enable lock-free algorithms in .NET, and what are the limitations compared to kernel synchronization?
**Answer:**
`Interlocked.CompareExchange` wraps hardware-level atomic instructions (such as `CMPXCHG` on x86/x64). It compares the value at a destination memory location with an expected value; if equal, it updates the destination with a new value in a single uninterruptible atomic CPU cycle.

**Lock-Free Stack Example:**
```csharp
public class LockFreeStack<T>
{
    private Node<T>? _head;

    public void Push(T item)
    {
        var node = new Node<T>(item);
        Node<T>? current;
        do
        {
            current = _head;
            node.Next = current;
        } while (Interlocked.CompareExchange(ref _head, node, current) != current);
    }
}
```

**Limitations vs Kernel Synchronization:**
1. **Spin Overhead Under Extreme Contention**: If many threads contend on the same memory location, CAS loops fail repeatedly and consume 100% CPU cycles spinning.
2. **ABA Problem**: If a pointer changes from A to B and back to A, a CAS operation succeeds even though state transitioned in between (mitigated via tagged pointers or versioned counters).
3. **Complexity**: Complex composite state mutations involving multiple invariants cannot be protected with a single 64-bit atomic CAS; transactional locks or STM are required.

---

### 9. What is a Cache Stampede (Thundering Herd), and what are the three architectural patterns used to prevent it in distributed systems?
**Answer:**
A Cache Stampede occurs when a high-traffic cache key expires or is invalidated. Thousands of concurrent requests simultaneously detect a cache miss and execute identical expensive database queries to regenerate the cached value, crashing the underlying database.

**Prevention Patterns:**
1. **Distributed Mutex / Lock**:
   - The first request detecting a cache miss acquires a distributed lock in Redis (`SET lock_key uuid NX EX 5`). Only the lock owner queries the DB and repopulates the cache; other requests wait or retry.
2. **Probabilistic Early Expiration (XFetch Algorithm)**:
   - Requests calculate whether to refresh the cache *before* it officially expires based on an exponential probability function:
     $$\Delta \times \beta \times \ln(\text{random}()) > \text{TTL}_{\text{remaining}}$$
   - As TTL approaches zero, the probability of an incoming request proactively refreshing the cache approaches 100%.
3. **Background Worker / Write-Behind Refresh**:
   - Web application threads never query the database on cache miss; cache items have no strict TTL. A background daemon periodically refreshes cache values asynchronously.

---

### 10. For LeetCode #297 (*Serialize and Deserialize Binary Tree*), compare the space and time trade-offs between Preorder DFS and Level-Order BFS codecs, and describe how you handle edge cases.
**Answer:**
Both codecs achieve optimal $\mathcal{O}(N)$ time complexity because each node is visited and serialized a constant number of times.

**Trade-off Comparison:**
| Metric | Preorder DFS Codec | Level-Order BFS Codec |
|---|---|---|
| **Traversal Order** | Root $\to$ Left subtree $\to$ Right subtree | Breadth-first level by level |
| **Data Structure** | Call stack recursion (or explicit stack) | FIFO `Queue<TreeNode?>` |
| **Memory Space** | $\mathcal{O}(H)$ where $H$ is tree height ($\mathcal{O}(\log N)$ balanced, $\mathcal{O}(N)$ skewed) | $\mathcal{O}(W)$ where $W$ is maximum width ($\le \frac{N+1}{2}$ for complete binary tree) |
| **Output String Size** | Emits $N$ values + $N+1$ sentinel markers (`#`) | May emit trailing `#` at the lowest leaf level unless trimmed |
| **Call Stack Risk** | Skewed trees with depth $10^5$ can cause `StackOverflowException` if recursive | No stack overflow risk; queue memory is heap-allocated |

**Edge Cases Handled:**
1. **Empty Tree (`root == null`)**: Serializes to single sentinel `#`; deserializer immediately returns `null`.
2. **Single Node / Negative Values**: String tokenizer accurately parses negative integers (`-42`) without treating `-` as a delimiter.
3. **Skewed Trees**: Nodes having only left or right children preserve structural shape because null sentinel tokens explicitly indicate missing branches.
