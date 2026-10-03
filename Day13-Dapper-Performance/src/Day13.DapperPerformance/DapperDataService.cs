using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Dapper;
using Microsoft.EntityFrameworkCore;

namespace Day13.DapperPerformance;

public class DapperDataService
{
    /// <summary>
    /// Demonstrates Dapper Multi-Mapping for 1-to-many relationships (Customer -> Orders).
    /// Uses splitOn: "Id" to delineate boundary between Customer and Order columns.
    /// Deduplicates parent Customer entities using a lookup dictionary.
    /// </summary>
    public async Task<List<Customer>> GetCustomersWithOrdersAsync(IDbConnection conn)
    {
        const string sql = """
            SELECT 
                c.Id, c.Name, c.Email,
                o.Id, o.CustomerId, o.TotalAmount, o.CreatedAtUtc
            FROM Customers c
            LEFT JOIN Orders o ON c.Id = o.CustomerId
            ORDER BY c.Id;
            """;

        var customerLookup = new Dictionary<int, Customer>();

        await conn.QueryAsync<Customer, Order?, Customer>(
            sql,
            (customer, order) =>
            {
                if (!customerLookup.TryGetValue(customer.Id, out var existingCustomer))
                {
                    existingCustomer = customer;
                    customerLookup.Add(existingCustomer.Id, existingCustomer);
                }

                if (order != null && order.Id > 0)
                {
                    existingCustomer.Orders.Add(order);
                }

                return existingCustomer;
            },
            splitOn: "Id");

        return customerLookup.Values.ToList();
    }

    /// <summary>
    /// Demonstrates Dapper's buffered query parameter.
    /// When buffered = true, Dapper eagerly materializes all rows into an internal List in memory.
    /// When buffered = false, Dapper defers execution and yields rows lazily from the active IDataReader.
    /// </summary>
    public IEnumerable<Order> GetOrders(IDbConnection conn, bool buffered)
    {
        const string sql = "SELECT Id, CustomerId, TotalAmount, CreatedAtUtc FROM Orders;";
        return conn.Query<Order>(sql, buffered: buffered);
    }

    /// <summary>
    /// Implements true non-allocating asynchronous streaming using IAsyncEnumerable.
    /// Leverages DbDataReader.ReadAsync() with Dapper's compiled row parser.
    /// </summary>
    public async IAsyncEnumerable<Order> StreamOrdersAsync(
        DbConnection conn,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (conn.State != ConnectionState.Open)
        {
            await conn.OpenAsync(cancellationToken);
        }

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id, CustomerId, TotalAmount, CreatedAtUtc FROM Orders ORDER BY Id;";

        await using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.SequentialAccess, cancellationToken);
        var rowParser = reader.GetRowParser<Order>();

        while (await reader.ReadAsync(cancellationToken))
        {
            yield return rowParser(reader);
        }
    }

    /// <summary>
    /// Profiles performance and memory allocation between Dapper raw SQL vs EF Core raw SQL (FromSqlRaw with AsNoTracking).
    /// </summary>
    public async Task<(BenchmarkExecutionMetric Dapper, BenchmarkExecutionMetric EfCore)> ProfileDapperVsEfCoreAsync(
        DbConnection conn,
        BenchmarkDbContext efContext)
    {
        const string sql = "SELECT Id, CustomerId, TotalAmount, CreatedAtUtc FROM Orders;";

        // 1. Profile Dapper
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        long dapperAllocBefore = GC.GetAllocatedBytesForCurrentThread();
        var sw = Stopwatch.StartNew();

        var dapperOrders = (await conn.QueryAsync<Order>(sql)).ToList();

        sw.Stop();
        long dapperAllocAfter = GC.GetAllocatedBytesForCurrentThread();
        var dapperMetric = new BenchmarkExecutionMetric(
            QueryType: "Dapper QueryAsync",
            RecordsFetched: dapperOrders.Count,
            AllocatedBytes: dapperAllocAfter - dapperAllocBefore,
            ElapsedMilliseconds: sw.Elapsed.TotalMilliseconds);

        // 2. Profile EF Core Raw SQL
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        long efAllocBefore = GC.GetAllocatedBytesForCurrentThread();
        sw.Restart();

        var efOrders = await efContext.Orders
            .FromSqlRaw(sql)
            .AsNoTracking()
            .ToListAsync();

        sw.Stop();
        long efAllocAfter = GC.GetAllocatedBytesForCurrentThread();
        var efMetric = new BenchmarkExecutionMetric(
            QueryType: "EF Core FromSqlRaw (AsNoTracking)",
            RecordsFetched: efOrders.Count,
            AllocatedBytes: efAllocAfter - efAllocBefore,
            ElapsedMilliseconds: sw.Elapsed.TotalMilliseconds);

        return (dapperMetric, efMetric);
    }
}
