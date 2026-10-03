# Day 10 - Senior .NET / Database Interview Questions: Transactions, ACID & MVCC

### Q1: What is the core architectural difference between pessimistic locking and MVCC?
- **Short answer**: Pessimistic locking acquires shared/exclusive locks to prevent concurrent conflicts, causing readers to block writers and writers to block readers. MVCC maintains multiple row versions with transaction timestamps, ensuring readers never block writers and writers never block readers.
- **Deeper answer**: Under 2-Phase Locking (2PL), reading a row takes a Shared Lock (S-lock), preventing any transaction from acquiring an Exclusive Lock (X-lock) to update that row. In high-traffic systems, this leads to lock contention, queueing, and deadlocks. MVCC stores historical row versions identified by `xmin` and `xmax`. Readers simply view the version corresponding to their snapshot timestamp without taking any locks on data rows.

---

### Q2: Why does PostgreSQL treat `READ UNCOMMITTED` as `READ COMMITTED`?
- **Short answer**: PostgreSQL's MVCC architecture inherently isolates transactions by checking tuple visibility against snapshots, making it impossible to read uncommitted tuple versions without deliberately bypassing the MVCC engine.
- **Deeper answer**: In lock-based databases (like SQL Server), Read Uncommitted allows "dirty reads" by skipping Shared Locks during reads. In PostgreSQL, there are no read locks to skip! When a reader inspects a tuple, it checks if `xmin` has committed. Because uncommitted tuples fail the visibility check, PostgreSQL naturally provides Read Committed guarantees at zero extra locking cost.

---

### Q3: What is "Write Skew", and which isolation level is required to prevent it?
- **Short answer**: Write skew occurs when two concurrent transactions read overlapping state, verify a business rule, and then update distinct, disjoint records such that the combined outcome violates the rule. It is only prevented by `SERIALIZABLE` isolation or explicit pessimistic locking (`SELECT FOR UPDATE`).
- **Deeper answer**: Consider a hospital requiring at least 1 doctor on-call. Doctors Alice and Bob are both on-call. Alice starts a transaction, sees 2 doctors on-call, and withdraws. Simultaneously, Bob starts a transaction, sees 2 doctors on-call, and withdraws. Both commit under `REPEATABLE READ` without row conflicts (Alice updated row A, Bob updated row B). The hospital now has 0 doctors on-call! Only `SERIALIZABLE` detects this serialization dependency and aborts one transaction.

---

### Q4: How does PostgreSQL implement Serializable Snapshot Isolation (SSI) without heavyweight table locks?
- **Short answer**: SSI tracks read-write dependencies at runtime using lightweight in-memory `SIREAD` lock flags and aborts transactions if a cycle (dependency cycle) is formed.
- **Deeper answer**: Traditional serializable engines use strict 2PL (table-level or range locks). PostgreSQL uses Serializable Snapshot Isolation (SSI). It does not block transactions. Instead, when a query reads a row or page, it creates a non-blocking `SIREAD` lock. If transaction T1 reads what T2 later writes, an rw-antidependency edge is recorded. If a cycle $T_1 \to T_2 \to T_1$ is detected, PostgreSQL raises error `40001: could not serialize access due to read/write dependencies among transactions`.

---

### Q5: How should a .NET application handle PostgreSQL serializable conflict exceptions (`40001`)?
- **Short answer**: Wrap the transaction in an automatic retry policy (e.g. using Polly or EF Core's `ExecutionStrategy`) with exponential backoff and jitter.
- **Deeper answer**: In Serializable isolation, conflicts and aborts are not bugs; they are expected concurrency control mechanisms. When Npgsql throws `PostgresException` with `SqlState == "40001"`, the application must rollback the aborted transaction and re-execute the entire unit of work from the beginning.
- **Practical example**:
```csharp
var strategy = dbContext.Database.CreateExecutionStrategy();
await strategy.ExecuteAsync(async () =>
{
    await using var tx = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable);
    // Execute business logic...
    await dbContext.SaveChangesAsync();
    await tx.CommitAsync();
});
```

---

### Q6: What is the Write-Ahead Log (WAL) and how does it guarantee durability during sudden server power loss?
- **Short answer**: WAL ensures changes are written sequentially and flushed to non-volatile disk before the corresponding database page in memory is modified or acknowledged to the client.
- **Deeper answer**: Writing random 8KB pages to disk on every transaction commit is prohibitively slow. Instead, PostgreSQL flushes an append-only WAL record containing the transaction changes. When a power crash occurs:
  1. Dirty pages in memory are lost.
  2. On reboot, the engine inspects the last checkpoint in the WAL.
  3. **REDO**: Replays all committed WAL records up to the crash point, restoring all committed changes into memory.
  4. **UNDO**: Discards uncommitted transaction modifications.

---

### Q7: What is the risk of long-running transactions in PostgreSQL?
- **Short answer**: Long-running transactions prevent `VACUUM` from cleaning up dead tuples, leading to severe table bloat, disk exhaustion, and degraded query performance.
- **Deeper answer**: A dead tuple cannot be reclaimed by `VACUUM` if its `xmax` is newer than the oldest active transaction's `xmin` (the transaction could theoretically still need to view the old version). If a transaction stays open for hours (e.g. an unclosed connection or slow analytical query), all updates and deletes across the entire database accumulate as dead rows on disk.

---

### Q8: What is the difference between `Pessimistic Concurrency` and `Optimistic Concurrency` in Entity Framework Core?
- **Short answer**: Pessimistic concurrency locks records at the database level (`SELECT FOR UPDATE`); Optimistic concurrency assumes no conflicts will occur and verifies a rowversion/timestamp column during `UPDATE`.
- **Deeper answer**: In EF Core, optimistic concurrency is configured via `.IsRowVersion()` or `[Timestamp]`. When executing `SaveChangesAsync()`, EF Core appends `WHERE Id = @id AND Version = @originalVersion`. If another process updated the row in the interim, `RowsAffected == 0`, and EF Core throws `DbUpdateConcurrencyException`.

---

### Q9: What happens when an unhandled exception occurs inside a `using var transaction = ...` block in .NET?
- **Short answer**: The transaction is automatically rolled back when the `IDbTransaction` is disposed.
- **Deeper answer**: `IDbTransaction` follows the RAII pattern. If `Commit()` or `CommitAsync()` was not explicitly executed before reaching the end of the `using` scope, the `Dispose()` method automatically issues a `ROLLBACK` command to the database server, restoring consistency.

---

### Q10: How do Deadlocks occur in database transactions and how do you design systems to prevent them?
- **Short answer**: Deadlocks occur when Transaction 1 holds a lock on resource A and requests resource B, while Transaction 2 holds resource B and requests resource A.
- **Deeper answer**: Database engines detect deadlocks via a directed wait-for graph, terminate one transaction with a deadlock error, and allow the other to proceed.
- **Prevention Rules**:
  1. **Consistent Lock Ordering**: Always acquire locks on entities in identical chronological order (e.g. always sort resource IDs: `var sortedIds = ids.OrderBy(x => x).ToList()`).
  2. **Short Transactions**: Minimize transaction duration; do not perform external HTTP calls or file I/O inside open database transactions.
  3. **Keep Isolation Minimal**: Use `Read Committed` unless higher levels are strictly required by business domain invariants.
