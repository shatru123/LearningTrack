# Day 41: Docker Deep Dive for .NET Services

## Architectural Overview
Containerizing modern .NET microservices requires deep knowledge of Linux container internals, cgroups resource isolation, garbage collector tuning, and container supply-chain security.

This module demonstrates production container engineering for .NET 8 services:
- **Multi-Stage Build Pipeline**:
  - Build stage with full .NET SDK image (`mcr.microsoft.com/dotnet/sdk:8.0`).
  - Publish stage creating release binaries without app hosts (`/p:UseAppHost=false`).
  - Runtime stage with ultra-minimal Ubuntu Chiseled image (`mcr.microsoft.com/dotnet/aspnet:8.0-jammy-chiseled`) reducing attack surface to zero package managers and zero interactive shells.
- **Docker Layer Caching Optimization**:
  - Copies `.csproj` files and executes `dotnet restore` in an isolated layer before copying the full source tree, allowing subsequent code changes to build instantly without re-downloading NuGet dependencies.
- **Non-Root Security Hardening**:
  - Drops root privileges by executing under unprivileged `USER app` (UID 1654), preventing container escape vulnerabilities from compromising host systems.
- **Cgroups & .NET GC Tuning**:
  - Analyzes cgroup v1 and v2 memory limits.
  - Automatically configures `DOTNET_GCHeapHardLimitPercent=75`, reserving 25% of container RAM for unmanaged allocations (native buffers, thread stacks, JIT compilation) to prevent Linux OOM Killer (`137 OOMKilled`) termination.
  - Balances Server GC (`DOTNET_gcServer=1`) thread counts across allocated container CPU quotas.
- **Static Dockerfile Linter & Analyzer**: Validates Dockerfile structure against enterprise security and performance guidelines.

```
       Dockerfile Build Flow                    Container Runtime Memory Layout
┌─────────────────────────────────┐       ┌─────────────────────────────────────────┐
│ Stage 1: Build (dotnet SDK)    │       │        Total Container Limit (cgroup)   │
│ - COPY *.csproj                 │       │               (e.g., 512 MB)            │
│ - RUN dotnet restore (CACHED!)  │       ├───────────────────────────┬─────────────┤
│ - COPY . .                      │       │   Managed GC Heap (75%)   │ Native (25%)│
│ - RUN dotnet build              │       │         (384 MB)          │   (128 MB)  │
└───────────────┬─────────────────┘       │                           │             │
                │                         │   - Generation 0, 1, 2    │ Thread Stacks│
                ▼                         │   - Large Object Heap     │ JIT Buffers │
┌─────────────────────────────────┐       │   - Pinned Object Heap    │ OS & Sockets│
│ Stage 2: Publish                │       └───────────────────────────┴─────────────┘
│ - dotnet publish -c Release     │
└───────────────┬─────────────────┘
                │
                ▼
┌─────────────────────────────────┐
│ Stage 3: Chiseled Runtime       │
│ - Base: aspnet:8.0-chiseled     │
│ - USER app (UID 1654, non-root) │
│ - COPY --from=publish           │
│ - Zero shell, zero apt-get      │
└─────────────────────────────────┘
```

---

## LeetCode #70 & #746: Climbing Stairs & Min Cost Climbing Stairs (Easy)
- **LeetCode #70 (Climbing Stairs)**:
  - Dynamic programming Fibonacci recurrence: $dp[i] = dp[i-1] + dp[i-2]$.
  - Solved in $O(N)$ time with $O(1)$ auxiliary memory using two rolling variables.
- **LeetCode #746 (Min Cost Climbing Stairs)**:
  - Dynamic programming recurrence: $dp[i] = \min(dp[i-1] + cost[i-1], dp[i-2] + cost[i-2])$.
  - Solved in $O(N)$ time and $O(1)$ space.

---

## Running Tests
```bash
dotnet test Day41-Docker-DotNet-Cgroups/tests/Day41.DockerDotNet.Tests/Day41.DockerDotNet.Tests.csproj
```
10 unit tests validate container GC heap calculations, Server vs Workstation GC thread allocation, Dockerfile security audits, and dynamic programming stairs solvers.
