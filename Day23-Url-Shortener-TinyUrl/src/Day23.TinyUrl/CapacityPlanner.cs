using System;

namespace Day23.TinyUrl;

/// <summary>
/// Encapsulates capacity calculations and architecture sizing for TinyURL.
/// Evaluates writes, reads, memory cache requirements, disk growth, and network throughput.
/// </summary>
public record CapacityPlanResult
{
    public long DailyWrites { get; init; }
    public long DailyReads { get; init; }
    public double AverageWriteQps { get; init; }
    public double PeakWriteQps { get; init; }
    public double AverageReadQps { get; init; }
    public double PeakReadQps { get; init; }
    public double StorageBytesPerYear { get; init; }
    public double StorageBytesPerFiveYears { get; init; }
    public double CacheMemoryBytes { get; init; }
    public double IngressBandwidthBytesPerSec { get; init; }
    public double EgressBandwidthBytesPerSec { get; init; }
}

public static class CapacityPlanner
{
    public const int SecondsPerDay = 86400;
    public const int BytesPerUrlRecord = 500; // ID, ShortUrl, LongUrl, Timestamps, UserId, Metadata

    /// <summary>
    /// Computes full capacity planning estimates for specified daily read and write workloads.
    /// </summary>
    public static CapacityPlanResult Calculate(
        long dailyWrites = 100_000_000L,
        long dailyReads = 1_000_000_000L,
        double peakMultiplier = 3.0,
        double cachePercentage = 0.20,
        int bytesPerRecord = BytesPerUrlRecord)
    {
        double avgWriteQps = (double)dailyWrites / SecondsPerDay;
        double peakWriteQps = avgWriteQps * peakMultiplier;

        double avgReadQps = (double)dailyReads / SecondsPerDay;
        double peakReadQps = avgReadQps * peakMultiplier;

        double storagePerYear = (double)dailyWrites * 365 * bytesPerRecord;
        double storagePerFiveYears = storagePerYear * 5.0;

        // Pareto 80/20 Rule: Cache 20% of daily reads
        double cacheMemory = (double)dailyReads * cachePercentage * bytesPerRecord;

        double ingressBytesPerSec = avgWriteQps * bytesPerRecord;
        double egressBytesPerSec = avgReadQps * bytesPerRecord;

        return new CapacityPlanResult
        {
            DailyWrites = dailyWrites,
            DailyReads = dailyReads,
            AverageWriteQps = Math.Round(avgWriteQps, 1),
            PeakWriteQps = Math.Round(peakWriteQps, 1),
            AverageReadQps = Math.Round(avgReadQps, 1),
            PeakReadQps = Math.Round(peakReadQps, 1),
            StorageBytesPerYear = storagePerYear,
            StorageBytesPerFiveYears = storagePerFiveYears,
            CacheMemoryBytes = cacheMemory,
            IngressBandwidthBytesPerSec = ingressBytesPerSec,
            EgressBandwidthBytesPerSec = egressBytesPerSec
        };
    }
}
