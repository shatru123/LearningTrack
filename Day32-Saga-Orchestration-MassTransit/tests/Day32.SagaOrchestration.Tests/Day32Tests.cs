using Day32.SagaOrchestration;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Day32.SagaOrchestration.Tests;

public class Day32Tests
{
    [Fact]
    public async Task Saga_HappyPath_CompletesOrderSuccessfully()
    {
        var services = new ServiceCollection();
        services.AddMassTransitTestHarness(cfg =>
        {
            cfg.AddSagaStateMachine<OrderFulfillmentStateMachine, OrderFulfillmentState>();
        });

        await using var provider = services.BuildServiceProvider(true);
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        var correlationId = Guid.NewGuid();
        var sagaHarness = harness.GetSagaStateMachineHarness<OrderFulfillmentStateMachine, OrderFulfillmentState>();

        // 1. Submit Order
        await harness.Bus.Publish(new OrderSubmitted(correlationId, "CUST-001", 350.00m, new List<string> { "SKU-A", "SKU-B" }));
        Assert.True(await sagaHarness.Consumed.Any<OrderSubmitted>());

        // 2. Reserve Inventory
        await harness.Bus.Publish(new InventoryReserved(correlationId, DateTime.UtcNow));
        Assert.True(await sagaHarness.Consumed.Any<InventoryReserved>());

        // 3. Authorize Payment
        await harness.Bus.Publish(new PaymentAuthorized(correlationId, "TXN-777", DateTime.UtcNow));
        Assert.True(await sagaHarness.Consumed.Any<PaymentAuthorized>());

        // 4. Ship Order
        await harness.Bus.Publish(new OrderShipped(correlationId, "TRACK-999-US", DateTime.UtcNow));
        Assert.True(await sagaHarness.Consumed.Any<OrderShipped>());

        var instance = sagaHarness.Created.Contains(correlationId);
        Assert.NotNull(instance);
        Assert.Equal("Completed", instance.CurrentState);
        Assert.Equal("TRACK-999-US", instance.TrackingNumber);
        Assert.Equal("TXN-777", instance.PaymentTransactionId);
        Assert.False(instance.InventoryCompensationTriggered);
    }

    [Fact]
    public async Task Saga_PaymentFailure_TransitionsToCompensatingState()
    {
        var services = new ServiceCollection();
        services.AddMassTransitTestHarness(cfg =>
        {
            cfg.AddSagaStateMachine<OrderFulfillmentStateMachine, OrderFulfillmentState>();
        });

        await using var provider = services.BuildServiceProvider(true);
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        var correlationId = Guid.NewGuid();
        var sagaHarness = harness.GetSagaStateMachineHarness<OrderFulfillmentStateMachine, OrderFulfillmentState>();

        await harness.Bus.Publish(new OrderSubmitted(correlationId, "CUST-002", 120.00m, new List<string> { "SKU-C" }));
        await harness.Bus.Publish(new InventoryReserved(correlationId, DateTime.UtcNow));
        await harness.Bus.Publish(new PaymentFailed(correlationId, "Card expired or insufficient balance"));

        Assert.True(await sagaHarness.Consumed.Any<PaymentFailed>());

        var instance = sagaHarness.Created.Contains(correlationId);
        Assert.NotNull(instance);
        Assert.Equal("CompensatingInventoryState", instance.CurrentState);
        Assert.True(instance.InventoryCompensationTriggered);
        Assert.Equal("Card expired or insufficient balance", instance.FailureReason);
    }

    [Fact]
    public async Task Saga_InventoryRelease_TransitionsToFaultedState()
    {
        var services = new ServiceCollection();
        services.AddMassTransitTestHarness(cfg =>
        {
            cfg.AddSagaStateMachine<OrderFulfillmentStateMachine, OrderFulfillmentState>();
        });

        await using var provider = services.BuildServiceProvider(true);
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        var correlationId = Guid.NewGuid();
        var sagaHarness = harness.GetSagaStateMachineHarness<OrderFulfillmentStateMachine, OrderFulfillmentState>();

        await harness.Bus.Publish(new OrderSubmitted(correlationId, "CUST-003", 50.00m, new List<string> { "SKU-D" }));
        await harness.Bus.Publish(new InventoryReserved(correlationId, DateTime.UtcNow));
        await harness.Bus.Publish(new PaymentFailed(correlationId, "Fraud suspected"));
        await harness.Bus.Publish(new InventoryReleased(correlationId, DateTime.UtcNow));

        Assert.True(await sagaHarness.Consumed.Any<InventoryReleased>());

        var instance = sagaHarness.Created.Contains(correlationId);
        Assert.NotNull(instance);
        Assert.Equal("Faulted", instance.CurrentState);
        Assert.NotNull(instance.InventoryReleasedAt);
    }

