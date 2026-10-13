using System;
using System.Collections.Generic;
using System.Linq;

namespace Day24.AdvancedSql;

public record PartitionMetadata(
    string TableName,
    DateTime RangeStartUtc,
    DateTime RangeEndUtc,
    long RowCount,
    double SizeMb
);

public record PartitionPruningPlan(
    DateTime QueryStartUtc,
    DateTime QueryEndUtc,
    List<string> ScannedPartitions,
    List<string> PrunedPartitions,
    double TotalScannedSizeMb,
    double TotalSavingsSizeMb
);

/// <summary>
/// PostgreSQL Declarative Table Partitioning Architecture Simulator.
/// Models range partitioning, query planner partition pruning, and zero-downtime maintenance.
/// </summary>
public class TablePartitioningStrategy
{
    private readonly List<PartitionMetadata> _partitions = new();

    public IReadOnlyList<PartitionMetadata> Partitions => _partitions;

    public void RegisterPartition(string tableName, DateTime startUtc, DateTime endUtc, long rowCount, double sizeMb)
    {
        _partitions.Add(new PartitionMetadata(tableName, startUtc, endUtc, rowCount, sizeMb));
    }

    /// <summary>
    /// Simulates PostgreSQL EXPLAIN query plan partition pruning for a given time interval filter.
    /// </summary>
    public PartitionPruningPlan SimulatePruning(DateTime queryStartUtc, DateTime queryEndUtc)
    {
        var scanned = new List<string>();
        var pruned = new List<string>();
        double scannedMb = 0;
        double savedMb = 0;

        foreach (var p in _partitions)
        {
            // Partition overlaps query range if:
            // p.RangeStart < queryEnd AND p.RangeEnd > queryStart
            bool overlaps = p.RangeStartUtc < queryEndUtc && p.RangeEndUtc > queryStartUtc;

            if (overlaps)
            {
                scanned.Add(p.TableName);
                scannedMb += p.SizeMb;
            }
            else
            {
                pruned.Add(p.TableName);
                savedMb += p.SizeMb;
            }
        }

        return new PartitionPruningPlan(
            QueryStartUtc: queryStartUtc,
            QueryEndUtc: queryEndUtc,
            ScannedPartitions: scanned,
            PrunedPartitions: pruned,
            TotalScannedSizeMb: Math.Round(scannedMb, 2),
            TotalSavingsSizeMb: Math.Round(savedMb, 2)
        );
    }

    /// <summary>
    /// Generates DDL scripts for declarative range partitioning in PostgreSQL.
    /// </summary>
    public static string GeneratePostgreSqlDdl(int year, int month)
    {
        var nextMonthDate = new DateTime(year, month, 1).AddMonths(1);
        var partName = $"sensor_readings_y{year:0000}m{month:00}";
        var startStr = $"{year:0000}-{month:00}-01 00:00:00+00";
        var endStr = $"{nextMonthDate.Year:0000}-{nextMonthDate.Month:00}-01 00:00:00+00";

        return $@"
-- 1. Create Root Partitioned Table (Parent)
CREATE TABLE IF NOT EXISTS sensor_readings (
    id BIGINT GENERATED ALWAYS AS IDENTITY,
    device_id VARCHAR(64) NOT NULL,
    recorded_at TIMESTAMPTZ NOT NULL,
    temperature NUMERIC(5, 2) NOT NULL,
    voltage NUMERIC(6, 3) NOT NULL,
    PRIMARY KEY (recorded_at, id)
) PARTITION BY RANGE (recorded_at);

-- 2. Create Child Range Partition for {year}-{month:00}
CREATE TABLE IF NOT EXISTS {partName}
PARTITION OF sensor_readings
FOR VALUES FROM ('{startStr}') TO ('{endStr}');

-- 3. Create Index Local to Partition
CREATE INDEX IF NOT EXISTS idx_{partName}_device_time 
ON {partName} (device_id, recorded_at DESC);
".Trim();
    }
}
