using Day30.RabbitMQDLQMassTransit;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Day30.RabbitMQDLQMassTransit.Tests;

public class Day30Tests
{
    [Fact]
    public void Dlq_WhenMessageRejected_RoutesToDeadLetterQueue()
    {
        var simulator = new DlqRetrySimulator();
        simulator.DeclareQueue("orders.dlq");
        simulator.DeclareQueue("orders.work", new QueueArguments(
            DeadLetterExchange: "dlx.direct",
            DeadLetterRoutingKey: "orders.dlq"
        ));

        var msg = new AmqpMessage("msg-101", "orders.work", "{\"orderId\":\"101\"}");
        simulator.Publish("orders.work", msg);

        Assert.Single(simulator.GetQueueMessages("orders.work"));
        Assert.Empty(simulator.GetQueueMessages("orders.dlq"));

        // Consumer encounters unhandled exception
        simulator.Reject("orders.work", msg, new InvalidOperationException("Payment gateway unreachable"), requeue: false);

        Assert.Empty(simulator.GetQueueMessages("orders.work"));
        var dlqMsgs = simulator.GetQueueMessages("orders.dlq");
        Assert.Single(dlqMsgs);
        Assert.Equal("msg-101", dlqMsgs[0].MessageId);
        Assert.Equal(1, dlqMsgs[0].RetryCount);
    }

    [Fact]
    public void Dlq_CapturesXDeathMetadata_WithReasonAndExceptionDetails()
    {
        var simulator = new DlqRetrySimulator();
        simulator.DeclareQueue("orders.dlq");
        simulator.DeclareQueue("orders.work", new QueueArguments(
            DeadLetterExchange: "dlx.direct",
            DeadLetterRoutingKey: "orders.dlq"
        ));

        var msg = new AmqpMessage("msg-202", "orders.work", "{\"sku\":\"SKU-CORRUPT\"}");
        simulator.Publish("orders.work", msg);

        simulator.Reject("orders.work", msg, new FormatException("Corrupt SKU checksum"), requeue: false);

        var dlqMsg = simulator.GetQueueMessages("orders.dlq").Single();
        Assert.Single(dlqMsg.DeathHistory);

        var death = dlqMsg.DeathHistory[0];
        Assert.Equal("orders.work", death.Queue);
        Assert.Equal(DeadLetterReason.Rejected, death.Reason);
        Assert.Equal("Corrupt SKU checksum", death.ExceptionMessage);
        Assert.Equal(nameof(FormatException), death.ExceptionType);
    }

    [Fact]
    public void Dlq_TtlExpiration_MovesMessageToDeadLetterQueue()
    {
        var simulator = new DlqRetrySimulator();
        simulator.DeclareQueue("orders.retry.dlx");
        simulator.DeclareQueue("orders.retry.5s", new QueueArguments(
            DeadLetterExchange: "dlx.direct",
            DeadLetterRoutingKey: "orders.retry.dlx",
            MessageTtlMs: 5000
        ));

        var msg = new AmqpMessage("msg-303", "orders.retry.5s", "{\"orderId\":\"303\"}");
        simulator.Publish("orders.retry.5s", msg);

        Assert.Single(simulator.GetQueueMessages("orders.retry.5s"));

        // Simulate TTL clock expiry
        simulator.ExpireTtl("orders.retry.5s", "msg-303");

        Assert.Empty(simulator.GetQueueMessages("orders.retry.5s"));
        var dlqMessages = simulator.GetQueueMessages("orders.retry.dlx");
        Assert.Single(dlqMessages);
        Assert.Equal(DeadLetterReason.Expired, dlqMessages[0].DeathHistory[0].Reason);
    }

