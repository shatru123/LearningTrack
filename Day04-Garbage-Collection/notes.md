# Day 04 - Engineering Notes: Garbage Collection Internals

## 1. Generational Hypothesis

The .NET Garbage Collector is built upon the **Weak Generational Hypothesis**:
1. Most newly allocated objects have short lifespans (locals, transient DTOs, string builders).
2. The older an object is, the longer it is likely to remain in use (caches, configuration, singletons).
3. Collecting a portion of the heap (Gen 0) is much faster than collecting the entire heap.

### Heap Segment Sizes and Roles
- **Gen 0**: Fresh allocations. Very fast allocation via pointer bumping (`allocation context`).
- **Gen 1**: Serves as a buffer. Objects that survive Gen 0 are promoted here.
- **Gen 2**: Tenured long-lived objects. Surviving Gen 1 objects promote here. Full collections sweep Gen 0, 1, and 2.
- **LOH (Large Object Heap)**: Objects $\ge 85,000$ bytes (or double arrays $\ge 1,000$ elements on 32-bit). Because copying large objects is expensive, LOH is swept rather than compacted by default.
- **POH (Pinned Object Heap)**: Objects pinned for interop or I/O (`GC.AllocateArray<T>(length, pinned: true)`). Avoids fragmenting Gen 0/1/2.

---

## 2. Server GC vs Workstation GC

| Feature | Workstation GC | Server GC |
|---|---|---|
| **Heap Count** | 1 logical heap | 1 heap per logical core (e.g. 16 heaps on 16 cores) |
| **GC Threads** | Runs on requesting thread or 1 background thread | 1 dedicated OS thread per core (`gc_thread`) running at highest priority |
| **Throughput** | Moderate | Maximum (scales linearly with CPU cores) |
| **Memory Overhead** | Low footprint | Higher baseline memory (multiple heap segments) |
| **Target Workload** | Desktop apps, CLI tools, small memory containers | High-throughput web APIs, microservices, databases |

### Enabling Server GC
In `.csproj`:
```xml
<PropertyGroup>
    <ServerGarbageCollection>true</ServerGarbageCollection>
</PropertyGroup>
```
Or via `runtimeconfig.json`:
```json
{
  "runtimeOptions": {
    "configProperties": {
      "System.GC.Server": true
    }
  }
}
```

---

## 3. Background GC vs Concurrent GC

- **Non-concurrent GC**: All application threads are frozen (Stop-The-World) for the entire duration of Gen 0, 1, and 2 sweeps.
- **Background GC** (current default):
  - Gen 2 collections run concurrently on dedicated background GC threads while application threads continue executing.
  - While a background Gen 2 collection is in progress, the GC can interrupt it to perform fast ephemeral Gen 0 / Gen 1 collections on other threads!
  - Dramatically reduces perceived p99 and p999 API pause times.

---

## 4. DSA Takeaways
- **Product of Array Except Self**:
  - Running prefix products forward store cumulative products of all elements before $i$.
  - Running suffix products backward accumulate into the same array in-place.
  - Achieves $O(n)$ time with $O(1)$ extra space without division.
