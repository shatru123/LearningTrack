# Day 26: Microservices Principles & Bounded Contexts

## Overview
**Day 26** launches **Phase 2: Microservices & Cloud-Native Architecture (Days 26–50)** of the 100-Day .NET + AI Engineering Track. This day focuses on Domain-Driven Design (DDD) strategic decomposition, Bounded Context boundaries, Anti-Corruption Layer (ACL) patterns, and in-process Domain Event dispatching, coupled with LeetCode #200 (*Number of Islands* - Medium) using DFS sinking and BFS queue graph exploration.

---

## Architectural Topics Covered

1. **Strategic DDD & Subdomain Decomposition**:
   - **Core Subdomain**: Primary competitive advantage (e.g. dynamic pricing and bespoke order workflow).
   - **Supporting Subdomain**: Custom-built auxiliary capabilities (e.g. inventory allocation).
   - **Generic Subdomain**: Off-the-shelf commoditized capabilities (e.g. payment gateway integration, authentication).
2. **Bounded Contexts & Context Mapping**:
   - `OrderingContext`: Manages order lifecycles, invariant validations, and domain event generation.
   - `InventoryContext`: Manages stock reservations and fulfillment availability.
   - `PaymentContext`: Integrates with external payment processors.
   - **Anti-Corruption Layer (ACL)**: Isolates the domain model from foreign, legacy, or vendor schemas by translating untyped upstream payloads into strongly typed domain models.
3. **In-Process Domain Event Architecture**:
   - Decoupled asynchronous event dispatcher (`IDomainEventDispatcher`).
   - Domain Events: `OrderPlacedEvent`, `InventoryReservedEvent`, `PaymentAuthorizedEvent`.
   - Aggregate roots managing internal event collections and emitting state transitions.
4. **Graph Algorithms: Number of Islands (LeetCode #200)**:
   - 2D grid graph traversal treating cells as vertices and cardinal adjacencies as edges.
   - Depth-First Search (DFS) with in-place sinking ($\mathcal{O}(M \times N)$ time).
   - Breadth-First Search (BFS) with FIFO queue ($\mathcal{O}(\min(M, N))$ space).

---

## Project Structure

```
Day26-Microservices-BoundedContexts/
├── src/
│   └── Day26.MicroservicesBoundedContexts/
│       ├── Day26.MicroservicesBoundedContexts.csproj
│       ├── DomainModels.cs
│       ├── ContextMappingAndAcl.cs
│       └── NumberOfIslandsSolver.cs
├── tests/
│   └── Day26.MicroservicesBoundedContexts.Tests/
│       ├── Day26.MicroservicesBoundedContexts.Tests.csproj
│       └── Day26Tests.cs
├── README.md
├── notes.md
└── interview-questions.md
```

---

## Running the Unit Tests

```bash
dotnet test Day26-Microservices-BoundedContexts/tests/Day26.MicroservicesBoundedContexts.Tests/Day26.MicroservicesBoundedContexts.Tests.csproj
```

### Test Suite Coverage
1. `Order_AddItem_AndCalculateTotal_AccuratelyAggregates`: Validates total price calculation and item aggregation.
2. `Order_Place_EmitsOrderPlacedDomainEvent`: Verifies domain event emission upon status transition to `Placed`.
3. `Order_Invariants_EnforceValidation`: Ensures validation rules for empty orders, zero quantity, negative currency, and mismatched currencies.
4. `InventoryItem_TryReserve_AndRelease_MaintainsInvariants`: Tests stock reservation, over-allocation prevention, and release.
5. `PaymentAntiCorruptionLayer_TranslatesLegacyGatewaySuccess`: Tests successful translation of snake_case legacy payment response into domain result.
6. `PaymentAntiCorruptionLayer_TranslatesInvalidPayloadGracefully`: Ensures malformed identifiers and currency amounts fail gracefully.
7. `OrderFulfillmentCoordinator_CoordinatesAcrossBoundedContexts`: Validates end-to-end event choreography and audit trail.
8. `NumberOfIslands_DfsAndBfs_StandardGrid_ComputesCorrectCount`: Validates LC #200 multi-island grid under both DFS and BFS.
9. `NumberOfIslands_HandlesEdgeCases_Correctly`: Tests null, empty, all-water, and all-land matrices.
10. `NumberOfIslands_HandlesDiagonalSeparation_AsDistinctIslands`: Confirms diagonally adjacent cells are treated as distinct islands.
