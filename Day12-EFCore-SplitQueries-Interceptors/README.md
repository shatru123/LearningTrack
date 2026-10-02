# Day 12: EF Core Split Queries, DbCommandInterceptor & Binary Search

## Overview
This module explores advanced Entity Framework Core query optimization by mitigating Cartesian explosion using `.AsSplitQuery()`, implementing database observability with `DbCommandInterceptor` for slow query auditing, and mastering $O(\log n)$ Binary Search algorithms.

---

## 1. EF Core Split Queries & Cartesian Explosion

### The Cartesian Product Problem
When loading multiple 1-to-many collection relationships with `.Include(...)` in a single SQL query:
- E.g., `Customer -> Orders -> OrderItems + Shipments`
- The database generates a CROSS/INNER JOIN among the collections.
- Result: **Cartesian Product**: If an order has 10 items and 4 shipments, the database returns $10 \times 4 = 40$ duplicate rows for that single order.
- Across 1,000 orders, this leads to tens of thousands of duplicate rows sent over the wire, driving massive network latency, high database memory pressure, and client deserialization overhead.

### Solution: `.AsSplitQuery()`
- EF Core issues separate SQL queries for each collection level:
  1. `SELECT ... FROM Orders`
  2. `SELECT ... FROM OrderItems WHERE OrderId IN (...)`
  3. `SELECT ... FROM Shipments WHERE OrderId IN (...)`
- Total rows transferred: $O(\text{Orders} + \text{Items} + \text{Shipments})$ instead of $O(\text{Orders} \times \text{Items} \times \text{Shipments})$.
- Trade-off: Multiple database roundtrips vs massive single-payload size. Recommended when multiple collection navigations are loaded.

---

## 2. Database Observability: `DbCommandInterceptor`

EF Core's interception mechanism allows deep hooks into command generation and execution:
- Inherit from `DbCommandInterceptor`.
- Override `ReaderExecuting` / `ReaderExecuted`, `ScalarExecuted`, `NonQueryExecuted` (both synchronous and asynchronous).
- Captures SQL text, execution duration, and flags queries exceeding latency thresholds (e.g. > 100ms) for alerting and APM telemetry.

---

## 3. Binary Search Algorithms

### LeetCode #704: Binary Search (Easy)
- **Problem**: Search for `target` in a sorted array in $O(\log n)$ time.
- **Approach**: Classic two-pointer (`left`, `right`) with midpoint calculation `left + (right - left) / 2` to prevent 32-bit integer overflow.
- **Complexity**: Time: $O(\log n)$, Space: $O(1)$.

### LeetCode #74: Search a 2D Matrix (Medium)
- **Problem**: Search an $m \times n$ matrix where each row is sorted and the first element of each row is greater than the last element of the previous row.
- **Approach**: Map the 2D matrix into a virtual 1D array of size $m \times n$. Use binary search from index `0` to `m * n - 1`. Map `mid` index to matrix coordinates:
  $$\text{row} = \lfloor\text{mid} / n\rfloor, \quad \text{col} = \text{mid} \bmod n$$
- **Complexity**: Time: $O(\log(m \cdot n))$, Space: $O(1)$.

---

## Running Tests
```bash
dotnet test tests/Day12.SplitQueriesInterceptors.Tests/Day12.SplitQueriesInterceptors.Tests.csproj
```
