# Deep Dive Notes: Event Sourcing & CQRS Pattern

## 1. Event Sourcing Core Principles
- **State as a Left Fold**:
  - In CRUD, `UPDATE accounts SET balance = 150 WHERE id = 1` destroys the history of how the balance became 150.
  - In Event Sourcing, state is derived:
    `state = events.Aggregate(initial, (acc, evt) => acc.Apply(evt))`
  - The Event Store is the single source of truth.
- **Optimistic Concurrency Control (OCC)**:
  - Stream table schema: `(stream_id, version, payload, timestamp)` with unique constraint on `(stream_id, version)`.
  - When appending, specify `ExpectedVersion`. If another process appended version 3 first, the unique constraint or check fails, aborting the write and forcing the caller to reload and re-evaluate business logic.

## 2. CQRS (Command Query Responsibility Segregation)
- **Asymmetric Scaling**:
  - Most business systems have an 80:20 or 99:1 read-to-write ratio.
  - Write Model: Optimized for transactional integrity, validation, and zero locks (aggregates write to append-only tables).
  - Read Model: Optimized for queries (denormalized relational tables, Elasticsearch indices, or Redis hashes).
- **Handling Eventual Consistency in UI**:
  - Because read projections update asynchronously after the event is committed, a read immediately following a write might not show the new item.
  - **Mitigation Strategies**:
    1. Optimistic UI update: The client updates local UI state immediately.
    2. Read-your-own-writes session token: Query model waits until projected version $\ge$ token version before responding.

## 3. LeetCode #131: Palindrome Partitioning DP Optimization
- A naive check reverses every substring, taking $O(K)$ time per substring. With $2^N$ possible partition combinations, this wastes CPU cycles.
- **DP Table Formulation**:
  - Let `dp[i, j]` represent whether `s[i..j]` is a palindrome.
  - Base case: `dp[i, i] = true` for all $i$.
  - For substrings of length $L \ge 2$:
    $$\text{dp}[i, j] = (s[i] == s[j]) \land (L == 2 \lor \text{dp}[i+1, j-1])$$
  - Filling the DP table takes $O(N^2)$ time once up front. Then, each palindrome check in the backtracking loop is a lightning-fast $O(1)$ table lookup.
