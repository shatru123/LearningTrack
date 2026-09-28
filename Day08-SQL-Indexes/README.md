# Day 08 - PostgreSQL & SQL Server B-Tree Index Architecture, Page Layout & Query Optimization

## Objective
Analyze B-Tree index physical layout and page structures in PostgreSQL and SQL Server, inspect index metadata using PostgreSQL's `pageinspect` extension, compare execution plans with `EXPLAIN (ANALYZE, BUFFERS)` across 4 distinct indexing scenarios, and implement data access using Dapper.

## Original Learning Tasks
1. Inspect PostgreSQL / SQL Server B-Tree page structure
2. DSA: Longest Substring Without Repeating Characters

## Practical Problem
Database queries that perform Sequential Scans across millions of rows cause severe disk I/O bottlenecks, buffer cache eviction, and high query latency in production. Furthermore, blindly adding indexes can degrade write throughput, cause index bloat, and fail to be utilized if queries violate the leftmost prefix rule, wrap indexed columns in scalar functions, or use leading wildcards (`LIKE '%...'`).

## What I Implemented
1. **Realistic Ticketing Schema & High-Volume Seeding**:
   - `Venues`, `Events`, `Customers`, `Orders`, and `Tickets` with realistic constraints and foreign keys.
   - High volume data generation script (`generate_series`) generating $150,000+$ rows for meaningful query plans.
2. **Safe Read-Only B-Tree Inspection (`pageinspect`)**:
   - Inspecting Meta Page (`bt_metap`): root block, tree height/depth, fast root, and engine version.
   - Inspecting Page Statistics (`bt_page_stats`): live items, dead items, free space, and page types (leaf, internal, root).
   - Inspecting Page Items (`bt_page_items`): item offsets, `ctid` (heap tuple pointers `(block, offset)`), and indexed data keys.
3. **EXPLAIN ANALYZE 4 Critical Scenarios**:
   - **Case 1: No Useful Index**: Sequential Scan with high shared buffer hits and filter overhead.
   - **Case 2: Single-Column B-Tree Index**: Bitmap Index Scan and Index Scan.
   - **Case 3: Composite Covering Index with INCLUDE**: Index-Only Scan with **zero heap fetches**.
   - **Case 4: Poorly Designed Indexes**: Function wrapping columns (`UPPER(Email)`), leading wildcards, and leftmost prefix violations.
4. **C# Dapper Data Access Repository**:
   - `DapperEventTicketRepository`: High-performance parameterized queries utilizing Dapper micro-ORM.
   - In-memory testing suite validating query mapping, sorting, and joins.
5. **DSA Solution**:
   - `LengthOfLongestSubstring`: Sliding window tracking character last-seen positions in $O(n)$ time and $O(1)$ auxiliary space.

## Architecture
```text
                     8KB B-Tree Index Page (PostgreSQL)
┌────────────────────────────────────────────────────────────────────────┐
│ PageHeaderData (24 bytes: pd_lsn, pd_checksum, pd_lower, pd_upper)     │
├────────────────────────────────────────────────────────────────────────┤
│ ItemIdData Array (Linp Pointers: 4 bytes each)                         │
│ [Linp 1] [Linp 2] [Linp 3] ... ───► grows downwards                    │
├────────────────────────────────────────────────────────────────────────┤
│ Free Space (available for new index entries before page split)        │
├────────────────────────────────────────────────────────────────────────┤
│ Index Tuple Data (Key value + ctid pointing to heap row) ◄── grows up  │
├────────────────────────────────────────────────────────────────────────┤
│ BTPageOpaqueData (Special Space, 16 bytes: btpo_prev, btpo_next, level)│
└────────────────────────────────────────────────────────────────────────┘

Traversal:
Query: EventId = 42
  Root Page ──► Downlink Keys ──► Leaf Page (Block X) ──► ctid (Page 12, Item 4) ──► Heap Table Row
```

## Key Concepts
- **B-Tree Structure**: Balanced search tree where all leaf pages reside at the exact same depth. Internal nodes store downlinks and routing keys; leaf nodes store indexed column values and heap tuple identifiers (`ctid` in Postgres, RID or Clustered Key in SQL Server).
- **8KB Page Architecture**: Both PostgreSQL and SQL Server store data in 8192-byte pages. Understanding page capacity ($N = \frac{\text{PageSize} - \text{Headers}}{\text{KeySize} + \text{PointerSize}}$) reveals how wide index keys cause shallow fan-out and deeper trees.
- **Index-Only Scan**: When an index covers all columns requested by a query (e.g. via `INCLUDE`), PostgreSQL reads solely from the index pages and avoids accessing table heap pages entirely.
- **HOT (Heap-Only Tuples)**: In PostgreSQL, if an updated row fits on the same page and no indexed columns were modified, PostgreSQL avoids creating new B-tree index entries, dramatically reducing write overhead and index bloat.
- **SQL Server Contrast**: In SQL Server, a table can be a Clustered Index (leaf level IS the data pages) or a Heap. Non-clustered indexes point to the Clustering Key (or RID if heap), resulting in "Key Lookups" unless covered.

## Testing
- Automated test suite (`SqlIndexesTests`, `DsaExercisesTests`).
- Verifies Dapper mapping and query execution for joined details, filtered ticket ordering, and customer orders.
- Validates B-Tree page capacity calculation and tuple packing formulas.
- Validates longest substring sliding window algorithm across edge cases.

## Performance Observations
- An Index-Only Scan using composite keys with `INCLUDE` reduced buffer hits from $>1000$ pages to only $3$ pages.
- Wrapping a column in a scalar function (`UPPER(col)`) causes the query planner to reject standard B-tree indexes, forcing a sequential table scan unless an expression index is created.

## Production Relevance
- Poor indexing is the leading cause of database CPU exhaustion and query timeouts in high-scale systems. Proper use of covering indexes (`INCLUDE`), avoiding leading wildcards, and understanding selectivity are essential for senior backend engineers.

## Common Mistakes
1. Indexing low-cardinality columns (e.g., `IsActive` boolean) with standard B-trees where sequential scans are faster.
2. Creating redundant indexes (e.g., indexing `(A)` when `(A, B)` already exists).
3. Over-indexing write-heavy tables, which multiplies write amplification and WAL generation.
4. Using `SELECT *` in queries, which prevents the query engine from performing Index-Only Scans.

## Senior Interview Questions
See [interview-questions.md](file:///Users/shatrughnaambhore/Shatru/Learning/Projects/LearningTrack/Day08-SQL-Indexes/interview-questions.md) for 10 in-depth architectural questions.

## What I Practically Understood
- The internal anatomy of an 8KB B-tree page (`PageHeaderData`, `ItemIdData`, `BTPageOpaqueData`).
- How to safely inspect production index health using PostgreSQL's `pageinspect` extension without locking tables.
- The difference between `Index Scan`, `Bitmap Index Scan`, and `Index-Only Scan` in execution plans.

## What I Need to Read Later
- PostgreSQL GiST, GIN, and BRIN indexes for geometric, full-text, and timeseries data.
- SQL Server Columnstore indexes and batch-mode processing on rowstores.

## Key Takeaways
Indexes are not magic flags; they are physical B-Tree data structures residing on 8KB disk pages. Designing high-performance database access requires aligning query predicates with leftmost prefix rules, utilizing covering indexes to eliminate heap fetches, and monitoring index bloat.
