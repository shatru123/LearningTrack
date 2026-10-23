# Day 36: Senior & Staff Interview Questions & Answers

### Q1: How do you design a notification system capable of handling 100 million notifications per day with peak bursts?
**Answer**:
1. **Asynchronous Ingestion API**: A stateless, highly-scalable API gateway receives notification requests, writes to an in-memory Redis deduplication store, and enqueues tasks into partitioned message queues (Kafka or RabbitMQ).
2. **Topic Partitioning by Channel & Priority**: Separate message queues by delivery speed requirements (`sms-critical`, `push-critical`, `email-bulk`). Critical queues (OTPs, password resets) have dedicated auto-scaled worker pools with unshared provider capacity.
3. **Worker Fleet**: Workers consume messages, verify user preferences from a read-through Redis cache, evaluate rate limit token buckets, and call downstream external providers (Twilio, SendGrid, FCM, APNs) using connection pooling and HTTP keep-alive.
4. **Dead-Letter Queues (DLQ)**: Failed deliveries after exponential retries are deposited into a DLQ for operational triage and alert auditing.

---

### Q2: How do you ensure high-priority notifications (OTPs) are delivered within 5 seconds even when millions of marketing notifications are queued?
**Answer**:
Never mix operational priority levels in the same FIFO message queue.
- Use **Priority-Isolated Queuing**: Maintain physically or logically separate queues for `High` (OTP, 2FA, fraud alerts), `Normal` (order confirmations, delivery tracking), and `Low` (marketing newsletters, weekly digests).
- Dedicated worker capacity: Dedicate 70% of worker threads/pods strictly to the `High` priority queue. If the `Low` queue experiences a massive backlog (e.g., 5 million marketing emails), it only impacts the `Low` workers; the `High` queue remains virtually empty and processed in sub-second latency.
- Provider prioritization: Reserve separate sub-accounts or dedicated outbound IP pools with vendors (e.g. Twilio High-Throughput Short Codes) exclusively for transactional traffic to prevent marketing email reputation degradation or rate limits from throttling OTPs.

---

### Q3: How do you implement rate limiting per user per channel (e.g. max 3 SMS per hour) at scale?
**Answer**:
Use a **Distributed Sliding Window** in Redis using Sorted Sets (`ZSET`):
- Key: `ratelimit:sms:{userId}`
- When an SMS request arrives at time $T$:
  1. `ZREMRANGEBYSCORE key 0 (T - 3600)` (remove timestamps older than 1 hour).
  2. `ZCARD key` (count remaining timestamps in the 1-hour window).
  3. If count $\ge 3$, reject with "Rate limit exceeded".
  4. Else, `ZADD key T T` and `EXPIRE key 3600`. Allow execution.
- Executed via an atomic Redis Lua script to prevent race conditions across parallel worker nodes.

---

### Q4: How do you handle third-party notification provider outages (e.g., Twilio outage)?
**Answer**:
1. **Provider Abstraction**: Define a generic `INotificationProvider` interface so application logic is completely decoupled from provider SDKs.
2. **Circuit Breakers (Polly in .NET)**: Track failure rates and timeout percentages. If Twilio returns HTTP 5xx or times out on > 20% of calls over 30 seconds, trip the breaker to `Open` state.
3. **Automated Secondary Failover**: When the primary circuit opens or throws an exception, dynamically re-route traffic to secondary providers (e.g. AWS SNS, Sinch, Infobip).
4. **Health Checking & Canary Probing**: The circuit transitions to `Half-Open` after 60 seconds, sending test canary probes to verify primary recovery before switching traffic back.

---

### Q5: How do you prevent duplicate notification delivery when clients retry failed requests?
**Answer**:
1. **Idempotency Keys**: Require an `Idempotency-Key` (e.g., derived from `eventId` or `orderId + action`).
2. **Atomic Lock / Cache**: Before processing, the worker attempts an atomic `SET key:notification:{idempotencyKey} "PROCESSING" EX 300 NX` in Redis.
   - If key already exists, return immediate cached success without re-sending.
