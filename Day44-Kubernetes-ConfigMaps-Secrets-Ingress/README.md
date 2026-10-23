# Day 44: Kubernetes ConfigMaps, Secrets & Ingress

## Overview
Day 44 covers decoupled application configuration and external routing architecture in Kubernetes using **ConfigMaps**, **Secrets**, and **Ingress Controllers** (NGINX / Traefik).

## Architecture Highlights
- **ConfigMaps**: Inject non-sensitive environment variables, JSON configs, and feature toggles into Pods without rebuilding container images.
- **Secrets**: Base64 encoded (or SealedSecrets / Vault CSI) sensitive database connection strings, JWT signing keys, and TLS certificates.
- **Hot-Reloading with `..data` Symlink**: Kubernetes mounts ConfigMaps/Secrets as volume files using atomic directory symlink swapping, enabling .NET `IOptionsSnapshot<T>` reloading without container restarts.
- **Ingress Controller (NGINX)**: Layer 7 HTTP/HTTPS reverse proxy handling TLS termination, virtual host dispatching, and path-based routing (`/orders` -> `order-service:80`, `/inventory` -> `inventory-service:80`).

## DSA Problems
- **LeetCode #647 (Palindromic Substrings)**: Center expansion counting odd and even palindromes in $\mathcal{O}(N^2)$ time and $\mathcal{O}(1)$ space.
- **LeetCode #91 (Decode Ways)**: 1D Dynamic Programming tracking single-digit ($1-9$) and two-digit ($10-26$) transitions with zero validation in $\mathcal{O}(N)$ time and $\mathcal{O}(1)$ space.

## Test Verification
Run the 10 automated unit tests:
```bash
dotnet test tests/Day44.KubernetesConfigIngress.Tests/Day44.KubernetesConfigIngress.Tests.csproj
```
