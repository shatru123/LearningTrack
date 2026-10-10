# Day 22: Deep Dive — Distributed Rate Limiting & BST Traversal Mechanics

## 1. Rate Limiting Algorithm Taxonomy & Trade-Offs

Rate limiting controls the consumption of resources by restricting the frequency of incoming operations across a given temporal window.

| Algorithm | Memory Complexity | Time Complexity | Burst Support | Smooth Traffic Outflow | Edge Spike Vulnerability |
|---|---|---|---|---|---|
| **Token Bucket** | $O(1)$ per client | $O(1)$ | Yes (up to capacity $C$) | Moderate | Low |
| **Leaky Bucket** | $O(1)$ or $O(QueueSize)$ | $O(1)$ | No (enforces fixed leak rate $R$) | Yes (strict constant flow) | Zero |
| **Fixed Window Counter** | $O(1)$ per client | $O(1)$ | Yes (within slot) | No (uneven burst at boundaries) | High ($2\times$ burst across boundaries) |
| **Sliding Window Log** | $O(M)$ where $M$ is request count | $O(\log M)$ to $O(M)$ | Yes | High | Zero (100% accurate) |
| **Sliding Window Counter** | $O(1)$ (2 counters per client) | $O(1)$ | Yes | High | Negligible ($\le 0.05\%$ variance) |

---

## 2. In-Depth Algorithmic Mechanics

### 2.1 Token Bucket
- **Mechanics**: A bucket holds at most $C$ tokens. Tokens refill continuously at rate $R$ tokens/second.
- **Refill Formula**:
  $$\text{Tokens}_{\text{new}} = \min(C, \text{Tokens}_{\text{old}} + (t_{\text{now}} - t_{\text{last}}) \times R)$$
- **Why it is widely used (AWS, Stripe)**: Supports natural bursts (e.g., page load with 10 assets) while bounding sustained average throughput.

### 2.2 Leaky Bucket
- **Mechanics**: Implemented as a FIFO queue of capacity $C$. Incoming requests enter the queue; a worker processes or transmits requests at a steady rate $R$. If the queue is full, new requests are dropped.
- **Ideal Use Case**: Egress traffic shaping (e.g. video streaming, webhook dispatchers to external third-party APIs).

### 2.3 Fixed Window Counter Boundary Burst Problem
- **The Vulnerability**: Consider a limit of 100 requests per 1-minute window ($12:00:00 - 12:01:00$).
  - An attacker sends 100 requests at $12:00:59$.
  - At $12:01:00$, the new window begins with counter reset to 0.
  - The attacker sends another 100 requests at $12:01:01$.
  - **Result**: 200 requests pass in a 2-second interval, doubling the intended maximum capacity!

### 2.4 Sliding Window Counter (Cloudflare Hybrid)
- Avoids storing individual timestamps by estimating the overlap:
  $$\text{Count}_{\text{est}} = \text{Count}_{\text{current}} + \text{Count}_{\text{prev}} \times \left(1 - \frac{t - t_{\text{current\_start}}}{\text{WindowSize}}\right)$$
- If a client made 100 requests in the previous 1-minute window, and at 15 seconds into the current minute makes another request with current count 10:
  $$\text{Count}_{\text{est}} = 10 + 100 \times \left(1 - \frac{15}{60}\right) = 10 + 75 = 85$$
- Memory footprint is strictly two numbers ($O(1)$) per client, making it the premier choice for multi-tenant cloud gateways.

---

## 3. Distributed Redis Rate Limiting Implementation

In a distributed cluster of web API servers, state cannot reside solely in process memory without causing traffic divergence (a client hitting server A, then server B).

### 3.1 Atomic Lua Script for Sliding Window Counter in Redis
```lua
-- KEYS[1]: Current window key (e.g., rl:client123:28800)
-- KEYS[2]: Previous window key (e.g., rl:client123:28799)
-- ARGV[1]: Max limit
-- ARGV[2]: Current timestamp in seconds
-- ARGV[3]: Window size in seconds

local curr_key = KEYS[1]
local prev_key = KEYS[2]
local limit = tonumber(ARGV[1])
local now = tonumber(ARGV[2])
local window = tonumber(ARGV[3])

local curr_count = tonumber(redis.call('get', curr_key) or "0")
local prev_count = tonumber(redis.call('get', prev_key) or "0")

local elapsed = now % window
local weight = (window - elapsed) / window
local est_count = curr_count + (prev_count * weight)

if est_count + 1 > limit then
    return 0 -- Rejected
else
    redis.call('incr', curr_key)
    redis.call('expire', curr_key, window * 2)
    return 1 -- Allowed
end
```

### 3.2 Key Resolution Hierarchy
1. **Authenticated Users**: `user:{user_id}` (prevents users from bypassing limits by rotating IP).
2. **API Clients**: `apikey:{sha256(api_key)}`.
3. **Public / Unauthenticated**: `ip:{client_ip}` (with special handling for `X-Forwarded-For` through trusted proxies to prevent header spoofing).

---

## 4. DSA: BST In-Order Traversal Invariants

### 4.1 In-Order Traversal Property
- In any valid Binary Search Tree:
  $$\text{In-Order}(T) = \text{Left} \to \text{Node} \to \text{Right} \implies \text{Strictly Ascending Sequence}$$
- If and only if $v_i < v_{i+1}$ for all sequential visits, the tree is a valid BST.

### 4.2 LeetCode #98 (Validate BST)
- Common Pitfall: Checking only `node.left.val < node.val < node.right.val` locally is insufficient. A node in a right subtree might be smaller than a grandparent node!
- Solution: Maintain global interval bounds $(\text{min}, \text{max})$ for each recursive branch, using `long?` to avoid integer overflow when root contains `int.MinValue` or `int.MaxValue`.

### 4.3 LeetCode #230 (Kth Smallest Element in BST)
- Rather than traversing all $N$ nodes to construct an array, an iterative stack traversal enables **early exit**:
  - Traverse left pushing to stack until leaf.
  - Pop, decrement $k$.
  - When $k == 0$, immediately return the current value!
  - Time Complexity: $O(H + k)$, where $H$ is tree height. Space: $O(H)$.
