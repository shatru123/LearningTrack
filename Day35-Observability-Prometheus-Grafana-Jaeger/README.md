# Day 35: Observability Stack: Prometheus, Grafana & Jaeger

## Architectural Overview
A resilient microservice architecture requires the Three Pillars of Observability: **Metrics**, **Traces**, and **Logs**, unified under OpenTelemetry.

This module implements production metrics instrumentation in .NET 8 using `System.Diagnostics.Metrics`:
- **RED Method (Service-Oriented Architecture)**:
  - **Rate**: `http.server.requests.count` (throughput in req/sec).
  - **Errors**: `http.server.requests.errors` (HTTP 5xx failures / error rate).
  - **Duration**: `http.server.request.duration.ms` (latency histogram).
- **USE Method (Infrastructure & Host Resources)**:
  - **Utilization**: `system.cpu.utilization` (CPU active percentage).
  - **Saturation**: `threadpool.queue.saturation` (ThreadPool backlog depth).
  - **Errors**: `system.hardware.errors` (device / network packet drop counts).
- **Statistical Percentiles**: High-precision calculation of P50 (median), P90, P95, and P99 latencies using nearest-rank percentile interpolation over histogram observations.
- **Prometheus Buckets**: Cumulative upper-bound histogram representation for PromQL `histogram_quantile(0.99, rate(http_duration_bucket[5m]))`.
- **Three-Way Correlation**: Injecting active `trace_id` and `span_id` directly into structured JSON logs for instant drilldown from Grafana metric alerts to Jaeger traces and Loki logs.
- **Full Observability Stack**: Complete `docker-compose.observability.yml` running OpenTelemetry Collector, Jaeger, Prometheus, and Grafana.

```
       .NET Application (System.Diagnostics.Metrics & Activity)
                                │
                 OTLP (gRPC 4317 / HTTP 4318)
                                ▼
                 ┌─────────────────────────────┐
                 │    OpenTelemetry Collector  │
                 └──────────────┬──────────────┘
                                │
        ┌───────────────────────┼────────────────────────┐
        │ Metrics (port 8889)   │ Traces (gRPC 14250)    │ Logs
        ▼                       ▼                        ▼
┌──────────────┐        ┌──────────────┐         ┌──────────────┐
│  Prometheus  │        │    Jaeger    │         │     Loki     │
│ (Time Series)│        │ (Trace Spans)│         │ (Aggregated) │
└───────┬──────┘        └───────┬──────┘         └───────┬──────┘
        │                       │                        │
        └───────────────────────┼────────────────────────┘
                                ▼
                     ┌─────────────────────┐
                     │  Grafana Dashboard  │
                     │  (Correlated Views) │
                     └─────────────────────┘
```

---

## LeetCode #355: Design Twitter (Medium)
Design a news feed system supporting `PostTweet`, `GetNewsFeed`, `Follow`, and `Unfollow`:
- **Algorithm**: $K$-way merge over user tweet streams using a Priority Queue (Max-Heap by timestamp).
- **Complexity**: $O(F \log F + 10 \log F)$ time to fetch top 10 tweets where $F$ is follower count; $O(1)$ post tweet; $O(F)$ space.
- **System Design Trade-off**: Fan-out on Read (Pull model) vs Fan-out on Write (Push model with Redis inbox for celebrities).

---

## Running Tests
```bash
dotnet test Day35-Observability-Prometheus-Grafana-Jaeger/tests/Day35.ObservabilityStack.Tests/Day35.ObservabilityStack.Tests.csproj
```
10 unit tests validate RED/USE metrics counters, P50/P90/P95/P99 latency calculations, Prometheus histogram buckets, structured logging correlation, and Twitter feed mechanics.
