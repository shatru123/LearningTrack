# Deep Dive Notes: Kubernetes Probes & HPA Auto-Scaling

## 1. Probe Comparison Matrix

| Probe | Purpose | On Failure Action | Target Endpoint |
|---|---|---|---|
| **Startup** | Shield slow bootstrap / cache warmup | Kills container after `failureThreshold * periodSeconds` | `/health/startup` (lightweight check) |
| **Liveness** | Detect unrecoverable deadlock / loop | Kubelet kills container and restarts according to restartPolicy | `/health/live` (process responsiveness) |
| **Readiness** | Signal readiness to receive user traffic | Kubelet unregisters Pod from Service Endpoints | `/health/ready` (deep dependency checks) |

## 2. HPA Control Loop Algorithm
The Horizontal Pod Autoscaler controller executes every 15 seconds by default (`--horizontal-pod-autoscaler-sync-period`):
$$\text{desiredReplicas} = \left\lceil \text{currentReplicas} \times \frac{\text{currentMetricValue}}{\text{targetMetricValue}} \right\rceil$$
To prevent thrashing, the controller applies:
1. **Tolerance Band**: If $|\text{ratio} - 1.0| \le 0.10$, no scaling action is taken.
2. **Stabilization Window**: Cool-down period (default 300 seconds for scale-down) picking the maximum recommended replicas over the past 5 minutes before executing scale-down.
