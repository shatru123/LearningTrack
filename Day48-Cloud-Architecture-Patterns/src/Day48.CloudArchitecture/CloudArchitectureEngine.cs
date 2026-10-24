namespace Day48.CloudArchitecture;

using System;
using System.Collections.Generic;
using System.Linq;

public enum RoutingMode
{
    ActivePassiveWarmStandby,
    ActiveActive
}

public record RegionHealth(string Region, bool IsHealthy, double LatencyMs, double ErrorRatePercent);

public class MultiRegionTrafficRouter
{
    public RoutingMode Mode { get; }
    public string PrimaryRegion { get; }
    public string SecondaryRegion { get; }
    public double ErrorRateFailoverThreshold { get; set; } = 20.0; // percent

    public MultiRegionTrafficRouter(
        string primaryRegion,
        string secondaryRegion,
        RoutingMode mode = RoutingMode.ActivePassiveWarmStandby)
    {
        PrimaryRegion = primaryRegion;
        SecondaryRegion = secondaryRegion;
        Mode = mode;
    }

    public string RouteTraffic(RegionHealth primaryHealth, RegionHealth secondaryHealth)
    {
        if (Mode == RoutingMode.ActivePassiveWarmStandby)
        {
            if (primaryHealth.IsHealthy && primaryHealth.ErrorRatePercent < ErrorRateFailoverThreshold)
            {
                return PrimaryRegion;
            }

            if (secondaryHealth.IsHealthy)
            {
                return SecondaryRegion;
            }

            // Both degraded: route to the one with lower error rate
            return primaryHealth.ErrorRatePercent <= secondaryHealth.ErrorRatePercent
                ? PrimaryRegion
                : SecondaryRegion;
        }
        else // ActiveActive
        {
            if (!primaryHealth.IsHealthy && secondaryHealth.IsHealthy) return SecondaryRegion;
            if (primaryHealth.IsHealthy && !secondaryHealth.IsHealthy) return PrimaryRegion;

            // Route to region with lowest latency
            return primaryHealth.LatencyMs <= secondaryHealth.LatencyMs ? PrimaryRegion : SecondaryRegion;
        }
    }

    public (int rtoMinutes, int rpoSeconds) EstimateDisasterRecoveryBounds(double replicationLagSec)
    {
        if (Mode == RoutingMode.ActivePassiveWarmStandby)
        {
            // Warm standby takes ~2-5 mins DNS propagation + warm up, RPO is DB replication lag
            int rto = 5;
            int rpo = (int)Math.Ceiling(replicationLagSec);
            return (rto, rpo);
        }
        else // Active-Active
        {
            // Immediate automated failover (< 1 min), RPO near 0 or replication lag
            int rto = 1;
            int rpo = (int)Math.Ceiling(replicationLagSec);
            return (rto, rpo);
        }
    }
}

public class DatabaseReplicationLagTracker
{
    public double CurrentLagMilliseconds { get; private set; }

    public void RecordReplicationEvent(DateTime primaryCommitTime, DateTime replicaApplyTime)
    {
        CurrentLagMilliseconds = Math.Max(0, (replicaApplyTime - primaryCommitTime).TotalMilliseconds);
    }

    public bool IsReadConsistent(double maxAcceptableStalenessMs)
    {
        return CurrentLagMilliseconds <= maxAcceptableStalenessMs;
    }
}

public enum StorageTier
{
    Hot,
    Cool,
    Cold,
    Archive
}

public class BlobStorageLifecycleEngine
{
    public static StorageTier EvaluateTier(int daysSinceModified)
    {
        if (daysSinceModified >= 180) return StorageTier.Archive;
        if (daysSinceModified >= 90) return StorageTier.Cold;
        if (daysSinceModified >= 30) return StorageTier.Cool;
        return StorageTier.Hot;
    }

    public static double CalculateMonthlyStorageCost(double sizeGb, StorageTier tier)
    {
        // Indicative Azure/AWS pricing per GB/month:
        // Hot: $0.018/GB, Cool: $0.010/GB, Cold: $0.0045/GB, Archive: $0.00099/GB
        double rate = tier switch
        {
            StorageTier.Hot => 0.018,
            StorageTier.Cool => 0.010,
            StorageTier.Cold => 0.0045,
            StorageTier.Archive => 0.00099,
            _ => 0.018
        };
        return sizeGb * rate;
    }
}

public class CloudCostOptimizationCalculator
{
    public static double ComputeMonthlyComputeCost(int instances, double hourlyOnDemandRate, string commitmentType)
    {
        double hourlyRate = commitmentType.ToLowerInvariant() switch
        {
            "ondemand" => hourlyOnDemandRate,
            "1year_reserved" => hourlyOnDemandRate * 0.60, // 40% discount
            "3year_reserved" => hourlyOnDemandRate * 0.40, // 60% discount
            "spot" => hourlyOnDemandRate * 0.25,           // 75% discount
            _ => hourlyOnDemandRate
        };

        return instances * hourlyRate * 730; // 730 average hours per month
    }

    public static double CalculateEgressCost(double gigabytesTransferred, string transferType)
    {
        // Pricing per GB:
        // Internet egress: $0.09/GB
        // Inter-region egress: $0.02/GB
        // Inter-AZ egress: $0.01/GB
        // Private endpoint / intra-VNet: $0.00/GB
        double rate = transferType.ToLowerInvariant() switch
        {
            "internet" => 0.09,
            "inter_region" => 0.02,
            "inter_az" => 0.01,
            "private_endpoint" => 0.00,
            _ => 0.09
        };

        return gigabytesTransferred * rate;
    }
}
