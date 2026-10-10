# Senior & Staff Engineering Interview Questions: Day 23 — URL Shortener (TinyURL) & Tree Reconstruction

### Q1: Walk through the capacity planning for a global URL Shortener handling 100M writes/day and 1B reads/day.
**Answer:**
1. **QPS Estimation**:
   - Write QPS: $\frac{100,000,000}{86,400} \approx 1,157\text{ req/sec}$ (Peak $3\times \approx 3,500\text{ QPS}$).
   - Read QPS: $\frac{1,000,000,000}{86,400} \approx 11,574\text{ req/sec}$ (Peak $3\times \approx 35,000\text{ QPS}$).
2. **Storage Estimation**:
   - Record size: 500 bytes (ID, short URL, long URL, timestamps, user ID, click stats).
   - Daily storage: $100\text{M} \times 500\text{ bytes} = 50\text{ GB/day}$.
   - 1-Year storage: $50\text{ GB} \times 365 \approx 18.25\text{ TB}$.
   - 5-Year storage: $18.25\text{ TB} \times 5 \approx 91.25\text{ TB}$.
3. **Cache Sizing (Pareto 80/20 Rule)**:
   - 20% of read URLs generate 80% of read traffic.
   - Cached URLs per day: $1\text{B} \times 0.20 = 200\text{M}$ URLs.
   - Cache RAM required: $200\text{M} \times 500\text{ bytes} = 100\text{ GB RAM}$.
4. **Bandwidth**:
   - Ingress: $1,157\text{ writes/sec} \times 500\text{ bytes} \approx 4.6\text{ Mbps}$.
   - Egress: $11,574\text{ reads/sec} \times 500\text{ bytes} \approx 46.3\text{ Mbps}$.

---

### Q2: Why is Twitter Snowflake preferred over UUID v4 or database auto-increment sequences for ID generation?
**Answer:**
- **Auto-Increment Sequences**: Create a central database bottleneck. Scaling writes across sharded databases requires multi-master synchronization or ticket servers (Flickr approach), introducing single points of failure.
- **UUID v4 (128-bit)**:
  - Generates 36-character hexadecimal strings. Base62 encoding a 128-bit UUID requires 22 characters, which completely defeats the purpose of a "short" URL!
  - UUIDs are completely unordered (random), causing severe B-Tree page splits and disk fragmentation when indexing in relational databases.
- **Twitter Snowflake (64-bit)**:
  - Generates 64-bit integers locally in memory without any network synchronization.
  - Encodes into short 7-8 character Base62 strings.
  - The leading 41 bits represent millisecond timestamps, ensuring IDs are **k-sorted** (monotonically increasing over time), which maximizes B-Tree index append efficiency and eliminates page splits.

---

### Q3: Why is Base62 encoding used instead of Base64 or MD5/SHA-256 hash truncation?
**Answer:**
- **Base64**: Uses characters `+`, `/`, and `=` (padding). In URLs, `+` is interpreted as space, `/` is a path separator, and `=` is a query parameter delimiter. Using Base64 requires URL percent-encoding (`%2B`, `%2F`), making short links longer and fragile.
- **Base62**: Uses strictly `[0-9a-zA-Z]` (62 alphanumeric characters), which are guaranteed to be URL-safe across all protocols and browsers without escape characters.
- **MD5/SHA-256 Truncation**: Hashing a long URL and taking the first 7 characters introduces hash collisions (birthday paradox). Handling collisions requires querying the database on every write and appending counter salts, adding database roundtrips.
- **Base62 Bijection**: Base62 is a positional numeral system conversion of a unique 64-bit integer. Because every Snowflake ID is guaranteed unique, the Base62 string has **zero collision risk** by mathematical definition.

---

### Q4: Should a URL shortener return HTTP 301 or HTTP 302/307 redirects?
**Answer:**
- **HTTP 301 (Moved Permanently)**:
  - Browsers cache the redirect locally. Subsequent clicks go directly to the target URL without contacting the TinyURL server.
  - *Advantage*: Dramatically cuts server traffic, network bandwidth, and hosting cost.
  - *Disadvantage*: **Cannot record click analytics** (referrers, device types, timestamps) after the initial visit.
- **HTTP 302 (Found) / HTTP 307 (Temporary Redirect)**:
  - Browsers re-query the TinyURL server on every single click.
  - *Advantage*: Enables real-time, 100% accurate click tracking, geographic analysis, and dynamic link routing.
- **Industry Standard**: Modern URL shorteners (Bitly, TinyURL) return **HTTP 302/307** because analytics are core to their business model, relying on distributed Redis caching to keep redirect latency $< 10\text{ms}$.

---

