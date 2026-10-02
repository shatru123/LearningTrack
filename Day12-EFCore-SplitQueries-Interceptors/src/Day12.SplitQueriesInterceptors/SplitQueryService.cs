using Microsoft.EntityFrameworkCore;

namespace Day12.SplitQueriesInterceptors;

public record QueryComparisonResult(
    int SingleQueryCommandCount,
    int SplitQueryCommandCount,
    int TotalOrders,
    int TotalItems,
    int TotalShipments,
    int TheoreticalCartesianRowCount,
    int SplitQueryTotalRowCount,
    double ExplosionFactor);

public class SplitQueryService
{
    /// <summary>
    /// Demonstrates the difference between single query (Cartesian product across multiple 1:N collections)
    /// versus split queries (.AsSplitQuery()).
    /// </summary>
    public async Task<QueryComparisonResult> CompareSingleVsSplitQueryAsync(
        EcommerceDbContext db,
        QueryAuditingInterceptor interceptor)
    {
        // 1. Single Query (Default or explicit .AsSingleQuery())
        interceptor.ClearLogs();
        var singleQueryOrders = await db.Orders
            .AsNoTracking()
            .AsSingleQuery()
            .Include(o => o.Items)
            .Include(o => o.Shipments)
            .ToListAsync();

        var singleQueryCommandCount = interceptor.ExecutedCommandCount;

        // 2. Split Query (.AsSplitQuery())
        interceptor.ClearLogs();
        var splitQueryOrders = await db.Orders
            .AsNoTracking()
            .AsSplitQuery()
            .Include(o => o.Items)
            .Include(o => o.Shipments)
            .ToListAsync();

        var splitQueryCommandCount = interceptor.ExecutedCommandCount;

        // Calculate Cartesian row stats
        int totalOrders = splitQueryOrders.Count;
        int totalItems = splitQueryOrders.Sum(o => o.Items.Count);
        int totalShipments = splitQueryOrders.Sum(o => o.Shipments.Count);

        // In a single query with multiple collection JOINs:
        // Each order generates: (Items.Count * Shipments.Count) rows (or 1 row if collections are empty).
        int theoreticalCartesianRows = splitQueryOrders.Sum(o =>
        {
            int itemCount = Math.Max(1, o.Items.Count);
            int shipmentCount = Math.Max(1, o.Shipments.Count);
            return itemCount * shipmentCount;
        });

        // In a split query:
        // EF Core issues 1 query for Orders, 1 for Items, 1 for Shipments:
        int splitQueryTotalRows = totalOrders + totalItems + totalShipments;

        double explosionFactor = splitQueryTotalRows > 0
            ? (double)theoreticalCartesianRows / splitQueryTotalRows
            : 1.0;

        return new QueryComparisonResult(
            SingleQueryCommandCount: singleQueryCommandCount,
            SplitQueryCommandCount: splitQueryCommandCount,
            TotalOrders: totalOrders,
            TotalItems: totalItems,
            TotalShipments: totalShipments,
            TheoreticalCartesianRowCount: theoreticalCartesianRows,
            SplitQueryTotalRowCount: splitQueryTotalRows,
            ExplosionFactor: explosionFactor);
    }
}
