# Senior & Staff Engineering Interview Questions: Day 24 — Advanced SQL & Tree Max Path Sum

### Q1: What is the exact semantic difference between `ROW_NUMBER()`, `RANK()`, and `DENSE_RANK()`?
**Answer:**
Given duplicate values in the `ORDER BY` clause (e.g., scores `[100, 90, 90, 80]`):
- **`ROW_NUMBER()`**: Generates a strictly unique, contiguous sequence of integers (`1, 2, 3, 4`) regardless of value ties. The ordering among tied rows is non-deterministic unless additional tie-breaker columns are specified.
- **`RANK()`**: Assigns identical ranks to tied rows, but skips subsequent rank numbers to match the row's 1-based ordinal position (`1, 2, 2, 4`). It leaves gaps in the sequence.
- **`DENSE_RANK()`**: Assigns identical ranks to tied rows without leaving gaps in the sequence (`1, 2, 2, 3`).
- *Use cases*:
  - Pagination / de-duplication: Use `ROW_NUMBER()`.
  - Competition leaderboards (Olympic medal ties): Use `RANK()`.
  - Salary bands / tier categories: Use `DENSE_RANK()`.

---

### Q2: How does the PostgreSQL query engine execute window functions, and how do you optimize them?
**Answer:**
1. **Execution Steps**:
   - The query planner groups rows via `PARTITION BY` and sorts them by `ORDER BY`.
   - Rows flow into a `WindowAgg` execution node which tracks state across the partitioned frame.
2. **Optimization Techniques**:
   - **Composite Index Matching**: Create an index with the exact column order: `CREATE INDEX idx_data ON my_table (partition_col, order_col)`. This allows the engine to stream rows directly into `WindowAgg` without an expensive `Sort` step.
   - **`work_mem` Tuning**: If the partition data exceeds `work_mem`, PostgreSQL spills sorted batches to disk (`external merge Disk`), multiplying query execution time. Increase `work_mem` specifically for analytical workloads.
   - **Filter Pushdown via CTE / Subquery**: Window functions cannot appear in `WHERE` clauses. Wrap in a subquery or CTE and filter on the outer query (e.g. `WHERE row_num = 1`).

---

### Q3: Why is specifying `ROWS` instead of the default `RANGE` framing critical for window query performance?
**Answer:**
- **The Pitfall**: In SQL, specifying `ORDER BY` without a frame clause defaults to `RANGE BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW`.
- **The Problem with `RANGE`**: `RANGE` groups rows with identical order values as "peers". The engine must read ahead to verify whether the next row has the same value before emitting a result. This forces buffering and prevents single-pass streaming.
- **The Fix with `ROWS`**: Explicitly specify `ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW` or `ROWS BETWEEN 5 PRECEDING AND CURRENT ROW`. `ROWS` operates purely on row counts regardless of duplicates, executing in a high-speed $O(N)$ streaming pass with minimal memory overhead.

---

### Q4: Explain the difference between Materialized and Inlined CTEs (`WITH` clauses) in PostgreSQL.
**Answer:**
- **Prior to PostgreSQL 12**: All CTEs acted as optimization fences—PostgreSQL always materialized CTE results into a temporary table in memory, preventing the optimizer from pushing outer `WHERE` predicates into the CTE.
- **PostgreSQL 12+ Behavior**: By default, simple CTEs referenced only once are **inlined** into the main query plan, allowing predicate pushdown and index scans across CTE boundaries.
- **Explicit Keywords**:
  - `WITH data AS MATERIALIZED (...)`: Forces PostgreSQL to evaluate the CTE once and store the result set in memory, preventing repeated execution if referenced multiple times in union or joins.
  - `WITH data AS NOT MATERIALIZED (...)`: Explicitly forces inlining, allowing the optimizer to push join conditions and indexes deep into the subquery.

---

### Q5: How does declarative table partitioning work in PostgreSQL, and how does the planner perform Partition Pruning?
**Answer:**
- **Architecture**: A root parent table defines partition keys (`PARTITION BY RANGE (recorded_at)`), while child tables store actual physical tuples.
- **Partition Pruning**:
  - **Static Pruning (Plan-Time)**: If the query specifies literal constants (`WHERE recorded_at >= '2026-10-01' AND recorded_at < '2026-11-01'`), the optimizer evaluates partition constraints and excludes child tables from the generated plan.
  - **Dynamic Pruning (Run-Time)**: If queries use parameterized values or dynamic functions (`WHERE recorded_at >= $1`), pruning occurs at execution time as parameters bind.
- **Impact**: Transforms full table scans over multi-terabyte datasets into localized scans over a single 10 GB partition, reducing disk I/O and buffer cache churn by 99%+.

---

