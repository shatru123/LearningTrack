# Deep Dive Notes: Notification Service Architecture

## 1. High-Scale Notification Pipeline Design
- **Scale Requirements**:
  - 10M daily active users $\times$ 10 notifications/day = 100M notifications/day.
  - Average load: $\approx 1,160$ messages/sec; Peak load: $15,000$ messages/sec (e.g. Flash sales, Breaking news).
- **Decoupled Architecture**:
  - Ingress API: Validates payload, verifies idempotency key, pushes message to Kafka/RabbitMQ.
  - Workers: Consume by channel and priority. For example, `sms-high-priority` topic has dedicated worker pools with strict SLAs (< 5s for OTPs), while `email-marketing-low` has rate-controlled worker pools to avoid blacklisting.
- **Circuit Breakers & Vendor Fallbacks**:
  - External vendors (Twilio, SendGrid, APNs, FCM) have fluctuating error rates and rate limits.
  - Wrap provider calls in Polly Circuit Breakers. If Twilio error rate exceeds 15% in a 30s window, trip the breaker and divert traffic to AWS SNS or Infobip automatically.

## 2. Throttling & User Preferences
- **Sliding-Window Rate Limiting**:
  - Redis Sorted Sets (`ZADD timestamp, ZREMRANGEBYSCORE 0 now - window, ZCARD`) allow sliding-window rate limiting per `(user_id, channel)`.
  - Enforces quiet hours (e.g. 10 PM – 8 AM in recipient's local timezone) by deferring non-urgent messages until morning.
- **Preference Storage**:
  - Cassandra or DynamoDB with primary key `(user_id)` and a bitmask/JSON column of channel opt-outs (`sms: false, email: true, push: true`). Read-heavy cached in Redis.

## 3. LeetCode #78 & #90 (Subsets) Backtracking Mechanics
- The recursion tree for subsets forms a state-space tree where each decision point represents including or excluding an element.
- For Subsets II (with duplicates):
  - Sorting brings duplicate numbers adjacent: e.g. `[1, 2, 2]`.
  - Sibling branches represent choices at the **same recursion level** (`i > start`). If `nums[i] == nums[i - 1]`, choosing `nums[i]` would generate the identical subtree already explored by `nums[i - 1]`.
  - Skipping `nums[i]` when `i > start` prunes duplicate branches cleanly in $O(1)$ without allocating secondary sets.
