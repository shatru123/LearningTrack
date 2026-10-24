# Day 49: Staff & Principal Interview Questions

### Q1: How do you design a distributed job scheduler to guarantee that a recurring cron job never runs concurrently on two nodes?
**Answer**:
1. **Leader-Follower Clustering**: Run a leader election (via etcd, Consul, or Redis Redlock with lease). Only the elected leader generates trigger instances.
2. **Database Row-Level Lock**: The leader updates the job record using `SELECT ... FOR UPDATE` or conditional write (`UPDATE Jobs SET Status = 'Running', Version = Version + 1 WHERE Id = @Id AND Version = @ExpectedVersion`).
3. **Fencing Tokens**: Every job execution ticket includes an epoch/fencing token. Workers check this token against a distributed lock before starting execution.
4. **Idempotency Key**: Execution key `hash(jobId, scheduledFireTimeUtc)` ensures deduplication if multiple instances accidentally trigger.

---

### Q2: What happens when the leader node suffers a 30-second Stop-the-World GC pause?
**Answer**:
During the GC pause, the leader cannot send heartbeat pings. The cluster considers the leader dead, elects a new leader, and issues a higher fencing token ($T_2 > T_1$). When the paused leader awakens:
- It attempts to write state using its stale fencing token ($T_1$).
- Storage / worker queues reject the write because $T_1 < T_2$.
- The old leader catches the fencing token mismatch exception, realizes it has lost leadership, and demotes itself to a follower.

---

### Q3: How do you implement delayed tasks (e.g., "send email in 3 days") at scale?
**Answer**:
1. **Hashed / Hierarchical Timing Wheel**: Circular buffer with time slots (seconds, minutes, hours). Tasks are placed in slots matching `(targetTime - currentTime) % wheelSize`. Offers $\mathcal{O}(1)$ insertion and expiration check.
2. **Redis Sorted Sets (ZSET)**: Store `member = taskId`, `score = targetTimestampUtc`. A background poller runs `ZRANGEBYSCORE tasks -inf <now>` to fetch due jobs with $\mathcal{O}(\log N + M)$ efficiency.
3. **Database Partitioning**: Partition scheduled jobs table by fire date (`fire_date`), querying only today\'s partition with an index on `fire_time`.

---

### Q4: Explain the difference between At-Least-Once and At-Most-Once execution semantics in job scheduling.
**Answer**:
- **At-Least-Once**: Scheduler dispatches the task and waits for an explicit ACK. If the worker crashes mid-execution or the ACK is lost over the network, the scheduler re-dispatches the task. Requires workers to be strictly **idempotent**.
- **At-Most-Once**: Scheduler marks the task as completed immediately upon dispatching it to the worker. If the worker crashes, the task is never re-run. Suitable only for non-critical telemetry or cache warmups.
Enterprise financial workflows use At-Least-Once with application-level idempotency tables.

---

### Q5: What are Misfire Policies in Quartz/Hangfire, and when do you use each?
**Answer**:
A misfire occurs when a job was scheduled to run at time $T$, but all worker threads were exhausted or the scheduler was down until $T + \Delta$.
- **Fire Now**: Immediately execute the missed job, then resume normal schedule (e.g. daily database backup).
- **Ignore / Reschedule Next**: Discard the missed execution and wait for the next scheduled cron tick (e.g. real-time stock ticker snapshot).
- **Fire All Missed**: Sequentially replay all missed triggers (e.g. hourly financial ledger reconciliation).

---

### Q6: How do you prevent the "Thundering Herd" problem when 10,000 jobs are scheduled at midnight (00:00:00)?
**Answer**:
1. **Cron Jitter / Staggering**: Add randomized jitter to trigger times (e.g., $00:00 \pm 180$ seconds) across non-time-critical jobs.
2. **Priority Rate-Limiting Queues**: Enqueue tasks into high, medium, and low priority queues, and throttle worker pull rates using token buckets.
3. **Backpressure**: Workers pull tasks only when they have free concurrency slots, preventing queue buffer overflows.

---

### Q7: Compare Quartz.NET, Hangfire Pro, and Temporal/Cadence for enterprise scheduling.
**Answer**:
- **Quartz.NET**: Mature, highly configurable cron engine; stores triggers in SQL tables via ADO.NET; lacks native distributed workflows or visual dashboard out of the box.
- **Hangfire Pro**: Excellent ASP.NET Core integration; Redis/SQL storage; beautiful web dashboard; great for fire-and-forget, delayed, and recurring jobs; weaker on long-running complex multi-step orchestration.
- **Temporal / Cadence**: Event-sourced code-as-configuration workflow orchestrator; resilient to process crashes, network partitions, and month-long pauses; superior for distributed state machine workflows and human-in-the-loop tasks.

---

### Q8: How do you monitor worker node liveness without overwhelming the database with heartbeat writes?
**Answer**:
- Avoid writing heartbeats directly to relational databases.
- Use **Redis Key Expiry**: Worker sets `SET worker:id:heartbeat "alive" EX 10` every 3 seconds. If the key expires, Redis Keyspace Notifications (`__keyevent@0__:expired`) alert the scheduler.
- Alternatively, use a lightweight gossip protocol (e.g. SWIM / Consul) where workers ping peer subsets, reducing $O(N)$ central load to $O(1)$ per node.

---

### Q9: Contrast Two-Pointer vs Monotonic Stack for LeetCode #42 Trapping Rain Water.
**Answer**:
- **Two Pointers**:
  - Traversal: Converges from both ends towards the highest peak.
  - Computation: Calculates water column-by-column vertically ($leftMax - height[left]$).
  - Complexity: $\mathcal{O}(N)$ time, $\mathcal{O}(1)$ auxiliary space.
- **Monotonic Stack**:
  - Traversal: Scans left to right once.
  - Computation: Calculates water layer-by-layer horizontally bounded by left and right walls.
  - Complexity: $\mathcal{O}(N)$ time, $\mathcal{O}(N)$ auxiliary space.

---

### Q10: Can Trapping Rain Water be generalized to 3D terrain (LeetCode #407)? What algorithm is required?
**Answer**:
Yes, in 3D terrain (height map grid $M 	imes N$), water spills over the lowest boundary cell.
- The 2D two-pointer approach fails because boundaries form an arbitrary 2D perimeter rather than just two endpoints.
- **Algorithm**: Use a **Min-Heap (Priority Queue)**.
  1. Push all outer boundary cells into the min-heap and mark them visited.
  2. While the heap is not empty, pop the lowest cell $(x, y)$ with height $H$.
  3. Explore all 4 adjacent unvisited neighbors. If neighbor height $H_{neighbor} < H$, water trapped $= H - H_{neighbor}$.
  4. Push neighbor into the heap with effective height $\max(H, H_{neighbor})$.
- Complexity: $\mathcal{O}(M \cdot N \log(M \cdot N))$ time, $\mathcal{O}(M \cdot N)$ space.
