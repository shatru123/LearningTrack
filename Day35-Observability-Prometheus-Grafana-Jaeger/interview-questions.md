# Day 35: Senior & Staff Interview Questions & Answers

### Q1: Why are averages (means) considered an antipattern for monitoring service latencies, and why are percentiles (P95, P99) preferred?
**Answer**:
Averages conceal outliers in skewed distributions. A microservice handling 10,000 req/sec might have an average latency of 25ms, while the 99th percentile is 3,500ms. In modern web architectures where an incoming user request fan-outs to 50 downstream microservices:
$$\text{Probability of hitting a P99 dependency} = 1 - (0.99)^{50} \approx 39.5\%$$
Nearly 40% of all user requests will experience a P99 tail latency! Relying on percentiles like P90, P95, and P99 guarantees SLA observability for the users experiencing degraded performance.

---

### Q2: What is the mathematical and architectural difference between RED and USE metrics?
**Answer**:
- **RED (Rate, Errors, Duration)**: Formulated for request-driven software services (HTTP APIs, gRPC, microservices). It measures what the *user/consumer* experiences. Rate = load; Errors = availability; Duration = responsiveness.
- **USE (Utilization, Saturation, Errors)**: Formulated for hardware and infrastructure resources (CPU, RAM, disk I/O, network sockets, thread pools). Utilization = percent busy time; Saturation = queued backlog waiting to be serviced; Errors = low-level driver or device faults.
- *Rule of thumb*: When investigating an alert, look at RED metrics to identify *which service* is degraded, then look at USE metrics to identify *which resource bottleneck* is causing it.

---

### Q3: How does Prometheus calculate `histogram_quantile(0.99, ...)` from discrete histogram buckets?
**Answer**:
Prometheus histograms record cumulative counts in pre-defined buckets (`le` = less than or equal to).
1. Prometheus aggregates the bucket counters across the specified time window via `rate()`.
2. It identifies the target rank: $0.99 \times \text{Total Count}$.
3. It finds the specific bucket $[le_1, le_2]$ that contains this rank.
4. It performs **linear interpolation** within that bucket assuming uniform distribution of observations between $le_1$ and $le_2$.
- *Implication*: If bucket boundaries are poorly spaced (e.g. jumping from 100ms to 5,000ms), the computed percentile will be an imprecise mathematical approximation.

---

### Q4: What is an OpenTelemetry Exemplar and how does it bridge Metrics and Traces?
**Answer**:
An Exemplar is an exact trace ID and span ID attached to a specific metric measurement observation in a histogram bucket.
- When an unusually slow request of 2,400ms is recorded into the `[1000, 5000]` bucket, OpenTelemetry attaches `trace_id = "abc123xyz"` to that histogram sample.
- In Grafana, when viewing a spike in P99 latency, clicking on the data point displays the linked Exemplar. Clicking it instantly opens the exact trace in Jaeger, eliminating manual searching through millions of log lines.

---

### Q5: In `System.Diagnostics.Metrics`, what is the difference between `Counter<T>`, `Histogram<T>`, and `ObservableGauge<T>`?
**Answer**:
- `Counter<T>`: Monotonically increasing value (only positive deltas, e.g. requests processed, bytes received). Cannot decrease.
- `Histogram<T>`: Measures distributions of values (e.g. durations, payload sizes). The SDK aggregates these into statistical buckets, counts, and sums.
- `ObservableGauge<T>`: Asynchronous callback-based metric polled by the SDK at scraping intervals. Used for instantaneous values that fluctuate up and down (e.g. CPU percentage, current memory in use, queue length).

---

### Q6: How do you design an observability pipeline to prevent metric cardinality explosion?
**Answer**:
High cardinality occurs when metric tags have unbounded unique values (e.g., `user_id`, `order_id`, `email`, timestamp).
- Storing 1,000,000 distinct `user_id` tags multiplies time series in Prometheus TSDB memory exponentially, leading to an OOM crash.
- **Rules**:
  1. Only use discrete, bounded enums for metric tags: `http.method`, `http.status_code`, `http.route` (templated `/orders/{id}`, never raw `/orders/12345`).
  2. Put high-cardinality identifiers (`order_id`, `user_id`) in **Traces (Spans)** and **Logs**, where columnar databases (Loki, ClickHouse, Jaeger) handle high cardinality efficiently.

---

### Q7: Explain the trade-offs between "Push" (OTLP/StatsD) vs "Pull" (Prometheus scraping) metric architectures.
**Answer**:
- **Pull (Prometheus)**:
  - Prometheus periodically scrapes `/metrics` HTTP endpoints.
  - *Pros*: Centralized rate control (scraping cannot overwhelm the server); easy liveness detection (if scrape fails, service is down); no state in client services.
  - *Cons*: Difficult to scrape short-lived ephemeral batch jobs or serverless functions (requires Pushgateway); complex firewall traversal across VPCs.
- **Push (OTLP to OTel Collector)**:
  - Applications push telemetry batches to a local collector daemon.
  - *Pros*: Uniform across metrics, traces, and logs; supports serverless/lambda environments; easy NAT/firewall egress.
  - *Cons*: Risk of overwhelming downstream telemetry backends during traffic bursts if collector queues overflow.

---

### Q8: In LeetCode #355 (Design Twitter), how does the $K$-way merge algorithm operate for `GetNewsFeed`?
**Answer**:
Each user has a chronologically sorted list of their own tweets.
1. When generating the feed for user $U$, identify all followed users $F_1, F_2, \dots, F_k$ plus $U$.
2. For each user who has posted at least one tweet, enqueue their most recent tweet into a Max-Heap along with their user ID and tweet index. The heap size is at most $|F| + 1$.
3. Dequeue the root tweet (the globally newest across all feeds) and add to the result feed.
4. Advance that user's index to their next newest tweet and enqueue it into the heap.
5. Repeat until 10 tweets have been harvested or the heap is empty.
- Time Complexity: $O(F \log F + 10 \log F) = O(F \log F)$.

---

### Q9: How would you scale the Twitter news feed system for a celebrity with 100 million followers?
**Answer**:
Using pure Fan-out on Write would require inserting 100,000,000 timeline entries into Redis whenever the celebrity tweets, causing massive write amplification and lag.
- **Solution (Hybrid Fan-out)**:
  1. Categorize users into standard users and celebrities (e.g. followers $> 50,000$).
  2. For standard users: Fan-out on Write to follower timeline caches.
  3. For celebrities: Do not fan out on write. Instead, when a follower opens their feed, do a fast parallel fetch of the recent tweets from celebrities they follow and dynamically merge them (fan-out on read) with their pre-computed inbox timeline in RAM.

---

### Q10: How does OpenTelemetry Collector act as a pipeline buffer and aggregator in enterprise deployments?
**Answer**:
Rather than having hundreds of microservice pods directly connect to Prometheus, Jaeger, and Datadog:
1. Microservices send OTLP over localhost/mesh to an OTel Collector agent or daemonset.
2. The Collector pipeline has three stages:
   - **Receivers**: Accept OTLP (gRPC/HTTP), Zipkin, Jaeger.
   - **Processors**: `memory_limiter`, `batch` (coalesce into bulk network requests), `filter` (drop unwanted metrics), and `tail_sampling` (keep only slow or failed traces).
   - **Exporters**: Fan out identical telemetry data to multiple targets simultaneously (e.g., Prometheus for metrics, Jaeger for tracing, AWS CloudWatch for auditing).