    [Fact]
    public void Dlq_QueueMaxLengthExceeded_DeadLettersOverflowMessage()
    {
        var simulator = new DlqRetrySimulator();
        simulator.DeclareQueue("orders.overflow.dlq");
        simulator.DeclareQueue("orders.bounded", new QueueArguments(
            DeadLetterExchange: "dlx.direct",
            DeadLetterRoutingKey: "orders.overflow.dlq",
            MaxLength: 2
        ));

        simulator.Publish("orders.bounded", new AmqpMessage("msg-1", "orders.bounded", "item-1"));
        simulator.Publish("orders.bounded", new AmqpMessage("msg-2", "orders.bounded", "item-2"));

        Assert.Equal(2, simulator.GetQueueMessages("orders.bounded").Count);
        Assert.Empty(simulator.GetQueueMessages("orders.overflow.dlq"));

        // 3rd message overflows bounded queue
        simulator.Publish("orders.bounded", new AmqpMessage("msg-3", "orders.bounded", "item-3"));

        Assert.Equal(2, simulator.GetQueueMessages("orders.bounded").Count);
        var overflowList = simulator.GetQueueMessages("orders.overflow.dlq");
        Assert.Single(overflowList);
        Assert.Equal("msg-3", overflowList[0].MessageId);
        Assert.Equal(DeadLetterReason.MaxLengthExceeded, overflowList[0].DeathHistory[0].Reason);
    }

    [Fact]
    public void ProgressiveRetry_TtlDelayedRetryPipeline_RequeuesBackToMainQueue()
    {
        var simulator = new DlqRetrySimulator();
        simulator.DeclareQueue("orders.work");
        simulator.DeclareQueue("orders.retry.5s", new QueueArguments(
            DeadLetterExchange: "dlx.direct",
            DeadLetterRoutingKey: "orders.work",
            MessageTtlMs: 5000
        ));

        var originalMsg = new AmqpMessage("retry-item", "orders.work", "{\"payload\":\"work\"}");
        simulator.Publish("orders.work", originalMsg);

        // Reject into retry queue
        var retryQueueConfig = new QueueArguments(DeadLetterExchange: "dlx.direct", DeadLetterRoutingKey: "orders.retry.5s");
        simulator.DeclareQueue("orders.work", retryQueueConfig);

        simulator.Reject("orders.work", originalMsg, new HttpRequestException("Service Unavailable (503)"));

        Assert.Empty(simulator.GetQueueMessages("orders.work"));
        Assert.Single(simulator.GetQueueMessages("orders.retry.5s"));

        // When 5s TTL expires, message is sent back to orders.work via DLX
        simulator.ExpireTtl("orders.retry.5s", "retry-item");

        Assert.Empty(simulator.GetQueueMessages("orders.retry.5s"));
        var reprocessed = simulator.GetQueueMessages("orders.work");
        Assert.Single(reprocessed);
        Assert.Equal(1, reprocessed[0].RetryCount);
    }

    [Fact]
    public async Task MassTransit_OrderStateMachine_TransitionsToSubmittedInitially()
    {
        var services = new ServiceCollection();
        services.AddMassTransitTestHarness(cfg =>
        {
            cfg.AddSagaStateMachine<OrderStateMachine, OrderStateInstance>();
        });

        await using var provider = services.BuildServiceProvider(true);
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        var correlationId = Guid.NewGuid();
        await harness.Bus.Publish(new OrderSubmittedEvent(correlationId, "ORD-9901", 149.99m));

        var sagaHarness = harness.GetSagaStateMachineHarness<OrderStateMachine, OrderStateInstance>();
        Assert.True(await sagaHarness.Consumed.Any<OrderSubmittedEvent>());

        var sagaInstance = sagaHarness.Created.Contains(correlationId);
        Assert.NotNull(sagaInstance);
        Assert.Equal("Submitted", sagaInstance.CurrentState);
        Assert.Equal("ORD-9901", sagaInstance.OrderNumber);
        Assert.Equal(149.99m, sagaInstance.TotalAmount);
    }

    [Fact]
    public async Task MassTransit_OrderStateMachine_TransitionsToCompletedOnPayment()
    {
        var services = new ServiceCollection();
        services.AddMassTransitTestHarness(cfg =>
        {
            cfg.AddSagaStateMachine<OrderStateMachine, OrderStateInstance>();
        });

        await using var provider = services.BuildServiceProvider(true);
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        var correlationId = Guid.NewGuid();
        await harness.Bus.Publish(new OrderSubmittedEvent(correlationId, "ORD-9902", 299.50m));
        await harness.Bus.Publish(new InventoryReservedEvent(correlationId, DateTime.UtcNow));
        await harness.Bus.Publish(new PaymentCompletedEvent(correlationId, "TXN-8812", DateTime.UtcNow));

        var sagaHarness = harness.GetSagaStateMachineHarness<OrderStateMachine, OrderStateInstance>();
        Assert.True(await sagaHarness.Consumed.Any<PaymentCompletedEvent>());

        var sagaInstance = sagaHarness.Created.Contains(correlationId);
        Assert.NotNull(sagaInstance);
        Assert.Equal("Completed", sagaInstance.CurrentState);
        Assert.Equal("TXN-8812", sagaInstance.PaymentTransactionId);
    }

