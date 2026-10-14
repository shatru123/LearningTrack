# Day 29: RabbitMQ Core Concepts: Exchanges & Queues

## Overview
**Day 29** explores enterprise asynchronous messaging fundamentals with **RabbitMQ** and the **AMQP 0-9-1** protocol. We implement and simulate all four AMQP exchange types (`Direct`, `Topic` with wildcard pattern matching, `Fanout` broadcast, and `Headers` multi-attribute matching), reliable delivery mechanisms (Publisher Confirms, manual consumer acknowledgments via `BasicAck`/`BasicNack`, and Dead Letter Exchanges / DLX), and Quality of Service (QoS) `PrefetchCount` backpressure controls. For DSA, we solve **LeetCode #130 (*Surrounded Regions*)** using reverse boundary DFS and **LeetCode #994 (*Rotting Oranges*)** using multi-source BFS contagion modeling.

---

## Architectural Topics Covered

1. **AMQP 0-9-1 Exchange Architectures**:
   - **Direct Exchange**: Strict 1:1 routing key equality (`order.created`).
   - **Topic Exchange**: Wildcard pattern routing with `*` (matching exactly one word) and `#` (matching zero or more words).
   - **Fanout Exchange**: High-speed broadcast forwarding to all bound queues, completely bypassing routing keys.
   - **Headers Exchange**: Multi-attribute header dictionary matching with `x-match: all` or `x-match: any`.
2. **Reliable Messaging & Failure Handling**:
   - **Publisher Confirms**: Asynchronous ACKs/NACKs returned from the broker guaranteeing disk persistence and queue delivery.
   - **Consumer Acknowledgments**: Manual `BasicAck`, `BasicNack(requeue: true)` (pushing redelivered message back to head of queue), and `BasicNack(requeue: false)` (discarding or forwarding).
   - **Dead Letter Exchange (DLX)**: Automated dead-lettering of rejected, expired, or unprocessable messages into dedicated dead-letter queues (`x-dead-letter-exchange`).
3. **Consumer QoS & Fair Dispatch**:
   - `basic.qos(prefetch_count)` limiting in-flight unacknowledged messages to prevent worker buffer bloat and ensure balanced distribution across heterogeneous consumers.
4. **Graph & Grid Traversal Algorithms**:
   - **LeetCode #130: Surrounded Regions (Medium)**: Reverse boundary DFS preserving border-connected `'O'` cells while flipping enclosed regions to `'X'`.
   - **LeetCode #994: Rotting Oranges (Medium)**: Multi-source BFS simulating minute-by-minute infection propagation, returning minimum elapsed minutes or -1.

---

## Project Structure

```
Day29-RabbitMQ-Core-Exchanges-Queues/
├── src/
│   └── Day29.RabbitMQCoreExchanges/
│       ├── Day29.RabbitMQCoreExchanges.csproj
│       ├── AmqpBrokerSimulator.cs
│       └── SurroundedRegionsAndOrangesSolver.cs
├── tests/
│   └── Day29.RabbitMQCoreExchanges.Tests/
│       ├── Day29.RabbitMQCoreExchanges.Tests.csproj
│       └── Day29Tests.cs
├── README.md
├── notes.md
└── interview-questions.md
```

---

## Running the Unit Tests

```bash
dotnet test Day29-RabbitMQ-Core-Exchanges-Queues/tests/Day29.RabbitMQCoreExchanges.Tests/Day29.RabbitMQCoreExchanges.Tests.csproj
```

### Test Suite Coverage
1. `DirectExchange_RoutesToExactMatchingQueue`: Validates exact routing key delivery to targeted queues without cross-talk.
2. `FanoutExchange_BroadcastsToAllBoundQueues`: Validates unconditional broadcast across all bound queues.
3. `TopicExchange_RoutesWithWildcards_StarAndHash`: Validates wildcard matching (`orders.*` vs `#.critical`).
4. `HeadersExchange_RoutesMatchingHeaders_AllAndAny`: Validates `x-match: all` and `x-match: any` header evaluation.
5. `PublisherConfirms_ReturnsAckOnRoutedMessage_AndNackOnMandatoryUnrouted`: Verifies publisher confirm ACK on successful queue routing and NACK on mandatory unrouted messages.
6. `ConsumerAck_RemovesMessageFromUnackedQueue`: Verifies manual BasicAck clears unacknowledged tracking.
7. `ConsumerNack_WithRequeue_PushesBackToHeadOfQueue`: Validates BasicNack with `requeue = true` flag and `Redelivered` state.
8. `DeadLetterExchange_RoutesRejectedMessageToDlx`: Verifies un-requeued rejected messages route automatically to DLX.
9. `SurroundedRegions_CapturesEnclosedO_AndPreservesBorderConnected`: Validates LC #130 matrix region capture.
10. `RottingOranges_MultiSourceBfs_CalculatesMinimumMinutesOrNegativeOne`: Validates LC #994 multi-source BFS contagion, edge cases, and unreachable cells.
