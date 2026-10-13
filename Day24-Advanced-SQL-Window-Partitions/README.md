# Day 24: Advanced SQL — CTEs, Window Functions & Table Partitions

Comprehensive analytical SQL engineering lab implementing ANSI window functions, declarative table partitioning with partition pruning simulation, and LeetCode #124 (Binary Tree Maximum Path Sum - Hard).

---

## 🎯 Architecture & Objectives

1. **SQL Window Functions**:
   - `ROW_NUMBER()`: Strict sequential numbering per partition without gaps.
   - `RANK()` vs `DENSE_RANK()`: Ordinal ranking with tie gap skipping (`1, 2, 2, 4`) vs contiguous ranking (`1, 2, 2, 3`).
   - `LAG()` and `LEAD()`: Inter-row metric comparisons (e.g. daily time-series deltas).
   - Rolling Window Aggregates: Running totals and moving averages over custom frames (`ROWS BETWEEN N PRECEDING AND CURRENT ROW`).
2. **PostgreSQL Declarative Table Partitioning**:
   - Time-series range partitioning (`PARTITION BY RANGE (recorded_at)`).
   - Query planner partition pruning reducing table scan I/O by orders of magnitude.
   - High-throughput zero-downtime data archival (`DROP TABLE` vs `DELETE`).
3. **DSA: Binary Tree Maximum Path Sum (LeetCode #124 - Hard)**:
   - Post-order DFS computing bottom-up maximum branch gains while updating global peak path sums.
   - Handles negative roots, clamping negative branch contributions in optimal $O(N)$ time and $O(H)$ stack space.

---

## 📁 Project Structure

```
Day24-Advanced-SQL-Window-Partitions/
├── src/
│   └── Day24.AdvancedSql/
│       ├── Day24.AdvancedSql.csproj
│       ├── WindowFunctionsEngine.cs         # In-memory window engine & analytical generator
│       ├── TablePartitioningStrategy.cs     # PostgreSQL range partitioning & pruning simulator
│       └── MaxPathSumSolver.cs              # LC #124 Binary Tree Max Path Sum (Hard)
├── tests/
│   └── Day24.AdvancedSql.Tests/
│       ├── Day24.AdvancedSql.Tests.csproj
│       └── Day24Tests.cs                    # 10 unit tests covering window functions & DSA
├── README.md
├── notes.md                                 # Deep dive architectural notes
├── interview-questions.md                   # 10 Senior/Staff interview Q&As
└── index.html                               # Standalone offline web guide
```

---

## 🧪 Running Tests

```bash
dotnet test tests/Day24.AdvancedSql.Tests/Day24.AdvancedSql.Tests.csproj
```
