# Day 09 - Engineering Notes: SQL Execution Plans & Index Tuning

## 1. Anatomy of PostgreSQL `EXPLAIN (ANALYZE, BUFFERS)`

When running an execution plan in PostgreSQL, `EXPLAIN` produces a query tree of execution nodes. Adding `(ANALYZE, BUFFERS)` forces execution and reports actual run-time metrics alongside optimizer estimates:

```sql
EXPLAIN (ANALYZE, BUFFERS, VERBOSE, SETTINGS)
SELECT c.name, o.total_amount
FROM customers c
JOIN orders o ON c.id = o.customer_id
WHERE o.created_at >= '2026-01-01';
```

### Core Execution Node Types

| Node Type | Algorithm | Best Suited For | Red Flags / Bottlenecks |
|---|---|---|---|
| **Seq Scan** | Linear scan across every heap block | Small tables, queries returning > 15-20% of table | Large tables with highly selective filters |
| **Index Scan** | Traverses B-Tree, fetches matching `ctid` heap tuples immediately | High selectivity, retrieving small row counts with clustered or non-covering queries | Random I/O disk thrashing when returning many rows |
| **Index Only Scan** | Fetches all requested columns from B-Tree leaf pages without visiting heap | Queries covered by index columns + `INCLUDE` clause | High heap fetches if visibility map dirty (vacuum lag) |
| **Bitmap Index + Heap Scan** | Phase 1: Builds bitmap of matching heap pages from B-Tree. Phase 2: Sequential-ish scan of heap pages | Medium selectivity (5-15% of table), multiple OR/AND index combinations (`BitmapAnd`, `BitmapOr`) | Lossy bitmap conversion if `work_mem` exhausted |
| **Nested Loop** | Outer loop drives inner lookup | Very small outer set with indexed inner lookup | Outer set unexpectedly large -> $O(N \times M)$ explosion |
| **Hash Join** | Builds in-memory hash table on inner relation, probes with outer relation | Medium-to-large unindexed sets with equality joins | Hash table exceeding `work_mem`, spilling into multiple disk batches |
| **Merge Join** | Simultaneously steps through two pre-sorted inputs | Large tables pre-sorted by join keys (via B-Tree) | Requiring explicit `Sort` node on disk |

---

## 2. Cost Models and Estimation Formulas

PostgreSQL cost estimation is based on disk page I/O and CPU operation units:
- `seq_page_cost = 1.0` (baseline reference cost)
- `random_page_cost = 4.0` (traditional spinning disk) or `1.1 - 1.5` (modern NVMe SSDs)
- `cpu_tuple_cost = 0.01` (cost to process one row)
- `cpu_index_tuple_cost = 0.005` (cost to inspect an index entry)
- `cpu_operator_cost = 0.0025` (cost of evaluating a WHERE condition operator)

### Startup Cost vs. Total Cost: `(cost=0.00..452.30)`
- **Startup Cost (`0.00`)**: Time to fetch the very first row.
  - `Seq Scan` has startup cost `0.00` (can read first row immediately).
  - `Sort` or `Hash` has high startup cost (must read and hash all inner rows before emitting row 1).
- **Total Cost (`452.30`)**: Estimated total effort to process all matching rows.

---

## 3. Buffer Cache Metrics & Disk Spills

```
Buffers: shared hit=452 read=12 dirtied=3 written=0
```
- **`shared hit`**: Blocks retrieved directly from PostgreSQL `shared_buffers` cache (sub-microsecond memory access).
- **`shared read`**: Blocks fetched from OS filesystem cache or physical NVMe/SSD disk.
- **Cache Hit Ratio**:
  $$\text{Hit Ratio} = \frac{\text{shared hit}}{\text{shared hit} + \text{shared read}}$$
  A production OLTP system should maintain a buffer cache hit ratio $> 99\%$.

### Disk Spills and `work_mem`
```
Sort Method: external merge  Disk: 4280kB
```
When `Sort` or `Hash` operations exceed `work_mem`, PostgreSQL falls back to temporary disk files:
- Drastically increases query latency (CPU-bound query becomes I/O-bound).
- Remedy: Increase `work_mem` per-session or tune `maintenance_work_mem`.

---

## 4. Cardinality Estimation Skews
- `rows=10 (actual rows=125000)`:
  - When estimated rows diverge significantly from actual rows ($> 5\times$), the optimizer chooses incorrect join algorithms (e.g. choosing a `Nested Loop` thinking there are 10 rows, leading to 125,000 index lookups).
  - Cause: Outdated statistics, correlated columns, or missing extended statistics (`CREATE STATISTICS`).
  - Fix: Run `ANALYZE tablename;` or increase `default_statistics_target`.
