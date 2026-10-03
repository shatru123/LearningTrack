# Day 12 - Engineering Notes: EF Core Split Queries & DbCommandInterceptor

## 1. The Cartesian Explosion Problem in Relational Queries

When an application loads multiple 1-to-many child collections in a single query:
```csharp
var customers = await context.Customers
    .Include(c => c.Orders)
        .ThenInclude(o => o.OrderItems)
    .Include(c => c.Orders)
        .ThenInclude(o => o.Shipments)
    .ToListAsync();
```

### Mathematical Explosion:
In a relational database, `JOIN` operations produce the Cartesian product of the matched collections:
$$\text{Returned Rows per Order} = |\text{OrderItems}| \times |\text{Shipments}|$$

If a Customer has:
- 10 Orders
- Each Order has 20 OrderItems
- Each Order has 4 Shipments

In a Single Query (`.AsSingleQuery()`):
- Total rows returned across the network:
  $$10 \times 20 \times 4 = 800 \text{ rows}$$
- Each row contains full duplicated customer columns, order columns, item columns, and shipment columns.
- Across 1,000 customers, this translates to **800,000 rows** transmitted over the wire for only $1,000 + 10,000 + 20,000 + 4,000 = 35,000$ actual entities!
- Consequences: Massive network bandwidth consumption, database buffer pool exhaustion, high GC Gen 2 allocations, and high client CPU materialization overhead.

---

## 2. Solution: `.AsSplitQuery()`

EF Core splits the relational graph into multiple independent SQL queries executed sequentially over the same database connection:
1. `SELECT ... FROM Customers`
2. `SELECT ... FROM Orders WHERE CustomerId IN (...)`
3. `SELECT ... FROM OrderItems WHERE OrderId IN (...)`
4. `SELECT ... FROM Shipments WHERE OrderId IN (...)`

### Row Count with Split Queries:
$$\text{Total Rows} = |\text{Customers}| + |\text{Orders}| + |\text{OrderItems}| + |\text{Shipments}| = 35,000 \text{ rows}$$
(Compared to 800,000 rows — a **95.6% reduction in data transferred**!)

### Trade-offs: Single Query vs. Split Query

| Characteristic | Single Query (`AsSingleQuery`) | Split Query (`AsSplitQuery`) |
|---|---|---|
| **SQL Queries** | Exactly 1 | $1 + N$ (1 per collection level) |
| **Network Payload** | Can explode quadratically ($O(A \times B)$) | Linear ($O(A + B)$) |
| **Consistency** | Strict single snapshot | Potential concurrency tear if child records change between sub-queries (unless wrapped in transaction) |
| **Database Roundtrips** | 1 roundtrip | Multiple roundtrips (mitigated by MARS or pipelining) |
| **Best Used For** | 1:1 relationships or single 1:N collection | Multiple 1:N collection navigations |

---

## 3. Database Observability with `DbCommandInterceptor`

EF Core provides diagnostic interception through `DbCommandInterceptor`. It intercepts SQL commands before and after execution at the ADO.NET level:

```csharp
public class SlowQueryInterceptor : DbCommandInterceptor
{
    private readonly long _thresholdMs;

    public SlowQueryInterceptor(long thresholdMs = 100) => _thresholdMs = thresholdMs;

    public override DbDataReader ReaderExecuted(
        DbCommand command, CommandExecutedEventData eventData, DbDataReader result)
    {
        if (eventData.Duration.TotalMilliseconds > _thresholdMs)
        {
            LogSlowQuery(command.CommandText, eventData.Duration);
        }
        return base.ReaderExecuted(command, eventData, result);
    }
}
```

### Key Use Cases for Interceptors:
1. **APM and Telemetry**: Capturing real SQL statements with execution durations.
2. **Slow Query Auditing**: Emitting alerts and OpenTelemetry spans for queries exceeding latency budgets.
3. **Tenant Security / Row-Level Security**: Injecting session parameters or tenant IDs into `DbCommand.CommandText`.
4. **Soft Delete / Query Tagging**: Automating `.TagWith()` tracing comments for distributed observability.
