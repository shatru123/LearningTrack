# Day 08 - Senior .NET Interview Questions: Database Indexes & B-Trees

### Q1: How does a B-Tree index work under the hood in relational databases?
- **Short answer**: A B-Tree is a self-balancing search tree where each node corresponds to a fixed-size disk page (8KB) storing sorted keys and child pointers, guaranteeing $O(\log N)$ search, insertion, and deletion.
- **Deeper answer**: Starting from the Root page, the database binary-searches the page's keys to identify the downlink pointer to the next level down. It traverses down through internal pages until reaching a Leaf page. In the leaf page, each entry stores the indexed column value and a row locator (`ctid` in Postgres, Clustering Key or RID in SQL Server) to locate the actual row.
- **Practical example**: A 3-level B-tree with a fan-out of 400 keys per 8KB page can index $400^3 = 64,000,000$ rows with only 3 disk page reads!

---

### Q2: What is the difference between an Index Scan, a Bitmap Index Scan, and an Index-Only Scan?
- **Short answer**: An Index Scan reads heap pages one row at a time via index pointers. A Bitmap Index Scan collects qualifying page IDs in a bitmap and visits heap pages sequentially. An Index-Only Scan answers the query directly from the index without reading heap pages at all.
- **Deeper answer**: When selectivity is high (returning 1–5 rows), direct Index Scan is fastest. When a query returns hundreds of rows scattered across pages, Index Scan causes random I/O thrashing; Bitmap Index Scan sorts the heap page addresses first so disk blocks are read sequentially. An Index-Only Scan bypasses heap reads entirely if the index covers all requested columns and the Visibility Map confirms no pending MVCC transactions.
- **Practical example**: `SELECT CustomerId, Status FROM Orders WHERE CustomerId = 5` performs an Index-Only Scan if an index on `(CustomerId, Status)` exists.

---

### Q3: What is the purpose of the `INCLUDE` clause in a composite index?
- **Short answer**: It appends non-key payload columns to the leaf pages of the B-tree without storing them in intermediate nodes or sorting by them.
- **Deeper answer**: Adding columns to the index key increases index tree width, reduces node fan-out, and limits max key size. Columns in `INCLUDE` are stored only at the leaf level, allowing queries to achieve Index-Only Scans (avoiding heap fetches or SQL Server key lookups) while keeping internal tree levels narrow and fast.
- **Practical example**:
```sql
CREATE INDEX idx_orders_customer ON Orders (CustomerId) INCLUDE (TotalAmount, OrderDate);
```

---

### Q4: What is the Leftmost Prefix Rule in composite B-Tree indexes?
- **Short answer**: A composite index on columns `(A, B, C)` can only be used for index seek operations if the query filters on `(A)`, `(A, B)`, or `(A, B, C)`.
- **Deeper answer**: Because B-Tree keys are sorted lexicographically starting with column `A`, the database cannot perform a binary search on column `B` or `C` alone without knowing the value of `A`. A query filtering only on `WHERE B = 10` cannot use an index seek on `(A, B, C)` and must fallback to a full index scan or sequential table scan.
- **Practical example**: An index on `(VenueId, Status)` accelerates `WHERE VenueId = 1 AND Status = 'Open'` and `WHERE VenueId = 1`, but does NOT accelerate `WHERE Status = 'Open'`.

---

### Q5: How do scalar functions on indexed columns defeat B-Tree index traversal?
- **Short answer**: Wrapping an indexed column in a function (e.g. `WHERE UPPER(Email) = ...`) prevents the engine from using the index because the index stores raw values, not the computed output.
- **Deeper answer**: The B-Tree contains values ordered by `Email`. The query engine does not evaluate functions in reverse to search the tree. To optimize such queries, you must create a Functional/Expression Index on the computed result.
- **Practical example**:
```sql
CREATE INDEX idx_customers_upper_email ON Customers (UPPER(Email));
```

---

### Q6: What is Heap-Only Tuples (HOT) optimization in PostgreSQL?
- **Short answer**: An optimization that avoids updating B-Tree indexes when a row is updated, provided the update fits on the same 8KB data page and no indexed columns were modified.
- **Deeper answer**: In standard MVCC updates, a new row version is written and every index on the table must insert a new pointer to the new `ctid`. With HOT, the old row tuple points directly to the new row tuple inside the same page via a chain pointer. The index continues pointing to the original root tuple, eliminating index write amplification and index bloat.
- **Practical example**: Frequently updating a non-indexed `LastLoginAt` timestamp benefits from HOT and does not touch any table indexes.

---

### Q7: What causes B-Tree Page Splits and why are they harmful to write performance?
- **Short answer**: A page split occurs when an insert or update requires writing to an 8KB index leaf page that has no free space remaining.
- **Deeper answer**: When an 8KB page is full, the engine must allocate a new 8KB page, copy approximately half the keys to the new page, update linked list pointers (`btpo_prev`, `btpo_next`), and insert a new routing key into the parent node (which may cascade page splits up to the root). This creates heavy disk I/O, WAL logging, and index fragmentation.
- **Practical example**: Using random UUIDs (`Guid.NewGuid()`) as primary keys causes random insertions across all leaf pages, triggering continuous page splits compared to sequential IDs.

---

### Q8: What is Index Selectivity and how does the Query Optimizer use it?
- **Short answer**: Selectivity is the ratio of rows satisfying a query predicate to the total number of rows in the table ($\text{Selectivity} = \frac{\text{Matching Rows}}{\text{Total Rows}}$).
- **Deeper answer**: If a condition matches $< 1–5\%$ of the table (high selectivity), the optimizer chooses an Index Scan. If the condition matches $> 15–20\%$ of the table (low selectivity, like `WHERE IsDeleted = false`), reading the index plus random heap seeks costs more I/O than simply streaming the entire table sequentially.
- **Practical example**: An index on a boolean column with 50/50 distribution will almost never be used by the optimizer for an index seek.

---

### Q9: How does SQL Server Clustered Index differ from PostgreSQL Heap tables?
- **Short answer**: In SQL Server, a clustered index table stores the actual data rows directly in the leaf pages of the B-tree. In PostgreSQL, all tables are heaps, and all indexes are secondary non-clustered indexes.
- **Deeper answer**: Because SQL Server clustered index leaf pages ARE the data rows, querying by the clustered key (typically PK) requires no secondary lookup. However, secondary non-clustered indexes in SQL Server store the clustering key value at their leaf nodes, requiring a "Key Lookup" back into the clustered tree for non-covered columns.
- **Practical example**: Changing a wide clustered key in SQL Server inflates the size of *every* non-clustered index on that table.

---

### Q10: How do you safely detect index bloat and unused indexes in production PostgreSQL?
- **Short answer**: By querying `pg_stat_user_indexes` for usage counts (`idx_scan = 0`) and using `pgstattuple` or `pageinspect` to measure dead tuples and free space.
- **Deeper answer**: Querying:
```sql
SELECT relname, idx_scan FROM pg_stat_user_indexes WHERE idx_scan = 0;
```
identifies indexes that are never used by queries but still incur write overhead on every INSERT/UPDATE. For bloat, `pgstattuple` or `REINDEX CONCURRENTLY` can rebuild fragmented B-tree pages without locking table reads or writes.
- **Practical example**: Dropping 5 unused indexes on a high-throughput table can instantly cut write I/O and replication lag in half.
