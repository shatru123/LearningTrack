using System.Data;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Day13.DapperPerformance.Tests;

public class Day13Tests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BenchmarkDbContext _efContext;
    private readonly DapperDataService _service;

    public Day13Tests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<BenchmarkDbContext>()
            .UseSqlite(_connection)
            .Options;

        _efContext = new BenchmarkDbContext(options);
        _efContext.Database.EnsureCreated();

        _service = new DapperDataService();

        SeedTestData();
    }

    private void SeedTestData()
    {
        var customer1 = new Customer { Name = "Alice", Email = "alice@example.com" };
        var customer2 = new Customer { Name = "Bob", Email = "bob@example.com" };
        var customer3 = new Customer { Name = "Charlie", Email = "charlie@example.com" };

        _efContext.Customers.AddRange(customer1, customer2, customer3);
        _efContext.SaveChanges();

        _efContext.Orders.AddRange(
            new Order { CustomerId = customer1.Id, TotalAmount = 100.50m, CreatedAtUtc = DateTime.UtcNow.AddDays(-2) },
            new Order { CustomerId = customer1.Id, TotalAmount = 250.00m, CreatedAtUtc = DateTime.UtcNow.AddDays(-1) },
            new Order { CustomerId = customer2.Id, TotalAmount = 75.25m, CreatedAtUtc = DateTime.UtcNow }
            // customer3 has 0 orders to test LEFT JOIN
        );
        _efContext.SaveChanges();
    }

    public void Dispose()
    {
        _efContext.Dispose();
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task GetCustomersWithOrdersAsync_ShouldCorrectlyMultiMapAndDeduplicate()
    {
        // Act
        var customers = await _service.GetCustomersWithOrdersAsync(_connection);

        // Assert
        customers.Should().HaveCount(3);

        var alice = customers.Single(c => c.Name == "Alice");
        alice.Orders.Should().HaveCount(2);
        alice.Orders.Select(o => o.TotalAmount).Should().Contain([100.50m, 250.00m]);

        var bob = customers.Single(c => c.Name == "Bob");
        bob.Orders.Should().HaveCount(1);
        bob.Orders.First().TotalAmount.Should().Be(75.25m);

        var charlie = customers.Single(c => c.Name == "Charlie");
        charlie.Orders.Should().BeEmpty();
    }

    [Fact]
    public void GetOrders_BufferedVsUnbuffered_ShouldYieldIdenticalResults()
    {
        // Act
        var bufferedOrders = _service.GetOrders(_connection, buffered: true).ToList();
        var unbufferedOrders = _service.GetOrders(_connection, buffered: false).ToList();

        // Assert
        bufferedOrders.Should().HaveCount(3);
        unbufferedOrders.Should().HaveCount(3);
        bufferedOrders.Select(o => o.Id).Should().Equal(unbufferedOrders.Select(o => o.Id));
    }

    [Fact]
    public async Task StreamOrdersAsync_ShouldStreamRecordsViaIAsyncEnumerable()
    {
        // Act
        var streamedOrders = new List<Order>();
        await foreach (var order in _service.StreamOrdersAsync(_connection))
        {
            streamedOrders.Add(order);
        }

        // Assert
        streamedOrders.Should().HaveCount(3);
        streamedOrders.Select(o => o.Id).Should().BeInAscendingOrder();
    }

    [Fact]
    public async Task ProfileDapperVsEfCore_ShouldFetchSameRowsAndMeasureMetrics()
    {
        // Act
        var (dapper, efCore) = await _service.ProfileDapperVsEfCoreAsync(_connection, _efContext);

        // Assert
        dapper.RecordsFetched.Should().Be(3);
        efCore.RecordsFetched.Should().Be(3);
        dapper.ElapsedMilliseconds.Should().BeGreaterThanOrEqualTo(0);
        efCore.ElapsedMilliseconds.Should().BeGreaterThanOrEqualTo(0);
    }

    [Theory]
    [InlineData(new[] { 3, 4, 5, 1, 2 }, 1)]
    [InlineData(new[] { 4, 5, 6, 7, 0, 1, 2 }, 0)]
    [InlineData(new[] { 11, 13, 15, 17 }, 11)]
    [InlineData(new[] { 2, 1 }, 1)]
    [InlineData(new[] { 1, 2 }, 1)]
    [InlineData(new[] { 42 }, 42)]
    [InlineData(new[] { -2, -1, -5, -4, -3 }, -5)]
    [InlineData(new[] { 5, 1, 2, 3, 4 }, 1)]
    public void FindMin_ShouldReturnMinimumElement(int[] nums, int expectedMin)
    {
        // Act
        int min = RotatedSortedArraySolvers.FindMin(nums);

        // Assert
        min.Should().Be(expectedMin);
    }

    [Fact]
    public void FindPivotIndex_ShouldReturnCorrectIndex()
    {
        // Arrange
        int[] nums = [4, 5, 6, 7, 0, 1, 2];

        // Act
        int pivotIndex = RotatedSortedArraySolvers.FindPivotIndex(nums);

        // Assert
        pivotIndex.Should().Be(4);
        nums[pivotIndex].Should().Be(0);
    }

    [Fact]
    public void FindMin_EmptyArray_ShouldThrowArgumentException()
    {
        // Act & Assert
        FluentActions.Invoking(() => RotatedSortedArraySolvers.FindMin([]))
            .Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(new[] { 2, 2, 2, 0, 1 }, 0)]
    [InlineData(new[] { 1, 3, 5 }, 1)]
    [InlineData(new[] { 3, 3, 1, 3 }, 1)]
    [InlineData(new[] { 10, 1, 10, 10, 10 }, 1)]
    [InlineData(new[] { 3, 1, 3, 3 }, 1)]
    [InlineData(new[] { 1, 1, 1, 1 }, 1)]
    public void FindMinWithDuplicates_ShouldHandleDuplicatesCorrectly(int[] nums, int expectedMin)
    {
        // Act
        int min = RotatedSortedArraySolvers.FindMinWithDuplicates(nums);

        // Assert
        min.Should().Be(expectedMin);
    }
}
