namespace Day26.MicroservicesBoundedContexts;

/// <summary>
/// DDD Strategic Classification of Subdomains.
/// </summary>
public enum SubdomainType
{
    Core,        // Primary competitive differentiator (e.g., Dynamic Pricing, Proprietary Order Engine)
    Supporting,  // Complements core domain, tailored to business (e.g., Inventory Allocation)
    Generic      // Off-the-shelf or standardized capabilities (e.g., Identity, Payments, Notifications)
}

/// <summary>
/// Domain Event marker interface.
/// </summary>
public interface IDomainEvent
{
    Guid EventId { get; }
    DateTime OccurredOnUtc { get; }
}

/// <summary>
/// Order Placed Domain Event emitted by the Ordering Bounded Context.
/// </summary>
public record OrderPlacedEvent(
    Guid OrderId,
    string CustomerId,
    string Sku,
    int Quantity,
    decimal TotalAmount,
    string Currency,
    Guid EventId,
    DateTime OccurredOnUtc) : IDomainEvent
{
    public OrderPlacedEvent(Guid orderId, string customerId, string sku, int quantity, decimal totalAmount, string currency)
        : this(orderId, customerId, sku, quantity, totalAmount, currency, Guid.NewGuid(), DateTime.UtcNow) { }
}

/// <summary>
/// Inventory Reserved Domain Event emitted by the Inventory Bounded Context.
/// </summary>
public record InventoryReservedEvent(
    Guid ReservationId,
    Guid OrderId,
    string Sku,
    int QuantityReserved,
    Guid EventId,
    DateTime OccurredOnUtc) : IDomainEvent
{
    public InventoryReservedEvent(Guid reservationId, Guid orderId, string sku, int quantityReserved)
        : this(reservationId, orderId, sku, quantityReserved, Guid.NewGuid(), DateTime.UtcNow) { }
}

/// <summary>
/// Payment Authorized Domain Event emitted by the Payment Bounded Context.
/// </summary>
public record PaymentAuthorizedEvent(
    Guid PaymentId,
    Guid OrderId,
    decimal Amount,
    string Currency,
    string AuthCode,
    Guid EventId,
    DateTime OccurredOnUtc) : IDomainEvent
{
    public PaymentAuthorizedEvent(Guid paymentId, Guid orderId, decimal amount, string currency, string authCode)
        : this(paymentId, orderId, amount, currency, authCode, Guid.NewGuid(), DateTime.UtcNow) { }
}

/// <summary>
/// Money Value Object with immutable semantics and currency guardrails.
/// </summary>
public readonly record struct Money
{
    public decimal Amount { get; }
    public string Currency { get; }

    public Money(decimal amount, string currency = "USD")
    {
        if (amount < 0)
            throw new ArgumentException("Money amount cannot be negative.", nameof(amount));
        if (string.IsNullOrWhiteSpace(currency))
            throw new ArgumentException("Currency must be specified.", nameof(currency));

        Amount = decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
        Currency = currency.ToUpperInvariant();
    }

    public static Money Zero(string currency = "USD") => new(0m, currency);

    public static Money operator +(Money a, Money b)
    {
        if (a.Currency != b.Currency)
            throw new InvalidOperationException($"Cannot add mismatched currencies: {a.Currency} and {b.Currency}");
        return new Money(a.Amount + b.Amount, a.Currency);
    }

    public static Money operator *(Money a, int multiplier)
    {
        if (multiplier < 0)
            throw new ArgumentException("Multiplier cannot be negative.", nameof(multiplier));
        return new Money(a.Amount * multiplier, a.Currency);
    }
}

/// <summary>
/// Sku Value Object ensuring normalized product identifiers.
/// </summary>
public readonly record struct Sku
{
    public string Value { get; }

    public Sku(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("SKU cannot be empty.", nameof(value));
        Value = value.Trim().ToUpperInvariant();
    }

    public override string ToString() => Value;
}

public enum OrderStatus
{
    Draft,
    Placed,
    Paid,
    Cancelled
}

