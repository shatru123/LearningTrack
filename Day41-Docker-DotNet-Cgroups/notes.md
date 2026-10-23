# Deep Dive Notes: Docker & Linux Cgroups for .NET Services

## 1. Linux Cgroups (Control Groups) v1 vs v2
- In containerized environments, the host Linux kernel imposes memory and CPU constraints via **cgroups**:
  - **cgroups v1**: Memory limits configured in `/sys/fs/cgroup/memory/memory.limit_in_bytes`.
  - **cgroups v2**: Unified hierarchy in `/sys/fs/cgroup/memory.max`.
- .NET 8 has full native awareness of cgroups v1 and v2. By default, the .NET runtime polls cgroups to determine available memory and CPU cores rather than reading total host machine specifications.

## 2. Preventing Linux OOMKilled (Exit Code 137)
- **Why do .NET containers get OOMKilled?**
  - If a container is given a 512MB limit in Kubernetes (`resources.limits.memory: 512Mi`), and `DOTNET_GCHeapHardLimitPercent` is not configured, .NET GC by default may allow the managed heap to expand up to ~75-80% of container memory.
  - However, total container memory = $\text{Managed GC Heap} + \text{Native Memory} + \text{Thread Stacks (1MB per thread)} + \text{JIT Code Cache} + \text{Socket/I/O Buffers}$.
  - Under heavy load or sudden thread creation, native memory pushes total usage past 512MB. The Linux kernel immediately sends `SIGKILL` (`exit code 137`), terminating the container without a managed exception or stack trace!
- **Recommended Configuration**:
  - Set `DOTNET_GCHeapHardLimitPercent=75` (reserving 25% for native buffers and OS overhead).
  - On containers with $\le 1$ core or $\le 512$MB RAM, evaluate Workstation GC (`DOTNET_gcServer=0`) to eliminate multi-heap thread overhead.

## 3. Chiseled Ubuntu Images vs Alpine
- **Alpine Linux**:
  - Uses `musl libc` instead of `glibc`.
  - Can cause compatibility and performance issues with native P/Invoke libraries or optimized SIMD vectorization.
- **Ubuntu Chiseled (`aspnet:8.0-jammy-chiseled`)**:
  - Created by Canonical and Microsoft.
  - Strips everything from Ubuntu except the exact shared libraries needed by .NET (including `glibc` and `OpenSSL`).
  - No `bash`, no `sh`, no `apt`, no `curl`, no `cat`.
  - Image size is ~100MB (similar to Alpine) but retains 100% `glibc` compatibility and dramatically reduces CVE exposure to near zero.
