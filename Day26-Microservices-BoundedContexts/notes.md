# Day 26: Deep Dive Notes — Microservices Principles & Bounded Contexts

## 1. Microservices Foundations & Distributed Realities

Moving from a monolithic architecture to microservices is not merely a deployment restructuring—it is an architectural partitioning of business capabilities and transactional boundaries.

```
+----------------------------------------------------------------------------------------------------+
|                                    DDD CONTEXT MAPPING TOPOLOGY                                     |
+----------------------------------------------------------------------------------------------------+
|                                                                                                    |
|    +-----------------------------+                           +-----------------------------+       |
|    |      Ordering Context       | --[ OrderPlacedEvent ]--> |      Inventory Context      |       |
|    |      (Core Subdomain)       |                           |   (Supporting Subdomain)    |       |
|    +-----------------------------+                           +-----------------------------+       |
|                   |                                                         |                      |
|                   v                                                         v                      |
|          [ Anti-Corruption Layer ]                                [ Stock Reservation ]            |
|                   |                                                                                |
|                   v                                                                                |
|    +-----------------------------+                                                                 |
|    |       Payment Context       |                                                                 |
|    |      (Generic Subdomain)    |                                                                 |
|    +-----------------------------+                                                                 |
|                                                                                                    |
+----------------------------------------------------------------------------------------------------+
```

### The 8 Fallacies of Distributed Computing (Deutsch & Gosling)
Every microservices engineer must design with the following operational realities in mind:
1. **The network is reliable**: False. Packet drops, transient partition timeouts, and socket resets will occur continuously.
2. **Latency is zero**: In-process method calls take $\approx 2\text{–}10\text{ ns}$; network hops take $0.5\text{–}50\text{ ms}$ (a million-fold slowdown).
3. **Bandwidth is infinite**: Serializing excessive payloads chokes network saturation.
4. **The network is secure**: Zero-trust mTLS and token validation are required at every boundary.
5. **Topology doesn't change**: Kubernetes pods, load balancers, and nodes scale and relocate dynamically.
6. **There is one administrator**: Microservices span heterogeneous squads, clusters, and clouds.
7. **Transport cost is zero**: JSON parsing, serialization, TLS handshake, and socket overhead consume CPU.
8. **The network is homogeneous**: Multiple OS versions, programming languages, and network stacks interact.

---

## 2. Domain-Driven Design (DDD): Strategic Design

### Subdomain Taxonomy
- **Core Subdomain**: The primary competitive engine of the enterprise. Bespoke, highly optimized, subject to frequent evolution.
- **Supporting Subdomain**: Custom software essential for core operations, but not a competitive differentiator (e.g., custom warehouse racking allocation).
- **Generic Subdomain**: Standardized domain problem shared across the industry (e.g., identity management, payment gateway integration, SMS notifications). Buy or integrate standard libraries whenever possible.

### Ubiquitous Language & Bounded Contexts
A **Bounded Context** defines the conceptual boundary within which a specific domain model and **Ubiquitous Language** strictly apply. 
A single real-world concept (e.g., a "Product") has radically different meanings and attributes across contexts:
- In `CatalogContext`: SKU, marketing title, SEO tags, rich media, category tree.
- In `InventoryContext`: SKU, bin location, weight, physical dimensions, units on hand, reorder point.
- In `BillingContext`: Price, tax code, ledger account, discount eligibility.

*Unifying these into a single bloated "Universal Product Entity" creates an unmaintainable monolith.*

### Context Mapping Patterns
1. **Shared Kernel**: Two bounded contexts share a subset of the domain model and database. Creates high coupling; should be restricted to rare shared utilities.
2. **Customer / Supplier (Upstream / Downstream)**: The Upstream context (Supplier) delivers services or data to the Downstream context (Customer). Downstream needs dictate delivery priority.
3. **Conformist**: Downstream conforms unconditionally to the Upstream schema without adaptation.
4. **Anti-Corruption Layer (ACL)**: Downstream implements a translation layer that maps foreign Upstream representations into clean Downstream domain concepts, preventing domain model corruption.
5. **Open Host Service (OHS) / Published Language (PL)**: Upstream exposes a stable, documented protocol (REST, gRPC, Protobuf, CloudEvents) for multiple consumers.

---

## 3. Tactical DDD Principles

### 1. Entities vs. Value Objects
- **Value Object**:
  - Defined entirely by its attributes (immutable).
  - No conceptual identity.
  - Structural equality (`record struct`, `record`, or overridden `Equals`).
  - Example: `Money(100, "USD")`, `Sku("LAPTOP-01")`, `Address("123 Main St", "NY")`.
- **Entity**:
  - Defined by an immutable unique identifier (`Id`).
  - Has a lifecycle and mutable state transitions.
  - Example: `OrderItem`, `Customer`.

### 2. Aggregate Roots & Consistency Boundaries
An **Aggregate** is a cluster of entities and value objects that are treated as a single transactional consistency unit:
- The **Aggregate Root** is the sole gatekeeper for all mutations within the aggregate.
- **External entities must never directly mutate interior aggregate entities**.
- Aggregates reference other aggregates exclusively by identifier (`Guid OrderId`), never by direct in-memory object reference.
- **Rule of Thumb**: One database transaction should modify exactly one Aggregate Root instance. Cross-aggregate coordination is handled via Domain Events and eventual consistency (Sagas / Process Managers).

### 3. In-Process Domain Events
- Aggregates record domain events when significant state transitions occur (`OrderPlaced`, `PaymentAuthorized`).
- Events are dispatched either immediately in-process or persisted to an Outbox table before committing the database transaction.

---

## 4. Graph Algorithms: Number of Islands (LeetCode #200)

### Problem Formulation
Given an $m \times n$ 2D binary grid representing '1' (land) and '0' (water), count the number of disconnected land masses (connected 4-directionally).

### 1. Depth-First Search (DFS) In-Place Sinking
- Scan the 2D matrix linearly from $(0, 0)$ to $(m-1, n-1)$.
- When a cell with value `'1'` is encountered:
  1. Increment `islandCount`.
  2. Invoke `DfsSink(r, c)`: recursively visit its 4 cardinal neighbors (`(r-1, c)`, `(r+1, c)`, `(r, c-1)`, `(r, c+1)`).
  3. Mark visited cells by overwriting `'1'` with `'0'` (sinking the island into water).
- **Time Complexity**: $\mathcal{O}(M \times N)$ — each grid cell is evaluated at most 5 times (1 linear scan + 4 neighbor checks).
- **Space Complexity**: $\mathcal{O}(M \times N)$ worst case recursion stack depth if the grid is solid land.

### 2. Breadth-First Search (BFS) Exploration
- When encountering `'1'`:
  1. Increment `islandCount`.
  2. Overwrite `'1'` with `'0'` immediately (prior to queue enqueue) to prevent duplicate enqueues from multiple neighbors.
  3. Enqueue `(r, c)`.
  4. While queue is non-empty, dequeue and explore all 4 cardinal neighbors. Enqueue matching unvisited lands and mark them `'0'`.
- **Time Complexity**: $\mathcal{O}(M \times N)$.
- **Space Complexity**: $\mathcal{O}(\min(M, N))$ — the queue holds at most the diagonal cross-section/perimeter of the island at any point in time.
