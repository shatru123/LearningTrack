# Day 09 - Senior .NET / Database Interview Questions: SQL Execution Plans & Index Tuning

### Q1: What is the difference between `EXPLAIN` and `EXPLAIN ANALYZE` in PostgreSQL?
- **Short answer**: `EXPLAIN` estimates costs and row counts without executing the query; `EXPLAIN ANALYZE` actually executes the query, outputting real elapsed execution times, actual row counts, and buffer metrics.
- **Deeper answer**: `EXPLAIN` uses catalog statistics (`pg_statistic`) and cost formulas to generate the planned tree. Running `EXPLAIN ANALYZE` triggers real execution, including side effects (e.g. running an `UPDATE` or `DELETE` inside `EXPLAIN ANALYZE` will modify actual data unless wrapped in a rolled-back transaction).
- **Practical example**:
```sql
BEGIN;
EXPLAIN (ANALYZE, BUFFERS) DELETE FROM orders WHERE status = 'Archived';
ROLLBACK; -- Prevents permanent deletion during query plan profiling
```

---

### Q2: What causes an Index Scan to be chosen over a Seq Scan, and when might the optimizer deliberately ignore an index?
- **Short answer**: The optimizer selects an Index Scan when the filter is selective (returning a small fraction of total table rows). It deliberately ignores an index when the query retrieves a large percentage of rows (e.g., > 15-20%), because sequential I/O is faster than random page lookups.
- **Deeper answer**: In an unclustered B-Tree index scan, each matched index leaf entry contains a `ctid` (page, offset) pointing to a physical heap block. If 25% of rows match, almost every 8KB table page will need to be visited. Reading pages randomly via index lookups is significantly more expensive than streaming the table sequentially block-by-block (`seq_page_cost = 1.0` vs `random_page_cost = 4.0`).
- **Practical tip**: If an index is ignored on a selective query, verify table statistics with `ANALYZE tablename` or check if a function call prevents index usage (e.g. `WHERE LOWER(email) = '...'` without an expression index).

---

### Q3: What is the difference between an Index Scan, an Index Only Scan, and a Bitmap Index Scan?
- **Short answer**: 
  - `Index Scan`: Traverses B-tree and visits the heap table for each row immediately.
  - `Index Only Scan`: Reads all data directly from index leaf pages without visiting the heap (requires clean visibility map).
  - `Bitmap Index Scan`: Collects matching heap block IDs into an in-memory bitmap, then visits heap pages sequentially.
- **Deeper answer**: 
  - In `Bitmap Index Scan`, individual row order is lost, but random I/O is converted into sequential I/O. It also allows combining multiple indexes with bitwise `BitmapAnd` and `BitmapOr`.
  - In `Index Only Scan`, PostgreSQL must check the page's visibility map bit. If tuples on that page were recently modified and not yet frozen by vacuuming, it must still perform a heap fetch (`Heap Fetches: N`).

---

### Q4: What does "Sort Method: external merge Disk" mean in an execution plan, and how do you remediate it?
- **Short answer**: The query's sorting or hashing operation exceeded the available in-memory `work_mem` buffer, forcing PostgreSQL to spill intermediate data onto disk temporary files.
- **Deeper answer**: Disk spills degrade performance by an order of magnitude due to disk write and read latency. Remediate by:
  1. Tuning `work_mem` specifically for the query session: `SET LOCAL work_mem = '64MB';`.
  2. Adding a composite index matching the `ORDER BY` clause so sorting is pre-computed by the B-Tree leaf order.
  3. Eliminating unnecessary columns from the `SELECT` list to reduce row width.

---

### Q5: What is a cardinality estimation skew, and why is it dangerous in complex join queries?
- **Short answer**: A mismatch between the optimizer's estimated row count and actual runtime row count (`rows=1 (actual rows=50000)`).
- **Deeper answer**: The query optimizer makes critical decisions based on estimated cardinality:
  - If it estimates 1 row, it will select a `Nested Loop Join`. If the actual rows are 50,000, it performs 50,000 separate index probes ($O(N \times M)$), locking CPU and thrashing the cache.
  - If it estimated 50,000 rows correctly, it would have chosen a `Hash Join` or `Merge Join` ($O(N + M)$).