    [Fact]
    public async Task Saga_CorrelationId_MatchesAcrossMultiEventFlow()
    {
        var services = new ServiceCollection();
        services.AddMassTransitTestHarness(cfg =>
        {
            cfg.AddSagaStateMachine<OrderFulfillmentStateMachine, OrderFulfillmentState>();
        });

        await using var provider = services.BuildServiceProvider(true);
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        var sagaHarness = harness.GetSagaStateMachineHarness<OrderFulfillmentStateMachine, OrderFulfillmentState>();

        await harness.Bus.Publish(new OrderSubmitted(id1, "CUST-1", 10m, new List<string> { "Item1" }));
        await harness.Bus.Publish(new OrderSubmitted(id2, "CUST-2", 20m, new List<string> { "Item2" }));
        Assert.True(await sagaHarness.Consumed.Any<OrderSubmitted>(x => x.Context.Message.CorrelationId == id1));
        Assert.True(await sagaHarness.Consumed.Any<OrderSubmitted>(x => x.Context.Message.CorrelationId == id2));

        await harness.Bus.Publish(new InventoryReserved(id1, DateTime.UtcNow));
        Assert.True(await sagaHarness.Consumed.Any<InventoryReserved>(x => x.Context.Message.CorrelationId == id1));

        var inst1 = sagaHarness.Created.Contains(id1);
        var inst2 = sagaHarness.Created.Contains(id2);

        Assert.NotNull(inst1);
        Assert.NotNull(inst2);
        Assert.Equal("InventoryReservedState", inst1.CurrentState);
        Assert.Equal("Submitted", inst2.CurrentState);
    }

    [Fact]
    public async Task Saga_AuditTrail_RecordsStepByStepWorkflowHistory()
    {
        var services = new ServiceCollection();
        services.AddMassTransitTestHarness(cfg =>
        {
            cfg.AddSagaStateMachine<OrderFulfillmentStateMachine, OrderFulfillmentState>();
        });

        await using var provider = services.BuildServiceProvider(true);
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        var correlationId = Guid.NewGuid();
        var sagaHarness = harness.GetSagaStateMachineHarness<OrderFulfillmentStateMachine, OrderFulfillmentState>();

        await harness.Bus.Publish(new OrderSubmitted(correlationId, "CUST-005", 250m, new List<string> { "X" }));
        Assert.True(await sagaHarness.Consumed.Any<OrderSubmitted>());

        await harness.Bus.Publish(new InventoryReserved(correlationId, DateTime.UtcNow));
        Assert.True(await sagaHarness.Consumed.Any<InventoryReserved>());

        await harness.Bus.Publish(new PaymentFailed(correlationId, "Network timeout"));
        Assert.True(await sagaHarness.Consumed.Any<PaymentFailed>());

        await harness.Bus.Publish(new InventoryReleased(correlationId, DateTime.UtcNow));
        Assert.True(await sagaHarness.Consumed.Any<InventoryReleased>());

        var instance = sagaHarness.Created.Contains(correlationId);
        Assert.NotNull(instance);
        Assert.Equal(4, instance.AuditTrail.Count);
        Assert.Contains("Order submitted", instance.AuditTrail[0]);
        Assert.Contains("Inventory successfully reserved", instance.AuditTrail[1]);
        Assert.Contains("compensation rollback", instance.AuditTrail[2]);
        Assert.Contains("Faulted", instance.AuditTrail[3]);
    }

    [Fact]
    public void Saga_Comparator_EvaluatesCouplingAndVisibility()
    {
        var orch = SagaArchitectureComparator.EvaluateOrchestration();
        var chor = SagaArchitectureComparator.EvaluateChoreography();

        Assert.True(orch.CouplingScore < chor.CouplingScore);
        Assert.True(orch.CentralVisibility);
        Assert.False(chor.CentralVisibility);
        Assert.True(orch.RollbackCoordination);
        Assert.False(chor.RollbackCoordination);
    }

    [Fact]
    public void WordLadder_LC127_FindsShortestPathLength()
    {
        // Example 1: begin = "hit", end = "cog", wordList = ["hot","dot","dog","lot","log","cog"]
        // Shortest transformation: "hit" -> "hot" -> "dot" -> "dog" -> "cog" (length = 5)
        var wordList = new List<string> { "hot", "dot", "dog", "lot", "log", "cog" };
        int result = WordLadderSolver.LadderLength("hit", "cog", wordList);
        Assert.Equal(5, result);
    }

    [Fact]
    public void WordLadder_LC127_ReturnsZeroWhenEndWordNotInList()
    {
        // Example 2: "cog" is not in wordList
        var wordList = new List<string> { "hot", "dot", "dog", "lot", "log" };
        int result = WordLadderSolver.LadderLength("hit", "cog", wordList);
        Assert.Equal(0, result);
    }

    [Fact]
    public void WordLadder_LC127_ReturnsZeroWhenNoPathExists()
    {
        var wordList = new List<string> { "hot", "dog" };
        int result = WordLadderSolver.LadderLength("hit", "cog", wordList);
        Assert.Equal(0, result);
    }

    [Fact]
    public void WordLadder_LC127_HandlesSingleStepTransformation()
    {
        var wordList = new List<string> { "hot" };
        int result = WordLadderSolver.LadderLength("hit", "hot", wordList);
        Assert.Equal(2, result);
    }
}
