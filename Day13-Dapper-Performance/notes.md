# Day 13 - Engineering Notes: Dapper High-Performance Data Access & Streaming

## 1. Dapper Architecture & Execution Pipeline

Dapper is a micro-ORM built as a set of extension methods on ADO.NET's `IDbConnection`.

### Internal Materialization Mechanism
1. **Dynamic IL Generation (`DynamicMethod`)**:
   - The first time Dapper executes a query against a POCO type `T`, it reads the column schema from the `IDataReader`.
   - It generates an optimized MSIL method using `System.Reflection.Emit.DynamicMethod`.
   - The method reads column ordinals, checks for `DBNull`, performs direct type casting, and instantiates the object.
2. **Delegate Caching**:
   - The compiled IL delegate is cached in a global dictionary keyed by `(Type, CommandText, ConnectionString, ParametersHash)`.
   - Subsequent executions execute the pre-compiled delegate directly with near-native hand-written ADO.NET speed.

---

## 2. Multi-Mapping (1:1 and 1:N Relationships)

### Dapper Column Splitting
When joining multiple tables, Dapper uses `splitOn: "Id"` to separate entity fields:
```csharp
var lookup = new Dictionary<int, Customer>();

await conn.QueryAsync<Customer, Order, Customer>(
    @"SELECT c.Id, c.Name, c.Email, o.Id, o.CustomerId, o.TotalAmount 
      FROM Customers c 
      LEFT JOIN Orders o ON c.Id = o.CustomerId",
    (customer, order) => {
        if (!lookup.TryGetValue(customer.Id, out var existing))
        {
            existing = customer;
            lookup.Add(existing.Id, existing);
        }
        if (order != null && order.Id > 0)
        {
            existing.Orders.Add(order);
        }
        return existing;
    },
    splitOn: "Id");

return lookup.Values.ToList();
```

---

## 3. Buffered vs. Unbuffered Execution

### `buffered: true` (Default)
- Reads the entire `IDataReader` into an in-memory `List<T>`.
- Immediately closes the underlying `DbDataReader` and connection.
- **Pros**: Connection returned to connection pool immediately; safe for concurrent reads on other threads.
- **Cons**: High peak memory allocation for queries returning tens of thousands of rows.

### `buffered: false`
- Returns an unbuffered `IEnumerable<T>`.
- Defers reader advancement until `foreach` iterates over the collection.
- **Pros**: Ultra-low memory footprint. Rows are processed and GC-reclaimed on the fly.
- **Cons**: The database connection and reader stay open for the entire duration of iteration.

---

## 4. True Non-Allocating Async Streaming (`IAsyncEnumerable<T>`)

Dapper's `QueryAsync<T>` buffers all results into a list. To achieve true non-buffering asynchronous streaming, combine ADO.NET `DbDataReader.ReadAsync()` with Dapper's compiled row parser:

```csharp
public async IAsyncEnumerable<Order> StreamOrdersAsync(
    DbConnection conn, [EnumeratorCancellation] CancellationToken ct = default)
{
    await using var cmd = conn.CreateCommand();
    cmd.CommandText = "SELECT Id, CustomerId, TotalAmount, CreatedAtUtc FROM Orders;";

    await using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.SequentialAccess, ct);
    var rowParser = reader.GetRowParser<Order>();

    while (await reader.ReadAsync(ct))
    {
        yield return rowParser(reader);
    }
}
```
This enables processing millions of database rows with constant $O(1)$ memory usage.
