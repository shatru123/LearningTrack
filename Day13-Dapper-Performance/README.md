# Day 13: High-Performance Data Access with Dapper & Rotated Array Search

## Overview
This module explores high-performance relational data access patterns using Dapper, multi-mapping relationships, memory-optimized buffered vs unbuffered query execution, non-allocating asynchronous streaming via `IAsyncEnumerable<T>`, comparative benchmarking against EF Core raw SQL queries, and modified binary search algorithms for rotated sorted arrays.

---

## 1. Dapper Architectural Mechanics

### Micro-ORM Philosophy
- Dapper operates as a lightweight extension method library on `IDbConnection`.
- It executes raw SQL directly against ADO.NET and materializes objects using dynamically emitted IL (`DynamicMethod`) cached in a global delegate dictionary.
- **Zero Change Tracking**: Dapper has no `ChangeTracker`, no identity map overhead, and no entity lifecycle state machines.

### Multi-Mapping (`1:1` and `1:N`)
- When joining tables (e.g. `Customers` and `Orders`), Dapper splits columns based on a delimiter (`splitOn: "Id"`).
- For `1:N` relationships, a local aggregation lookup (`Dictionary<int, Customer>`) deduplicates parent instances while accumulating child order records:
  ```csharp
  var lookup = new Dictionary<int, Customer>();
  await conn.QueryAsync<Customer, Order, Customer>(
      sql,
      (customer, order) => {
          if (!lookup.TryGetValue(customer.Id, out var current))
              lookup.Add(customer.Id, current = customer);
          if (order != null)
              current.Orders.Add(order);
          return current;
      },
      splitOn: "Id");
  ```

### Buffered vs Unbuffered Queries
- **`buffered: true` (Default)**:
  - Reads the entire `IDataReader` into an in-memory `List<T>`, immediately closes the reader, and returns `IEnumerable<T>`.
  - Suitable for small-to-medium result sets where the connection should be returned to the pool immediately.
- **`buffered: false`**:
  - Defers reader consumption, yielding rows lazily using a custom iterator over the open `IDataReader`.
  - Dramatically lowers peak heap memory allocations when processing large datasets.

### Asynchronous Streaming (`IAsyncEnumerable<T>`)
- True asynchronous, non-buffering streaming over network sockets:
  ```csharp
  await using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.SequentialAccess, ct);
  var parser = reader.GetRowParser<Order>();
  while (await reader.ReadAsync(ct))
  {
      yield return parser(reader);
  }
  ```

### Dapper vs EF Core Raw SQL
| Metric | Dapper (`QueryAsync`) | EF Core (`FromSqlRaw` / `SqlQuery`) |
|---|---|---|
| Materialization | Dynamic IL emit / compiled delegate | Expression tree compilation / materialized pipeline |
| Change Tracking | None (Native read-only) | Optional (`AsNoTracking()`) |
| Memory Overhead | Extremely low | Low with `AsNoTracking`, Moderate with tracking |
| Multi-mapping | Explicit `splitOn` | Automatic navigation graph resolution |

---

## 2. DSA: Find Minimum in Rotated Sorted Array

### LeetCode #153: Find Minimum in Rotated Sorted Array (Medium)
- **Problem**: An ascending sorted array with unique elements was rotated $k$ times ($1 \le k \le n$). Find the minimum element in $O(\log n)$ time.
- **Algorithm (Modified Binary Search)**:
  - Compare `nums[mid]` with `nums[right]`.
  - If `nums[mid] > nums[right]`: the pivot (minimum element) MUST lie in the right sub-array `[mid + 1, right]`. Hence, `left = mid + 1`.
  - Else (`nums[mid] <= nums[right]`): the pivot is either at `mid` or in the left sub-array `[left, mid]`. Hence, `right = mid`.
  - Terminate when `left == right`. The element at `nums[left]` is the minimum.
- **Complexity**: Time: $O(\log n)$, Space: $O(1)$.

### LeetCode #154: With Duplicates (Hard Extension)
- When `nums[mid] == nums[right]`, we cannot definitively discard either half. We safely decrement `right--`.
- Average Time: $O(\log n)$, Worst-case Time: $O(n)$ (e.g. all identical elements except one).

---

## Running Tests
```bash
dotnet test tests/Day13.DapperPerformance.Tests/Day13.DapperPerformance.Tests.csproj
```
