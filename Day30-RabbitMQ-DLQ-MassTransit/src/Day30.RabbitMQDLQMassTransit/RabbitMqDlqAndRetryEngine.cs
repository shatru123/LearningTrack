using System.Collections.Concurrent;
using MassTransit;

namespace Day30.RabbitMQDLQMassTransit;

#region AMQP DLQ & Retry Topology Models

public enum DeadLetterReason
{
    Rejected,
    Expired,
    MaxLengthExceeded
}

public sealed record XDeathEntry(
    string Queue,
    DeadLetterReason Reason,
    int Count,
    string Exchange,
    DateTime Timestamp,
    string? ExceptionMessage,
    string? ExceptionType
);

public sealed record AmqpMessage(
    string MessageId,
    string RoutingKey,
    string Payload,
    int RetryCount = 0,
    int? TtlMilliseconds = null,
    List<XDeathEntry>? DeathHistory = null
)
{
    public List<XDeathEntry> DeathHistory { get; init; } = DeathHistory ?? new List<XDeathEntry>();
}

public sealed record QueueArguments(
    string? DeadLetterExchange = null,
    string? DeadLetterRoutingKey = null,
    int? MessageTtlMs = null,
    int? MaxLength = null
);

#endregion

#region DLQ & Progressive Retry Simulator Engine

public class DlqRetrySimulator
{
    private readonly ConcurrentDictionary<string, QueueArguments> _queueConfigs = new();
    private readonly ConcurrentDictionary<string, List<AmqpMessage>> _queues = new();
    private readonly ConcurrentDictionary<string, Action<AmqpMessage>> _consumers = new();
    private readonly object _lock = new();

    public int MaxRetries { get; set; } = 3;

    public void DeclareQueue(string queueName, QueueArguments? arguments = null)
    {
        _queueConfigs[queueName] = arguments ?? new QueueArguments();
        _queues.GetOrAdd(queueName, _ => new List<AmqpMessage>());
    }

    public void RegisterConsumer(string queueName, Action<AmqpMessage> consumer)
    {
        _consumers[queueName] = consumer;
    }

    public void Publish(string queueName, AmqpMessage message)
    {
        lock (_lock)
        {
            if (!_queues.ContainsKey(queueName))
            {
                DeclareQueue(queueName);
            }

            var queue = _queues[queueName];
            var config = _queueConfigs.GetValueOrDefault(queueName, new QueueArguments());

            // Check MaxLength dead-lettering
            if (config.MaxLength.HasValue && queue.Count >= config.MaxLength.Value)
            {
                if (!string.IsNullOrEmpty(config.DeadLetterExchange))
                {
                    var targetQueue = config.DeadLetterRoutingKey ?? config.DeadLetterExchange;
                    DeadLetter(message, queueName, targetQueue, DeadLetterReason.MaxLengthExceeded, "Queue reached max-length capacity");
                    return;
                }
            }

            queue.Add(message);
        }
    }

    public void Reject(string currentQueue, AmqpMessage message, Exception reason, bool requeue = false)
    {
        lock (_lock)
        {
            if (_queues.TryGetValue(currentQueue, out var list))
            {
                list.Remove(message);
            }

            if (requeue)
            {
                Publish(currentQueue, message);
                return;
            }

            var config = _queueConfigs.GetValueOrDefault(currentQueue, new QueueArguments());
            if (string.IsNullOrEmpty(config.DeadLetterExchange))
            {
                throw new InvalidOperationException($"Queue '{currentQueue}' has no dead-letter exchange configured.");
            }

            var nextRetry = message.RetryCount + 1;
            var targetQueue = config.DeadLetterRoutingKey ?? config.DeadLetterExchange;

            var updatedMessage = message with
            {
                RetryCount = nextRetry
            };

            DeadLetter(updatedMessage, currentQueue, targetQueue, DeadLetterReason.Rejected, reason.Message, reason.GetType().Name);
        }
    }

    public void ExpireTtl(string delayQueueName, string messageId)
    {
        lock (_lock)
        {
            if (!_queues.TryGetValue(delayQueueName, out var list)) return;
            var msg = list.FirstOrDefault(m => m.MessageId == messageId);
            if (msg == null) return;

            list.Remove(msg);
            var config = _queueConfigs.GetValueOrDefault(delayQueueName, new QueueArguments());
            if (!string.IsNullOrEmpty(config.DeadLetterExchange))
            {
                var targetQueue = config.DeadLetterRoutingKey ?? config.DeadLetterExchange;
                DeadLetter(msg, delayQueueName, targetQueue, DeadLetterReason.Expired, "Message TTL expired in delayed retry queue");
            }
        }
    }

