using Refit;

namespace Day28.ResilientHttpRefitPolly;

public record InventoryItemDto(
    string Sku,
    int AvailableStock,
    decimal UnitPrice,
    string WarehouseLocation);

public record StockReservationRequest(
    string Sku,
    int Quantity,
    string OrderReference);

public record StockReservationResponse(
    Guid ReservationId,
    string Sku,
    int ReservedQuantity,
    bool IsSuccess,
    string? ErrorMessage);

public record OrderSummaryDto(
    Guid OrderId,
    string CustomerId,
    decimal TotalAmount,
    string Status);

/// <summary>
/// Type-safe HTTP REST client for Inventory Service generated at compile time by Refit.
/// </summary>
public interface IInventoryServiceClient
{
    [Get("/api/inventory/{sku}")]
    Task<ApiResponse<InventoryItemDto>> GetInventoryItemAsync(
        string sku,
        [Header("X-Correlation-ID")] string? correlationId = null,
        CancellationToken cancellationToken = default);

    [Post("/api/inventory/reserve")]
    [Headers("Accept: application/json")]
    Task<ApiResponse<StockReservationResponse>> ReserveStockAsync(
        [Body] StockReservationRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Type-safe HTTP REST client for Order Service generated at compile time by Refit.
/// </summary>
public interface IOrderServiceClient
{
    [Get("/api/orders/{orderId}")]
    Task<ApiResponse<OrderSummaryDto>> GetOrderAsync(
        Guid orderId,
        CancellationToken cancellationToken = default);
}
