# Day 45: Kubernetes Probes & Auto-Scaling (HPA)

## Overview
Day 45 details production health monitoring and dynamic load scaling in Kubernetes using **Startup Probes**, **Liveness Probes**, **Readiness Probes**, and the **Horizontal Pod Autoscaler (HPA)**.

## Core Mechanisms
- **Startup Probe (`/health/startup`)**: Protects slow-initializing ASP.NET Core apps (e.g. initial EF Core migrations, cache pre-warming). Disables liveness and readiness checks until passing, preventing premature container restarts.
- **Liveness Probe (`/health/live`)**: Detects deadlocks and unrecoverable internal crashes. Failure exceeding `failureThreshold` causes kubelet to kill the container and restart it.
- **Readiness Probe (`/health/ready`)**: Verifies backing dependencies (database, Redis, message broker). Failure temporarily evicts the Pod from Service Endpoints without restarting the container, preventing HTTP 502/503 errors.
- **Horizontal Pod Autoscaler (HPA)**: Dynamically scales replica count based on CPU utilization, memory pressure, and custom Prometheus metrics (RPS). Includes scale-down stabilization windows (`300s`) to prevent rapid scaling oscillation (flapping).

## DSA Problems
- **LeetCode #322 (Coin Change)**: Unbounded knapsack dynamic programming in $\mathcal{O}(A \cdot C)$ time and $\mathcal{O}(A)$ space.
- **LeetCode #152 (Maximum Product Subarray)**: Tracking both running minimum and maximum products to handle negative number sign inversion in $\mathcal{O}(N)$ time and $\mathcal{O}(1)$ space.

## Test Verification
Run the 10 automated unit tests:
```bash
dotnet test tests/Day45.KubernetesProbesHPA.Tests/Day45.KubernetesProbesHPA.Tests.csproj
```