### Q5: How would you shard the database for a URL Shortener storing 100TB of data?
**Answer:**
- **Sharding Key**: `Hash(SnowflakeId) % NumberOfShards`.
- **Why ID instead of UserID or ShortCode?**
  - If sharding by `UserId`, anonymous URLs or non-logged-in users cannot be sharded evenly, and resolving a short URL without knowing the user ID would require broadcast queries across all shards!
  - Because `ShortCode` is simply the Base62 representation of `SnowflakeId`, we can instantly decode `ShortCode` back to `SnowflakeId` on the read path:
    $$\text{ShardId} = \text{Base62Decode}(\text{ShortCode}) \pmod N$$
  - This allows read and write requests to route directly to the single target database shard in $O(1)$ time without scatter-gather overhead.

---

### Q6: How do you prevent URL enumeration and scraping attacks?
**Answer:**
- **The Vulnerability**: If IDs are sequential (e.g. 10001, 10002), an attacker can easily iterate through Base62 codes (`Base62(10001)`, `Base62(10002)`) to scrape all private internal URLs created by users.
- **Mitigation**:
  1. **Bit Shuffling / Feistel Cipher**: Apply a reversible pseudo-random permutation or lightweight Feistel cipher to the Snowflake ID before Base62 encoding. This preserves mathematical 1-to-1 bijection while making sequential IDs look completely random.
  2. **Rate Limiting**: Apply sliding window rate limits on redirect endpoints per IP to halt automated crawlers.
  3. **Custom Aliases Protection**: Enforce authentication and reserved keyword blacklists (e.g., `admin`, `api`, `login`, `health`).

---

### Q7: How do you handle clock backwards drift in a distributed Snowflake generator?
**Answer:**
- **The Issue**: Network Time Protocol (NTP) adjustments can occasionally shift the machine clock backward by several milliseconds. Generating IDs during a backward jump could produce duplicate IDs.
- **Mitigations**:
  1. **Spin Wait for Small Drift**: If `now < lastTimestamp` and `lastTimestamp - now <= 5ms`, spin-wait in memory until the clock catches up to `lastTimestamp`.
  2. **Rejection on Major Drift**: If drift exceeds 5ms, reject ID generation and trigger alerts to avoid data corruption.
  3. **Monotonic Clocks**: Use clock sources that guarantee monotonicity (e.g. `Stopwatch.GetTimestamp()` or NTP slew mode instead of step mode, which gradually adjusts clock speed rather than jumping backward).

---

### Q8: What caching topology should be used to achieve sub-10ms redirect latency at 35,000 QPS?
**Answer:**
- **Two-Tier Cache Architecture**:
  1. **L1 In-Memory Cache (Process RAM)**: Holds the top 1,000 ultra-hot URLs (e.g. viral campaign links) inside ASP.NET Core memory cache (`IMemoryCache`). Lookups execute in sub-microsecond time with zero network overhead.
  2. **L2 Distributed Cache (Redis Cluster)**: 100 GB cluster sharded via consistent hashing holding the top 20% of daily read URLs with a 24-hour TTL. Lookups take $< 1\text{ms}$.
- **Cache-Aside Pattern**:
  - Read: Check L1 $\to$ Check L2 $\to$ Fallback to DB shard $\to$ Populate L2 and L1.
  - Hot-key protection: Single-flight mutex to avoid cache stampedes when hot keys expire.

---

### Q9: How would you detect and block malicious phishing links submitted to TinyURL?
**Answer:**
1. **Synchronous Validation**:
   - Query Google Safe Browsing API or Cloudflare Radar API during URL creation.
   - If marked malicious, immediately reject write with HTTP 400 Bad Request.
2. **Asynchronous Deep Scanning**:
   - Push newly created URLs to a message queue (Kafka / Redis Streams).
   - Headless browser workers follow redirects, inspect domain reputation, analyze landing page SSL certificates, and run ML phishing classifiers.
   - If flagged, mark the record `IsSuspicious = true` in DB and purge from Redis.
3. **Interstitial Warning Page**:
   - For newly created, unverified domains, display a brief 3-second security warning page: *"You are leaving TinyURL to visit example.com. Proceed with caution."*

---

### Q10: Explain LeetCode #105 (Construct Binary Tree from Preorder & Inorder). How does the hash map optimize runtime from $O(N^2)$ to $O(N)$?
**Answer:**
- **Core Principle**:
  - `preorder`: The first element is always the subtree root.
  - `inorder`: The root element splits the tree into left and right subtrees.
- **Naive Pitfall**:
  - In each recursive step, scanning the inorder array linearly to find `rootVal` takes $O(N)$ time. For an unbalanced or skewed tree of depth $N$, this produces $O(N^2)$ runtime.
- **Hash Map Optimization**:
  - Build `inorderMap[val] = index` once in $O(N)$ time before starting recursion.
  - During recursive calls:
    ```csharp
    int inRoot = inorderMap[rootVal]; // O(1)
    root.left = Build(inStart, inRoot - 1);
    root.right = Build(inRoot + 1, inEnd);
    ```
  - Lookups run in $O(1)$ time. With $N$ total recursive calls (one per tree node), total execution time drops to optimal $O(N)$ with $O(N)$ auxiliary space.
