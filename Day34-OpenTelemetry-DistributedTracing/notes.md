# Deep Dive Notes: OpenTelemetry in .NET & Distributed Tracing

## 1. W3C TraceContext & Baggage Standards
Distributed systems rely on standard headers to avoid vendor lock-in across Jaeger, Zipkin, Datadog, and Dynatrace:
- **`traceparent` Header Format**:
  - `version-trace_id-parent_id-trace_flags`
  - Version: `00`
  - Trace ID: 16 bytes (32 hex characters)
  - Parent / Span ID: 8 bytes (16 hex characters)
  - Trace Flags: 8-bit field (e.g. `01` = recorded/sampled)
- **`tracestate` Header**:
  - Comma-separated list of opaque vendor-specific pairs (e.g. `congo=t61rcWkgMzE,rojo=00f067aa0ba902b7`).
- **`baggage` Header**:
  - Key-value pairs propagated across boundaries that do not belong to trace identity itself (e.g., tenant ID, account tier, datacenter location).
  - High caution: baggage is propagated to **all** downstream services, including external ones if not filtered.

## 2. .NET Runtime Architecture: `ActivitySource` vs `Activity`
- `ActivitySource`: Factory that creates and starts `Activity` instances. In modern .NET (since .NET 5/6), this is the official OpenTelemetry API.
- If no `ActivityListener` has registered interest in the `ActivitySource`, `StartActivity()` returns `null`. This makes telemetry overhead strictly zero when tracing is disabled!
- `ActivityKind`:
  - `Server`: Synchronous RPC or HTTP request received.
  - `Client`: Synchronous outbound RPC or HTTP call.
  - `Producer`: Asynchronous message published to queue or topic.
  - `Consumer`: Asynchronous message received from queue or topic.
  - `Internal`: Internal computation or sub-operation.

## 3. LeetCode #621 (Task Scheduler) Derivation
- Let $M$ be the maximum frequency of any task, and $K$ be the number of distinct tasks having frequency $M$.
- Arrange the tasks in a grid of $M - 1$ chunks, each with width $n + 1$.
- In the final chunk, only the $K$ most frequent tasks need to be placed.
- Empty slots in the earlier chunks can be filled by other less frequent tasks without extending the grid.
- If there are more tasks than the grid capacity, no idle time is needed, so total time is simply `tasks.Length`.
- Therefore:
  $$\text{Result} = \max(\text{tasks.Length},\; (M - 1) \times (n + 1) + K)$$
