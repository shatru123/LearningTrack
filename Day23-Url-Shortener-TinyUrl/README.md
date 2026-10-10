# Day 23: System Design — URL Shortener (TinyURL) & DSA Tree Reconstruction

Comprehensive systems design lab implementing a distributed URL Shortener (TinyURL) service, capacity planning models, Twitter Snowflake 64-bit ID generation, Base62 bi-directional encoding, cache-aside Redis layer, database sharding, and LeetCode #105 binary tree reconstruction.

---

## 🎯 Architecture & Objectives

1. **Capacity Planning & Workload Estimation**:
   - Workload: 100M daily writes, 1B daily reads (10:1 read-to-write ratio).
   - Average Throughput: ~1,157 Write QPS, ~11,574 Read QPS (Peak 3x: ~3,500 Write QPS, ~35,000 Read QPS).
   - Storage Sizing: ~500 bytes/record $\to$ 18.25 TB/year, 91.25 TB over 5 years.
   - Cache Sizing: 20% of daily reads cached (Pareto 80/20 rule) $\to$ 100 GB RAM working set.
2. **Twitter Snowflake 64-bit Distributed ID Generation**:
   - Layout: 1 sign bit + 41 millisecond timestamp bits (69-year span) + 5 datacenter bits + 5 worker bits + 12 sequence bits.
   - High Throughput: Generates up to 4,096 IDs per millisecond per node (4.096M IDs/sec) with zero network lock contention.
   - Clock Skew Defense: Spin-waits or halts generation if system clock drifts backwards.
3. **Bi-Directional Base62 Codec**:
   - Positional notation across `[0-9a-zA-Z]` (62 chars) providing URL safety without escape sequences.
   - Mathematical 1-to-1 bijection with zero hash collisions.
4. **TinyURL Service & Database Sharding**:
   - Partitioning by `Hash(SnowflakeId) % ShardCount`.
   - Two-tier cache-aside layer with configurable TTL and auto-purging.
   - Collision-safe custom alias assignment.
   - Click analytics telemetry.
5. **DSA Tree Reconstruction**:
   - **LeetCode #105 (Construct Binary Tree from Preorder & Inorder Traversal)**: Divide-and-conquer recursion leveraging an $O(1)$ in-order hash map lookup in optimal $O(N)$ time and $O(N)$ space.

---

## 📁 Project Structure

```
Day23-Url-Shortener-TinyUrl/
├── src/
│   └── Day23.TinyUrl/
│       ├── Day23.TinyUrl.csproj
│       ├── CapacityPlanner.cs               # Capacity & SLA calculation models
│       ├── SnowflakeIdGenerator.cs          # 64-bit Twitter Snowflake generator
│       ├── Base62Codec.cs                   # Loss-less Base62 encoder/decoder
│       ├── TinyUrlService.cs                # Sharded service with Cache-Aside
│       └── TreeReconstructionSolvers.cs     # LC #105 Preorder + Inorder solver
├── tests/
│   └── Day23.TinyUrl.Tests/
│       ├── Day23.TinyUrl.Tests.csproj
│       └── Day23Tests.cs                    # 10 unit tests covering system & DSA
├── README.md
├── notes.md                                 # Deep dive architectural notes
├── interview-questions.md                   # 10 Senior/Staff interview Q&As
└── index.html                               # Standalone offline web guide
```

---

## 🧪 Running Tests

```bash
dotnet test tests/Day23.TinyUrl.Tests/Day23.TinyUrl.Tests.csproj
```
