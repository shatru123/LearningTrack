# Deep Dive Notes: High-Throughput Chat Architecture

## 1. WebSocket Connection Management & Gateway Tier
- **C10M Problem (10 Million Connections)**:
  - Long-lived persistent TCP connections consume file descriptors and socket buffers.
  - Linux tuning: `sysctl -w fs.file-max=2097152`, `ulimit -n 1048576`, tuning `tcp_rmem` and `tcp_wmem` buffer sizes (down from 128KB to 4KB per socket idle) allows a single 64GB RAM node to hold 1M idle WebSocket connections.
- **Connection Routing**:
  - Load balancers (Envoy, NGINX) use IP hash or consistent hashing for sticky routing, or simple round-robin with WebSocket upgrade handling.
  - Gateway servers maintain an internal mapping: `Dictionary<UserId, List<WebSocketSession>>`.

## 2. Message Ordering & Storage Engines
- **Why NTP Wall Clocks Fail**:
  - In distributed systems, clock skew between servers can be 50–200ms. If Message B arrives 10ms after Message A but Server 2's clock is 50ms behind Server 1, sorting by timestamp reverses message order.
- **Monotonic Sequencer Solutions**:
  - **Redis INCR per channel**: `INCR channel:{id}:seq` provides an atomic 64-bit integer.
  - **Snowflake IDs**: 64-bit IDs containing timestamp prefix + machine ID + local sequence counter (guarantees rough time ordering, but gaps can exist).
- **Storage: Apache Cassandra / ScyllaDB**:
  - Primary Key: `((channel_id), sequence_number) WITH CLUSTERING ORDER BY (sequence_number ASC)`.
  - Partitioning by `channel_id` groups all messages of a room on the same storage node/replica. Sequential range queries (`SELECT * WHERE channel_id = ? AND sequence_number > ? LIMIT 50`) hit a contiguous SSTable disk block with sub-millisecond seek latency.

## 3. LeetCode #39 & #40: Backtracking Combinations Tree
- **State Space**:
  - At each node, decide whether to include `candidates[i]` and recurse on the remainder.
  - Sorting the array beforehand enables immediate termination of the loop (`if (candidates[i] > remain) break;`), pruning massive subtrees.
- **Combination Sum I vs II**:
  - In I, elements can be picked repeatedly $\implies$ next call is `Backtrack(i, remain - candidates[i])`.
  - In II, elements cannot be reused $\implies$ next call is `Backtrack(i + 1, remain - candidates[i])`.
  - In II, duplicate elements exist $\implies$ duplicate branches at the same depth are skipped via `if (i > start && candidates[i] == candidates[i - 1]) continue;`.
