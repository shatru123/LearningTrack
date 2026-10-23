# Day 37: System Design: High-Throughput Chat System

## Architectural Overview
Real-time chat systems (Slack, Discord, WhatsApp) support hundreds of millions of concurrent WebSocket connections, instant messaging, presence tracking, and ordered message histories.

This module implements a distributed real-time chat architecture in .NET 8 featuring:
- **WebSocket Session Management & Scale**: Decouples client connections across distributed gateway hubs (`DistributedChatHub`).
- **Presence Service**: Heartbeat ping/pong mechanism tracking user states (`Online`, `Offline`, `Away`) with timeout-based expiration and instant disconnection cleanup.
- **Cross-Server Pub/Sub Replication**: Simulates Redis Pub/Sub backplane. When Alice sends a message to Server East, it publishes to the Redis topic; Server West receives the event and pushes to Bob over his active local WebSocket.
- **Monotonic Message Sequencing**: Channel-level sequencers assign sequential integer IDs ($1, 2, 3, \dots$), resolving clock-drift race conditions across distributed gateway nodes and guaranteeing total causal order.
- **Read Receipts & Typing Indicators**: Tracks `last_read_sequence` per user per channel and broadcasts transient typing indicators to peer participants without echoing to sender.

```
 Client A (Alice)                        Client B (Bob)
        │                                       │
WebSocket Connection                    WebSocket Connection
        ▼                                       ▼
┌──────────────┐                        ┌──────────────┐
│ Chat Hub 1   │                        │ Chat Hub 2   │
│ (US-East)    │                        │ (US-West)    │
└───────┬──────┘                        └───────▲──────┘
        │                                       │
        │ 1. Assign Sequence #                  │ 3. Forward to Bob
        │ 2. Publish to Redis                   │
        ▼                                       │
┌───────────────────────────────────────────────┴──────┐
│                Redis Pub/Sub Cluster                 │
│         Topic: chat:channel:{channel_id}             │
└───────────────────────┬──────────────────────────────┘
                        │
                        ▼ (Async Append)
        ┌───────────────────────────────┐
        │   ScyllaDB / Cassandra Store  │
        │ (Partition: channel_id,       │
        │  Clustering: sequence_number) │
        └───────────────────────────────┘
```

---

## LeetCode #39 & #40: Combination Sum I & II (Medium)
- **LeetCode #39 (Combination Sum I)**:
  - Elements can be reused infinitely.
  - Recursion advances with index `i` (allowing current element reuse).
  - Prunes early when sorted candidate exceeds remaining sum.
- **LeetCode #40 (Combination Sum II)**:
  - Input array contains duplicate numbers; each element used at most once.
  - Recursion advances with `i + 1`.
  - Prunes duplicate sibling branches:
    `if (i > start && candidates[i] == candidates[i - 1]) continue;`

---

## Running Tests
```bash
dotnet test Day37-SystemDesign-Chat-System/tests/Day37.ChatSystem.Tests/Day37.ChatSystem.Tests.csproj
```
10 unit tests validate presence heartbeats and timeouts, channel sequence monotonicity, cross-server pub/sub delivery, read receipts, typing indicators, and combination backtracking.
