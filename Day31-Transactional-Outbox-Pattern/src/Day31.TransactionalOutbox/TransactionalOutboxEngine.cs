using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace Day31.TransactionalOutbox;

#region Domain & Outbox Entities

public sealed class Order
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string OrderNumber { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = "Created";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class OutboxMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string EventType { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public DateTime OccurredOnUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ProcessedOnUtc { get; set; }
    public string? Error { get; set; }
    public int RetryCount { get; set; }
    public DateTime? LockedUntilUtc { get; set; }
    public string? LockId { get; set; }
    public bool IsDeadLettered { get; set; }
}

public sealed class InboxMessage
{
    public Guid Id { get; set; }
    public string EventType { get; set; } = string.Empty;
    public DateTime ProcessedAtUtc { get; set; } = DateTime.UtcNow;
    public string HandlerName { get; set; } = string.Empty;
}

#endregion

#region Database Context

public class OutboxDbContext : DbContext
{
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

    public OutboxDbContext(DbContextOptions<OutboxDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Order>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(x => x.OrderNumber).IsRequired().HasMaxLength(64);
            b.Property(x => x.TotalAmount).HasPrecision(18, 2);
        });

        modelBuilder.Entity<OutboxMessage>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(x => x.EventType).IsRequired().HasMaxLength(128);
            b.Property(x => x.Payload).IsRequired();
            b.HasIndex(x => new { x.ProcessedOnUtc, x.IsDeadLettered, x.LockedUntilUtc });
        });

        modelBuilder.Entity<InboxMessage>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(x => x.EventType).IsRequired().HasMaxLength(128);
        });
    }
}

#endregion

#region Message Broker Abstraction

public interface IMessageBroker
{
    Task PublishAsync(string eventType, string payload, CancellationToken cancellationToken = default);
}

public class InMemoryMessageBroker : IMessageBroker
{
    public List<(string EventType, string Payload)> PublishedMessages { get; } = new();
    public bool SimulateFailure { get; set; }
    public int FailureCount { get; set; }

    public Task PublishAsync(string eventType, string payload, CancellationToken cancellationToken = default)
    {
        if (SimulateFailure)
        {
            FailureCount++;
            throw new HttpRequestException("Simulated broker transport failure");
        }

        PublishedMessages.Add((eventType, payload));
        return Task.CompletedTask;
    }
}

#endregion

#region Services: Order Service, Outbox Publisher & Idempotent Consumer

public class OrderService
{
    private readonly OutboxDbContext _dbContext;

    public OrderService(OutboxDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// Atomically persists an Order and its corresponding domain event to the Outbox table.
    /// Demonstrates dual-write prevention within a single transactional unit of work.
    /// </summary>
    public async Task<Order> CreateOrderAsync(string orderNumber, decimal totalAmount, bool simulateFailureAfterDbSave = false)
    {
        var order = new Order
        {
            OrderNumber = orderNumber,
            TotalAmount = totalAmount,
            Status = "Created"
        };

        var domainEvent = new
        {
            OrderId = order.Id,
            OrderNumber = order.OrderNumber,
            Amount = order.TotalAmount,
            CreatedAt = order.CreatedAtUtc
        };

        var outboxMessage = new OutboxMessage
        {
            EventType = "OrderCreatedEvent",
            Payload = JsonSerializer.Serialize(domainEvent),
            OccurredOnUtc = DateTime.UtcNow
        };

        _dbContext.Orders.Add(order);
        _dbContext.OutboxMessages.Add(outboxMessage);

        if (simulateFailureAfterDbSave)
        {
            // Throw before saving or simulate rollback
            throw new InvalidOperationException("Simulated failure during order unit-of-work");
        }

        await _dbContext.SaveChangesAsync();
        return order;
    }
}

public class OutboxPublisherWorker
{
    private readonly OutboxDbContext _dbContext;
    private readonly IMessageBroker _messageBroker;

    public int MaxRetries { get; set; } = 3;

    public OutboxPublisherWorker(OutboxDbContext dbContext, IMessageBroker messageBroker)
    {
        _dbContext = dbContext;
        _messageBroker = messageBroker;
    }

    /// <summary>
    /// Polls pending outbox messages, leases rows, publishes to message broker, and marks processed.
    /// </summary>
    public async Task<int> ProcessOutboxMessagesAsync(int batchSize = 10, string? workerId = null, CancellationToken cancellationToken = default)
    {
        workerId ??= Guid.NewGuid().ToString("N");
        var now = DateTime.UtcNow;

        // 1. Fetch unleased, unprocessed, non-dead-lettered messages
        var pending = await _dbContext.OutboxMessages
            .Where(m => m.ProcessedOnUtc == null &&
                        !m.IsDeadLettered &&
                        (m.LockedUntilUtc == null || m.LockedUntilUtc < now))
            .OrderBy(m => m.OccurredOnUtc)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        if (pending.Count == 0) return 0;

        // 2. Acquire lease (optimistic concurrency / lock window)
        var leaseDuration = TimeSpan.FromSeconds(30);
        foreach (var msg in pending)
        {
            msg.LockedUntilUtc = now.Add(leaseDuration);
            msg.LockId = workerId;
        }
        await _dbContext.SaveChangesAsync(cancellationToken);

        int processedCount = 0;

        // 3. Dispatch each leased message to the broker
        foreach (var msg in pending)
        {
            try
            {
                await _messageBroker.PublishAsync(msg.EventType, msg.Payload, cancellationToken);

                // Publication confirmed -> mark processed and clear lock
                msg.ProcessedOnUtc = DateTime.UtcNow;
                msg.LockedUntilUtc = null;
                msg.LockId = null;
                msg.Error = null;
                processedCount++;
            }
            catch (Exception ex)
            {
                msg.RetryCount++;
                msg.Error = ex.Message;

                if (msg.RetryCount >= MaxRetries)
                {
                    // Move to dead-letter state
                    msg.IsDeadLettered = true;
                    msg.LockedUntilUtc = null;
                    msg.LockId = null;
                }
                else
                {
                    // Exponential backoff lease delay: 2^retry seconds
                    var backoffDelay = TimeSpan.FromSeconds(Math.Pow(2, msg.RetryCount));
                    msg.LockedUntilUtc = DateTime.UtcNow.Add(backoffDelay);
                    msg.LockId = null;
                }
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return processedCount;
    }
}

public class IdempotentConsumer
{
    private readonly OutboxDbContext _dbContext;

    public IdempotentConsumer(OutboxDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// Processes an incoming event with Inbox deduplication to guarantee exactly-once side-effects.
    /// </summary>
    public async Task<bool> HandleEventAsync(Guid messageId, string eventType, Func<Task> handleAction, string handlerName = "OrderConsumer")
    {
        // 1. Check if already processed
        var alreadyProcessed = await _dbContext.InboxMessages.AnyAsync(i => i.Id == messageId);
        if (alreadyProcessed)
        {
            // Duplicate detected: acknowledge without re-executing side-effects
            return false;
        }

        // 2. Execute business side effect
        await handleAction();

        // 3. Atomically record message in Inbox
        _dbContext.InboxMessages.Add(new InboxMessage
        {
            Id = messageId,
            EventType = eventType,
            ProcessedAtUtc = DateTime.UtcNow,
            HandlerName = handlerName
        });

        await _dbContext.SaveChangesAsync();
        return true;
    }
}

#endregion
