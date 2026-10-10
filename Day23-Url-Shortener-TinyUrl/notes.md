# Day 23: Deep Dive — TinyURL System Design & Tree Reconstruction Mechanics

## 1. High-Level Architecture Overview

A URL shortener is a classic read-heavy distributed system requiring high availability, low latency redirects, and scalable write throughput.

```
[Client]
   │
   ▼
[Cloudflare / Edge CDN] ── (301/302 Cache for Hottest URLs)
   │
   ▼
[Load Balancer (Layer 7)]
   │
   ▼
[API Gateway / App Service]
   ├── [Snowflake ID Generator] (In-process, decentralized)
   ├── [Base62 Codec] (Deterministic bijection)
   │
   ├──► [L1/L2 Redis Cluster] (Cache-Aside, ~100GB hot working set)
   │
   └──► [Sharded Database (PostgreSQL / DynamoDB)] (Hash(ID) sharding)
```

---

## 2. Capacity Planning & Math Formulas

### 2.1 Traffic Estimation
- **Daily Writes**: $100\text{M}$ URLs/day.
- **Daily Reads**: $1\text{B}$ redirects/day (10:1 Read to Write ratio).
- **Average Write QPS**:
  $$\text{Write QPS} = \frac{100,000,000}{86,400\text{ s}} \approx 1,157.4\text{ requests/sec}$$
  - Peak Write QPS ($3\times$): $\approx 3,472\text{ QPS}$.
- **Average Read QPS**:
  $$\text{Read QPS} = \frac{1,000,000,000}{86,400\text{ s}} \approx 11,574.1\text{ requests/sec}$$
  - Peak Read QPS ($3\times$): $\approx 34,722\text{ QPS}$.

### 2.2 Storage Calculations
- **Per Record Sizing**:
  - `ID`: 8 bytes (64-bit Snowflake int)
  - `ShortCode`: 7-8 bytes
  - `LongUrl`: ~250 bytes
  - `CreatedAtUtc`: 8 bytes
  - `ExpiresAtUtc`: 8 bytes
  - `UserId`: 16 bytes (UUID)
  - `ClickCount`: 8 bytes
  - Index overhead & padding: ~195 bytes
  - **Total**: $500\text{ bytes}$ per record.
- **Yearly Growth**:
  $$100\text{M} \times 365 \times 500\text{ bytes} = 18.25\text{ TB/year}$$
- **5-Year Growth**: $18.25 \times 5 = 91.25\text{ TB}$.

### 2.3 Memory Cache Sizing (Pareto 80/20 Rule)
- 20% of daily reads generate 80% of traffic.
- Requests to cache daily: $1\text{B} \times 0.20 = 200\text{M}$ URLs.
- RAM Required:
  $$200,000,000 \times 500\text{ bytes} = 100,000,000,000\text{ bytes} = 100\text{ GB RAM}$$
- A Redis cluster configured with 4 nodes (32GB RAM each) comfortably caches the entire hot working set with sub-millisecond redirect latency.

---

## 3. ID Generation Architecture Comparison

| Mechanism | Coordination | Size | B-Tree Friendly | Collision Risk | Base62 Length |
|---|---|---|---|---|---|
| **DB Auto-Increment** | High (Single point of failure) | 64-bit | Yes | Zero | 7 chars |
| **UUID v4** | None (Decentralized) | 128-bit | No (Random index thrashing) | Negligible | 22 chars (Too long!) |
| **MD5 / SHA-256 Truncation** | None | Hash | No | High (requires check-and-retry) | 7 chars |
| **Ticket Server (Flickr)** | Medium (Central MySQL) | 64-bit | Yes | Zero | 7 chars |
| **Twitter Snowflake** | **Zero (In-process bitwise)** | **64-bit** | **Yes (k-sorted by time)** | **Zero** | **7-11 chars** |

### Twitter Snowflake Bit Allocation
```
 1 bit  41 bits (Timestamp in ms)        5 bits (DC)  5 bits (Worker) 12 bits (Seq)
┌──────┬────────────────────────────────┬────────────┬───────────────┬─────────────┐
│  0   │ 011010101110010110010101101... │   00010    │     00101     │ 000000000001│
└──────┴────────────────────────────────┴────────────┴───────────────┴─────────────┘
```
- **$2^{41}\text{ ms} \approx 69.7\text{ years}$** longevity from epoch.
- Up to $32 \times 32 = 1,024$ worker instances.
- Up to $4,096$ IDs per millisecond per worker instance ($4.096\text{M IDs/sec}$).

---

## 4. HTTP 301 vs. 302 / 307 Redirect Trade-Offs

- **HTTP 301 (Moved Permanently)**:
  - *Browser behavior*: Browsers cache the redirect permanently. Subsequent clicks navigate straight to the long URL without contacting the TinyURL server.
  - *Pros*: Extreme reduction in server load and egress bandwidth.
  - *Cons*: **Cannot track click analytics** after the initial visit!
- **HTTP 302 (Found) / HTTP 307 (Temporary Redirect)**:
  - *Browser behavior*: Browsers re-request the short URL every time.
  - *Pros*: **100% accurate click analytics**, referrer tracking, and user geography telemetry.
  - *Cons*: Every click hits the TinyURL infrastructure, requiring Redis caching to maintain sub-10ms response SLAs.

---

## 5. DSA: Reconstructing Binary Tree from Preorder & Inorder Traversal

### 5.1 Traversal Properties
- **Preorder**: `[Root, Left Subtree..., Right Subtree...]` $\implies$ The first element is always the subtree root!
- **Inorder**: `[Left Subtree..., Root, Right Subtree...]` $\implies$ The root splits elements into left and right subtrees!

### 5.2 Algorithm & Complexity
1. Maintain `preIndex` starting at 0.
2. Select `rootVal = preorder[preIndex++]`.
3. Locate `rootVal` in the inorder array at index `inRoot`.
4. Left subtree nodes span inorder indices $[inStart, inRoot - 1]$.
5. Right subtree nodes span inorder indices $[inRoot + 1, inEnd]$.
6. Recursively construct `root.left`, then `root.right`.

- **Optimization**: By building a hash map of `value -> inorderIndex` in $O(N)$ upfront, step 3 executes in $O(1)$ instead of $O(N)$ linear scans.
- **Overall Complexity**:
  - Time: $O(N)$
  - Space: $O(N)$ for the recursion stack and dictionary.
