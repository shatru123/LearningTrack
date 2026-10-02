using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Day12.SplitQueriesInterceptors.Tests;

public class Day12Tests
{
    private static EcommerceDbContext CreateInMemoryContext(
        QueryAuditingInterceptor interceptor,
        out SqliteConnection connection)
    {
        connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<EcommerceDbContext>()
            .UseSqlite(connection)
            .AddInterceptors(interceptor)
            .Options;

        var context = new EcommerceDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    [Fact]
    public async Task SplitQueryService_ShouldDemonstrateCommandSplittingAndPreventCartesianExplosion()
    {
        // Arrange
        var interceptor = new QueryAuditingInterceptor(slowQueryThresholdMs: 200);
        using var context = CreateInMemoryContext(interceptor, out var connection);
        using (connection)
        {
            var customer = new Customer { Name = "Global Logistics", Email = "ops@globallogistics.com" };
            context.Customers.Add(customer);
            await context.SaveChangesAsync();

            // Order 1: 5 items, 3 shipments => Cartesian = 15 rows
            var order1 = new Order { CustomerId = customer.Id };
            for (int i = 1; i <= 5; i++)
            {
                order1.Items.Add(new OrderItem { ProductName = $"Item {i}", Quantity = i, UnitPrice = 10m * i });
            }
            for (int s = 1; s <= 3; s++)
            {
                order1.Shipments.Add(new Shipment { Carrier = "FedEx", TrackingNumber = $"TRK-1-{s}" });
            }

            // Order 2: 4 items, 2 shipments => Cartesian = 8 rows
            var order2 = new Order { CustomerId = customer.Id };
            for (int i = 1; i <= 4; i++)
            {
                order2.Items.Add(new OrderItem { ProductName = $"Item B-{i}", Quantity = i, UnitPrice = 25m });
            }
            for (int s = 1; s <= 2; s++)
            {
                order2.Shipments.Add(new Shipment { Carrier = "UPS", TrackingNumber = $"TRK-2-{s}" });
            }

            context.Orders.AddRange(order1, order2);
            await context.SaveChangesAsync();

            var service = new SplitQueryService();

            // Act
            var result = await service.CompareSingleVsSplitQueryAsync(context, interceptor);

            // Assert
            result.TotalOrders.Should().Be(2);
            result.TotalItems.Should().Be(9);
            result.TotalShipments.Should().Be(5);

            // Single query emits 1 SQL query with Cartesian joins
            result.SingleQueryCommandCount.Should().Be(1);

            // Split query emits 3 distinct SQL queries (Orders, OrderItems, Shipments)
            result.SplitQueryCommandCount.Should().Be(3);

            // Theoretical Cartesian rows: 15 + 8 = 23 rows
            result.TheoreticalCartesianRowCount.Should().Be(23);

            // Split query total rows: 2 + 9 + 5 = 16 rows
            result.SplitQueryTotalRowCount.Should().Be(16);

            result.ExplosionFactor.Should().BeGreaterThan(1.0);
        }
    }

    [Fact]
    public async Task QueryAuditingInterceptor_ShouldCaptureCommandsAndExecutionMetrics()
    {
        // Arrange
        var capturedLogs = new List<CommandAuditLog>();
        var interceptor = new QueryAuditingInterceptor(
            slowQueryThresholdMs: 0, // Flag everything to verify slow query detection
            onQueryLogged: log => capturedLogs.Add(log));

        using var context = CreateInMemoryContext(interceptor, out var connection);
        using (connection)
        {
            // Act
            context.Customers.Add(new Customer { Name = "Audit Customer", Email = "audit@test.com" });
            await context.SaveChangesAsync();

            var customers = await context.Customers.ToListAsync();

            // Assert
            interceptor.ExecutedCommandCount.Should().BeGreaterThan(0);
            interceptor.Logs.Should().NotBeEmpty();
            capturedLogs.Should().NotBeEmpty();

            // All logged commands should have non-null command text and valid timestamp
            interceptor.Logs.Should().AllSatisfy(log =>
            {
                log.CommandText.Should().NotBeNullOrWhiteSpace();
                log.DurationMilliseconds.Should().BeGreaterThanOrEqualTo(0);
                log.ExecutedAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
                log.IsSlowQuery.Should().BeTrue(); // because threshold was 0
            });
        }
    }

    [Theory]
    [InlineData(new[] { -1, 0, 3, 5, 9, 12 }, 9, 4)]
    [InlineData(new[] { -1, 0, 3, 5, 9, 12 }, 2, -1)]
    [InlineData(new[] { 5 }, 5, 0)]
    [InlineData(new[] { 5 }, 1, -1)]
    [InlineData(new[] { 1, 2, 3, 4, 5 }, 1, 0)]
    [InlineData(new[] { 1, 2, 3, 4, 5 }, 5, 4)]
    [InlineData(new int[0], 10, -1)]
    public void BinarySearch_ShouldFindExpectedIndices(int[] nums, int target, int expected)
    {
        // Act
        var result = BinarySearchSolvers.Search(nums, target);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void SearchMatrix_ShouldFindTargetInMatrix()
    {
        // Arrange
        int[][] matrix =
        [
            [1, 3, 5, 7],
            [10, 11, 16, 20],
            [23, 30, 34, 60]
        ];

        // Act & Assert
        BinarySearchSolvers.SearchMatrix(matrix, 3).Should().BeTrue();
        BinarySearchSolvers.SearchMatrix(matrix, 1).Should().BeTrue();
        BinarySearchSolvers.SearchMatrix(matrix, 60).Should().BeTrue();
        BinarySearchSolvers.SearchMatrix(matrix, 16).Should().BeTrue();
        BinarySearchSolvers.SearchMatrix(matrix, 13).Should().BeFalse();
        BinarySearchSolvers.SearchMatrix(matrix, 0).Should().BeFalse();
        BinarySearchSolvers.SearchMatrix(matrix, 100).Should().BeFalse();
    }

    [Fact]
    public void SearchMatrix_SingleRowAndSingleColumn_ShouldWorkCorrectly()
    {
        // 1x3 matrix
        int[][] singleRow = [[1, 3, 5]];
        BinarySearchSolvers.SearchMatrix(singleRow, 3).Should().BeTrue();
        BinarySearchSolvers.SearchMatrix(singleRow, 4).Should().BeFalse();

        // 3x1 matrix
        int[][] singleCol = [[1], [3], [5]];
        BinarySearchSolvers.SearchMatrix(singleCol, 3).Should().BeTrue();
        BinarySearchSolvers.SearchMatrix(singleCol, 2).Should().BeFalse();

        // 1x1 matrix
        int[][] singleCell = [[42]];
        BinarySearchSolvers.SearchMatrix(singleCell, 42).Should().BeTrue();
        BinarySearchSolvers.SearchMatrix(singleCell, 10).Should().BeFalse();
    }
}
