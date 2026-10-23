# Day 32: Saga Pattern: Orchestration vs Choreography

## Architectural Overview

In microservices architectures, business processes inherently cross service and database boundaries. Since Two-Phase Commit (2PC) is non-viable over distributed cloud networks, systems employ the **Saga Pattern**—a sequence of local transactions where each step updates a local database and emits an event or message to trigger the next step.

When a step fails (e.g. credit card declined, stock unavailable), the saga must guarantee data consistency by executing an ordered sequence of **Compensating Transactions** that undo the side effects of preceding successful steps.

This module delivers a production-grade implementation of **State Machine Saga Orchestration with MassTransit**, comparing it against Choreography, and solves LeetCode #127 (*Word Ladder* - Hard) via Bidirectional BFS.

---

## Key Architectural Concepts

### 1. Orchestration vs Choreography
- **Choreography (Decentralized)**:
  - Services listen to domain events and independently decide what to execute next.
  - *Risk*: "Pinball Machine" effect where tracing end-to-end execution flow requires distributed query aggregation; circular event loops are easy to introduce; compensating rollbacks across 4+ services become nearly impossible to reason about.
- **Orchestration (Centralized State Coordinator)**:
  - A dedicated coordinator (`OrderFulfillmentStateMachine`) tracks overall saga state, defines transitions, and explicitly dispatches commands (`ReserveInventoryCommand`, `ProcessPaymentCommand`).
  - *Benefits*: Single source of truth for workflow state; centralized timeout and error handling; audit trails.

### 2. Forward & Compensating Transitions
```
[OrderSubmitted] ---> State: Submitted
                            |
                 [InventoryReserved] ---> State: InventoryReservedState
                                                |
                       +------------------------+------------------------+
                       | (Payment Authorized)                            | (Payment Failed)
                       v                                                 v
               State: PaymentAuthorizedState                  State: CompensatingInventoryState
                       |                                                 |
                 [OrderShipped]                                  [InventoryReleased]
                       v                                                 v
               State: Completed                                  State: Faulted
```

### 3. Concurrency & State Persistence
Saga state instances persist in a relational store (PostgreSQL) using an optimistic concurrency column (`Version`). Concurrent events correlate by `CorrelationId` to ensure atomic state updates.

---

## Data Structures & Algorithms: Word Ladder

### LeetCode #127: Word Ladder (Hard)
- **Problem**: Given `beginWord`, `endWord`, and a dictionary `wordList`, find the length of the shortest transformation sequence such that adjacent words differ by exactly 1 character and all intermediate words are in the dictionary.
- **Bidirectional BFS Algorithm**:
  - Maintain two search frontiers (`beginSet` and `endSet`).
  - At each step, expand the smaller set to prune search branch explosion ($b^{d/2} + b^{d/2} \ll b^d$).
  - Generate 1-character mutations for each word using character substitutions `'a'..'z'`.
  - When a mutation intersects the opposing set, return `distance + 1`.
- **Complexity**: $\mathcal{O}(N \times L^2)$ time, $\mathcal{O}(N \times L)$ space, where $N$ is vocabulary size and $L$ is word length.

---

## Test Suite Summary

All 10 unit tests pass with 100% coverage:
1. `Saga_HappyPath_CompletesOrderSuccessfully`
2. `Saga_PaymentFailure_TransitionsToCompensatingState`
3. `Saga_InventoryRelease_TransitionsToFaultedState`
4. `Saga_CorrelationId_MatchesAcrossMultiEventFlow`
5. `Saga_AuditTrail_RecordsStepByStepWorkflowHistory`
6. `Saga_Comparator_EvaluatesCouplingAndVisibility`
7. `WordLadder_LC127_FindsShortestPathLength`
8. `WordLadder_LC127_ReturnsZeroWhenEndWordNotInList`
9. `WordLadder_LC127_ReturnsZeroWhenNoPathExists`
10. `WordLadder_LC127_HandlesSingleStepTransformation`
