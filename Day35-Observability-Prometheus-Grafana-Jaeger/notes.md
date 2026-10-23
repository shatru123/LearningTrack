# Deep Dive Notes: Observability Stack & Metric Architectures

## 1. RED vs USE Methodologies
- **RED Method (Tom Wilkie)**:
  - Focused on **Services** (Request-driven architectures, HTTP / gRPC).
  - **Rate**: Number of requests per second.
  - **Errors**: Number of failing requests per second.
  - **Duration**: Distribution of time taken by requests.
- **USE Method (Brendan Gregg)**:
  - Focused on **Resources** (Hardware, OS, CPUs, Disks, Network interfaces, ThreadPools).
  - **Utilization**: The average time that the resource was busy servicing work (e.g., 85% CPU).
  - **Saturation**: The degree to which extra work is queued waiting for the resource (e.g., Run Queue length > Core count).
  - **Errors**: Count of error events (e.g., dropped packets, disk drive I/O retries).

## 2. Percentiles & Histogram Quantile Math
Averages (means) are dangerously misleading in distributed systems due to multimodal distributions and long tail latency.
- If 99 requests take 10ms and 1 request takes 1,000ms:
  - Mean = 19.9ms (looks healthy).
  - P99 = 1,000ms (unacceptable 1-second delay for 1 in 100 users).
- **Nearest-Rank Percentile Formula**:
  $$\text{Rank} = \frac{P}{100} \times (N - 1)$$
  Linear interpolation between $\lfloor \text{Rank} \rfloor$ and $\lceil \text{Rank} \rceil$ provides accurate tail latency curves.
- In Prometheus, `histogram_quantile(0.99, sum(rate(http_duration_bucket[5m])) by (le))` uses linear interpolation across pre-aggregated exponential or explicit bucket bounds.

## 3. LeetCode #355: Fan-out on Read vs Fan-out on Write
- **Fan-out on Read (Pull)**:
  - Tweets are stored per user. When generating a feed, query all followees and perform a $k$-way merge using a min/max-heap.
  - *Pros*: Instant $O(1)$ post tweet; inactive users consume zero background compute.
  - *Cons*: Heavy read latency when following hundreds of users ($O(F \log F)$).
- **Fan-out on Write (Push)**:
  - When a user tweets, push the tweet ID into the Redis timeline cache of every follower.
  - *Pros*: Extremely fast $O(1)$ feed reads.
  - *Cons*: "Celebrity problem" — a user with 50 million followers triggers 50 million Redis writes per tweet!
- **Hybrid Production Architecture (Twitter / X)**:
  - Normal users: Fan-out on write (push to followers).
  - Celebrities (>25k followers): Fan-out on read (merged dynamically into the feed at read time).