3. **Provider-Level Idempotency**: Forward the idempotency key to downstream providers that support it (e.g. Stripe, SendGrid, FCM message IDs) so that even if the internal worker crashes mid-network transmission, the vendor will not dispatch two duplicate physical SMS/emails.

---

### Q6: How do you store and enforce user notification preferences and "Do Not Disturb" (quiet hours)?
**Answer**:
- Store user preferences in a primary NoSQL database (DynamoDB/Cassandra) with high read availability, cached in Redis:
  ```json
  {
    "userId": "usr_99",
    "optedOut": ["marketing_email", "promotional_sms"],
    "quietHours": { "start": "22:00", "end": "08:00", "timezone": "America/New_York" }
  }
  ```
- Before queuing or dispatching non-critical notifications, convert the current UTC time to the user's localized timezone.
- If current time falls within `quietHours`, delay message delivery by calculating seconds until 08:00 AM and enqueueing into a delayed/scheduled queue (e.g., RabbitMQ dead-letter exchange with TTL or Redis Sorted Set schedule).

---

### Q7: What telemetry and metrics should be monitored on a notification service?
**Answer**:
- **Ingestion Rate vs Delivery Rate**: Graphing messages queued vs messages acknowledged by vendors.
- **Queue Lag / Backlog Depth**: Monitored per channel and priority; triggers alerts if `sms-critical` lag > 100 messages.
- **Provider Latency & Error Rate**: P95/P99 latency of Twilio, SendGrid, APNs, and FCM HTTP endpoints.
- **Delivery Receipt (DLR) Conversion**: Percentage of messages marked "Delivered" by mobile carriers via incoming webhooks.
- **Bounce & Complaint Rates**: Hard bounces and spam reports on email channels to maintain domain reputation (SPF/DKIM/DMARC).

---

### Q8: In LeetCode #78 (Subsets), what is the difference in time and space complexity between Backtracking and Bit Manipulation?
**Answer**:
- Both algorithms have a time complexity of $O(N \times 2^N)$ because there are $2^N$ total subsets, and copying each subset of average length $N/2$ into the final result requires $O(N)$ work.
- **Backtracking**:
  - Recursion tree depth is $N$. Space complexity is $O(N)$ for the recursion stack and temporary subset list.
  - Generates subsets in lexicographical prefix order.
- **Bit Manipulation**:
  - Loops from $0$ to $2^N - 1$. Iterative with $O(1)$ auxiliary memory (ignoring the output list).
  - Elegant and avoids recursion overhead, but limited to $N \le 64$ due to integer bit width.

---

### Q9: In LeetCode #90 (Subsets II), why does sorting the array and checking `if (i > start && nums[i] == nums[i - 1]) continue;` eliminate duplicate subsets?
**Answer**:
- When generating subsets from an array with duplicates (e.g. `[1, 2, 2]`), duplicate subsets occur when the same value is selected at the same decision depth across different branches.
- Sorting guarantees all duplicate numbers are placed contiguously.
- In the loop `for (int i = start; i < nums.Length; i++)`:
  - When $i = \text{start}$, we are exploring the *first* available instance of this number at this recursion level.
  - When $i > \text{start}$ and $\text{nums}[i] == \text{nums}[i - 1]$, we are considering a *sibling* branch at the exact same tree level that has the same value.
  - Pruning sibling duplicates skips generating identical subtrees without needing an expensive `HashSet` of lists.

---

### Q10: How do you handle mobile push notification token invalidation (Apple APNs / Google FCM)?
**Answer**:
Mobile push tokens expire when users uninstall the app, switch devices, or wipe data.
1. When calling APNs/FCM, the vendor returns specific error codes: APNs `BadDeviceToken` (410 Gone) or FCM `UNREGISTERED` / `INVALID_ARGUMENT`.
2. The notification worker intercepts these specific HTTP response codes.
3. The worker asynchronously emits a `DeviceTokenInvalidatedEvent` to an internal queue.
4. An account service updates the device registry database, marking the token inactive/deleted so subsequent notifications are not sent, saving bandwidth and preventing provider penalty rate limiting.
