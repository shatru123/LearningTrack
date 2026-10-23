using System;

namespace Day45.KubernetesProbesHPA;

public enum ProbeResult { Success, Failure }

public record ProbeState(
    int ConsecutiveFailures,
    int ConsecutiveSuccesses,
    bool IsPassing
);

public class KubernetesHpaAndProbeEngine
{
    public (bool TriggerRestart, bool IsInEndpoints) EvaluateProbes(
        ProbeResult livenessResult,
        ProbeResult readinessResult,
        ref ProbeState livenessState,
        ref ProbeState readinessState,
        int livenessFailureThreshold = 3,
        int readinessFailureThreshold = 2
    )
    {
        // 1. Evaluate Liveness Probe
        bool triggerRestart = false;
        if (livenessResult == ProbeResult.Failure)
        {
            livenessState = livenessState with
            {
                ConsecutiveFailures = livenessState.ConsecutiveFailures + 1,
                ConsecutiveSuccesses = 0,
                IsPassing = false
            };
            if (livenessState.ConsecutiveFailures >= livenessFailureThreshold)
            {
                triggerRestart = true;
            }
        }
        else
        {
            livenessState = livenessState with
            {
                ConsecutiveFailures = 0,
                ConsecutiveSuccesses = livenessState.ConsecutiveSuccesses + 1,
                IsPassing = true
            };
        }

        // 2. Evaluate Readiness Probe
        bool isInEndpoints = readinessState.IsPassing;
        if (readinessResult == ProbeResult.Failure)
        {
            readinessState = readinessState with
            {
                ConsecutiveFailures = readinessState.ConsecutiveFailures + 1,
                ConsecutiveSuccesses = 0,
                IsPassing = false
            };
            if (readinessState.ConsecutiveFailures >= readinessFailureThreshold)
            {
                isInEndpoints = false;
            }
        }
        else
        {
            readinessState = readinessState with
            {
                ConsecutiveFailures = 0,
                ConsecutiveSuccesses = readinessState.ConsecutiveSuccesses + 1,
                IsPassing = true
            };
            isInEndpoints = true;
        }

        return (triggerRestart, isInEndpoints);
    }

    public int CalculateDesiredReplicas(
        int currentReplicas,
        double currentMetricValue,
        double targetMetricValue,
        int minReplicas = 2,
        int maxReplicas = 10,
        double tolerance = 0.1
    )
    {
        if (targetMetricValue <= 0 || currentReplicas <= 0) return currentReplicas;

        double ratio = currentMetricValue / targetMetricValue;

        // Tolerance check: if ratio is within [1 - tolerance, 1 + tolerance], no scaling
        if (Math.Abs(ratio - 1.0) <= tolerance)
        {
            return currentReplicas;
        }

        int desired = (int)Math.Ceiling(currentReplicas * ratio);

        // Clamp between minReplicas and maxReplicas
        if (desired < minReplicas) desired = minReplicas;
        if (desired > maxReplicas) desired = maxReplicas;

        return desired;
    }
}
