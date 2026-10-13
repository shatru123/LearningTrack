using Day26.MicroservicesBoundedContexts;
using Xunit;

namespace Day26.MicroservicesBoundedContexts.Tests;

public class Day26Tests
{
    [Fact]
    public void Order_AddItem_AndCalculateTotal_AccuratelyAggregates()
    {
        var order = new Order(Guid.NewGuid(), "CUST-1001");
        var sku1 = new Sku("PROD-LAPTOP");
        var sku2 = new Sku("PROD-MOUSE");

        order.AddItem(sku1, 2, new Money(1200.00m, "USD"));
        order.AddItem(sku2, 1, new Money(50.00m, "USD"));

        var total = order.CalculateTotal();

        Assert.Equal(2450.00m, total.Amount);
        Assert.Equal("USD", total.Currency);
        Assert.Equal(OrderStatus.Draft, order.Status);
    }

    [Fact]
    public void Order_Place_EmitsOrderPlacedDomainEvent()
    {
        var order = new Order(Guid.NewGuid(), "CUST-1002");
        var sku = new Sku("PROD-KEYBOARD");
        order.AddItem(sku, 1, new Money(150.00m, "USD"));

        order.Place();

        Assert.Equal(OrderStatus.Placed, order.Status);
        Assert.Single(order.DomainEvents);

        var domainEvent = Assert.IsType<OrderPlacedEvent>(order.DomainEvents[0]);
        Assert.Equal(order.Id, domainEvent.OrderId);
        Assert.Equal("CUST-1002", domainEvent.CustomerId);
        Assert.Equal("PROD-KEYBOARD", domainEvent.Sku);
        Assert.Equal(1, domainEvent.Quantity);
        Assert.Equal(150.00m, domainEvent.TotalAmount);
    }

    [Fact]
    public void Order_Invariants_EnforceValidation()
    {
        var order = new Order(Guid.NewGuid(), "CUST-1003");
        var sku = new Sku("PROD-MONITOR");

        // Placing empty order throws
        Assert.Throws<InvalidOperationException>(() => order.Place());

        // Zero or negative quantity throws
        Assert.Throws<ArgumentOutOfRangeException>(() => order.AddItem(sku, 0, new Money(300m)));

        // Negative money throws
        Assert.Throws<ArgumentException>(() => new Money(-10m));

        // Mismatched currencies throw
        var usd = new Money(100m, "USD");
        var eur = new Money(50m, "EUR");
        Assert.Throws<InvalidOperationException>(() => _ = usd + eur);
    }

    [Fact]
    public void InventoryItem_TryReserve_AndRelease_MaintainsInvariants()
    {
        var sku = new Sku("PROD-HEADSET");
        var inventory = new InventoryItem(sku, initialStock: 10);

        var reserved = inventory.TryReserve(4, out var reservationId);
        Assert.True(reserved);
        Assert.NotEqual(Guid.Empty, reservationId);
        Assert.Equal(6, inventory.AvailableStock);
        Assert.Equal(4, inventory.ReservedStock);

        // Attempting to reserve more than available fails
        var overReserve = inventory.TryReserve(7, out _);
        Assert.False(overReserve);

        // Release stock
        inventory.ReleaseReservation(4);
        Assert.Equal(10, inventory.AvailableStock);
        Assert.Equal(0, inventory.ReservedStock);
    }

    [Fact]
    public void PaymentAntiCorruptionLayer_TranslatesLegacyGatewaySuccess()
    {
        var acl = new PaymentAntiCorruptionLayer();
        var orderId = Guid.NewGuid();
        var rawPayload = new LegacyPaymentGatewayPayload(
            tx_id: "txn_987654",
            order_ref: orderId.ToString(),
            amt_cents: "245000",
            curr: "usd",
            status_code: "AUTH_200",
            auth_token: "AUTH_XYZ_789"
        );

        var result = acl.Translate(rawPayload);

        Assert.True(result.IsSuccessful);
        Assert.Equal(orderId, result.OrderId);
        Assert.Equal(2450.00m, result.Amount);
        Assert.Equal("USD", result.Currency);
        Assert.Equal("AUTH_XYZ_789", result.AuthorizationCode);
        Assert.Null(result.FailureReason);
    }

