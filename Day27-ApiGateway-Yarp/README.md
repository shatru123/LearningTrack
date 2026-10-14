# Day 27: API Gateway Architecture with YARP

## Overview
**Day 27** dives into cloud-native API Gateway patterns using **YARP (Yet Another Reverse Proxy)** in .NET 8. We explore reverse proxy routing architectures, transform pipelines (header forwarding/removal, path rewriting), active & passive health check monitoring, and distributed token bucket rate limiting at the gateway layer. For DSA, we tackle **LeetCode #133 (*Clone Graph*)** using visited hash map node tracking across cyclic graphs and **LeetCode #695 (*Max Area of Island*)** using recursive DFS and iterative BFS.

---

## Architectural Topics Covered

1. **YARP (Yet Another Reverse Proxy) Architecture**:
   - Routes, Clusters, Destinations, and Match Criteria (`Path`, `Hosts`, `Headers`).
   - Active Health Checks (`Interval`, `Timeout`, `ConsecutiveFailuresPolicy`, `/health` probes) vs Passive Failure detection.
   - Load Balancing Policies: `RoundRobin`, `PowerOfTwoChoices`, `LeastRequests`.
2. **Gateway Transform Pipeline**:
   - Header enrichment: Injecting `X-Forwarded-Prefix`, `X-Forwarded-For`, `X-Forwarded-Host`, `X-Correlation-ID`.
   - Header sanitization: Stripping internal secrets, confidential tokens, and sensitive headers before forwarding downstream.
   - Path transformations: `PathRemovePrefix` and prefix routing to backends.
3. **Gateway Rate Limiting**:
   - Token Bucket rate limiting partitioned by Client IP or API Key.
   - Emission of standard RFC headers (`Retry-After`, `X-RateLimit-Remaining`) and HTTP 429 Too Many Requests.
4. **Graph Algorithms**:
   - **LeetCode #133: Clone Graph (Medium)**: Deep copy of an undirected cyclic graph using a `Dictionary<Node, Node>` to resolve cyclic dependencies in $\mathcal{O}(V + E)$ time.
   - **LeetCode #695: Max Area of Island (Medium)**: Finding the largest connected 4-directional component on a 2D binary grid in $\mathcal{O}(M \times N)$ time.

---

## Project Structure

```
Day27-ApiGateway-Yarp/
├── src/
│   └── Day27.ApiGatewayYarp/
│       ├── Day27.ApiGatewayYarp.csproj
│       ├── YarpGatewayEngine.cs
│       └── GraphAndIslandSolver.cs
├── tests/
│   └── Day27.ApiGatewayYarp.Tests/
│       ├── Day27.ApiGatewayYarp.Tests.csproj
│       └── Day27Tests.cs
├── README.md
├── notes.md
└── interview-questions.md
```

---

## Running the Unit Tests

```bash
dotnet test Day27-ApiGateway-Yarp/tests/Day27.ApiGatewayYarp.Tests/Day27.ApiGatewayYarp.Tests.csproj
```

### Test Suite Coverage
1. `YarpConfigBuilder_ConstructsValidRoutesAndClusters`: Validates programmatic construction of route and cluster configurations with active health check parameters.
2. `YarpGatewayEngine_ForwardsRequest_ToMatchingCluster`: Verifies route prefix matching, destination URI synthesis, and correlation header injection.
3. `YarpGatewayEngine_AppliesTransforms_HeadersAndPath`: Validates header removal (client secrets), header addition, and path prefix rewriting.
4. `YarpGatewayEngine_FiltersOutUnhealthyDestinations`: Verifies that traffic automatically bypasses destinations flagged as `Unhealthy`.
5. `YarpGatewayEngine_Returns503_WhenAllDestinationsUnhealthy`: Verifies HTTP 503 response when all destinations in a cluster fail.
6. `GatewayRateLimiter_EnforcesLimitAndEmits429`: Tests token bucket depletion, emission of HTTP 429, and `Retry-After` calculation.
7. `CloneGraph_Dfs_ClonesCyclicGraphAccurately`: Tests LC #133 recursive DFS deep clone preserving cyclic neighbor references.
8. `CloneGraph_Bfs_ClonesCyclicGraphAccurately`: Tests LC #133 iterative BFS deep clone.
9. `CloneGraph_HandlesEdgeCases`: Validates null inputs, isolated single nodes, and self-loop nodes.
10. `MaxAreaOfIsland_DfsAndBfs_CalculatesCorrectMaxArea`: Tests LC #695 across standard multi-island grids, water-only grids, and single-cell grids.
