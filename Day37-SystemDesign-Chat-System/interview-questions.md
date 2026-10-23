# Day 37: Senior & Staff Interview Questions & Answers

### Q1: How do you scale a WebSocket gateway to maintain 10 million concurrent active connections?
**Answer**:
1. **OS & Kernel Tuning**:
   - Increase file descriptor limits (`ulimit -n 1048576`).
   - Reduce TCP read/write buffer allocations (`net.ipv4.tcp_rmem = 4096 87380 4194304`, `net.ipv4.tcp_wmem`). For 1M idle connections, saving 50KB RAM per socket saves 50GB of memory.
   - Enable `SO_REUSEPORT` and `epoll`/`kqueue` event-driven non-blocking I/O (built into .NET Kestrel/Sockets).
2. **Stateless Gateway Tier**:
   - Deploy a cluster of WebSocket gateways behind Layer 4 Network Load Balancers (AWS NLB) with TLS termination.
   - Maintain minimal session state in the gateway (only socket handle and user ID).
3. **Session Registry**:
   - Store mapping of `UserId -> GatewayNodeIP` in Redis with TTL heartbeats so other services know which gateway holds a specific user's socket.

---

### Q2: Why is Redis Pub/Sub alone insufficient for chat message persistence, and how do you pair it with a database?
**Answer**:
Redis Pub/Sub is strictly "fire-and-forget":
- If a client is temporarily offline, or a network flap occurs between Redis and the gateway, messages published to Redis Pub/Sub are lost forever. Redis does not buffer messages for disconnected pub/sub subscribers.
- **Production Architecture**:
  1. The API receives the message.
  2. The message is written first to persistent storage (Cassandra/ScyllaDB) or an append-only commit log (Kafka / Redis Streams).
  3. Then the message is published to Redis Pub/Sub for real-time delivery to online users.
  4. When an offline user reconnects, they query the database for messages where `sequence_number > last_received_sequence`.

---

### Q3: How do you guarantee strict causal message ordering in a group chat across distributed servers without NTP clock synchronization?
**Answer**:
NTP clock drift between servers makes timestamps non-monotonic and prone to race conditions.
- **Per-Channel Atomic Sequencers**:
  - Each chat room/channel has an independent monotonic sequence counter.
  - Implement using Redis `INCR channel:{id}:seq` or an atomic database sequence.
  - Every message sent to the channel receives an incremented sequence number ($1, 2, 3, \dots$).
- **Client Synchronization**:
  - The client displays messages sorted by `sequence_number`.
  - If a client receives sequence #5 and previously had #3, it immediately detects a gap (missing #4) and requests the missing message from the server's history endpoint before rendering.

---

### Q4: What is the optimal database schema for storing chat messages in Apache Cassandra / ScyllaDB?
**Answer**:
Cassandra uses a two-part primary key: Partition Key and Clustering Columns.
```sql
CREATE TABLE channel_messages (
    channel_id uuid,
    sequence_id bigint,
    message_id uuid,
    sender_id uuid,
    content text,
    created_at timestamp,
    PRIMARY KEY ((channel_id), sequence_id)
) WITH CLUSTERING ORDER BY (sequence_id ASC);
```
- `channel_id` is the Partition Key: all messages for a given room are stored together on the same cluster replica nodes.
- `sequence_id` is the Clustering Column: messages are written to disk sorted contiguously. Fetching the last 50 messages (`WHERE channel_id = ? AND sequence_id > ? LIMIT 50`) is a fast sequential disk read.
- For extremely active channels (millions of messages), add a bucket (e.g. `(channel_id, year_month)`) to prevent giant partitions (> 100MB).

---

### Q5: How do you design a Presence Service (Online/Offline/Last Seen) for 100 million users without overloading databases?
**Answer**:
1. **Heartbeat & Ephemeral TTL**:
   - Clients send a lightweight ping every 30 seconds over the existing WebSocket connection.
   - The gateway writes to Redis: `SET user:{id}:presence "ONLINE" EX 60`.
   - If a client abruptly disconnects without TCP FIN (e.g., cell tower loss, tunnel), the Redis key expires after 60s, automatically marking them offline.