    [Fact]
    public async Task MassTransit_OrderStateMachine_TransitionsToFaultedOnOrderFault()
    {
        var services = new ServiceCollection();
        services.AddMassTransitTestHarness(cfg =>
        {
            cfg.AddSagaStateMachine<OrderStateMachine, OrderStateInstance>();
        });

        await using var provider = services.BuildServiceProvider(true);
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        var correlationId = Guid.NewGuid();
        await harness.Bus.Publish(new OrderSubmittedEvent(correlationId, "ORD-9903", 50.00m));
        await harness.Bus.Publish(new OrderFaultedEvent(correlationId, "Inventory out of stock"));

        var sagaHarness = harness.GetSagaStateMachineHarness<OrderStateMachine, OrderStateInstance>();
        Assert.True(await sagaHarness.Consumed.Any<OrderFaultedEvent>());

        var sagaInstance = sagaHarness.Created.Contains(correlationId);
        Assert.NotNull(sagaInstance);
        Assert.Equal("Faulted", sagaInstance.CurrentState);
        Assert.Equal("Inventory out of stock", sagaInstance.FaultReason);
    }

    [Fact]
    public void CourseSchedule_LC207_DetectsValidScheduleAndCycles()
    {
        // Example 1: numCourses = 2, prerequisites = [[1,0]] -> true
        var prereq1 = new int[][] { new[] { 1, 0 } };
        Assert.True(CourseScheduleSolver.CanFinishKahn(2, prereq1));
        Assert.True(CourseScheduleSolver.CanFinishDfs(2, prereq1));

        // Example 2: numCourses = 2, prerequisites = [[1,0],[0,1]] -> false (cycle)
        var prereq2 = new int[][] { new[] { 1, 0 }, new[] { 0, 1 } };
        Assert.False(CourseScheduleSolver.CanFinishKahn(2, prereq2));
        Assert.False(CourseScheduleSolver.CanFinishDfs(2, prereq2));

        // Complex DAG: 4 courses: 0->1, 0->2, 1->3, 2->3 -> true
        var prereq3 = new int[][]
        {
            new[] { 1, 0 },
            new[] { 2, 0 },
            new[] { 3, 1 },
            new[] { 3, 2 }
        };
        Assert.True(CourseScheduleSolver.CanFinishKahn(4, prereq3));
        Assert.True(CourseScheduleSolver.CanFinishDfs(4, prereq3));
    }

    [Fact]
    public void CourseScheduleII_LC210_ProducesValidTopologicalOrdering()
    {
        // numCourses = 4, prereqs: 1->0, 2->0, 3->1, 3->2
        var prereqs = new int[][]
        {
            new[] { 1, 0 },
            new[] { 2, 0 },
            new[] { 3, 1 },
            new[] { 3, 2 }
        };

        var kahnOrder = CourseScheduleSolver.FindOrderKahn(4, prereqs);
        Assert.Equal(4, kahnOrder.Length);
        Assert.Equal(0, kahnOrder[0]); // 0 must be first since it has 0 prereqs
        Assert.Equal(3, kahnOrder[3]); // 3 must be last

        var dfsOrder = CourseScheduleSolver.FindOrderDfs(4, prereqs);
        Assert.Equal(4, dfsOrder.Length);

        // Cyclic case returns empty array
        var cyclePrereqs = new int[][]
        {
            new[] { 0, 1 },
            new[] { 1, 2 },
            new[] { 2, 0 }
        };
        Assert.Empty(CourseScheduleSolver.FindOrderKahn(3, cyclePrereqs));
        Assert.Empty(CourseScheduleSolver.FindOrderDfs(3, cyclePrereqs));
    }
}
