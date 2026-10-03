# Day 12 - Senior .NET Interview Questions: EF Core Split Queries & DbCommandInterceptor

### Q1: What is Cartesian explosion in Entity Framework Core, and how do you diagnose it?
- **Short answer**: Cartesian explosion occurs when multiple 1-to-many collections are joined in a single SQL query, resulting in row counts multiplying across collections ($A \times B$) rather than adding ($A + B$).
- **Deeper answer**: It manifests as slow query performance, high database CPU, and excessive managed memory allocations. In EF Core, if multiple sibling `.Include()` collections are queried without `.AsSplitQuery()`, the query plan issues multiple `LEFT JOIN` operations, duplicating parent columns across every permutation of child rows.
- **Diagnosis**: Enable EF Core logging or run a SQL Profiler; EF Core issues warning `RelationalEventId.MultipleCollectionIncludeWarning`.

---

### Q2: What is the main operational risk of using `.AsSplitQuery()`, and how do you mitigate it?
- **Short answer**: Concurrency tearing (data inconsistency across sub-queries) if records are inserted or deleted between the execution of the first query and subsequent split queries.
- **Deeper answer**: Because `.AsSplitQuery()` sends multiple separate `SELECT` statements, if another transaction updates or deletes an order item between query 1 and query 2, the client could materialize an inconsistent state.
- **Mitigation**: Wrap the query within a database transaction with `READ COMMITTED` or `REPEATABLE READ` isolation:
```csharp
await using var tx = await context.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead);
var data = await context.Customers.Include(...).AsSplitQuery().ToListAsync();
await tx.CommitAsync();
```

---

### Q3: How do you configure `.AsSplitQuery()` globally across an entire application?
- **Short answer**: In `DbContextOptionsBuilder.UseNpgsql()` or `.UseSqlServer()`, configure `UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery)`.
- **Practical example**:
```csharp
services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString, o =>
        o.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery)));
```
Individual queries can still override the global setting using `.AsSingleQuery()`.

---

### Q4: What is a `DbCommandInterceptor`, and what lifecycle events does it hook into?
- **Short answer**: A middleware hook in EF Core that intercepts low-level `DbCommand` operations before and after execution across `Reader`, `Scalar`, and `NonQuery` operations (both sync and async).
- **Deeper answer**: Methods include:
  - `ReaderExecuting` / `ReaderExecutedAsync`: Intercepts `SELECT` queries returning rows.
  - `NonQueryExecuting` / `NonQueryExecutedAsync`: Intercepts `INSERT`, `UPDATE`, `DELETE`, and DDL.
  - `ScalarExecuting` / `ScalarExecutedAsync`: Intercepts aggregate functions like `COUNT()`.
  - `CommandFailed` / `CommandFailedAsync`: Intercepts database exceptions and connection errors.

---

### Q5: How can you modify SQL commands dynamically using a `DbCommandInterceptor`?
- **Short answer**: Mutate `DbCommand.CommandText` inside the `Executing` methods (`ReaderExecuting`, `NonQueryExecuting`) before the command is dispatched to ADO.NET.
- **Deeper answer**:
```csharp
public override InterceptionResult<DbDataReader> ReaderExecuting(
    DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
{
    // Append query hint or tracing comment
    command.CommandText = $"/* TraceId: {Activity.Current?.Id} */ " + command.CommandText;
    return base.ReaderExecuting(command, eventData, result);
}
```

---

### Q6: What is the difference between `DbCommandInterceptor` and `SaveChangesInterceptor`?
- **Short answer**: `DbCommandInterceptor` operates at the low-level SQL/ADO.NET layer; `SaveChangesInterceptor` operates at the high-level EF Core entity state layer before and after change detection.
- **Deeper answer**:
  - Use `SaveChangesInterceptor` (`SavingChanges`, `SavedChanges`) to set audit properties on entities (`CreatedAt`, `LastModifiedBy`, `TenantId`) or dispatch domain events before records are converted to SQL.
  - Use `DbCommandInterceptor` for database connection hooks, SQL query modification, slow query logging, and low-level performance telemetry.

---

### Q7: Can a `DbCommandInterceptor` suppress or replace query execution results?
- **Short answer**: Yes. Return a non-empty `InterceptionResult<T>` from an `Executing` method to bypass the actual database call and return mock or cached data.
- **Deeper answer**: By returning `InterceptionResult<DbDataReader>.SuppressWithResult(cachedReader)`, EF Core never sends the SQL command to the physical database. This technique is used to implement second-level distributed caching (e.g. Redis) directly within the EF Core pipeline.

---

### Q8: What is Multiple Active Result Sets (MARS), and how does it relate to Split Queries?
- **Short answer**: MARS allows a single SQL Server connection to maintain multiple active `SqlDataReader` streams simultaneously, enabling interleaved reads.
- **Deeper answer**: When MARS is disabled in SQL Server, executing a second query while the first reader is still streaming throws an exception. In Split Queries without MARS, EF Core must read and buffer each query's entire result into memory before opening the next split query. Enabling MARS allows concurrent streaming across sub-queries.

---

### Q9: How do you trace and correlate EF Core SQL queries in production using OpenTelemetry?
- **Short answer**: Use query tagging via `.TagWith()` combined with OpenTelemetry's `OpenTelemetry.Instrumentation.EntityFrameworkCore` package.
- **Deeper answer**:
```csharp
var orders = await context.Orders
    .TagWith("Endpoint: CheckoutController.ProcessOrder")
    .Where(o => o.CustomerId == id)
    .ToListAsync();
```
EF Core inserts `-- Endpoint: CheckoutController.ProcessOrder` as a leading SQL comment. APM tools (Datadog, Dynatrace, New Relic) and database query profilers (`pg_stat_statements`) group queries by this tag, isolating slow application paths immediately.

---

### Q10: Why can `.AsSplitQuery()` lead to N+1 query problems if misused?
- **Short answer**: If combined with inappropriate joins or projection in client code, splitting queries on deep hierarchical trees can generate dozens of separate queries per HTTP request.
- **Deeper answer**: While Split Queries solve Cartesian explosion on sibling collections (e.g. `Orders -> Items` and `Orders -> Shipments`), applying them indiscriminately across deep nesting levels (4+ levels deep) increases network roundtrips. On high-latency database connections (e.g. cross-region clouds), network roundtrip latency will outweigh the memory savings of row deduplication.