- **Remediation**: Run `ANALYZE`, increase `default_statistics_target` (from 100 to 500 or 1000), or create multi-column extended statistics:
```sql
CREATE STATISTICS stat_orders_cust_status ON customer_id, status FROM orders;
ANALYZE orders;
```

---

### Q6: How do covering indexes with the `INCLUDE` clause improve query performance?
- **Short answer**: They allow `Index Only Scans` by storing non-key payload columns directly in the leaf pages without bloating the B-Tree root/internal navigation nodes.
- **Deeper answer**: In standard composite indexes (`CREATE INDEX ON orders (customer_id, total_amount)`), both columns participate in B-Tree ordering. In covering indexes (`CREATE INDEX ON orders (customer_id) INCLUDE (total_amount)`), only `customer_id` is sorted; `total_amount` is appended only to the leaf level. This keeps index search trees shallow, lowers index maintenance costs on insert, and avoids all heap lookups for queries filtering on `customer_id` and returning `total_amount`.

---

### Q7: What is the significance of `shared hit` vs `shared read` in `EXPLAIN (BUFFERS)`?
- **Short answer**: `shared hit` represents 8KB database pages served from RAM (`shared_buffers`), while `shared read` represents pages fetched from disk/OS filesystem cache.
- **Deeper answer**: An ideal OLTP query should have a near 100% buffer hit ratio (`shared read = 0`). If a query consistently has high `shared read`, it indicates cold cache, missing indexes, sequential scanning of large tables, or `shared_buffers` undersizing.
- **Formula**:
$$\text{Buffer Hit Ratio} = \frac{\text{shared hit}}{\text{shared hit} + \text{shared read}} \times 100\%$$

---

### Q8: Why can adding an index sometimes slow down an application?
- **Short answer**: Every index imposes write amplification on `INSERT`, `UPDATE`, and `DELETE` operations, increases database storage footprint, and consumes memory in `shared_buffers`.
- **Deeper answer**:
  1. On every `INSERT`, every table index must be updated and may trigger 8KB page splits.
  2. On `UPDATE`, if an indexed column changes, it prevents HOT (Heap-Only Tuples) optimization, forcing new index entries across all indexes on the table.
  3. Indexes compete with table data for buffer cache space. Unused or redundant indexes cause cache pollution.

---

### Q9: How does parameter sniffing or prepared statement caching affect query plans in .NET applications?
- **Short answer**: A generic plan generated for the first executed parameter values may be suboptimal for subsequent parameters with different data distributions.
- **Deeper answer**: In Npgsql and Dapper, prepared statements use custom plans for the first 5 executions, after which PostgreSQL evaluates whether to switch to a generic plan. If parameter 1 matches 2 rows (favoring an Index Scan) but parameter 2 matches 1,000,000 rows (favoring a Seq Scan), reusing the cached plan results in severe performance degradation.
- **Remediation**: In Npgsql, use `NpgsqlCommand.DesignTimeVisible = false` or adjust `PrepareThreshold` to prevent inappropriate plan sharing.

---

### Q10: How do you identify missing indexes in PostgreSQL production environments?
- **Short answer**: Inspect `pg_stat_user_tables` for tables with high `seq_scan` relative to `idx_scan`, and analyze slow query logs via `pg_stat_statements`.
- **Deeper answer**:
```sql
SELECT 
    schemaname, relname, 
    seq_scan, idx_scan, 
    ROUND(100.0 * idx_scan / NULLIF(seq_scan + idx_scan, 0), 2) AS idx_scan_percent
FROM pg_stat_user_tables
WHERE (seq_scan + idx_scan) > 1000
ORDER BY seq_scan DESC
LIMIT 10;
```
Tables where `idx_scan_percent` is low indicate high sequential scanning and prime candidates for targeted index analysis using `EXPLAIN (ANALYZE, BUFFERS)`.
