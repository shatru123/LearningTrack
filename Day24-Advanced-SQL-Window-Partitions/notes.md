# Day 24: Deep Dive — SQL Windowing, Partitioning & Tree Path Optimization

## 1. Window Functions Internals in PostgreSQL

Window functions perform calculations across sets of table rows related to the current row without collapsing multiple rows into a single summary row (unlike `GROUP BY`).

### 1.1 The PostgreSQL WindowAgg Node
- In an `EXPLAIN (ANALYZE, BUFFERS)` execution plan, window functions introduce a **`WindowAgg`** node.
- **Pre-requisite**: The input rows must be sorted according to `PARTITION BY` and `ORDER BY`. If no index matches the ordering, PostgreSQL inserts an explicit `Sort` node.
- **Memory Impact**: The sorted rows are spooled into a tuplestore in memory (`work_mem`). If the partition size exceeds `work_mem`, it spills to temporary disk files (`Sort Method: external merge Disk`), causing severe I/O degradation.

### 1.2 Window Framing: `ROWS` vs `RANGE`
- **Default Framing**: When `ORDER BY` is specified without an explicit frame, SQL defaults to:
  $$\text{RANGE BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW}$$
- **Performance Hazard**: `RANGE` evaluates duplicate peer values (all rows with the same `ORDER BY` value belong to the same range). This requires looking ahead and buffering rows.
- **Best Practice**: Always specify `ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW` (or custom bounds) if exact physical rows are desired. `ROWS` executes in a single streaming pass ($O(N)$), saving CPU and memory.

---

## 2. Declarative Table Partitioning Architecture

PostgreSQL declarative partitioning splits large tables into smaller physical chunks while presenting a single virtual relation to applications.

```
                  ┌─────────────────────────────────┐
                  │ sensor_readings (Virtual Parent)│
                  │   PARTITION BY RANGE (recorded) │
                  └────────────────┬────────────────┘
                                   │
         ┌─────────────────────────┼─────────────────────────┐
         ▼                         ▼                         ▼
┌──────────────────┐      ┌──────────────────┐      ┌──────────────────┐
│  sensor_2026_m08 │      │  sensor_2026_m09 │      │  sensor_2026_m10 │
│ [Aug 1 - Sep 1)  │      │ [Sep 1 - Oct 1)  │      │ [Oct 1 - Nov 1)  │
└──────────────────┘      └──────────────────┘      └──────────────────┘
```

### 2.1 Partition Pruning Mechanics
- **Static Pruning**: Occurs during query planning when query constants are known (`WHERE recorded_at >= '2026-10-01'`). The optimizer removes non-matching partitions from the plan before execution begins.
- **Run-Time Pruning**: Occurs during execution when filters involve parameters or volatile functions (`WHERE recorded_at >= CURRENT_DATE - 7`). The executor evaluates bounds on the fly and skips entire sub-table scans.

### 2.2 Maintenance: `DROP TABLE` vs. `DELETE`
- **Why `DELETE` Fails on Big Data**:
  - `DELETE FROM sensor_readings WHERE recorded_at < '2025-01-01'` writes an MVCC dead tuple marker for every deleted row.
  - Generates massive Write-Ahead Log (WAL) traffic, saturating replication bandwidth.
  - Leaves table and index disk pages bloated until costly `VACUUM FULL` is performed.
- **The Partitioning Advantage**:
  - `DROP TABLE sensor_readings_y2025m01;`
  - Instantly unlinks the underlying disk file in $O(1)$ time.
  - Generates negligible WAL entries, causes zero disk fragmentation, and requires zero vacuum debt.

---

## 3. DSA: LeetCode #124 (Binary Tree Maximum Path Sum)

### 3.1 The Two Distinctions of a Node's Value
In a binary tree, a valid path can turn at most once at its highest peak node:
1. **The Peak Anchor Path**:
   $$\text{Price}_{\text{peak}} = \text{node.val} + \max(0, \text{leftGain}) + \max(0, \text{rightGain})$$
   This path cannot be extended further upward to the parent because doing so would form a branch fork, violating the path constraint.
2. **The Branch Gain Contributed to Parent**:
   $$\text{Gain}_{\text{parent}} = \text{node.val} + \max(0, \max(\text{leftGain}, \text{rightGain}))$$
   This selects at most ONE child branch so the parent can extend the path upward.

### 3.2 Complexity
- **Time Complexity**: $O(N)$ — Each node is visited exactly once via post-order DFS.
- **Space Complexity**: $O(H)$ — Recursion call stack bounded by tree height ($O(\log N)$ balanced, $O(N)$ skewed).
