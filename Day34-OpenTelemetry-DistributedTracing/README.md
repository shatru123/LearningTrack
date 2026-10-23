# Day 34: OpenTelemetry in .NET: Distributed Tracing

## Architectural Overview
Distributed tracing tracks requests as they flow across process, machine, and protocol boundaries (e.g. Reverse Proxy -> API Gateway -> HTTP Microservices -> Asynchronous Message Brokers -> Database Stores).

This module implements native W3C TraceContext propagation and telemetry instrumentation using .NET 8 BCL primitives:
- **`ActivitySource` & `Activity`**: High-performance, zero-allocation diagnostic tracing APIs standard in .NET.
- **W3C TraceContext Specification**: `traceparent` (`00-{trace_id}-{span_id}-{flags}`) and `tracestate` carrier propagation.
- **Baggage Propagation**: Cross-cutting contextual key-value pairs (e.g. `tenant.id`, `user.tier`) passed transparently across network hops.
- **Multi-Hop Trace Hierarchy**:
  - Hop 1: API Gateway (`ActivityKind.Server`)
  - Hop 2: Order Service (`ActivityKind.Server`)
  - Hop 2b: Message Broker Publisher (`ActivityKind.Producer`)
  - Hop 3: Worker Consumer (`ActivityKind.Consumer`)
- **Span Events & Tags**: Semantic telemetry attributes (`messaging.system`, `order.id`, `http.route`) and timeline milestones.

```
Incoming Request
       │
       ▼
┌──────────────┐   traceparent / baggage    ┌──────────────────┐
│  API Gateway │ ─────────────────────────> │   Order Service  │
│(Server Span) │   (W3C HTTP Headers)       │   (Server Span)  │
└──────────────┘                            └─────────┬────────┘
                                                      │
                                    publish event     │ RabbitMQ Producer Span
                                    with traceparent  │
                                                      ▼
                                            ┌──────────────────┐
                                            │ RabbitMQ Broker  │
                                            └─────────┬────────┘
                                                      │
                                                      ▼
                                            ┌──────────────────┐
                                            │ Inventory Worker │
                                            │ (Consumer Span)  │
                                            └──────────────────┘
```

---

## LeetCode #621: Task Scheduler (Medium)
Given a CPU task queue and a cooldown period $n$:
- **Greedy Formula**:
  $$\text{Intervals} = \max(\text{tasks.Length},\; (\text{maxFreq} - 1) \times (n + 1) + \text{maxCount})$$
  - Time Complexity: $O(N)$
  - Space Complexity: $O(1)$ [26 uppercase characters]
- **Max-Heap Simulation**:
  - Uses `PriorityQueue` and a cooldown queue to simulate CPU clock cycles, task execution, and cooldown availability.

---

## Running Tests
```bash
dotnet test Day34-OpenTelemetry-DistributedTracing/tests/Day34.OpenTelemetryTracing.Tests/Day34.OpenTelemetryTracing.Tests.csproj
```
10 unit tests validate W3C parsing, baggage propagation across HTTP and RabbitMQ boundaries, parent-child span trees, and task scheduling math and simulation.
