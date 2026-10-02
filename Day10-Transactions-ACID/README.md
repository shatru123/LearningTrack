# Day 10: SQL Transactions, ACID & Isolation Levels

## Overview
Day 10 covers relational database transaction guarantees (ACID), Write-Ahead Logging (WAL), multi-version concurrency control (MVCC) in PostgreSQL, simulating transaction isolation anomalies (Dirty Read, Non-Repeatable Read, Phantom Read, Write Skew), and classic stack data structures (Valid Parentheses & MinStack).

---

## 1. ACID Guarantees & PostgreSQL MVCC

- **Atomicity**: All changes within a `BEGIN ... COMMIT` block succeed or all are rolled back. Guaranteed via Write-Ahead Log (WAL) records before page flushes to disk.
- **Consistency**: Invariants and constraints (foreign keys, uniqueness, check constraints) are preserved across transactions.
- **Isolation**: Concurrent transactions execute without cross-contamination.
- **Durability**: Committed data survives system crashes once WAL is fsynced to non-volatile storage.

### PostgreSQL MVCC Tuple Structure:
Every row in a PostgreSQL heap page includes system columns:
- `xmin`: 32-bit transaction ID (XID) that inserted this tuple.
- `xmax`: 32-bit XID of the transaction that updated or deleted this tuple (0 if live).
- `t_ctid`: Physical tuple identifier `(block_number, offset)`.

---

## 2. SQL Isolation Levels & Concurrency Anomalies

| Isolation Level | Dirty Read | Non-Repeatable Read | Phantom Read | Serialization Anomaly (Write Skew) |
| :--- | :---: | :---: | :---: | :---: |
| **Read Uncommitted** | Allowed in ANSI *(Prevented in PG)* | Allowed | Allowed | Allowed |
| **Read Committed** *(PG Default)* | **Prevented** | Allowed | Allowed | Allowed |
| **Repeatable Read** | **Prevented** | **Prevented** | **Prevented in PG** *(Snapshot at Tx start)* | Allowed |
| **Serializable (SSI)** | **Prevented** | **Prevented** | **Prevented** | **Prevented** *(SIREAD locks abort on conflict)* |

> [!NOTE]
> In PostgreSQL, `Repeatable Read` prevents **both** Non-Repeatable Reads and Phantom Reads because a single MVCC snapshot is captured at transaction start (`xmin`, `xmax`, `xip_list`). Only **Serializable** prevents Write Skew / Serialization Anomalies via Serializable Snapshot Isolation (SSI).

---

## 3. DSA Challenges: Stack Data Structures

### LeetCode #20: Valid Parentheses
- Uses a `Stack<char>` to verify matched pairs `()`, `[]`, `{}` in $O(N)$ time and $O(N)$ space.
- Fast-fail condition: Odd string lengths are immediately invalid.

### LeetCode #155: Min Stack
- Supports `Push`, `Pop`, `Top`, and `GetMin` in $O(1)$ constant time.
- Implemented using pairs `(Value, CurrentMin)` where `CurrentMin = Math.Min(val, previousMin)`.
