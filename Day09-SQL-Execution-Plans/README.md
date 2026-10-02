# Day 09: SQL Execution Plans & Index Tuning

## Overview
Day 09 focuses on relational database query execution plan internals in PostgreSQL (`EXPLAIN (ANALYZE, BUFFERS)`), identifying execution node topologies, cost models, buffer pool behavior, and solving the sliding window algorithm challenge LeetCode #424.

---

## 1. PostgreSQL Execution Plan Architecture

When PostgreSQL parses and optimizes a query, the cost-based optimizer generates a tree of execution plan nodes.

### Common Scan Nodes:
- **Seq Scan (Sequential Scan)**: Scans every 8KB disk page of the relation sequentially. Optimal for small tables or when retrieving a large percentage of rows (>15–20%).
- **Index Scan**: Traverses the B-Tree index structure to locate leaf node index tuples containing Heap Tuple Pointers (`ctid`), then fetches the actual row from the heap page.
- **Index Only Scan**: Traverses the B-Tree index and reads required columns directly from index leaf pages. Verifies visibility using the PostgreSQL **Visibility Map**; avoids reading heap pages entirely if the page is marked all-visible.
- **Bitmap Index Scan & Bitmap Heap Scan**: Used when multiple index rows match or when combining multiple indexes via bitmap AND/OR. The index scan builds an in-memory TID bitmap of physical heap blocks; the bitmap heap scan then visits heap blocks in physical sequential disk order, minimizing random I/O.

### Common Join Nodes:
- **Nested Loop**: Iterates through outer rows and probes inner index for each row. Optimal for small outer result sets.
- **Hash Join**: Builds an in-memory hash table of the inner relation and probes it with outer tuples. If the hash table exceeds `work_mem`, it spills into temporary files on disk (catastrophic performance drop).
- **Merge Join**: Merges two already-sorted inputs in $O(N + M)$ time.

---

## 2. Reading `EXPLAIN (ANALYZE, BUFFERS)`

```text
Hash Join  (cost=3.25..35.40 rows=100 width=72) (actual time=0.042..0.890 rows=100 loops=1)
  Hash Cond: (orders.customer_id = customers.id)
  Buffers: shared hit=182 read=4 temp read=2 written=2
```

### Metrics Explained:
- `cost=3.25..35.40`:
  - `3.25`: Startup cost (estimated I/O + CPU effort before first row is emitted).
  - `35.40`: Total cost to emit all rows.
- `actual time=0.042..0.890`: Wall-clock time in milliseconds (startup..total).
- `rows=100 loops=1`: Actual rows emitted across loops.
- `Buffers: shared hit=182 read=4`:
  - `shared hit`: 182 blocks (182 × 8KB = 1.45 MB) found in PostgreSQL `shared_buffers` cache (no disk I/O).
  - `shared read`: 4 blocks read from operating system cache or disk storage.
  - `temp read/written`: Temporary files written to disk because `work_mem` was exceeded.

---

## 3. DSA Challenge: Longest Repeating Character Replacement (LeetCode #424)

- **Problem**: Given a string $s$ and integer $k$, choose at most $k$ characters and replace them with any other uppercase English letter. Return the length of the longest substring containing identical letters.
- **Optimal Approach**: Sliding Window with frequency table.
- **Invariants**:
  - In window $[L, R]$, window length is $R - L + 1$.
  - Replacement count is $(R - L + 1) - \text{maxFreq}$.
  - If replacements $> k$, shift $L$ forward and decrement character frequency.
- **Complexity**:
  - **Time**: $O(N)$ single pass over string.
  - **Space**: $O(1)$ auxiliary memory (26 English letters).
