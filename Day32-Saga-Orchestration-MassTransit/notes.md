# Day 32 Deep Dive Notes: Saga Orchestration, Compensating Actions & Bidirectional BFS

## 1. Semantic Compensation vs Technical Rollback

### The Core Difference
In a single database, an ACID rollback is a **technical rollback**: the database engine scans the undo log and physically restores page bytes to their prior binary state, as if the transaction never occurred.
In distributed microservices, a **semantic rollback** (compensating transaction) is fundamentally different:
- You cannot "un-charge" a credit card; you must issue an explicit `RefundPayment` API call with a distinct transaction ID.
- You cannot "un-send" a push notification; you must send an `OrderCancelledNotification`.
- You cannot delete a shipped parcel from the FedEx truck; you must initiate a `RecallOrReturnPackage` return flow.

Therefore, compensating actions create **new forward transactions** that semantically counter the business effect of previous actions, leaving an immutable audit trail.

---

## 2. The Three Transaction Types in a Saga

Garcia-Molina & Salem's seminal Saga taxonomy divides steps into three categories:

1. **Compensatable Transactions**:
   - Steps that precede the Pivot.
   - Must have an associated compensating transaction that can be reliably invoked if later steps fail.
   - Example: `ReserveInventory` has compensation `ReleaseInventory`.
2. **Pivot Transaction**:
   - The architectural "Point of No Return".
   - If the pivot transaction succeeds, the saga is committed to forward completion.
   - If the pivot transaction fails, the saga aborts and executes compensations backwards.
   - Example: `CapturePayment`.
3. **Retryable Transactions**:
   - Steps that occur strictly after the Pivot transaction.
   - These steps **never have compensating transactions** because they are guaranteed to eventually succeed via exponential retries and dead-letter queues.
   - Example: `DispatchShippingOrder`, `SendConfirmationEmail`.

```
[Compensatable 1] ---> [Compensatable 2] ---> [PIVOT] ---> [Retryable 1] ---> [Retryable 2]
      |                      |                   |              |                 |
  (Can undo)             (Can undo)         (Point of      (Must succeed,   (Must succeed,
                                            No Return)     keep retrying)   keep retrying)
```

---

## 3. MassTransit State Machine Saga Internals

### Automatonymous State Machine DSL
MassTransit uses an expression-tree driven fluent state machine engine:
- `InstanceState(x => x.CurrentState)` binds the current state string or integer to the persisted entity.
- `Initially(...)` declares transitions acceptable only from the uninitialized state.
- `During(State, ...)` declares legal transitions when the saga is in that specific state. Unexpected events can be ignored or routed to an unhandled handler.
- `CorrelateById(...)` uses the `CorrelationId` property to lock and load the saga instance from PostgreSQL/EF Core.

### Concurrency Handling with PostgreSQL
When events arrive concurrently:
```sql
UPDATE "OrderFulfillmentState" 
SET "CurrentState" = @p0, "Version" = @p1, ...
WHERE "CorrelationId" = @p2 AND "Version" = @p3;
```
If the row has already been modified by another thread, `rowsAffected == 0`, triggering a `DbUpdateConcurrencyException`. MassTransit's retry middleware re-reads the row and retries event evaluation.

---

## 4. DSA Deep Dive: Bidirectional BFS & Word Ladder

### The Branching Factor Explosion
Suppose a word graph has an average branching factor $b \approx 10$ (number of valid 1-letter mutation neighbors) and the shortest path has depth $d = 6$:
- **Standard BFS**:
  $$\text{Nodes Visited} \approx b^d = 10^6 = 1,000,000 \text{ nodes}$$
- **Bidirectional BFS**:
  Two search trees meeting at depth $d/2 = 3$:
  $$\text{Nodes Visited} \approx 2 \times b^{d/2} = 2 \times 10^3 = 2,000 \text{ nodes}$$
  **Speedup**: Over $500\times$ faster!

### Dynamic Set Swapping Optimization
```csharp
if (beginSet.Count > endSet.Count)
{
    var temp = beginSet;
    beginSet = endSet;
    endSet = temp;
}
```
By always expanding the smaller frontier, the algorithm automatically picks the side of the search tree with fewer branches, preventing exponential blowouts when one side encounters dense clusters of English words.