### Q6: How do you perform zero-downtime data archival on high-volume tables (e.g. dropping data older than 90 days)?
**Answer:**
- **Anti-Pattern (`DELETE WHERE`)**:
  - Executing `DELETE FROM logs WHERE created_at < NOW() - INTERVAL '90 days'` locks rows, writes massive dead tuples to WAL, exhausts replication bandwidth, and leaves table bloat that requires intrusive vacuum operations.
- **Partitioning Strategy (`DROP TABLE`)**:
  - Partition the table by month or week.
  - To retire data older than 90 days, run:
    ```sql
    ALTER TABLE logs DETACH PARTITION logs_y2025m01;
    DROP TABLE logs_y2025m01;
    ```
  - `DETACH CONCURRENTLY` decouples the partition without taking an exclusive lock on the parent table.
  - `DROP TABLE` instantaneously unlinks the underlying disk files at the filesystem level in milliseconds with zero WAL bloat.

---

### Q7: Does PostgreSQL support global indexes across partitioned tables? How should indexes be designed?
**Answer:**
- **No Global Indexes**: Unlike Oracle, PostgreSQL does **not** support global indexes spanning across multiple partitions. Every index in PostgreSQL is local to its specific child partition.
- **Implications for Primary Keys**:
  - Any `PRIMARY KEY` or `UNIQUE` constraint on a partitioned table **must include the partition key columns** (e.g. `PRIMARY KEY (recorded_at, id)`).
  - Without the partition key, PostgreSQL cannot enforce uniqueness across different child tables without scanning all partitions.
- **Local Index Optimization**: Each child table can have specialized local indexes tailored to its data access patterns (e.g. partial indexes or BRIN indexes for older read-only archival partitions vs B-Trees for recent partitions).

---

### Q8: How do you write an analytical query to calculate rolling 7-day moving averages and daily growth deltas on time-series data?
**Answer:**
```sql
WITH DailyTotals AS (
    SELECT 
        account_id,
        DATE_TRUNC('day', trans_time) AS trans_day,
        SUM(amount) AS daily_amount
    FROM transactions
    WHERE trans_time >= NOW() - INTERVAL '60 days'
    GROUP BY account_id, DATE_TRUNC('day', trans_time)
)
SELECT 
    account_id,
    trans_day,
    daily_amount,
    -- 1. Day-over-day delta using LAG
    daily_amount - LAG(daily_amount, 1, 0) OVER (
        PARTITION BY account_id ORDER BY trans_day
    ) AS daily_delta,
    -- 2. 7-day moving average using frame specification
    AVG(daily_amount) OVER (
        PARTITION BY account_id 
        ORDER BY trans_day 
        ROWS BETWEEN 6 PRECEDING AND CURRENT ROW
    ) AS rolling_7day_avg
FROM DailyTotals
ORDER BY account_id, trans_day;
```

---

### Q9: Walk through LeetCode #124 (Binary Tree Maximum Path Sum). Why does a node calculate two different sums during traversal?
**Answer:**
- **The Core Constraint**: A valid tree path cannot fork; a node can only connect to at most one parent and at most one child branch when extended.
- **The Two Sums**:
  1. **Peak Anchor Sum (Internal Candidate)**:
     $$\text{PathSum} = \text{node.val} + \max(0, \text{leftGain}) + \max(0, \text{rightGain})$$
     Here, the path turns around `node` (traveling from the left child, through `node`, down to the right child). This path cannot be extended further up to the parent. We update the global maximum with this value.
  2. **Branch Contribution (Returned to Parent)**:
     $$\text{Gain}_{\text{return}} = \text{node.val} + \max(0, \max(\text{leftGain}, \text{rightGain}))$$
     The node can extend only ONE branch to its parent. Negative branch sums are clamped to $0$ to avoid diminishing the total.
- **Complexity**: $O(N)$ time visiting each node once; $O(H)$ recursion stack space.

---

### Q10: How do you handle edge cases in LeetCode #124 when all node values in the tree are negative?
**Answer:**
- **The Edge Case**: Consider a tree where every node is negative, e.g. `[-5, -10, -2]`.
- **Handling**:
  - The problem requires a non-empty path (at least one node must be picked).
  - If we initialize `maxSum = 0`, the solver would erroneously return `0` instead of `-2`!
  - **Correct Approach**:
    1. Initialize `maxSum = int.MinValue`.
    2. Clamp subtree gains with `Math.Max(0, childGain)`. For leaf nodes with negative values, `leftGain = 0` and `rightGain = 0`.
    3. The peak anchor sum is `node.val + 0 + 0 = node.val`.
    4. The global maximum updates to `Math.Max(int.MinValue, -2) = -2`.
    5. The result correctly selects the single least negative node.
