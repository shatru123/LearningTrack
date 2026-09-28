using System.Data;
using Dapper;

namespace Day08.SqlIndexes;

public class EventDetails
{
    public int EventId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public DateTime EventDate { get; set; }
    public decimal BasePrice { get; set; }
    public string Status { get; set; } = string.Empty;
    public string VenueName { get; set; } = string.Empty;
    public int Capacity { get; set; }
}

public class TicketSummary
{
    public long TicketId { get; set; }
    public int EventId { get; set; }
    public string SeatNumber { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class CustomerOrderSummary
{
    public int OrderId { get; set; }
    public int CustomerId { get; set; }
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime OrderDate { get; set; }
}

public interface IEventTicketRepository
{
    Task<EventDetails?> GetEventDetailsAsync(int eventId);
    Task<IEnumerable<TicketSummary>> GetAvailableTicketsAsync(int eventId);
    Task<IEnumerable<CustomerOrderSummary>> GetCustomerOrdersByStatusAsync(int customerId, string status);
}

/// <summary>
/// High-performance data access repository utilizing Dapper micro-ORM.
/// Parameterized queries prevent SQL injection and leverage prepared statements / B-tree index seeks.
/// </summary>
public class DapperEventTicketRepository : IEventTicketRepository
{
    private readonly IDbConnection _connection;

    public DapperEventTicketRepository(IDbConnection connection)
    {
        _connection = connection;
    }

    public async Task<EventDetails?> GetEventDetailsAsync(int eventId)
    {
        const string sql = @"
            SELECT 
                e.EventId, e.Name, e.Category, e.EventDate, e.BasePrice, e.Status,
                v.Name AS VenueName, v.Capacity
            FROM Events e
            INNER JOIN Venues v ON e.VenueId = v.VenueId
            WHERE e.EventId = @EventId;";

        return await _connection.QuerySingleOrDefaultAsync<EventDetails>(sql, new { EventId = eventId });
    }

    public async Task<IEnumerable<TicketSummary>> GetAvailableTicketsAsync(int eventId)
    {
        // Leverages composite index on Tickets (EventId, Status)
        const string sql = @"
            SELECT TicketId, EventId, SeatNumber, Price, Status
            FROM Tickets
            WHERE EventId = @EventId AND Status = 'Available'
            ORDER BY Price ASC;";

        return await _connection.QueryAsync<TicketSummary>(sql, new { EventId = eventId });
    }

    public async Task<IEnumerable<CustomerOrderSummary>> GetCustomerOrdersByStatusAsync(int customerId, string status)
    {
        // Leverages covering index on Orders (CustomerId, Status) INCLUDE (TotalAmount, OrderDate)
        const string sql = @"
            SELECT OrderId, CustomerId, TotalAmount, Status, OrderDate
            FROM Orders
            WHERE CustomerId = @CustomerId AND Status = @Status
            ORDER BY OrderDate DESC;";

        return await _connection.QueryAsync<CustomerOrderSummary>(sql, new { CustomerId = customerId, Status = status });
    }
}
