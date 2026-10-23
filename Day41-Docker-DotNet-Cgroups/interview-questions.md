# Day 41: Senior & Staff Interview Questions & Answers

### Q1: Why does a .NET container get terminated with exit code 137 (OOMKilled) in Kubernetes even when managed memory appears low?
**Answer**:
Exit code 137 indicates the Linux kernel terminated the process with `SIGKILL` (signal 9: $128 + 9 = 137$) because cgroup memory limit was breached.
- The .NET Garbage Collector only manages the **Managed GC Heap**.
- A .NET process additionally consumes **Native Memory**:
  - Thread stacks (by default, 1 MB virtual memory per thread in Linux).
  - JIT compilation code cache and type metadata.
  - Native socket buffers and libuv / socket network buffers.
  - Native memory from third-party libraries (e.g. gRPC, OpenTelemetry, image processing, OpenSSL).
- If container limit is 500MB and GC heap is 380MB, but 150 threads spawn or native buffers surge by 150MB, total memory exceeds 500MB and the cgroup OOM killer instantly kills the pod.

---

### Q2: What is `DOTNET_GCHeapHardLimitPercent` and how does it prevent container OOMKills?
**Answer**:
`DOTNET_GCHeapHardLimitPercent` configures the maximum percentage of the container's cgroup memory limit that the .NET Garbage Collector is allowed to allocate for the managed heap.
- In a container with 1GB limit, configuring `DOTNET_GCHeapHardLimitPercent=75` instructs the GC that the managed heap must never exceed 750MB.
- As the heap approaches 750MB, the GC triggers aggressive Gen 2 collections rather than continuing to expand.
- The remaining 250MB (25%) is permanently preserved as a safety cushion for unmanaged memory, thread stacks, and OS page cache, preventing kernel OOMKills.

---

### Q3: How do Server GC and Workstation GC behave inside Docker containers with CPU limits?
**Answer**:
- **Server GC (`DOTNET_gcServer=1`)**:
  - Creates a dedicated GC heap and a dedicated GC background thread for *each* logical CPU core recognized by the runtime.
  - In a large multi-core node where a container is constrained to `resources.limits.cpu: "1"`, if .NET reads host cores (e.g. 64 cores), Server GC allocates 64 separate heaps and threads! This causes catastrophic thread context switching and massive memory overhead.
  - .NET 8 correctly parses cgroup CPU quotas (`cpu.cfs_quota_us / cpu.cfs_period_us`).
  - *Recommendation*: For small containers ($\le 1$ core or $\le 512$MB RAM), switch to Workstation GC (`DOTNET_gcServer=0`). For $\ge 2$ cores, Server GC offers superior throughput.

---

### Q4: Why is Docker layer order critical for .NET builds, and how does copying `.csproj` first optimize CI/CD pipelines?
**Answer**:
Docker caches intermediate image layers based on content hashes.
- If you run `COPY . .` followed by `RUN dotnet restore`, any change to a single comment or `.cs` file invalidates the copy layer and **forces `dotnet restore` to re-execute from scratch**, downloading hundreds of megabytes of NuGet packages on every commit.
- By structuring the Dockerfile as:
  ```dockerfile
  COPY *.csproj .
  RUN dotnet restore
  COPY . .
  RUN dotnet build --no-restore
  ```
  Docker caches the `restore` layer. As long as `.csproj` has not changed, NuGet restore completes in 0.0 seconds from local Docker layer cache.

---

### Q5: What are the security benefits of using Ubuntu Chiseled containers (`aspnet:8.0-jammy-chiseled`) over Alpine or standard Ubuntu?
**Answer**:
- **Standard Ubuntu/Debian**: Includes package managers (`apt`, `dpkg`), shells (`bash`, `sh`), coreutils (`curl`, `wget`, `cat`, `ls`), and dozens of unnecessary packages, creating a large CVE attack surface and giving attackers tools for lateral movement if RCE occurs.
- **Alpine Linux**: Uses `musl libc`, which can cause compatibility quirks with P/Invoke native binaries, ICU globalization issues, and slower allocator performance compared to `glibc`.
- **Ubuntu Chiseled**:
  - Contains only the exact binary dependencies required by the .NET runtime (including `glibc` and `OpenSSL`).
  - Contains **zero package managers**, **zero shells**, and **zero utilities**.
  - Attackers cannot invoke `curl | bash` or spawn interactive reverse shells.
  - Yields near-zero vulnerability scan results in Trivy/Docker Scout while retaining standard `glibc` performance.

