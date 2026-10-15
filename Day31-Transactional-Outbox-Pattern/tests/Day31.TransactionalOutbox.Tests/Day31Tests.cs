using Day31.TransactionalOutbox;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Day31.TransactionalOutbox.Tests;

public class Day31Tests
{
    private OutboxDbContext CreateInMemoryDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<OutboxDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;
        return new OutboxDbContext(options);
    }

    [Fact]
    public async Task OrderService_AtomicCreate_PersistsOrderAndOutboxMessage()
    {
        using var context = CreateInMemoryDbContext(nameof(OrderService_AtomicCreate_PersistsOrderAndOutboxMessage));
        var service = new OrderService(context);

        var order = await service.CreateOrderAsync("ORD-1001", 189.50m);

        Assert.NotNull(order);
        Assert.Equal(1, await context.Orders.CountAsync());
        Assert.Equal(1, await context.OutboxMessages.CountAsync());

        var outbox = await context.OutboxMessages.SingleAsync();
        Assert.Equal("OrderCreatedEvent", outbox.EventType);
        Assert.Null(outbox.ProcessedOnUtc);
        Assert.False(outbox.IsDeadLettered);
        Assert.Contains("ORD-1001", outbox.Payload);
    }

    [Fact]
    public async Task OrderService_SimulatedRollback_PreventsDualWriteHazard()
    {
        using var context = CreateInMemoryDbContext(nameof(OrderService_SimulatedRollback_PreventsDualWriteHazard));
        var service = new OrderService(context);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateOrderAsync("ORD-FAIL-1", 99.00m, simulateFailureAfterDbSave: true)
        );

        // Neither the order nor the outbox event should be persisted in database
        Assert.Equal(0, await context.Orders.CountAsync());
        Assert.Equal(0, await context.OutboxMessages.CountAsync());
    }

    [Fact]
    public async Task OutboxPublisher_ProcessesPendingMessages_AndMarksThemProcessed()
    {
        using var context = CreateInMemoryDbContext(nameof(OutboxPublisher_ProcessesPendingMessages_AndMarksThemProcessed));
        var broker = new InMemoryMessageBroker();
        var orderService = new OrderService(context);
        var publisher = new OutboxPublisherWorker(context, broker);

        await orderService.CreateOrderAsync("ORD-2001", 49.99m);
        await orderService.CreateOrderAsync("ORD-2002", 79.99m);

        Assert.Empty(broker.PublishedMessages);

        int processed = await publisher.ProcessOutboxMessagesAsync(batchSize: 10);

        Assert.Equal(2, processed);
        Assert.Equal(2, broker.PublishedMessages.Count);

        var messages = await context.OutboxMessages.ToListAsync();
        Assert.All(messages, m =>
        {
            Assert.NotNull(m.ProcessedOnUtc);
            Assert.Null(m.LockedUntilUtc);
            Assert.Null(m.LockId);
        });
    }

    [Fact]
    public async Task OutboxPublisher_RespectsBatchSizeLimit()
    {
        using var context = CreateInMemoryDbContext(nameof(OutboxPublisher_RespectsBatchSizeLimit));
        var broker = new InMemoryMessageBroker();
        var orderService = new OrderService(context);
        var publisher = new OutboxPublisherWorker(context, broker);

        for (int i = 1; i <= 5; i++)
        {
            await orderService.CreateOrderAsync($"ORD-BATCH-{i}", 10.00m * i);
        }

        // Process with batch size of 2
        int processedBatch1 = await publisher.ProcessOutboxMessagesAsync(batchSize: 2);
        Assert.Equal(2, processedBatch1);
        Assert.Equal(2, broker.PublishedMessages.Count);

        // Remaining 3 should still be unprocessed
        int unprocessed = await context.OutboxMessages.CountAsync(m => m.ProcessedOnUtc == null);
        Assert.Equal(3, unprocessed);
    }

    [Fact]
    public async Task OutboxPublisher_WorkerLeasing_PreventsConcurrentDoubleProcessing()
    {
        using var context = CreateInMemoryDbContext(nameof(OutboxPublisher_WorkerLeasing_PreventsConcurrentDoubleProcessing));
        var broker = new InMemoryMessageBroker();
        var orderService = new OrderService(context);

        await orderService.CreateOrderAsync("ORD-LOCK-1", 100.00m);

        // Worker 1 acquires lock on the message
        var msg = await context.OutboxMessages.SingleAsync();
        msg.LockedUntilUtc = DateTime.UtcNow.AddMinutes(5);
        msg.LockId = "worker-1";
        await context.SaveChangesAsync();

        // Worker 2 attempts to process
        var worker2 = new OutboxPublisherWorker(context, broker);
        int processedByWorker2 = await worker2.ProcessOutboxMessagesAsync(batchSize: 10, workerId: "worker-2");

        // Message was leased by worker-1, so worker-2 skips it
        Assert.Equal(0, processedByWorker2);
        Assert.Empty(broker.PublishedMessages);
    }

    [Fact]
    public async Task OutboxPublisher_TransientBrokerFailure_IncrementsRetryAndAppliesBackoff()
    {
        using var context = CreateInMemoryDbContext(nameof(OutboxPublisher_TransientBrokerFailure_IncrementsRetryAndAppliesBackoff));
        var broker = new InMemoryMessageBroker { SimulateFailure = true };
        var orderService = new OrderService(context);
        var publisher = new OutboxPublisherWorker(context, broker);

        await orderService.CreateOrderAsync("ORD-RETRY-1", 25.00m);

        int processed = await publisher.ProcessOutboxMessagesAsync(batchSize: 10);
        Assert.Equal(0, processed);

        var msg = await context.OutboxMessages.SingleAsync();
        Assert.Null(msg.ProcessedOnUtc);
        Assert.Equal(1, msg.RetryCount);
        Assert.NotNull(msg.Error);
        Assert.NotNull(msg.LockedUntilUtc);
        Assert.True(msg.LockedUntilUtc > DateTime.UtcNow);
        Assert.False(msg.IsDeadLettered);
    }

    [Fact]
    public async Task OutboxPublisher_ExceedingMaxRetries_DeadLettersOutboxMessage()
    {
        using var context = CreateInMemoryDbContext(nameof(OutboxPublisher_ExceedingMaxRetries_DeadLettersOutboxMessage));
        var broker = new InMemoryMessageBroker { SimulateFailure = true };
        var publisher = new OutboxPublisherWorker(context, broker) { MaxRetries = 2 };

        var outboxMsg = new OutboxMessage
        {
            EventType = "OrderCreatedEvent",
            Payload = "{}",
            RetryCount = 1 // Already failed once
        };
        context.OutboxMessages.Add(outboxMsg);
        await context.SaveChangesAsync();

        // Second failure reaches MaxRetries (2)
        await publisher.ProcessOutboxMessagesAsync(batchSize: 10);

        var updated = await context.OutboxMessages.SingleAsync();
        Assert.Equal(2, updated.RetryCount);
        Assert.True(updated.IsDeadLettered);
        Assert.Null(updated.LockedUntilUtc);
    }

    [Fact]
    public async Task IdempotentConsumer_ProcessesNewMessage_AndRecordsInInbox()
    {
        using var context = CreateInMemoryDbContext(nameof(IdempotentConsumer_ProcessesNewMessage_AndRecordsInInbox));
        var consumer = new IdempotentConsumer(context);

        var messageId = Guid.NewGuid();
        int executionCount = 0;

        bool handled = await consumer.HandleEventAsync(
            messageId,
            "OrderCreatedEvent",
            () =>
            {
                executionCount++;
                return Task.CompletedTask;
            }
        );

        Assert.True(handled);
        Assert.Equal(1, executionCount);
        Assert.Equal(1, await context.InboxMessages.CountAsync());

        var inbox = await context.InboxMessages.SingleAsync();
        Assert.Equal(messageId, inbox.Id);
        Assert.Equal("OrderCreatedEvent", inbox.EventType);
    }

    [Fact]
    public async Task IdempotentConsumer_IgnoresDuplicateDelivery_GuaranteesExactlyOnceSideEffects()
    {
        using var context = CreateInMemoryDbContext(nameof(IdempotentConsumer_IgnoresDuplicateDelivery_GuaranteesExactlyOnceSideEffects));
        var consumer = new IdempotentConsumer(context);

        var messageId = Guid.NewGuid();
        int executionCount = 0;

        // First delivery
        bool firstResult = await consumer.HandleEventAsync(
            messageId,
            "OrderCreatedEvent",
            () =>
            {
                executionCount++;
                return Task.CompletedTask;
            }
        );

        // Redelivery / duplicate delivery
        bool secondResult = await consumer.HandleEventAsync(
            messageId,
            "OrderCreatedEvent",
            () =>
            {
                executionCount++;
                return Task.CompletedTask;
            }
        );

        Assert.True(firstResult);
        Assert.False(secondResult); // Second should be recognized as duplicate
        Assert.Equal(1, executionCount); // Side-effects executed exactly once!
        Assert.Equal(1, await context.InboxMessages.CountAsync());
    }

    [Fact]
    public void RedundantConnection_LC684_FindsCycleEdgeUsingUnionFind()
    {
        // Example 1: edges = [[1,2],[1,3],[2,3]] -> [2,3]
        var edges1 = new int[][]
        {
            new[] { 1, 2 },
            new[] { 1, 3 },
            new[] { 2, 3 }
        };
        var res1 = RedundantConnectionSolver.FindRedundantConnection(edges1);
        Assert.Equal(new[] { 2, 3 }, res1);

        // Example 2: edges = [[1,2],[2,3],[3,4],[1,4],[1,5]] -> [1,4]
        var edges2 = new int[][]
        {
            new[] { 1, 2 },
            new[] { 2, 3 },
            new[] { 3, 4 },
            new[] { 1, 4 },
            new[] { 1, 5 }
        };
        var res2 = RedundantConnectionSolver.FindRedundantConnection(edges2);
        Assert.Equal(new[] { 1, 4 }, res2);

        // Disjoint Set Union unit check: path compression and union by rank
        var dsu = new DisjointSetUnion(5);
        Assert.True(dsu.Union(1, 2));
        Assert.True(dsu.Union(2, 3));
        Assert.True(dsu.IsConnected(1, 3));
        Assert.False(dsu.Union(1, 3)); // Cycle!
    }
}
