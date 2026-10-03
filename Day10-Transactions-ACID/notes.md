# Day 10 - Engineering Notes: SQL Transactions, ACID & MVCC Internals

## 1. ACID Guarantees & Low-Level Mechanics

| Property | Meaning | Underlying Database Mechanism |
|---|---|---|
| **Atomicity** | All operations in a transaction succeed or none take effect. | Write-Ahead Logging (WAL) & Undo/Redo logs with transaction abort rollback. |
| **Consistency** | Database transitions from one valid state to another according to constraints. | Schema constraints, check conditions, foreign keys, and application-level business invariants. |
| **Isolation** | Concurrent transactions execute without interfering with one another. | Multi-Version Concurrency Control (MVCC) and Two-Phase Locking (2PL / SSI). |
| **Durability** | Once committed, data survives power loss, crashes, and OS restarts. | Synchronous WAL flush to persistent NVMe/SSD storage before `COMMIT` returns acknowledgment. |

---

## 2. PostgreSQL MVCC (Multi-Version Concurrency Control)

Unlike lock-based engines that block readers while writing, PostgreSQL uses MVCC:
> **"Readers never block writers, and writers never block readers."**

### Tuple Header Structure
Every row tuple on an 8KB page contains internal MVCC metadata:
- **`xmin` (32-bit/64-bit)**: The Transaction ID (XID) of the transaction that inserted the row.
- **`xmax` (32-bit/64-bit)**: The Transaction ID that deleted or updated the row (0 if live).
- **`t_infomask` (16-bit)**: Bit flags describing tuple state (`HEAP_XMIN_COMMITTED`, `HEAP_XMIN_INVALID`, `HEAP_XMAX_COMMITTED`).
- **`t_ctid`**: Physical location pointer `(page, offset)` pointing to the current or newer version of the tuple.

### Update Mechanism
When an `UPDATE` occurs:
1. PostgreSQL does NOT overwrite the existing row in place.
2. It sets `xmax = current_xid` on the old tuple.
3. It creates an entirely new tuple with `xmin = current_xid` and `xmax = 0`.
4. It sets the old tuple's `ctid` to point to the new tuple.
5. Obsolete versions are later purged by the `VACUUM` process.

---

## 3. ANSI Isolation Levels & Concurrency Anomalies

| Isolation Level | Dirty Read | Non-Repeatable Read | Phantom Read | Serialization Anomaly (Write Skew) |
|---|---|---|---|---|
| **Read Uncommitted** | Possible (prevented in PG) | Possible | Possible | Possible |
| **Read Committed** (PG Default) | Prevented | Possible | Possible | Possible |
| **Repeatable Read** | Prevented | Prevented | Prevented in PG | Possible |
| **Serializable** (SSI) | Prevented | Prevented | Prevented | Prevented |

### Anatomy of Anomalies

1. **Dirty Read (G1)**: Transaction T2 reads uncommitted modifications made by T1. If T1 rolls back, T2 operated on data that never existed. *(Note: PostgreSQL maps Read Uncommitted to Read Committed, completely prohibiting Dirty Reads).*
2. **Non-Repeatable Read (Fuzzy Read - G2a)**: T1 reads a row, T2 updates or deletes that row and commits. T1 re-reads the row and observes changed values.
3. **Phantom Read (A3)**: T1 executes a range query (`WHERE balance > 1000`). T2 inserts a new row satisfying the predicate and commits. T1 re-executes the query and sees a new "phantom" row.
4. **Write Skew**: Two concurrent transactions read overlapping datasets, make localized decisions based on constraints, and commit distinct modifications that collectively violate global business invariants (e.g. Doctor On-Call shift problem).
