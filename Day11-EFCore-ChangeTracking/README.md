# Day 11: EF Core Change Tracking & Stack Problems

## Overview
This module explores internal Entity Framework Core change tracking mechanics, comparing snapshot tracking against notification tracking, analyzing performance implications of `AsNoTracking()` vs `AsNoTrackingWithIdentityResolution()`, and solving foundational stack algorithmic problems.

---

## 1. EF Core Change Tracking Mechanics

### Entity Lifecycle States
EF Core tracks entity instances using `EntityEntry.State`:
1. **Detached**: The entity is not tracked by the context.
2. **Added**: The entity is tracked and scheduled for an `INSERT` upon `SaveChanges()`.
3. **Unchanged**: The entity is tracked and has no pending modifications.
4. **Modified**: One or more properties have changed and an `UPDATE` will be generated.
5. **Deleted**: The entity is tracked and scheduled for a `DELETE` upon `SaveChanges()`.

### Snapshot vs. Notification Tracking
- **Snapshot Tracking (Default POCO)**:
  - EF Core takes a snapshot clone of property values when an entity is queried or attached.
  - Calling `ChangeTracker.DetectChanges()` performs an $O(N \times P)$ field-by-field comparison between current values and snapshot values.
  - Can incur CPU and memory overhead with thousands of entities.
- **Notification Tracking (`INotifyPropertyChanged`, `INotifyPropertyChanging`)**:
  - The entity immediately notifies the DbContext whenever a property is mutated.
  - Eliminates the need for full snapshot comparison scans during `DetectChanges()`.

### `AsNoTracking()` vs `AsNoTrackingWithIdentityResolution()`
- **`AsNoTracking()`**:
  - No entities are added to the `ChangeTracker`.
  - Fast for read-only queries.
  - **Caveat**: When querying relationships (e.g., Orders with Customer), duplicate parent instances are created across child entities (referential inequality `!ReferenceEquals(o1.Customer, o2.Customer)`), leading to memory duplication.
- **`AsNoTrackingWithIdentityResolution()`**:
  - Does NOT track entities for changes (remains read-only).
  - Uses a lightweight, query-scoped identity map to guarantee that identical primary keys resolve to the exact same object reference (`ReferenceEquals(o1.Customer, o2.Customer) == true`).

---

## 2. LeetCode Stack Solvers

### LeetCode #150: Evaluate Reverse Polish Notation (Medium)
- **Problem**: Evaluate postfix arithmetic expressions using operators `+`, `-`, `*`, `/`.
- **Approach**: Push operands onto a stack. When an operator is encountered, pop the right operand followed by the left operand, evaluate, and push the result back. Integer division truncates toward zero.
- **Complexity**: Time: $O(n)$, Space: $O(n)$.

### LeetCode #739: Daily Temperatures (Medium)
- **Problem**: Given daily temperatures, find how many days to wait for a warmer temperature.
- **Approach**: Monotonic decreasing stack holding indices. When current temperature is higher than the index at `stack.Peek()`, resolve the wait days as `i - prevIndex`.
- **Complexity**: Time: $O(n)$, Space: $O(n)$.

---

## Running Tests
```bash
dotnet test tests/Day11.EfCoreTracking.Tests/Day11.EfCoreTracking.Tests.csproj
```
