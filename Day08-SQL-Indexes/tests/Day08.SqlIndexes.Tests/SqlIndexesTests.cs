using System.Data;
using Dapper;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Day08.SqlIndexes.Tests;

public class SqlIndexesTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DapperEventTicketRepository _repository;

    public SqlIndexesTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        InitializeDatabase(_connection);
        _repository = new DapperEventTicketRepository(_connection);
    }

    private static void InitializeDatabase(IDbConnection db)
    {
        const string schema = @"
            CREATE TABLE Venues (
                VenueId INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL,
                City TEXT NOT NULL,
                Capacity INTEGER NOT NULL
            );

            CREATE TABLE Events (
                EventId INTEGER PRIMARY KEY AUTOINCREMENT,
                VenueId INTEGER NOT NULL,
                Name TEXT NOT NULL,
                Category TEXT NOT NULL,
                EventDate TEXT NOT NULL,
                BasePrice REAL NOT NULL,
                Status TEXT NOT NULL
            );

            CREATE TABLE Orders (
                OrderId INTEGER PRIMARY KEY AUTOINCREMENT,
                CustomerId INTEGER NOT NULL,
                TotalAmount REAL NOT NULL,
                Status TEXT NOT NULL,
                OrderDate TEXT NOT NULL
            );

            CREATE TABLE Tickets (
                TicketId INTEGER PRIMARY KEY AUTOINCREMENT,
                EventId INTEGER NOT NULL,
                OrderId INTEGER,
                SeatNumber TEXT NOT NULL,
                Price REAL NOT NULL,
                Status TEXT NOT NULL
            );

            -- Sample Data
            INSERT INTO Venues (VenueId, Name, City, Capacity) VALUES (1, 'Emirates Stadium', 'London', 60000);
            INSERT INTO Events (EventId, VenueId, Name, Category, EventDate, BasePrice, Status)
            VALUES (1001, 1, 'Arsenal vs Chelsea', 'Football', '2026-10-15 19:45:00', 150.00, 'Scheduled');

            INSERT INTO Tickets (EventId, OrderId, SeatNumber, Price, Status)
            VALUES 
                (1001, NULL, 'Block-A-1', 150.00, 'Available'),
                (1001, NULL, 'Block-A-2', 175.00, 'Available'),
                (1001, 501, 'Block-VIP-1', 300.00, 'Sold');

            INSERT INTO Orders (OrderId, CustomerId, TotalAmount, Status, OrderDate)
            VALUES 
                (501, 42, 300.00, 'Completed', '2026-09-20 14:00:00'),
                (502, 42, 150.00, 'Pending', '2026-09-21 10:00:00');
        ";

        db.Execute(schema);
    }

    [Fact]
    public async Task GetEventDetailsAsync_ReturnsJoinedDetails()
    {
        var details = await _repository.GetEventDetailsAsync(1001);

        Assert.NotNull(details);
        Assert.Equal(1001, details.EventId);
        Assert.Equal("Arsenal vs Chelsea", details.Name);
        Assert.Equal("Emirates Stadium", details.VenueName);
        Assert.Equal(60000, details.Capacity);
    }

    [Fact]
    public async Task GetAvailableTicketsAsync_ReturnsOnlyAvailableTicketsOrderedByPrice()
    {
        var tickets = (await _repository.GetAvailableTicketsAsync(1001)).ToList();

        Assert.Equal(2, tickets.Count);
        Assert.All(tickets, t => Assert.Equal("Available", t.Status));
        Assert.True(tickets[0].Price <= tickets[1].Price);
    }

    [Fact]
    public async Task GetCustomerOrdersByStatusAsync_ReturnsFilteredOrders()
    {
        var completedOrders = (await _repository.GetCustomerOrdersByStatusAsync(42, "Completed")).ToList();

        Assert.Single(completedOrders);
        Assert.Equal(501, completedOrders[0].OrderId);
        Assert.Equal(300m, completedOrders[0].TotalAmount);
    }

    [Fact]
    public void BTreeStructuralAnalyzer_ComputesPageCapacities_Accurately()
    {
        var layout = BTreeStructuralAnalyzer.AnalyzePageCapacity(averageIndexKeySizeBytes: 16);

        Assert.Equal(8192, layout.PageSizeBytes);
        Assert.Equal(24, layout.PageHeaderSizeBytes);
        Assert.Equal(4, layout.LinpPointerSizeBytes);
        // (8192 - 24 - 16) / (4 + 16) = 8152 / 20 = 407 tuples per page
        Assert.Equal(407, layout.MaxTuplesPerPage);
    }

    public void Dispose()
    {
        _connection.Close();
        _connection.Dispose();
    }
}
