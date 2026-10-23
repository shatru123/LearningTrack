using MassTransit;

namespace Day32.SagaOrchestration;

#region Saga Events & Commands

public record OrderSubmitted(Guid CorrelationId, string CustomerId, decimal Amount, List<string> Items);
public record InventoryReserved(Guid CorrelationId, DateTime ReservedAt);
public record PaymentAuthorized(Guid CorrelationId, string TransactionId, DateTime AuthorizedAt);
public record PaymentFailed(Guid CorrelationId, string Reason);
public record InventoryReleased(Guid CorrelationId, DateTime ReleasedAt);
public record OrderShipped(Guid CorrelationId, string TrackingNumber, DateTime ShippedAt);

// Commands dispatched by the Orchestrator
public record ReserveInventoryCommand(Guid CorrelationId, List<string> Items);
public record ProcessPaymentCommand(Guid CorrelationId, decimal Amount);
public record ReleaseInventoryCommand(Guid CorrelationId, string Reason);
public record ShipOrderCommand(Guid CorrelationId);

#endregion

#region Saga State Instance

public class OrderFulfillmentState : SagaStateMachineInstance
{
    public Guid CorrelationId { get; set; }
    public string CurrentState { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public List<string> Items { get; set; } = new();
    public DateTime? SubmittedAt { get; set; }
    public DateTime? InventoryReservedAt { get; set; }
    public DateTime? PaymentAuthorizedAt { get; set; }
    public string? PaymentTransactionId { get; set; }
    public string? FailureReason { get; set; }
    public bool InventoryCompensationTriggered { get; set; }
    public DateTime? InventoryReleasedAt { get; set; }
    public string? TrackingNumber { get; set; }
    public List<string> AuditTrail { get; set; } = new();
}

#endregion

#region Saga State Machine Implementation

public class OrderFulfillmentStateMachine : MassTransitStateMachine<OrderFulfillmentState>
{
    public State Submitted { get; private set; } = null!;
    public State InventoryReservedState { get; private set; } = null!;
    public State PaymentAuthorizedState { get; private set; } = null!;
    public State CompensatingInventoryState { get; private set; } = null!;
    public State Completed { get; private set; } = null!;
    public State Faulted { get; private set; } = null!;

    public Event<OrderSubmitted> OrderSubmittedEvent { get; private set; } = null!;
    public Event<InventoryReserved> InventoryReservedEvent { get; private set; } = null!;
    public Event<PaymentAuthorized> PaymentAuthorizedEvent { get; private set; } = null!;
    public Event<PaymentFailed> PaymentFailedEvent { get; private set; } = null!;
    public Event<InventoryReleased> InventoryReleasedEvent { get; private set; } = null!;
    public Event<OrderShipped> OrderShippedEvent { get; private set; } = null!;

    public OrderFulfillmentStateMachine()
    {
        InstanceState(x => x.CurrentState);

        Event(() => OrderSubmittedEvent, x => x.CorrelateById(m => m.Message.CorrelationId));
        Event(() => InventoryReservedEvent, x => x.CorrelateById(m => m.Message.CorrelationId));
        Event(() => PaymentAuthorizedEvent, x => x.CorrelateById(m => m.Message.CorrelationId));
        Event(() => PaymentFailedEvent, x => x.CorrelateById(m => m.Message.CorrelationId));
        Event(() => InventoryReleasedEvent, x => x.CorrelateById(m => m.Message.CorrelationId));
        Event(() => OrderShippedEvent, x => x.CorrelateById(m => m.Message.CorrelationId));

        Initially(
            When(OrderSubmittedEvent)
                .Then(context =>
                {
                    context.Saga.CustomerId = context.Message.CustomerId;
                    context.Saga.TotalAmount = context.Message.Amount;
                    context.Saga.Items = context.Message.Items;
                    context.Saga.SubmittedAt = DateTime.UtcNow;
                    context.Saga.AuditTrail.Add($"Order submitted for customer {context.Message.CustomerId}");
                })
                .TransitionTo(Submitted)
        );

        During(Submitted,
            When(InventoryReservedEvent)
                .Then(context =>
                {
                    context.Saga.InventoryReservedAt = context.Message.ReservedAt;
                    context.Saga.AuditTrail.Add("Inventory successfully reserved");
                })
                .TransitionTo(InventoryReservedState)
        );

        During(InventoryReservedState,
            When(PaymentAuthorizedEvent)
                .Then(context =>
                {
                    context.Saga.PaymentAuthorizedAt = context.Message.AuthorizedAt;
                    context.Saga.PaymentTransactionId = context.Message.TransactionId;
                    context.Saga.AuditTrail.Add($"Payment authorized with txn {context.Message.TransactionId}");
                })
                .TransitionTo(PaymentAuthorizedState),
            When(PaymentFailedEvent)
                .Then(context =>
                {
                    context.Saga.FailureReason = context.Message.Reason;
                    context.Saga.InventoryCompensationTriggered = true;
                    context.Saga.AuditTrail.Add($"Payment failed: {context.Message.Reason}. Initiating inventory compensation rollback.");
                })
                .TransitionTo(CompensatingInventoryState)
        );

        During(PaymentAuthorizedState,
            When(OrderShippedEvent)
                .Then(context =>
                {
                    context.Saga.TrackingNumber = context.Message.TrackingNumber;
                    context.Saga.AuditTrail.Add($"Order shipped with tracking {context.Message.TrackingNumber}");
                })
                .TransitionTo(Completed)
        );

        During(CompensatingInventoryState,
            When(InventoryReleasedEvent)
                .Then(context =>
                {
                    context.Saga.InventoryReleasedAt = context.Message.ReleasedAt;
                    context.Saga.AuditTrail.Add("Inventory release confirmed. Order marked as Faulted.");
                })
                .TransitionTo(Faulted)
        );
    }
}

#endregion

#region Architecture Comparison Metrics

public static class SagaArchitectureComparator
{
    public static (int CouplingScore, bool CentralVisibility, bool RollbackCoordination) EvaluateOrchestration()
    {
        // Orchestration: Central state machine, high visibility, coordinated compensating actions
        return (CouplingScore: 2, CentralVisibility: true, RollbackCoordination: true);
    }

    public static (int CouplingScore, bool CentralVisibility, bool RollbackCoordination) EvaluateChoreography()
    {
        // Choreography: Point-to-point event reaction, difficult global tracing, complex multi-step rollbacks
        return (CouplingScore: 7, CentralVisibility: false, RollbackCoordination: false);
    }
}

#endregion
