namespace Day26.MicroservicesBoundedContexts;

/// <summary>
/// Upstream raw legacy payload from third-party payment gateway.
/// Contains snake_case fields and stringified numeric formats.
/// </summary>
public record LegacyPaymentGatewayPayload(
    string tx_id,
    string order_ref,
    string amt_cents,
    string curr,
    string status_code,
    string auth_token);

/// <summary>
/// Clean domain model for downstream Payment Bounded Context.
/// </summary>
public record PaymentAuthorizationResult(
    Guid OrderId,
    decimal Amount,
    string Currency,
    string AuthorizationCode,
    bool IsSuccessful,
    string? FailureReason);

/// <summary>
/// Anti-Corruption Layer (ACL) isolating the Core Domain from legacy/third-party schemas.
/// Protects domain models from leaking external vendor types and formatting quirks.
/// </summary>
public class PaymentAntiCorruptionLayer
{
    public PaymentAuthorizationResult Translate(LegacyPaymentGatewayPayload rawPayload)
    {
        if (rawPayload == null)
            throw new ArgumentNullException(nameof(rawPayload));

        if (!Guid.TryParse(rawPayload.order_ref, out var orderId))
        {
            return new PaymentAuthorizationResult(
                Guid.Empty,
                0m,
                rawPayload.curr ?? "USD",
                string.Empty,
                IsSuccessful: false,
                FailureReason: "Invalid order identifier format.");
        }

        if (!decimal.TryParse(rawPayload.amt_cents, out var cents))
        {
            return new PaymentAuthorizationResult(
                orderId,
                0m,
                rawPayload.curr ?? "USD",
                string.Empty,
                IsSuccessful: false,
                FailureReason: "Invalid amount representation.");
        }

        var amount = decimal.Round(cents / 100m, 2);
        var isSuccess = rawPayload.status_code == "AUTH_200" || rawPayload.status_code == "SUCCESS";

        return new PaymentAuthorizationResult(
            OrderId: orderId,
            Amount: amount,
            Currency: (rawPayload.curr ?? "USD").ToUpperInvariant(),
            AuthorizationCode: rawPayload.auth_token ?? string.Empty,
            IsSuccessful: isSuccess,
            FailureReason: isSuccess ? null : $"Gateway returned error code: {rawPayload.status_code}");
    }
}

/// <summary>
/// In-process asynchronous domain event dispatcher.
/// Decouples bounded contexts while maintaining transactional observability.
/// </summary>
public interface IDomainEventDispatcher
{
    void RegisterHandler<TEvent>(Func<TEvent, CancellationToken, Task> handler) where TEvent : IDomainEvent;
    Task DispatchAsync<TEvent>(TEvent domainEvent, CancellationToken cancellationToken = default) where TEvent : IDomainEvent;
    int DispatchedCount { get; }
}

public class InMemoryDomainEventDispatcher : IDomainEventDispatcher
{
    private readonly Dictionary<Type, List<Delegate>> _handlers = new();
    private readonly object _lock = new();
    private int _dispatchedCount;

    public int DispatchedCount => _dispatchedCount;

    public void RegisterHandler<TEvent>(Func<TEvent, CancellationToken, Task> handler) where TEvent : IDomainEvent
    {
        lock (_lock)
        {
            var eventType = typeof(TEvent);
            if (!_handlers.TryGetValue(eventType, out var list))
            {
                list = new List<Delegate>();
                _handlers[eventType] = list;
            }
            list.Add(handler);
        }
    }

    public async Task DispatchAsync<TEvent>(TEvent domainEvent, CancellationToken cancellationToken = default) where TEvent : IDomainEvent
    {
        List<Delegate> targets;
        lock (_lock)
        {
            var eventType = typeof(TEvent);
            if (!_handlers.TryGetValue(eventType, out var list))
                return;

            targets = new List<Delegate>(list);
        }

        foreach (var d in targets)
        {
            if (d is Func<TEvent, CancellationToken, Task> typedHandler)
            {
                await typedHandler(domainEvent, cancellationToken).ConfigureAwait(false);
                Interlocked.Increment(ref _dispatchedCount);
            }
        }
    }
}

/// <summary>
/// High-level orchestration showing Bounded Context interactions via Events and ACL.
/// </summary>
public class OrderFulfillmentCoordinator
{
    private readonly IDomainEventDispatcher _dispatcher;
    private readonly InventoryItem _inventoryItem;
    private readonly PaymentAntiCorruptionLayer _paymentAcl;

    public List<string> AuditLog { get; } = new();

    public OrderFulfillmentCoordinator(
        IDomainEventDispatcher dispatcher,
        InventoryItem inventoryItem,
        PaymentAntiCorruptionLayer paymentAcl)
    {
        _dispatcher = dispatcher;
        _inventoryItem = inventoryItem;
        _paymentAcl = paymentAcl;

        _dispatcher.RegisterHandler<OrderPlacedEvent>(HandleOrderPlacedAsync);
        _dispatcher.RegisterHandler<InventoryReservedEvent>(HandleInventoryReservedAsync);
    }

    public async Task ProcessOrderPlacementAsync(Order order)
    {
        order.Place();
        foreach (var ev in order.DomainEvents)
        {
            if (ev is OrderPlacedEvent placed)
            {
                await _dispatcher.DispatchAsync(placed).ConfigureAwait(false);
            }
        }
        order.ClearDomainEvents();
    }

    private Task HandleOrderPlacedAsync(OrderPlacedEvent ev, CancellationToken ct)
    {
        AuditLog.Add($"[OrderingContext] Order {ev.OrderId} placed for {ev.Quantity}x {ev.Sku}.");

        if (_inventoryItem.TryReserve(ev.Quantity, out var resId))
        {
            AuditLog.Add($"[InventoryContext] Stock reserved: {resId} for SKU {ev.Sku}.");
            var reservedEvent = new InventoryReservedEvent(resId, ev.OrderId, ev.Sku, ev.Quantity);
            return _dispatcher.DispatchAsync(reservedEvent, ct);
        }

        AuditLog.Add($"[InventoryContext] Out of stock for SKU {ev.Sku}.");
        return Task.CompletedTask;
    }

    private Task HandleInventoryReservedAsync(InventoryReservedEvent ev, CancellationToken ct)
    {
        AuditLog.Add($"[Fulfillment] Inventory confirmed for Order {ev.OrderId}. Ready for Payment Bounded Context.");
        return Task.CompletedTask;
    }

    public bool ProcessPaymentWithAcl(Order order, LegacyPaymentGatewayPayload gatewayResponse)
    {
        var aclResult = _paymentAcl.Translate(gatewayResponse);
        if (aclResult.IsSuccessful && aclResult.OrderId == order.Id)
        {
            order.MarkPaid();
            AuditLog.Add($"[PaymentContext] Payment authorized for Order {order.Id} with Code {aclResult.AuthorizationCode}.");
            return true;
        }

        AuditLog.Add($"[PaymentContext] Payment failed for Order {order.Id}: {aclResult.FailureReason}");
        return false;
    }
}