    [Fact]
    public void PaymentAntiCorruptionLayer_TranslatesInvalidPayloadGracefully()
    {
        var acl = new PaymentAntiCorruptionLayer();
        var rawPayload = new LegacyPaymentGatewayPayload(
            tx_id: "txn_err",
            order_ref: "not-a-valid-guid",
            amt_cents: "invalid_cents",
            curr: "eur",
            status_code: "ERR_500",
            auth_token: ""
        );

        var result = acl.Translate(rawPayload);

        Assert.False(result.IsSuccessful);
        Assert.Equal(Guid.Empty, result.OrderId);
        Assert.NotNull(result.FailureReason);
    }

    [Fact]
    public async Task OrderFulfillmentCoordinator_CoordinatesAcrossBoundedContexts()
    {
        var dispatcher = new InMemoryDomainEventDispatcher();
        var sku = new Sku("PROD-SERVER");
        var inventory = new InventoryItem(sku, initialStock: 5);
        var paymentAcl = new PaymentAntiCorruptionLayer();

        var coordinator = new OrderFulfillmentCoordinator(dispatcher, inventory, paymentAcl);

        var order = new Order(Guid.NewGuid(), "CUST-CORP-1");
        order.AddItem(sku, 2, new Money(5000.00m, "USD"));

        await coordinator.ProcessOrderPlacementAsync(order);

        Assert.Equal(3, inventory.AvailableStock);
        Assert.Equal(2, inventory.ReservedStock);
        Assert.Equal(2, dispatcher.DispatchedCount); // OrderPlacedEvent + InventoryReservedEvent

        // Now test payment through ACL
        var gatewayResponse = new LegacyPaymentGatewayPayload(
            tx_id: "tx_112233",
            order_ref: order.Id.ToString(),
            amt_cents: "1000000",
            curr: "usd",
            status_code: "SUCCESS",
            auth_token: "AUTH_OK"
        );

        var paymentSuccess = coordinator.ProcessPaymentWithAcl(order, gatewayResponse);

        Assert.True(paymentSuccess);
        Assert.Equal(OrderStatus.Paid, order.Status);
        Assert.Contains(coordinator.AuditLog, log => log.Contains("[PaymentContext] Payment authorized"));
    }

    [Fact]
    public void NumberOfIslands_DfsAndBfs_StandardGrid_ComputesCorrectCount()
    {
        var solver = new NumberOfIslandsSolver();

        // 3 Islands grid:
        // 1 1 0 0 0
        // 1 1 0 0 0
        // 0 0 1 0 0
        // 0 0 0 1 1
        var grid = new char[][]
        {
            new[] { '1', '1', '0', '0', '0' },
            new[] { '1', '1', '0', '0', '0' },
            new[] { '0', '0', '1', '0', '0' },
            new[] { '0', '0', '0', '1', '1' }
        };

        var dfsResult = solver.NumIslandsDfs(grid);
        var bfsResult = solver.NumIslandsBfs(grid);

        Assert.Equal(3, dfsResult);
        Assert.Equal(3, bfsResult);
    }

    [Fact]
    public void NumberOfIslands_HandlesEdgeCases_Correctly()
    {
        var solver = new NumberOfIslandsSolver();

        Assert.Equal(0, solver.NumIslandsDfs(null));
        Assert.Equal(0, solver.NumIslandsBfs(null));
        Assert.Equal(0, solver.NumIslandsDfs(Array.Empty<char[]>()));
        Assert.Equal(0, solver.NumIslandsBfs(Array.Empty<char[]>()));

        // All water
        var waterGrid = new char[][]
        {
            new[] { '0', '0' },
            new[] { '0', '0' }
        };
        Assert.Equal(0, solver.NumIslandsDfs(waterGrid));
        Assert.Equal(0, solver.NumIslandsBfs(waterGrid));

        // All land
        var landGrid = new char[][]
        {
            new[] { '1', '1' },
            new[] { '1', '1' }
        };
        Assert.Equal(1, solver.NumIslandsDfs(landGrid));
        Assert.Equal(1, solver.NumIslandsBfs(landGrid));
    }

    [Fact]
    public void NumberOfIslands_HandlesDiagonalSeparation_AsDistinctIslands()
    {
        var solver = new NumberOfIslandsSolver();

        // Diagonals are NOT connected:
        // 1 0
        // 0 1
        var diagonalGrid = new char[][]
        {
            new[] { '1', '0' },
            new[] { '0', '1' }
        };

        Assert.Equal(2, solver.NumIslandsDfs(diagonalGrid));
        Assert.Equal(2, solver.NumIslandsBfs(diagonalGrid));
    }
}