/// <summary>
/// Order Aggregate Root in Ordering Bounded Context.
/// Enforces business invariants and encapsulates Domain Events.
/// </summary>
public class Order
{
    private readonly List<IDomainEvent> _domainEvents = new();
    private readonly List<OrderItem> _items = new();

    public Guid Id { get; }
    public string CustomerId { get; }
    public OrderStatus Status { get; private set; }
    public IReadOnlyList<OrderItem> Items => _items.AsReadOnly();
    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    public Order(Guid id, string customerId)
    {
        if (string.IsNullOrWhiteSpace(customerId))
            throw new ArgumentException("Customer ID is required.", nameof(customerId));

        Id = id == Guid.Empty ? Guid.NewGuid() : id;
        CustomerId = customerId.Trim();
        Status = OrderStatus.Draft;
    }

    public void AddItem(Sku sku, int quantity, Money unitPrice)
    {
        if (Status != OrderStatus.Draft)
            throw new InvalidOperationException($"Cannot modify order in status: {Status}");
        if (quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be greater than zero.");

        var existing = _items.FirstOrDefault(i => i.Sku == sku);
        if (existing != null)
        {
            _items.Remove(existing);
            _items.Add(new OrderItem(sku, existing.Quantity + quantity, unitPrice));
        }
        else
        {
            _items.Add(new OrderItem(sku, quantity, unitPrice));
        }
    }

    public Money CalculateTotal()
    {
        if (_items.Count == 0)
            return Money.Zero();

        var currency = _items[0].UnitPrice.Currency;
        var total = Money.Zero(currency);
        foreach (var item in _items)
        {
            total += item.UnitPrice * item.Quantity;
        }
        return total;
    }

    public void Place()
    {
        if (Status != OrderStatus.Draft)
            throw new InvalidOperationException($"Cannot place order in status: {Status}");
        if (_items.Count == 0)
            throw new InvalidOperationException("Cannot place an order with zero line items.");

        Status = OrderStatus.Placed;
        var total = CalculateTotal();
        var primaryItem = _items[0];

        _domainEvents.Add(new OrderPlacedEvent(
            Id,
            CustomerId,
            primaryItem.Sku.Value,
            primaryItem.Quantity,
            total.Amount,
            total.Currency
        ));
    }

    public void MarkPaid()
    {
        if (Status != OrderStatus.Placed)
            throw new InvalidOperationException($"Only placed orders can be marked paid. Current status: {Status}");
        Status = OrderStatus.Paid;
    }

    public void Cancel()
    {
        if (Status == OrderStatus.Paid)
            throw new InvalidOperationException("Cannot cancel an already paid order.");
        Status = OrderStatus.Cancelled;
    }

    public void ClearDomainEvents() => _domainEvents.Clear();
}

public record OrderItem(Sku Sku, int Quantity, Money UnitPrice);

/// <summary>
/// Inventory Item Aggregate Root in Inventory Bounded Context.
/// </summary>
public class InventoryItem
{
    public Sku Sku { get; }
    public int AvailableStock { get; private set; }
    public int ReservedStock { get; private set; }

    public InventoryItem(Sku sku, int initialStock)
    {
        if (initialStock < 0)
            throw new ArgumentException("Initial stock cannot be negative.", nameof(initialStock));
        Sku = sku;
        AvailableStock = initialStock;
        ReservedStock = 0;
    }

    public bool TryReserve(int quantity, out Guid reservationId)
    {
        if (quantity <= 0 || AvailableStock < quantity)
        {
            reservationId = Guid.Empty;
            return false;
        }

        AvailableStock -= quantity;
        ReservedStock += quantity;
        reservationId = Guid.NewGuid();
        return true;
    }

    public void ReleaseReservation(int quantity)
    {
        if (quantity <= 0 || ReservedStock < quantity)
            throw new InvalidOperationException("Cannot release more stock than currently reserved.");

        ReservedStock -= quantity;
        AvailableStock += quantity;
    }
}
