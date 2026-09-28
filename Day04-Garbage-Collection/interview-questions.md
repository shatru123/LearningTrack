# Day 04 - Senior .NET Interview Questions: Garbage Collection Internals

### Q1: What is the Generational Hypothesis and how does .NET exploit it?
- **Short answer**: The hypothesis states that most objects die young. .NET exploits this by dividing the heap into Gen 0, Gen 1, and Gen 2, collecting young generations frequently and rapidly without scanning the whole heap.
- **Deeper answer**: Gen 0 collections are extremely cheap because the GC assumes most objects are unreachable. It traces roots, copies the few surviving objects into Gen 1, and instantly resets the Gen 0 allocation pointer. Gen 2 is only collected when ephemeral collections fail to reclaim enough memory or when system memory is low.
- **Practical example**: A local DTO created to handle an HTTP request dies within milliseconds and is collected in Gen 0 in microseconds.

---

### Q2: What is the difference between Server GC and Workstation GC in .NET?
- **Short answer**: Workstation GC uses 1 heap and minimal background threads; Server GC creates a dedicated heap and dedicated high-priority GC thread per CPU core.
- **Deeper answer**: Workstation GC is tuned for desktop UI responsiveness and low memory footprint. Server GC partitions the heap by CPU core, allowing parallel allocation and parallel collection across cores to maximize server throughput. In containers with limited CPU/memory quotas, Server GC must be tuned carefully to avoid CPU throttling.
- **Practical example**: On a 16-core machine, Server GC allocates 16 separate heaps, allowing 16 threads to allocate memory simultaneously without lock contention.

---

### Q3: What is the Large Object Heap (LOH) threshold and why does it exist?
- **Short answer**: The threshold is 85,000 bytes. It exists because copying large memory blocks during GC compaction is prohibitively expensive.
- **Deeper answer**: When objects $\ge 85,000$ bytes are allocated, copying them during Gen 0/1 sweeps would cause severe CPU cache and memory bus degradation. Therefore, LOH objects are allocated directly into Gen 2 and are swept (marked and freed) rather than compacted by default.
- **Practical example**: Allocating `new byte[100_000]` goes directly to the LOH. Repeatedly allocating large transient arrays fragments the LOH and triggers full Gen 2 collections.

---

### Q4: How does Background GC reduce latency compared to traditional Non-Concurrent GC?
- **Short answer**: Background GC performs Gen 2 sweeps on a dedicated background thread while user code continues executing, and permits ephemeral Gen 0/1 collections during the process.
- **Deeper answer**: In older non-concurrent GC, a Gen 2 collection froze all threads for hundreds of milliseconds. In Background GC, only short pause phases occur (initial mark and final sweep). If the application allocates heavily during a background Gen 2, ephemeral foreground collections can run and finish without waiting for the background sweep to complete.
- **Practical example**: Background GC keeps ASP.NET Core API p99 latencies within single-digit milliseconds during background memory reclamation.

---

### Q5: What is the Pinned Object Heap (POH) introduced in .NET 5?
- **Short answer**: A dedicated heap segment specifically for pinned objects to prevent heap fragmentation in Gen 0, 1, and 2.
- **Deeper answer**: Pinning an object prevents the GC from moving it during compaction, creating "islands" that fragment ephemeral segments. The POH isolates pinned buffers (e.g. for socket reads or native interop) into their own heap segment so regular GC compaction in other generations is uninhibited.
- **Practical example**:
```csharp
byte[] pinnedBuffer = GC.AllocateArray<byte>(4096, pinned: true);
```

---

### Q6: Why is calling `GC.Collect()` considered an anti-pattern in production ASP.NET Core services?
- **Short answer**: It forces a full, blocking generation collection, suspends all threads, and disrupts the GC's self-tuning algorithms.
- **Deeper answer**: The GC dynamically adjusts generation budgets based on allocation rates and survival patterns. Forcing a collection promotes short-lived objects that were about to die into Gen 1 or Gen 2 prematurely, increasing overall heap size and ensuring future collections are more expensive.
- **Practical example**: Calling `GC.Collect()` inside an HTTP endpoint can turn a 2ms request into a 150ms latency spike for all concurrent users.

---

### Q7: What are Card Tables and Write Barriers in the .NET GC?
- **Short answer**: Mechanisms used by the GC to track when a reference in an older generation is updated to point to an object in a younger generation.
- **Deeper answer**: To collect Gen 0 without scanning all of Gen 1 and Gen 2 for incoming references, the JIT inserts a tiny write barrier instruction whenever a reference field is assigned. The write barrier marks a bit in a byte array called the "Card Table". During a Gen 0 collection, the GC only needs to scan the specific cards marked dirty.
- **Practical example**: When an existing singleton `OrderService` assigns a newly created `Order` to a field, the JIT executes a write barrier marking the corresponding card dirty.

---

### Q8: How can you diagnose high GC pause times and allocation pressure in a live production environment?
- **Short answer**: Using `dotnet-counters`, `dotnet-trace`, and `dotnet-dump` to inspect `% Time in GC`, `Allocation Rate`, and generation collection counts.
- **Deeper answer**: Monitor `System.Runtime` counters:
  - `% Time in GC`: Should ideally stay $< 5\%$; values $> 15\%$ indicate critical memory pressure.
  - `Gen 0 / Gen 1 / Gen 2 Collections / sec`: A high ratio of Gen 2 to Gen 0 indicates premature promotion or LOH abuse.
  - Collect an EventPipe trace with `dotnet-trace` to inspect allocation call stacks.
- **Practical example**: `dotnet-counters monitor --process-id <PID> --counters System.Runtime`

---

### Q9: What is `GCSettings.LatencyMode` and when would you use `SustainedLowLatency`?
- **Short answer**: It controls how aggressively the GC collects memory to trade off throughput vs pause duration.
- **Deeper answer**: In `SustainedLowLatency` mode, the GC disables full blocking Gen 2 collections as long as memory permits, performing only Background Gen 2 and ephemeral collections. It is used in financial trading systems or low-latency audio processing during critical execution windows.
- **Practical example**:
```csharp
GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency;
```

---

### Q10: How does .NET 8 / 9 DATAS (Dynamic Adaptation To Application Sizes) GC alter Server GC behavior?
- **Short answer**: DATAS dynamically adjusts heap size based on application demand, dramatically reducing memory usage in small or bursting workloads.
- **Deeper answer**: Traditional Server GC aggressively retains large heap segments across all cores, which can consume hundreds of megabytes even for idle microservices. DATAS monitors application throughput and adapts the number of active heaps and segment sizes, making Server GC viable in memory-constrained Kubernetes pods.
- **Practical example**: In `.csproj`: `<GarbageCollectionAdaptationMode>1</GarbageCollectionAdaptationMode>`.
