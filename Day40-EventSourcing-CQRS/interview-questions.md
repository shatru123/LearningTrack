# Day 40: Senior & Staff Interview Questions & Answers

### Q1: What is Event Sourcing, and how does it fundamentally differ from traditional CRUD persistence?
**Answer**:
- **CRUD (Create, Read, Update, Delete)**: Directly mutates the current snapshot of an entity in a database table. Prior states are overwritten and lost unless secondary audit tables are maintained.
- **Event Sourcing**: Replaces mutable tables with an immutable, append-only log of domain events (`AccountOpened`, `FundsDeposited`, `FundsWithdrawn`).
  - Current state is not stored as the primary source of truth; it is derived by replaying (folding) all past events from genesis to present.
  - Guarantees 100% auditability, eliminates object-relational mapping impedance mismatch, enables time-travel debugging, and provides native event publishing for microservice event-driven choreography.

---

### Q2: What is CQRS, and why is it frequently paired with Event Sourcing?
**Answer**:
CQRS (Command Query Responsibility Segregation) separates data modification (Commands) from data retrieval (Queries):
- **Why paired**: An event-sourced write model is structurally an append-only log (`StreamId`, `Version`, `Payload`). Querying complex relational filters (e.g., "Find all accounts in California with balance > \$5,000 created in 2024") on an append-only event log would require replaying every event in the entire database, which is $O(N)$ and prohibitively expensive.
- **Solution**: Event handlers listen to committed events and project (materialize) them into read-optimized SQL tables, Elasticsearch indices, or Redis caches. Commands hit the aggregate; queries hit the projections.

---

### Q3: How do you implement Optimistic Concurrency Control in an append-only Event Store?
**Answer**:
1. When loading an aggregate root to execute a command, record its current version (e.g. `Version = 4`).
2. The aggregate executes business logic and emits new uncommitted events (e.g., versions 5 and 6).
3. The event store executes an atomic append:
   `INSERT INTO events (stream_id, version, payload) VALUES ...`
4. The database enforces a unique composite constraint on `(stream_id, version)` or checks `WHERE stream_id = @id AND version = @expectedVersion`.
5. If another concurrent request already appended version 5, the insert fails with a unique constraint violation.
6. The application catches this error, raises an `OptimisticConcurrencyException`, reloads the fresh aggregate state, and retries the command.

---

### Q4: What is Snapshotting in Event Sourcing, and when should it be triggered?
**Answer**:
As an entity's lifecycle lengthens, replaying thousands of historical events on every load degrades latency ($O(N)$ event deserialization).
- **Snapshotting**: The system periodically serializes the aggregate's in-memory state alongside its version (e.g., at version 100, `AccountSnapshot(Balance=1500, Version=100)`).
- When loading the aggregate, fetch the newest snapshot first ($O(1)$) and only replay events with `Version > Snapshot.Version`.
- **Triggers**: Periodic threshold (every $N=100$ events), time-based, or asynchronous background snapshot worker.

---

### Q5: How do you handle schema evolution and breaking changes in immutable domain events ("Versioning Events")?
**Answer**:
Because events are immutable and persisted permanently, old events cannot be casually rewritten in the database.
- **Strategies**:
  1. **Upcasting**: Middleware intercepts raw JSON during deserialization. If an event is version 1, an upcaster transforms it into version 2 schema (e.g., splitting `FullName` into `FirstName` and `LastName`) before handing it to the aggregate.
  2. **Additive Schema Evolution**: Make changes backward-compatible: new properties must be nullable or have safe defaults; never delete or rename properties.
  3. **Event Migration / Copy-and-Replace**: Create a background migration script that reads from stream V1, translates events, and writes to stream V2.

---

### Q6: How do you guarantee idempotency and "at-least-once" projection updates in CQRS?
**Answer**:
Projections subscribe to the event stream over message brokers or database change data capture (CDC).
- Network glitches cause event re-deliveries.
- **Projection Idempotency**:
  - Maintain a `last_processed_version` column in the projection database within the **same local transaction** as the projection update:
    ```sql
    UPDATE account_views 
    SET balance = balance + @amount, last_version = @version 
    WHERE account_id = @id AND last_version = @version - 1;
    ```
  - If an event with `version <= last_version` arrives, the projection ignores it, preventing duplicate mutations.

---

### Q7: How do you comply with GDPR "Right to be Forgotten" in an immutable Event Store?
**Answer**:
GDPR requires permanently deleting personal identifiable data (PII), which conflicts with append-only immutability.
- **Crypto-Shredding (Cryptographic Erasure)**:
  - Personal data fields (name, email, SSN) inside domain events are encrypted with a unique per-user symmetric key ($K_{\text{user}}$).
  - The keys are stored in a dedicated key vault.
  - When the user requests account deletion, simply destroy $K_{\text{user}}$.
  - The events remain in the append-only store preserving cryptographic hashes and stream version integrity, but the PII is permanently and irrecoverably rendered unreadable ciphertext.

---

### Q8: In LeetCode #131 (Palindrome Partitioning), how does Dynamic Programming optimize the backtracking search?
**Answer**:
- Checking if a substring $s[i..j]$ is a palindrome naively requires $O(j - i)$ two-pointer comparisons.
- In backtracking, the same substring is evaluated repeatedly across different partitioning prefixes.
- Precomputing a 2D boolean table `dp[i, j]` using the recurrence:
  $$\text{dp}[i, j] = (s[i] == s[j]) \land (j - i \le 2 \lor \text{dp}[i+1, j-1])$$
  takes $O(N^2)$ time once.
- During backtracking, verifying if a substring is a valid palindrome is reduced from $O(N)$ to $O(1)$ memory lookup, dramatically pruning recursive branches and reducing execution time.

---

### Q9: What are the primary trade-offs and downsides of Event Sourcing?
**Answer**:
- **Complexity**: Significantly steeper learning curve than standard CRUD; developers must think in terms of immutable facts rather than current state.
- **Eventual Consistency**: Read projections are asynchronously updated, requiring UI accommodation (optimistic rendering, spinners, or polling).
- **Storage Overhead**: An append-only store grows monotonically and requires partitioning, archival, and snapshotting strategies.
- **Query Impedance**: Directly querying the event store for ad-hoc business reporting is impractical; an external projection layer (CQRS) is mandatory.

---

### Q10: How do you test an Event-Sourced Aggregate Root?
**Answer**:
Event-sourced aggregates are pure domain models with zero external I/O dependencies, making them exceptionally easy and fast to unit test using the **Given-When-Then** specification pattern:
- **Given**: A sequence of past historical events:
  `[AccountOpened(balance=100), FundsDeposited(amount=50)]`
- **When**: A command is executed:
  `account.Withdraw(30)`
- **Then**: Assert that the expected domain event is emitted:
  `[FundsWithdrawn(amount=30, version=3)]`
- Or assert that a domain exception is thrown:
  `account.Withdraw(500) -> throws InsufficientFundsException`