2. **Fan-out Optimization**:
   - Do NOT broadcast presence changes to all friends on every heartbeat.
   - Instead, only broadcast presence transitions (Offline $\to$ Online, Online $\to$ Offline).
   - Use lazy-loading: When user opens a friend list or chat room, query the presence state of only the visible users in that viewport.

---

### Q6: How do you implement "Message Read Receipts" (Single check, Double check, Blue check)?
**Answer**:
Instead of storing a read receipt record per message per user (which causes massive write amplification in group chats):
- Store a single monotonic pointer per user per channel: `LastReadSequenceNumber`.
  ```sql
  CREATE TABLE channel_read_pointers (
      channel_id uuid,
      user_id uuid,
      last_read_sequence bigint,
      PRIMARY KEY (channel_id, user_id)
  );
  ```
- When a user views the chat up to message #500, update their pointer to 500.
- Any message with `sequence_number <= 500` is implicitly read by that user.
- To render read status in a 1-on-1 chat: If Bob's `last_read_sequence >= Alice's message sequence`, show blue checkmark.

---

### Q7: How do you implement transient features like "Typing Indicators" without disk writes?
**Answer**:
Typing indicators are ephemeral, short-lived events that should never be persisted to a relational or NoSQL database.
1. When user types, client sends a lightweight WebSocket payload: `{"action": "typing", "channelId": "123"}`.
2. The gateway server throttles typing events from the client (e.g., at most once every 3 seconds).
3. The event is pushed directly to Redis Pub/Sub topic `channel:typing:{channelId}`.
4. Gateway servers holding connections for other members of that channel forward the typing event to their sockets.
5. The receiving client shows the typing bubble and starts a 4-second local animation timer; if no refresh arrives, it clears automatically.

---

### Q8: In LeetCode #39 (Combination Sum), why is the recursive call invoked with index `i` rather than `0` or `i + 1`?
**Answer**:
- If invoked with `0`, the algorithm would explore combinations in different permutations (e.g., both `[2, 3]` and `[3, 2]`), resulting in duplicate combinations.
- If invoked with `i + 1`, each element could only be used at most once, which fails the requirement that the same number may be chosen an unlimited number of times.
- By invoking with `i`, we allow the current candidate to be reused repeatedly while enforcing that future choices are $\ge$ the current candidate index, guaranteeing all generated combinations are strictly sorted and unique.

---

### Q9: In LeetCode #40 (Combination Sum II), why is sorting mandatory before backtracking?
**Answer**:
1. **Early Pruning**: When candidates are sorted, if `candidates[i] > remain`, we know every candidate after index `i` is also greater than `remain`. We can `break` out of the loop immediately rather than continuing useless iterations.
2. **Duplicate Subtree Elimination**: Sorting brings duplicate values together. The condition `if (i > start && candidates[i] == candidates[i - 1]) continue;` allows us to skip identical sibling branches at the current recursion level, preventing duplicate combinations in the output.

---

### Q10: How do you handle End-to-End Encryption (E2EE) in a distributed chat architecture?
**Answer**:
In E2EE (e.g. Signal Protocol / WhatsApp):
1. The server is treated as an untrusted, blind relay. The server only sees ciphertext and cryptographic metadata (pre-keys, identity keys, ratcheting parameters).
2. Users register public identity keys and one-time prekeys with a Key Distribution Center (KDC).
3. To start a chat, Alice fetches Bob's prekeys, initiates the Double Ratchet Algorithm, and generates a shared session key.
4. Alice encrypts the payload locally; the server stores and routes the encrypted blob. Only Bob's private device key can decrypt the message.
5. Search and media processing must be executed locally on the client device.
