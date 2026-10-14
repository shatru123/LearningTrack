# Day 27: Deep Dive Notes — API Gateway Architecture with YARP

## 1. The API Gateway Architectural Pattern

In a microservices topology, client applications (web SPAs, mobile apps, IoT devices, third-party partners) should not directly communicate with dozens of discrete backend microservices. Doing so leaks internal service topology, creates tight coupling, and requires each service to reimplement authentication, rate limiting, and SSL termination.

```
+----------------------------------------------------------------------------------------------------+
|                                    API GATEWAY ARCHITECTURE                                        |
+----------------------------------------------------------------------------------------------------+
|                                                                                                    |
|    [ Web / Mobile Clients ]                                                                        |
|               |                                                                                    |
|               v                                                                                    |
|    +------------------------------------------------------------------------------------------+    |
|    |                             YARP REVERSE PROXY GATEWAY                                   |    |
|    |------------------------------------------------------------------------------------------|    |
|    |  • TLS Termination         • Route Matcher (/api/v1/orders/*)                            |    |
|    |  • JWT Validation          • Request Transforms (Header injection, path stripping)      |    |
|    |  • Token Bucket Limiter    • Active/Passive Health Checking                              |    |
|    |  • Distributed Tracing     • Load Balancing (RoundRobin / PowerOfTwoChoices)             |    |
|    +------------------------------------------------------------------------------------------+    |
|               |                                      |                                             |
|               v                                      v                                             |
|    +---------------------+                +---------------------+                                  |
|    |   Ordering Cluster  |                |  Inventory Cluster  |                                  |
|    | (Node 1, Node 2...) |                | (Node 1, Node 2...) |                                  |
|    +---------------------+                +---------------------+                                  |
|                                                                                                    |
+----------------------------------------------------------------------------------------------------+
```

### Key Gateway Topologies
1. **Edge Gateway**: Central entry point handling public internet traffic, DDoS mitigation, and global TLS termination.
2. **Backend for Frontend (BFF)**: Tailored gateways for specific client experiences (e.g., Mobile BFF vs Desktop Web BFF), aggregating multiple downstream calls into client-optimized DTOs.
3. **Internal Gateway / Service Mesh Ingress**: Routes East-West traffic between internal VPCs or Kubernetes clusters.

### Layer 4 vs. Layer 7 Reverse Proxies
- **Layer 4 (Transport Layer)**: Proxies raw TCP/UDP byte streams. Extremely high throughput and low CPU overhead, but cannot inspect HTTP paths, headers, cookies, or payloads.
- **Layer 7 (Application Layer)**: Proxies HTTP/1.1, HTTP/2, HTTP/3, and gRPC. Full visibility into URL paths, headers, and request bodies, enabling intelligent routing, header rewriting, rate limiting, and circuit breaking.

---

## 2. YARP (Yet Another Reverse Proxy) Internals

**YARP** is Microsoft's high-performance reverse proxy toolkit built on top of ASP.NET Core and `.NET Kestrel`.

### 1. Route and Cluster Configuration
- **Route (`RouteConfig`)**: Defines the incoming match conditions (e.g., path pattern `/api/orders/{**catch-all}`, allowed HTTP methods, host headers) and links to a target Cluster.
- **Cluster (`ClusterConfig`)**: Represents a logical microservice comprised of one or more physical **Destinations** (IP/port or DNS name).
- **Destinations (`DestinationConfig`)**: Individual instances of the service.

### 2. Request Transform Pipeline
Transforms execute sequentially before the request is forwarded:
- **Header Transforms**:
  - `RequestHeaderAdd`: Injects headers such as `X-Forwarded-Prefix`, `X-Forwarded-For`, `X-Correlation-ID`.
  - `RequestHeaderRemove`: Strips sensitive internal authentication tokens or client secrets.
- **Path Transforms**:
  - `PathRemovePrefix`: Strips gateway API versioning (e.g., `/api/v1/orders/1` $\to$ `/orders/1`).
  - `PathPrefix`: Prepends a backend-specific namespace.
- **Query Transforms**: Append or sanitize query parameters.

### 3. Destination Health Checking
- **Active Health Checks**: YARP's background worker periodically sends probes (e.g. `GET /health`) to every registered destination at configured intervals. Destinations that fail consecutive probes are removed from the healthy routing pool.
- **Passive Health Checks**: Monitored inline during live traffic. If an attempt to reach a destination results in a connection timeout or 5xx response, YARP immediately marks that destination as degraded without waiting for the next active probe.

### 4. Load Balancing Policies
- `RoundRobin`: Distributes requests sequentially across all healthy destinations.
- `PowerOfTwoChoices`: Selects two random healthy destinations and picks the one with the fewest active requests (prevents thundering herd).
- `LeastRequests`: Scans all destinations and chooses the one with the lowest active connection count.

---

## 3. Graph Algorithms: LeetCode #133 & #695

### LeetCode #133: Clone Graph (Medium)
- **Problem**: Given a reference of a node in a connected undirected graph, return a deep copy (clone) of the graph.
- **Cyclic Hazard**: Graphs often contain cycles ($A \leftrightarrow B$). A naive recursive traversal results in infinite recursion and `StackOverflowException`.
- **Solution Strategy**:
  - Use a hash table `Dictionary<Node, Node>` mapping each original node to its newly allocated clone.
  - When visiting a node:
    1. If already present in `visited`, immediately return the clone.
    2. Otherwise, allocate `clone = new Node(node.val)`, register in `visited[node] = clone`.
    3. Recursively (or via BFS queue) clone each neighbor and append to `clone.neighbors`.
  - **Time Complexity**: $\mathcal{O}(V + E)$ where $V$ is vertices and $E$ is edges.
  - **Space Complexity**: $\mathcal{O}(V)$ for the hash map and traversal stack.

### LeetCode #695: Max Area of Island (Medium)
- **Problem**: Given an $m \times n$ binary matrix `grid`, an island is a 4-directionally connected group of `1`s. Return the maximum area of an island.
- **Solution Strategy**:
  - Iterate through every cell $(r, c)$ in the matrix.
  - When encountering `grid[r][c] == 1`:
    - Run DFS or BFS to traverse the entire connected component.
    - Sink visited land cells by mutating `grid[r][c] = 0` (or using a `bool[][] visited` set) to ensure every cell is evaluated in $\mathcal{O}(1)$ amortized operations.
    - Accumulate the area (`1 + sum(neighbors)`) and update `maxArea = max(maxArea, currentArea)`.
  - **Time Complexity**: $\mathcal{O}(M \times N)$ — every cell is processed at most once.
  - **Space Complexity**: $\mathcal{O}(M \times N)$ worst-case DFS call stack (or $\mathcal{O}(\min(M, N))$ for BFS queue).
