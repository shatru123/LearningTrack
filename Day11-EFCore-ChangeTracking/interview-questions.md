# Day 11 - Senior .NET Interview Questions: EF Core Change Tracking & Optimization

### Q1: How does EF Core's default snapshot change tracking work internally?
- **Short answer**: EF Core creates a clone of the entity's property values when it is queried or attached. During `DetectChanges()`, it performs a field-by-field equality comparison between current values and the snapshot clone to determine what changed.
- **Deeper answer**: The snapshot is stored in the internal `StateManager`. When `ChangeTracker.DetectChanges()` is called, EF Core loops through all tracked entries. For each property, it invokes value comparers (e.g. `ValueComparer<T>`) to check for equality. If a difference is found, the property's `IsModified` flag is set to `true`, and the entity's state transitions to `Modified`.
- **Performance consideration**: For large graphs (e.g., 5,000+ entities), `DetectChanges()` causes high CPU spikes.

---

### Q2: What is the exact difference between `AsNoTracking()` and `AsNoTrackingWithIdentityResolution()`?
- **Short answer**: `AsNoTracking()` creates duplicate heap objects for duplicate parent entities across relationship joins; `AsNoTrackingWithIdentityResolution()` maintains a query-scoped identity map so that all matching child rows point to the exact same parent object instance, while still avoiding change tracking.
- **Deeper answer**: 
  - In `AsNoTracking()`, EF Core streams rows from `DbDataReader` and materializes fresh objects for each row without checking if that entity ID was already seen.
  - In `AsNoTrackingWithIdentityResolution()`, an ephemeral dictionary checks primary keys during materialization. This guarantees referential equality (`ReferenceEquals(o1.Customer, o2.Customer) == true`), cuts memory consumption on joined collections, and avoids adding entities to the long-lived `ChangeTracker`.

---

### Q3: When should you disable `ChangeTracker.AutoDetectChangesEnabled`?
- **Short answer**: During bulk inserts, updates, or iterations over thousands of entities in memory to avoid $O(N^2)$ quadratic slowdowns.
- **Deeper answer**: By default, methods like `Add()`, `Attach()`, `Remove()`, and `Find()` trigger `DetectChanges()`. If you add 10,000 entities in a loop, `DetectChanges()` runs 10,000 times, scanning an increasingly large tracked graph ($O(N^2)$).
- **Remediation**:
```csharp
context.ChangeTracker.AutoDetectChangesEnabled = false;
try {
    foreach (var item in largeBatch) context.Items.Add(item);
    context.ChangeTracker.DetectChanges(); // Run once at the end
    await context.SaveChangesAsync();
} finally {
    context.ChangeTracker.AutoDetectChangesEnabled = true;
}
```

---

### Q4: What is the difference between `Attach()`, `Update()`, and setting `EntityEntry.State = EntityState.Modified`?
- **Short answer**: `Attach()` marks the entity `Unchanged`; `Update()` marks every property of the entity and its reachable graph as `Modified`; setting `State = Modified` marks only scalar properties as modified.
- **Deeper answer**:
  - `context.Attach(entity)`: Tracks the entity without generating an `UPDATE` unless properties are subsequently modified.
  - `context.Update(entity)`: Unconditionally sets `IsModified = true` on **all properties**, resulting in an `UPDATE` statement that overwrites every column in the table, even unmodified ones.
  - Setting specific property: `context.Entry(entity).Property(e => e.Status).IsModified = true;` generates an optimal `UPDATE table SET status = @val WHERE id = @id` affecting only the target column.

---

### Q5: How does Notification Tracking improve performance compared to Snapshot Tracking?
- **Short answer**: Entities implement `INotifyPropertyChanged` and `INotifyPropertyChanging`, notifying EF Core directly when property setters are invoked. This eliminates the CPU cost of scanning snapshots during `DetectChanges()`.
- **Deeper answer**: When configured with `ChangeTrackingStrategy.ChangingAndChangedNotificationsWithOriginalValues`, EF Core does not store full property snapshot clones. Instead, when a property setter fires `PropertyChanging`, EF Core records the original value; when `PropertyChanged` fires, it immediately marks the property as modified. In enterprise applications processing large batches, this provides near-zero change detection overhead.

---

### Q6: What causes a `DbUpdateConcurrencyException`, and how do you resolve it?
- **Short answer**: An optimistic concurrency token (such as a `rowversion` column or `[ConcurrencyCheck]` attribute) was modified in the database between when the entity was read and when `SaveChangesAsync()` was called.
- **Deeper answer**: EF Core generates an update with a concurrency clause: `UPDATE products SET price = @p WHERE id = @id AND row_version = @expectedVersion`. When 0 rows are updated, EF Core throws `DbUpdateConcurrencyException`.
- **Resolution Strategy**:
```csharp
try {
    await context.SaveChangesAsync();
} catch (DbUpdateConcurrencyException ex) {
    var entry = ex.Entries.Single();
    var databaseValues = await entry.GetDatabaseValuesAsync();
    // Resolve via ClientWins, DatabaseWins, or Custom Merge
    entry.OriginalValues.SetValues(databaseValues);
    await context.SaveChangesAsync();
}
```

---

### Q7: Can EF Core track two distinct object instances with the same primary key in the same `DbContext`?
- **Short answer**: No. EF Core enforces identity uniqueness per `DbContext` instance and will throw an `InvalidOperationException`.
- **Deeper answer**: The `ChangeTracker` relies on a primary key dictionary to guarantee that an identity maps to exactly one in-memory instance. If entity `A` with `Id = 5` is already tracked, attempting to `Attach` or query entity `B` with `Id = 5` throws: *"The instance of entity type 'Customer' cannot be tracked because another instance with the same key value for {'Id'} is already being tracked."*

---

### Q8: What is the purpose of `DbContext.ChangeTracker.Clear()` introduced in EF Core 5.0?
- **Short answer**: Clears all tracked entities from the `DbContext`, resetting the `ChangeTracker` to an empty state without needing to instantiate a new `DbContext`.
- **Deeper answer**: In long-lived `DbContext` instances or background processing loops (e.g. batch jobs), entities accumulate in memory, degrading query performance and risking stale data lookups. Calling `context.ChangeTracker.Clear()` detaches all entities at once in $O(1)$ operations, freeing memory and preventing memory leaks.

---

### Q9: How do Shadow Properties and Backing Fields interact with Change Tracking?
- **Short answer**: Shadow properties are properties defined in the EF Core model that do not exist as C# properties in the class. Backing fields allow EF Core to read and write fields directly, bypassing property getters/setters.
- **Deeper answer**:
  - Shadow properties (e.g. `LastModifiedUtc`) are stored in EF Core's internal state buffer and tracked like regular properties.
  - Backing fields (`_price`) can be configured via `.UsePropertyAccessMode(PropertyAccessMode.Field)`. When loading from database, EF Core writes directly to the private field, preventing domain logic or validation triggers inside property setters from executing during query materialization.

---

### Q10: Why should you avoid using a single `DbContext` instance concurrently across multiple threads?
- **Short answer**: `DbContext` is not thread-safe. Concurrent access causes corrupted `ChangeTracker` state, invalid database connection commands, and `InvalidOperationException`.
- **Deeper answer**: ADO.NET connections and EF Core's `StateManager` maintain mutable internal state buffers. If Thread A calls `SaveChangesAsync()` while Thread B calls `context.Users.ToListAsync()`, both threads attempt to write to the same underlying socket and update `EntityEntry` dictionaries concurrently, leading to crashes, data corruption, and connection pool exhaustion.
