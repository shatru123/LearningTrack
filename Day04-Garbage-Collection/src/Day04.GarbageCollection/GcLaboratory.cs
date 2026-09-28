using System.Buffers;
using System.Runtime;
using System.Text;

namespace Day04.GarbageCollection;

public record GcRuntimeInfo(
    bool IsServerGc,
    GCLatencyMode LatencyMode,
    int MaxGeneration,
    long TotalMemoryBytes);

public record AllocationBenchmarkResult(
    long NaiveConcatBytes,
    long StringBuilderBytes,
    long OptimizedBytes);

public class GcLaboratory
{
    public static GcRuntimeInfo GetRuntimeInfo()
    {
        return new GcRuntimeInfo(
            GCSettings.IsServerGC,
            GCSettings.LatencyMode,
            GC.MaxGeneration,
            GC.GetTotalMemory(forceFullCollection: false));
    }

    /// <summary>
    /// Demonstrates generational promotion:
    /// Gen 0 (ephemeral short-lived) -> Gen 1 (buffer/intermediate) -> Gen 2 (long-lived tenured).
    /// </summary>
    public static (int InitialGen, int AfterGen0Collect, int AfterGen1Collect) DemonstrateGenerationalPromotion()
    {
        // Allocate a small object in Gen 0
        object liveObject = new byte[64];
        int initialGen = GC.GetGeneration(liveObject);

        // Force Gen 0 collection; surviving objects promote to Gen 1
        GC.Collect(0, GCCollectionMode.Forced, blocking: true);
        int afterGen0 = GC.GetGeneration(liveObject);

        // Force Gen 1 collection; surviving objects promote to Gen 2
        GC.Collect(1, GCCollectionMode.Forced, blocking: true);
        int afterGen1 = GC.GetGeneration(liveObject);

        return (initialGen, afterGen0, afterGen1);
    }

    /// <summary>
    /// Demonstrates the Large Object Heap (LOH) threshold.
    /// Objects >= 85,000 bytes bypass Gen 0/1 and are allocated directly in Gen 2 (LOH).
    /// </summary>
    public static (int SmallObjectGen, int LargeObjectGen) DemonstrateLohAllocation()
    {
        // 1KB object (< 85,000 bytes) -> goes to Gen 0
        byte[] smallObj = new byte[1024];
        int smallGen = GC.GetGeneration(smallObj);

        // 85,000+ byte object -> goes directly to Large Object Heap (Gen 2)
        byte[] largeObj = new byte[85000];
        int largeGen = GC.GetGeneration(largeObj);

        return (smallGen, largeGen);
    }

    /// <summary>
    /// Compares allocation profiles:
    /// 1. Naive string concatenation: creates O(N^2) allocations.
    /// 2. StringBuilder: pre-sized buffer amortizes allocations.
    /// 3. ArrayPool / string.Create: zero intermediate heap allocations.
    /// </summary>
    public static AllocationBenchmarkResult CompareAllocationImpact(int iterations = 100)
    {
        const string token = "TicketEvent";

        // 1. Naive string concatenation
        long start1 = GC.GetAllocatedBytesForCurrentThread();
        string concatResult = string.Empty;
        for (int i = 0; i < iterations; i++)
        {
            concatResult += token;
        }
        long naiveBytes = GC.GetAllocatedBytesForCurrentThread() - start1;

        // 2. Pre-sized StringBuilder
        long start2 = GC.GetAllocatedBytesForCurrentThread();
        StringBuilder sb = new(token.Length * iterations);
        for (int i = 0; i < iterations; i++)
        {
            sb.Append(token);
        }
        string sbResult = sb.ToString();
        long sbBytes = GC.GetAllocatedBytesForCurrentThread() - start2;

        // 3. Optimized with ArrayPool
        long start3 = GC.GetAllocatedBytesForCurrentThread();
        int totalLength = token.Length * iterations;
        char[] rented = ArrayPool<char>.Shared.Rent(totalLength);
        try
        {
            Span<char> targetSpan = rented.AsSpan(0, totalLength);
            for (int i = 0; i < iterations; i++)
            {
                token.AsSpan().CopyTo(targetSpan.Slice(i * token.Length));
            }
            string optResult = new string(targetSpan);
        }
        finally
        {
            ArrayPool<char>.Shared.Return(rented);
        }
        long optBytes = GC.GetAllocatedBytesForCurrentThread() - start3;

        return new AllocationBenchmarkResult(naiveBytes, sbBytes, optBytes);
    }
}