---

### Q6: Why should production containers always run under `USER app` instead of `root`?
**Answer**:
By default, Docker containers run as UID 0 (`root`), which corresponds to root on the underlying host Linux kernel unless user namespaces are enabled.
- If a security vulnerability (e.g. container escape via kernel privilege escalation, runc CVE-2024-21626, or volume mount abuse) is exploited in a root container, the attacker immediately gains root access to the entire host Kubernetes node.
- Running as an unprivileged user (in .NET 8: `USER app`, UID 1654) limits the blast radius: the compromised process cannot access host hardware, read other containers' data, or modify system files.

---

### Q7: How does .NET 8 handle cgroups v1 vs cgroups v2 in modern Linux kernels (e.g. RHEL 9, Ubuntu 22.04+)?
**Answer**:
- **cgroups v1**: Multi-hierarchy system where memory, cpu, and blkio were managed under separate `/sys/fs/cgroup/memory` and `/sys/fs/cgroup/cpu` controllers.
- **cgroups v2**: Unified single-hierarchy controller (`/sys/fs/cgroup/memory.max`, `memory.current`, `cpu.max`).
- .NET 8 automatically detects whether the kernel is running cgroups v1 or v2 at runtime startup:
  - If cgroups v2 is active, it reads `memory.max` for container limits.
  - It dynamically adjusts GC heap boundaries and thread pool sizing without requiring manual flags or environment overrides.

---

### Q8: In LeetCode #70 (Climbing Stairs), why is the solution equivalent to the Fibonacci sequence?
**Answer**:
To reach step $n$, the climber must take either:
1. A 1-step leap from step $n - 1$, or
2. A 2-step leap from step $n - 2$.
These two sets of paths are mutually exclusive and collectively exhaustive.
Therefore, the number of distinct ways to reach step $n$ is:
$$\text{ways}(n) = \text{ways}(n - 1) + \text{ways}(n - 2)$$
With base cases $\text{ways}(1) = 1$ and $\text{ways}(2) = 2$, this matches the Fibonacci sequence shifted by one index. Solved iteratively in $O(N)$ time and $O(1)$ memory.

---

### Q9: In LeetCode #746 (Min Cost Climbing Stairs), what is the optimal DP state transition?
**Answer**:
Let $dp[i]$ be the minimum cost to reach step $i$.
- You can reach step $i$ from step $i - 1$ (paying $cost[i - 1]$) or from step $i - 2$ (paying $cost[i - 2]$).
- Recurrence:
  $$dp[i] = \min(dp[i - 1] + cost[i - 1],\; dp[i - 2] + cost[i - 2])$$
- Since you can start at index 0 or index 1, the base cases are $dp[0] = 0$ and $dp[1] = 0$.
- To reach the top of the staircase ($i = \text{cost.Length}$), only the previous two values are needed, allowing $O(1)$ rolling variable space.

---

### Q10: What are best practices for Kubernetes Liveness and Readiness Probes for a containerized .NET service?
**Answer**:
1. **Separate Liveness and Readiness Endpoints**:
   - `/healthz/live`: Checks if the process is responsive (returns 200). Should NEVER check external dependencies (DB, Redis). If DB is down, failing liveness causes Kubernetes to restart the pod, causing a catastrophic restart storm.
   - `/healthz/ready`: Checks if the pod can accept traffic (verifies DB connection, Redis ping, warm caches). If failing, Kubernetes removes the pod from the Service load balancer without restarting it.
2. **Use ASP.NET Core Health Checks**:
   - `services.AddHealthChecks().AddCheck("db", ...);`
   - Map with `app.MapHealthChecks("/healthz/ready")` and `app.MapHealthChecks("/healthz/live", new HealthCheckOptions { Predicate = _ => false })`.
