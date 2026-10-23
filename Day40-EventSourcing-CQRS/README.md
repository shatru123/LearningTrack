# Day 40: Event Sourcing & CQRS Foundations

## Architectural Overview
Traditional CRUD applications mutate state in place, overwriting historical data and making auditing, time-travel debugging, and distributed synchronization complex.

**Event Sourcing** models application state as an append-only sequence of immutable domain events:
$$\text{Current State} = \text{FoldLeft}(\text{Initial State},\; \text{Events})$$

This module implements enterprise Event Sourcing and CQRS (Command Query Responsibility Segregation) in .NET 8:
- **Aggregate Root Pattern**: `BankAccountAggregate` validates commands (`Open`, `Deposit`, `Withdraw`, `Close`) against current business rules and emits immutable domain events.
- **Append-Only Event Store**: Stores events ordered by monotonic version per stream (`AccountId`).
- **Optimistic Concurrency Control**: Employs `ExpectedVersion` checking during append operations to detect and reject concurrent write collisions (`OptimisticConcurrencyException`).
- **State Reconstruction & Snapshotting**: Rebuilds aggregate state by applying events sequentially. Periodic snapshots bound replay time from $O(N)$ to $O(k)$ where $k \le \text{snapshot interval}$.
- **CQRS Read-Model Projections**: Separates the write model from the query model. An `AccountProjectionManager` listens to committed events and materializes denormalized read views (`AccountSummaryView`) optimized for high-speed queries.

```
                  Command: Deposit($100)
                           │
                           ▼
               ┌───────────────────────┐
               │ BankAccountAggregate  │ ──(Validate Business Rules)
               └───────────┬───────────┘
                           │
                 Emit FundsDepositedEvent
                           │
                           ▼
               ┌───────────────────────┐
               │      Event Store      │ (Append-Only Stream)
               │ (ExpectedVersion = 2) │
               └───────────┬───────────┘
                           │
              ┌────────────┴────────────┐
              ▼                         ▼
    ┌───────────────────┐     ┌───────────────────┐
    │  State Snapshot   │     │ CQRS Read View    │
    │  (Every N Events) │     │ (Projection View) │
    └───────────────────┘     └───────────────────┘
```

---

## LeetCode #131: Palindrome Partitioning (Medium)
Partition string $s$ such that every substring is a palindrome:
- **DP Precomputation**: Constructs a 2D boolean dynamic programming table `dp[i, j]` where `dp[i, j] = true` if `s[i..j]` is a palindrome. Base cases handle length 1 and 2; transitions evaluate `s[i] == s[j] && dp[i+1, j-1]`.
- **Backtracking**: Traverses partitions using $O(1)$ DP palindrome verification, avoiding redundant $O(K)$ string reversal checks per branch.

---

## Running Tests
```bash
dotnet test Day40-EventSourcing-CQRS/tests/Day40.EventSourcing.Tests/Day40.EventSourcing.Tests.csproj
```
10 unit tests validate aggregate business rules, event store append/replay, optimistic concurrency conflict detection, snapshot rehydration, CQRS read view projections, and palindrome partitioning.
