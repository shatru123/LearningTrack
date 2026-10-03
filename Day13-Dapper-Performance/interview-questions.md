# Day 13 - Senior .NET Interview Questions: Dapper Performance & Micro-ORM Patterns

### Q1: Why is Dapper almost as fast as hand-written ADO.NET code?
- **Short answer**: Dapper compiles custom IL delegates at runtime using `DynamicMethod` and caches them. It performs zero change tracking, zero expression tree parsing, and zero dirty checking.
- **Deeper answer**: When an ADO.NET reader executes, mapping fields to properties using standard reflection (`PropertyInfo.SetValue()`) is slow due to boxing and metadata lookups. Dapper inspects the `IDataReader` schema once, emits IL instructions directly calling the property setters and type casts, and compiles it into a `Func<IDataReader, T>` delegate. Future executions invoke this delegate directly with no reflection overhead.

---

### Q2: What is the difference between `Query<T>()` and `QueryAsync<T>()` in Dapper regarding buffering?
- **Short answer**: `Query<T>` supports `buffered: false` for deferred lazy streaming, while `QueryAsync<T>` currently always buffers the entire result set into an in-memory `List<T>` before returning.
- **Deeper answer**: `QueryAsync<T>` returns `Task<IEnumerable<T>>`. Because `IEnumerable<T>` is a synchronous iterator interface, Dapper cannot yield items asynchronously across `await` expressions using classic `IEnumerable`. Therefore, `QueryAsync` eagerly reads the entire `DbDataReader` into an internal list. To achieve true asynchronous streaming, developers must use `DbDataReader.ReadAsync()` with `reader.GetRowParser<T>()` yielding an `IAsyncEnumerable<T>`.

---

### Q3: How does Dapper's multi-mapping determine where one entity ends and another begins?
- **Short answer**: Using the `splitOn` parameter (defaults to `"Id"`), which tells Dapper to split columns whenever a column name matches the delimiter.
- **Deeper answer**: In a query joining `Users` and `Roles`:
```sql
SELECT u.Id, u.Name, r.Id, r.RoleName FROM Users u JOIN Roles r ON u.RoleId = r.Id
```
When Dapper encounters the second column named `Id` (matching `splitOn: "Id"`), it ceases mapping properties on the `User` object and begins mapping subsequent columns to the `Role` object, passing both to the user's mapping lambda.

---

### Q4: When would you choose Dapper over EF Core in an enterprise architecture?
- **Short answer**: When you need raw SQL performance, batch processing, bulk operations, reporting/analytics with complex joins, or want full control over database execution plans.
- **Deeper answer**:
  - **Dapper**: Ideal for read-heavy microservices, high-throughput CQRS query handlers, reporting pipelines, and DBAs who require hand-tuned SQL with optimizer hints.
  - **EF Core**: Ideal for write-heavy business domains, transactional domain models requiring unit-of-work (`SaveChanges`), complex optimistic concurrency, migrations, and database schema generation.
  - **Hybrid Pattern**: Many high-scale .NET architectures use EF Core for commands (`INSERT`/`UPDATE` with domain validation) and Dapper for queries (`SELECT` projections).

---

### Q5: What is the risk of string interpolation in Dapper SQL queries, and how does Dapper prevent SQL injection?
- **Short answer**: String interpolation (`$"SELECT * FROM Users WHERE Name = '{name}'"`) leads to critical SQL injection. Dapper prevents injection via parameterized queries using anonymous objects.
- **Deeper answer**:
  - Insecure: `conn.Query<User>($"SELECT * FROM Users WHERE Name = '{userInput}'")`
  - Secure: `conn.Query<User>("SELECT * FROM Users WHERE Name = @Name", new { Name = userInput })`
  Dapper converts properties of the anonymous object into strongly-typed `DbParameter` instances, ensuring input is treated strictly as data, never as executable SQL.

---

### Q6: How does Dapper handle `IN` clauses with lists of parameters?
- **Short answer**: Dapper natively expands `IEnumerable` parameters into a comma-separated list of individual parameterized values.
- **Deeper answer**:
```csharp
var ids = new[] { 1, 2, 3, 4, 5 };
var orders = await conn.QueryAsync<Order>(
    "SELECT * FROM Orders WHERE Id IN @Ids", new { Ids = ids });
```
Dapper dynamically rewrites the SQL to: `SELECT * FROM Orders WHERE Id IN (@Ids1, @Ids2, @Ids3, @Ids4, @Ids5)` and binds 5 separate parameters, preventing injection while avoiding manual string manipulation.

---

### Q7: What is `SqlMapper.TypeHandler<T>`, and what problem does it solve?
- **Short answer**: A custom serializer/deserializer hook that enables Dapper to map non-primitive types (such as JSON columns, value objects, or custom Enums) to and from database columns.
- **Deeper answer**:
```csharp
public class JsonTypeHandler<T> : SqlMapper.TypeHandler<T>
{
    public override void SetValue(IDbDataParameter parameter, T value)
        => parameter.Value = JsonSerializer.Serialize(value);

    public override T Parse(object value)
        => JsonSerializer.Deserialize<T>(value.ToString()!)!;
}
// Registered once at startup:
SqlMapper.AddTypeHandler(new JsonTypeHandler<Address>());
```

---

### Q8: How does Dapper handle multiple result sets from a stored procedure or batch script?
- **Short answer**: Using `conn.QueryMultipleAsync()`, returning a `GridReader` that allows sequentially reading each result set.
- **Deeper answer**:
```csharp
const string sql = @"
    SELECT * FROM Customers WHERE Id = @Id;
    SELECT * FROM Orders WHERE CustomerId = @Id;";

using var multi = await conn.QueryMultipleAsync(sql, new { Id = 42 });
var customer = await multi.ReadSingleOrDefaultAsync<Customer>();
var orders = (await multi.ReadAsync<Order>()).ToList();
```
This executes in a single database network roundtrip, maximizing throughput.

---

### Q9: What happens if an unbuffered Dapper query (`buffered: false`) is not fully enumerated?
- **Short answer**: The underlying `IDataReader` and connection remain open, leading to connection leaks and connection pool starvation.
- **Deeper answer**: If a method returns an unbuffered `IEnumerable<Order>` and the caller exits early (e.g. `var first = service.GetOrders().First()`), the iterator does not reach the end of the stream. Unless the caller wraps the enumeration in a `using` statement or consumes the iterator fully, the `IDataReader` remains locked on the connection, preventing other queries from reusing that connection.

---

### Q10: How do you benchmark Dapper against EF Core accurately without JIT warm-up bias?
- **Short answer**: Use `BenchmarkDotNet`, which runs multiple warm-up iterations, prevents JIT tiering distortions, measures hardware performance counters, and tracks Gen 0/1/2 GC allocations.
- **Deeper answer**: Naive benchmarking with `Stopwatch` produces inaccurate results due to cold start JIT compilation, connection pooling warm-up, and garbage collector interference. BenchmarkDotNet isolates runs in separate target processes, calculates statistical variance, and accurately reports exact heap memory allocations using `[MemoryDiagnoser]`.
