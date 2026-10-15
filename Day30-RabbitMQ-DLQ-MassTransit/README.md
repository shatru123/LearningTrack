# Day 30: RabbitMQ Dead-Letter Queues & MassTransit

## Architectural Overview

In high-throughput microservices architectures, unhandled message processing failures, transient downstream outages, and poison messages (malformed or unprocessable payloads) pose catastrophic risks to system availability. Without robust dead-lettering and progressive retry policies, failed messages either trigger infinite retry storms that starve healthy messages (Head-of-Line blocking) or are dropped silently resulting in data loss.

This module provides a production-grade implementation of **AMQP Dead-Letter Exchanges (DLX)**, **TTL-based Exponential Delayed Retry Queues**, and **MassTransit Saga State Machine Orchestration** in .NET 8.

---

## Key Architectural Concepts

### 1. AMQP Dead-Letter Exchange (DLX) Mechanics
A queue declares dead-letter routing via standard AMQP 0-9-1 arguments:
- `x-dead-letter-exchange`: The exchange that receives dead-lettered messages.
- `x-dead-letter-routing-key`: Optional replacement routing key for dead-lettered routing.
- `x-message-ttl`: Per-message or per-queue time-to-live in milliseconds.
- `x-max-length`: Maximum message capacity; overflowing messages are diverted to DLX instead of dropped.

A message is dead-lettered under three specific conditions:
1. **Explicit Rejection**: Consumer issues `basic.reject` or `basic.nack` with `requeue = false`.
2. **TTL Expiration**: Message resides in a queue past its `x-message-ttl` expiration without being consumed.
3. **Queue Max-Length Overflow**: Queue exceeds its declared capacity limit.

### 2. Progressive Delayed Retry Topology (Zero-Consumer DLX Loop)
Instead of blocking worker threads with `Task.Delay()` inside consumers:
1. On failure (attempt $N < \text{Max}$), the consumer rejects the message into a delayed queue (`orders.retry.5s`).
2. The delay queue has NO consumers attached and is configured with `x-message-ttl: 5000` and `x-dead-letter-exchange: dlx.direct`, `x-dead-letter-routing-key: orders.work`.
3. When the TTL expires, the broker automatically re-routes the message back into the active processing queue (`orders.work`).
4. If failures persist beyond threshold ($N \ge \text{Max}$), the message routes directly to the poison DLQ (`orders.dlq`) with full `x-death` diagnosis headers.

```
[Publisher] ---> [orders.work] ---> [Consumer] (Fails attempt 1)
                     ^                    |
                     | (TTL expires)      v (Reject to DLX)
              [orders.retry.5s] <--- [dlx.direct]
                     (No consumers)
                                          | (Fails attempt 3)
                                          v
                                    [orders.dlq] (Poison Triage)
```

### 3. MassTransit State Machine Saga Coordination
Distributed transactions spanning multiple bounded contexts (`Order`, `Inventory`, `Payment`) require compensating choreographies or orchestrations. `MassTransitStateMachine<TInstance>` correlates events by `CorrelationId` and provides:
- State transitions: `Initial` $\to$ `Submitted` $\to$ `InventoryReserved` $\to$ `Completed`.
- Fault handling: Any unexpected fault routes to `Faulted` state triggering compensating commands.

---

## Data Structures & Algorithms: Topological Sorting

### LeetCode #207: Course Schedule (Medium)
- **Problem**: Given `numCourses` and directed prerequisite pairs `[a, b]` ($b \to a$), determine if it is possible to finish all courses.
- **Kahn's Algorithm (BFS)**:
  - Calculate in-degrees for all $V$ courses.
  - Enqueue all courses with in-degree 0.
  - Dequeue node $u$, decrement neighbors' in-degrees, enqueue when in-degree hits 0.
  - If processed count equals $numCourses$, graph is a DAG (no cycles).
- **Complexity**: $\mathcal{O}(V + E)$ time, $\mathcal{O}(V + E)$ space.

### LeetCode #210: Course Schedule II (Medium)
- **Problem**: Return the ordering of courses you should take to finish all courses. If impossible due to cycles, return empty array.
- **Algorithm**: Kahn's BFS records each dequeued node into the output array. If output size is $numCourses$, the valid topological order is returned; otherwise, an empty array is returned.

---

## Test Suite Summary

All 10 unit tests pass with 100% coverage:
1. `Dlq_WhenMessageRejected_RoutesToDeadLetterQueue`
2. `Dlq_CapturesXDeathMetadata_WithReasonAndExceptionDetails`
3. `Dlq_TtlExpiration_MovesMessageToDeadLetterQueue`
4. `Dlq_QueueMaxLengthExceeded_DeadLettersOverflowMessage`
5. `ProgressiveRetry_TtlDelayedRetryPipeline_RequeuesBackToMainQueue`
6. `MassTransit_OrderStateMachine_TransitionsToSubmittedInitially`
7. `MassTransit_OrderStateMachine_TransitionsToCompletedOnPayment`
8. `MassTransit_OrderStateMachine_TransitionsToFaultedOnOrderFault`
9. `CourseSchedule_LC207_DetectsValidScheduleAndCycles`
10. `CourseScheduleII_LC210_ProducesValidTopologicalOrdering`
