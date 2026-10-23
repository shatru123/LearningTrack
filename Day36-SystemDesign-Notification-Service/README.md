# Day 36: System Design: Notification Service

## Architectural Overview
A large-scale notification system must deliver billions of notifications across multiple delivery channels (Email, SMS, Mobile Push, In-App WebSockets) with strict latency guarantees, priority isolation, and abuse protections.

This module implements a resilient notification dispatch engine in .NET 8 featuring:
- **Priority-Based Dispatch Queues**: Separate prioritized scheduling (`High` for OTPs/Security alerts, `Normal` for transactional receipts, `Low` for marketing/digests). High-priority items always preempt low-priority items.
- **Provider Abstraction & Automated Failover**: Interface `INotificationProvider` with multi-vendor redundancy (e.g., Primary Twilio -> Secondary AWS SNS fallback on circuit degradation).
- **Per-User Rate Limiting & Throttling**: Sliding window rate limiter preventing user notification fatigue and provider API overage (e.g. max 5 SMS/min).
- **User Preference & Opt-Out Matrix**: Fine-grained subscription settings honoring user preferences per channel.
- **Idempotent Deduplication**: Cache-backed idempotency key verification preventing duplicate charges or duplicate SMS delivery during network retries.

```
Incoming Notification Request
               │
               ▼
     ┌───────────────────┐
     │ Idempotency Check │ ──(Duplicate)──> Return Cached / Deduplicated
     └─────────┬─────────┘
               │ (New)
               ▼
     ┌───────────────────┐
     │ User Preferences  │ ──(Opted Out)──> Reject (Opted Out)
     └─────────┬─────────┘
               │ (Subscribed)
               ▼
     ┌───────────────────┐
     │   Rate Limiter    │ ──(Exceeded)───> Reject (Throttled)
     └─────────┬─────────┘
               │ (Under limit)
               ▼
     ┌───────────────────┐
     │  Priority Queue   │
     │ High > Norm > Low │
     └─────────┬─────────┘
               │
      Dispatch to Provider
               │
       ┌───────┴───────┐
       ▼               ▼
[Primary Provider] ──(Fails)──> [Secondary Fallback Provider]
 (Twilio / FCM)                  (AWS SNS / APNS)
```

---

## LeetCode #78 & #90: Subsets I & II (Medium)
- **LeetCode #78 (Subsets)**:
  - Generate power set of size $2^N$ for unique elements.
  - Implemented via Backtracking (DFS tree exploration) and Bit Manipulation ($2^N$ binary bitmasks).
- **LeetCode #90 (Subsets II)**:
  - Generate power set when array contains duplicate elements.
  - Sorts input and skips duplicate sibling branches:
    `if (i > start && nums[i] == nums[i - 1]) continue;`
  - Guarantees $100\%$ unique subset generation without post-processing HashSet allocations.

---

## Running Tests
```bash
dotnet test Day36-SystemDesign-Notification-Service/tests/Day36.NotificationService.Tests/Day36.NotificationService.Tests.csproj
```
10 unit tests validate priority queue ordering, automated provider failover, user opt-out enforcement, sliding-window rate limiting, idempotency deduplication, and power set generation.
