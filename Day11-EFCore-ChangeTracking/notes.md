# Day 11 - Engineering Notes: EF Core Change Tracking Mechanics

## 1. Entity State Lifecycle Transitions

Entity Framework Core tracks entity instances through `EntityEntry<T>.State`:

```
               ┌───────────┐
               │ Detached  │ ◄────── (After SaveChanges on Deleted)
               └─────┬─────┘
                     │
         DbContext.Add(entity)
                     │
                     ▼
               ┌───────────┐
               │   Added   │
               └─────┬─────┘
                     │
          SaveChanges (INSERT)
                     │
                     ▼
               ┌───────────┐
      ┌──────► │ Unchanged │ ◄────── (SaveChanges on Modified)
      │        └─────┬─────┘
      │              │
      │       Mutate property (DetectChanges)
      │              │
      │              ▼
      │        ┌───────────┐
      └─────── │ Modified  │
               └─────┬─────┘
                     │
        DbContext.Remove(entity)
                     │
                     ▼
               ┌───────────┐
               │  Deleted  │
               └─────┬─────┘
                     │
          SaveChanges (DELETE)
                     │
                     ▼
               ┌───────────┐
               │ Detached  │
               └───────────┘
```

---

## 2. Snapshot vs. Notification Tracking

### Snapshot Tracking (Default POCO)
- When EF Core queries an entity, it takes an internal snapshot clone of all scalar and navigation properties.
- When `DetectChanges()` is invoked (automatically before `SaveChanges()`, `Add()`, `Remove()`, etc.):
  - It iterates over every tracked entity in the `ChangeTracker`.
  - It performs an $O(N \times P)$ property-by-property equality comparison against the snapshot.
- **Overhead**: Allocates 2x memory per entity (live object + snapshot clone) and consumes high CPU during bulk operations with thousands of entities.

### Notification Tracking (`INotifyPropertyChanged`, `INotifyPropertyChanging`)
- The entity itself raises `PropertyChanging` (capturing original value) and `PropertyChanged` (notifying EF Core immediately upon mutation).
- Configured via:
  ```csharp
  modelBuilder.Entity<Customer>()
      .HasChangeTrackingStrategy(ChangeTrackingStrategy.ChangingAndChangedNotificationsWithOriginalValues);
  ```
- **Performance**: Eliminates the need for full snapshot comparisons during `DetectChanges()`. EF Core marks properties dirty instantly when setters are called.

---

## 3. `AsNoTracking()` vs `AsNoTrackingWithIdentityResolution()`

### The Referential Duplication Problem
Consider an Order query loading its parent Customer:
```csharp
var orders = await context.Orders
    .AsNoTracking()
    .Include(o => o.Customer)
    .ToListAsync();
```
If 100 orders belong to the same Customer (ID = 1):
- With `AsNoTracking()`: EF Core instantiates **100 distinct Customer objects** on the heap!
- `ReferenceEquals(orders[0].Customer, orders[1].Customer)` is **FALSE**.
- Result: Memory bloat and referential inconsistency in memory.

### The Identity Resolution Solution
```csharp
var orders = await context.Orders
    .AsNoTrackingWithIdentityResolution()
    .Include(o => o.Customer)
    .ToListAsync();
```
- EF Core maintains a temporary, query-scoped dictionary to resolve primary keys.
- All 100 orders reference the **exact same Customer instance** in memory.
- `ReferenceEquals(orders[0].Customer, orders[1].Customer)` is **TRUE**.
- Entities remain untracked (no snapshot copies, no change detection overhead).
