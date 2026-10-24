# Technical Notes: Distributed Job Scheduler Architecture

### 1. Leader Election & Monotonic Fencing Tokens
In a distributed scheduler cluster, only the leader node queries the job database and enqueues due tasks to worker queues:
- **Lease Lock**: Leader acquires a time-bounded lease in Redis or Zookeeper/etcd.
- **Fencing Token**: To prevent a GC-paused or partition-delayed former leader from corrupting state, each leadership election increments a strictly monotonic 64-bit integer (`FencingToken`). Database updates reject writes from stale fencing tokens.

### 2. Worker Node Liveness & Failover
Worker nodes maintain periodic heartbeats (e.g., every 5s). When a worker fails:
1. Heartbeat monitor detects elapsed time exceeds timeout threshold (e.g., 15s).
2. Worker node is evicted from active pool.
3. Unacknowledged in-flight tasks assigned to the dead worker are re-enqueued to the pending queue for other workers to claim.

### 3. LeetCode #42: Trapping Rain Water
- **Two Pointers**: Maintain `leftMax` and `rightMax`. At each step, if `height[left] < height[right]`, the water level is bounded by `leftMax`. If `height[left] >= leftMax`, update `leftMax`; otherwise, add `leftMax - height[left]`. Advance `left`. Symmetric for `right`. Time: $\mathcal{O}(N)$, Space: $\mathcal{O}(1)$.
- **Monotonic Stack**: Keep a stack of decreasing bar indices. When a taller bar is encountered, pop the bottom, calculate the horizontal layer of trapped water between current bar and the new stack top: `boundedHeight = min(height[left], height[right]) - height[bottom]`, `width = right - left - 1`. Time: $\mathcal{O}(N)$, Space: $\mathcal{O}(N)$.