    private void DeadLetter(
        AmqpMessage message,
        string sourceQueue,
        string targetQueue,
        DeadLetterReason reason,
        string? details = null,
        string? exceptionType = null)
    {
        var deathEntry = new XDeathEntry(
            Queue: sourceQueue,
            Reason: reason,
            Count: message.RetryCount,
            Exchange: "dlx.direct",
            Timestamp: DateTime.UtcNow,
            ExceptionMessage: details,
            ExceptionType: exceptionType
        );

        var updatedHistory = new List<XDeathEntry>(message.DeathHistory) { deathEntry };
        var deadLetteredMsg = message with { DeathHistory = updatedHistory };

        Publish(targetQueue, deadLetteredMsg);
    }

    public IReadOnlyList<AmqpMessage> GetQueueMessages(string queueName)
    {
        lock (_lock)
        {
            return _queues.TryGetValue(queueName, out var list) ? list.ToList() : Array.Empty<AmqpMessage>();
        }
    }

    public void DispatchNext(string queueName)
    {
        AmqpMessage? msg = null;
        Action<AmqpMessage>? consumer = null;

        lock (_lock)
        {
            if (_queues.TryGetValue(queueName, out var list) && list.Count > 0)
            {
                msg = list[0];
            }
            _consumers.TryGetValue(queueName, out consumer);
        }

        if (msg != null && consumer != null)
        {
            consumer(msg);
        }
    }
}

#endregion

#region MassTransit State Machine Saga Implementation

public record OrderSubmittedEvent(Guid CorrelationId, string OrderNumber, decimal TotalAmount);
public record InventoryReservedEvent(Guid CorrelationId, DateTime ReservedAt);
public record PaymentCompletedEvent(Guid CorrelationId, string TransactionId, DateTime CompletedAt);
public record OrderFaultedEvent(Guid CorrelationId, string Reason);

public class OrderStateInstance : SagaStateMachineInstance
{
    public Guid CorrelationId { get; set; }
    public string CurrentState { get; set; } = string.Empty;
    public string OrderNumber { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public DateTime? InventoryReservedAt { get; set; }
    public DateTime? PaymentCompletedAt { get; set; }
    public string? PaymentTransactionId { get; set; }
    public string? FaultReason { get; set; }
}

public class OrderStateMachine : MassTransitStateMachine<OrderStateInstance>
{
    public State Submitted { get; private set; } = null!;
    public State InventoryReserved { get; private set; } = null!;
    public State Completed { get; private set; } = null!;
    public State Faulted { get; private set; } = null!;

    public Event<OrderSubmittedEvent> OrderSubmitted { get; private set; } = null!;
    public Event<InventoryReservedEvent> InventoryReservedEvent { get; private set; } = null!;
    public Event<PaymentCompletedEvent> PaymentCompleted { get; private set; } = null!;
    public Event<OrderFaultedEvent> OrderFaulted { get; private set; } = null!;

    public OrderStateMachine()
    {
        InstanceState(x => x.CurrentState);

        Event(() => OrderSubmitted, x => x.CorrelateById(context => context.Message.CorrelationId));
        Event(() => InventoryReservedEvent, x => x.CorrelateById(context => context.Message.CorrelationId));
        Event(() => PaymentCompleted, x => x.CorrelateById(context => context.Message.CorrelationId));
        Event(() => OrderFaulted, x => x.CorrelateById(context => context.Message.CorrelationId));

        Initially(
            When(OrderSubmitted)
                .Then(context =>
                {
                    context.Saga.OrderNumber = context.Message.OrderNumber;
                    context.Saga.TotalAmount = context.Message.TotalAmount;
                    context.Saga.SubmittedAt = DateTime.UtcNow;
                })
                .TransitionTo(Submitted)
        );

        During(Submitted,
            When(InventoryReservedEvent)
                .Then(context =>
                {
                    context.Saga.InventoryReservedAt = context.Message.ReservedAt;
                })
                .TransitionTo(InventoryReserved),
            When(OrderFaulted)
                .Then(context =>
                {
                    context.Saga.FaultReason = context.Message.Reason;
                })
                .TransitionTo(Faulted)
        );

        During(InventoryReserved,
            When(PaymentCompleted)
                .Then(context =>
                {
                    context.Saga.PaymentCompletedAt = context.Message.CompletedAt;
                    context.Saga.PaymentTransactionId = context.Message.TransactionId;
                })
                .TransitionTo(Completed),
            When(OrderFaulted)
                .Then(context =>
                {
                    context.Saga.FaultReason = context.Message.Reason;
                })
                .TransitionTo(Faulted)
        );

        SetCompletedWhenFinalized();
    }
}

#endregion
