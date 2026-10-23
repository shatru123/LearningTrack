# Day 43: Kubernetes Architecture: Pods, Deployments & Services

## Overview
Day 43 explores the core architecture of **Kubernetes (K8s)** control plane and worker nodes, focusing on deploying cloud-native .NET microservices via production Declarative YAML manifests (`Deployment`, `ClusterIP`, `NodePort`, `LoadBalancer`).

## Core Concepts
- **Control Plane**:
  - `kube-apiserver`: The central REST API gateway and admission controller validating and persisting cluster state into `etcd`.
  - `etcd`: Strongly consistent distributed key-value store using Raft consensus for state storage.
  - `kube-scheduler`: Assigns newly created Pods to nodes using filtering (resource capacity) and scoring (anti-affinity, spread).
  - `kube-controller-manager`: Runs reconciliation control loops (Deployment controller, ReplicaSet controller).
- **Worker Node**:
  - `kubelet`: Node agent registering the node, executing PodSpecs via Container Runtime Interface (CRI), and reporting health.
  - `kube-proxy`: Maintains network forwarding rules (IPVS/iptables) mapping Virtual ClusterIP VIPs to healthy Pod Endpoints.
- **Deployments & Services**:
  - `RollingUpdate` with `maxSurge: 25%` and `maxUnavailable: 0` for zero-downtime releases.
  - Hardened securityContext (`runAsNonRoot: true`, `readOnlyRootFilesystem: true`, dropping all Linux capabilities).

## DSA Problem: LeetCode #5 (Longest Palindromic Substring)
- Expand Around Center approach running in $\mathcal{O}(N^2)$ time and $\mathcal{O}(1)$ space.
- Dynamic Programming table $dp[i, j]$ memoizing sub-palindromes.

## Test Verification
Run the 10 automated unit tests:
```bash
dotnet test tests/Day43.KubernetesArchitecture.Tests/Day43.KubernetesArchitecture.Tests.csproj
```
