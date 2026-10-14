using Day27.ApiGatewayYarp;
using Xunit;
using Yarp.ReverseProxy.Configuration;

namespace Day27.ApiGatewayYarp.Tests;

public class Day27Tests
{
    [Fact]
    public void YarpConfigBuilder_ConstructsValidRoutesAndClusters()
    {
        var builder = new YarpConfigBuilder();
        builder.AddCluster(
            clusterId: "orders-cluster",
            destinationAddresses: new Dictionary<string, string>
            {
                ["dest-1"] = "http://orders-svc-1:5001",
                ["dest-2"] = "http://orders-svc-2:5001"
            },
            loadBalancingPolicy: "RoundRobin",
            enableHealthChecks: true);

        builder.AddRoute(
            routeId: "orders-route",
            clusterId: "orders-cluster",
            pathPattern: "/api/orders/{**catch-all}");

        var (routes, clusters) = builder.Build();

        Assert.Single(routes);
        Assert.Single(clusters);
        Assert.Equal("orders-route", routes[0].RouteId);
        Assert.Equal("orders-cluster", routes[0].ClusterId);
        Assert.Equal(2, clusters[0].Destinations?.Count);
        Assert.NotNull(clusters[0].HealthCheck?.Active);
        Assert.True(clusters[0].HealthCheck!.Active!.Enabled);
    }

    [Fact]
    public void YarpGatewayEngine_ForwardsRequest_ToMatchingCluster()
    {
        var limiter = new GatewayRateLimiter(capacity: 10, refillRatePerSecond: 10.0);
        var engine = new YarpGatewayEngine(limiter);

        var route = new RouteConfig
        {
            RouteId = "inventory-route",
            ClusterId = "inventory-cluster",
            Match = new RouteMatch { Path = "/api/inventory/{**catch-all}" }
        };
        engine.RegisterRoute(route);

        var dest1 = new DownstreamDestination("inv-node-1", new Uri("http://inventory-host:5002/"));
        engine.RegisterCluster("inventory-cluster", new[] { dest1 });

        var request = new GatewayRequest(
            Path: "/api/inventory/items/SKU-999",
            Method: "GET",
            Host: "api.company.com",
            ClientIp: "10.0.0.1",
            Headers: new Dictionary<string, string> { ["User-Agent"] = "TestClient" });

        var result = engine.Forward(request);

        Assert.Equal(200, result.StatusCode);
        Assert.Equal("inv-node-1", result.DestinationId);
        Assert.Equal("http://inventory-host:5002/api/inventory/items/SKU-999", result.DestinationUri?.ToString());
        Assert.NotNull(result.TransformedHeaders);
        Assert.True(result.TransformedHeaders.ContainsKey("X-Correlation-ID"));
    }

    [Fact]
    public void YarpGatewayEngine_AppliesTransforms_HeadersAndPath()
    {
        var limiter = new GatewayRateLimiter(capacity: 10, refillRatePerSecond: 10.0);
        var transforms = new TransformRule
        {
            AddHeaders = new Dictionary<string, string> { ["X-Forwarded-Prefix"] = "/public-gateway" },
            RemoveHeaders = new HashSet<string> { "X-Client-Secret", "Internal-Token" },
            PathRemovePrefix = "/api/v1"
        };

        var engine = new YarpGatewayEngine(limiter, transforms);

        var route = new RouteConfig
        {
            RouteId = "orders-route",
            ClusterId = "orders-cluster",
            Match = new RouteMatch { Path = "/api/v1/orders/{**catch-all}" }
        };
        engine.RegisterRoute(route);

        var dest = new DownstreamDestination("order-node-1", new Uri("http://orders-host:5001/"));
        engine.RegisterCluster("orders-cluster", new[] { dest });

        var request = new GatewayRequest(
            Path: "/api/v1/orders/123",
            Method: "GET",
            Host: "api.company.com",
            ClientIp: "192.168.1.50",
            Headers: new Dictionary<string, string>
            {
                ["X-Client-Secret"] = "super-confidential-key",
                ["Internal-Token"] = "token-secret",
                ["Accept"] = "application/json"
            });

        var result = engine.Forward(request);

        Assert.Equal(200, result.StatusCode);
        Assert.Equal("/orders/123", result.TransformedPath);
        Assert.False(result.TransformedHeaders!.ContainsKey("X-Client-Secret"));
        Assert.False(result.TransformedHeaders.ContainsKey("Internal-Token"));
        Assert.Equal("/public-gateway", result.TransformedHeaders["X-Forwarded-Prefix"]);
    }

    [Fact]
    public void YarpGatewayEngine_FiltersOutUnhealthyDestinations()
    {
        var limiter = new GatewayRateLimiter(capacity: 10, refillRatePerSecond: 10.0);
        var engine = new YarpGatewayEngine(limiter);

        var route = new RouteConfig
        {
            RouteId = "payment-route",
            ClusterId = "payment-cluster",
            Match = new RouteMatch { Path = "/api/payments/{**catch-all}" }
        };
        engine.RegisterRoute(route);

        var destHealthy = new DownstreamDestination("pay-healthy", new Uri("http://pay-1:5003/"))
        {
            HealthStatus = DestinationHealthStatus.Healthy
        };
        var destUnhealthy = new DownstreamDestination("pay-dead", new Uri("http://pay-2:5003/"))
        {
            HealthStatus = DestinationHealthStatus.Unhealthy
        };

        engine.RegisterCluster("payment-cluster", new[] { destUnhealthy, destHealthy });

        for (int i = 0; i < 5; i++)
        {
            var request = new GatewayRequest(
                Path: "/api/payments/charge",
                Method: "POST",
                Host: "api.company.com",
                ClientIp: "10.0.0.1",
                Headers: new Dictionary<string, string>());

            var result = engine.Forward(request);
            Assert.Equal(200, result.StatusCode);
            Assert.Equal("pay-healthy", result.DestinationId);
        }
    }

    [Fact]
    public void YarpGatewayEngine_Returns503_WhenAllDestinationsUnhealthy()
    {
        var limiter = new GatewayRateLimiter(capacity: 10, refillRatePerSecond: 10.0);
        var engine = new YarpGatewayEngine(limiter);

        var route = new RouteConfig
        {
            RouteId = "failover-route",
            ClusterId = "failover-cluster",
            Match = new RouteMatch { Path = "/api/fail/{**catch-all}" }
        };
        engine.RegisterRoute(route);

        var dest = new DownstreamDestination("fail-node", new Uri("http://fail:5000/"))
        {
            HealthStatus = DestinationHealthStatus.Unhealthy
        };
        engine.RegisterCluster("failover-cluster", new[] { dest });

        var request = new GatewayRequest(
            Path: "/api/fail/test",
            Method: "GET",
            Host: "api.company.com",
            ClientIp: "10.0.0.1",
            Headers: new Dictionary<string, string>());

        var result = engine.Forward(request);

        Assert.Equal(503, result.StatusCode);
        Assert.Contains("All destinations for cluster 'failover-cluster' are unhealthy", result.ErrorMessage);
    }

    [Fact]
    public void GatewayRateLimiter_EnforcesLimitAndEmits429()
    {
        var limiter = new GatewayRateLimiter(capacity: 2, refillRatePerSecond: 0.1);
        var engine = new YarpGatewayEngine(limiter);

        var route = new RouteConfig
        {
            RouteId = "rate-route",
            ClusterId = "rate-cluster",
            Match = new RouteMatch { Path = "/api/ping" }
        };
        engine.RegisterRoute(route);
        engine.RegisterCluster("rate-cluster", new[] { new DownstreamDestination("node-1", new Uri("http://ping:80/")) });

        var req = new GatewayRequest("/api/ping", "GET", "api.test.com", "1.2.3.4", new Dictionary<string, string>());

        var res1 = engine.Forward(req);
        var res2 = engine.Forward(req);
        var res3 = engine.Forward(req);

        Assert.Equal(200, res1.StatusCode);
        Assert.Equal(200, res2.StatusCode);
        Assert.Equal(429, res3.StatusCode);
        Assert.NotNull(res3.TransformedHeaders);
        Assert.True(res3.TransformedHeaders.ContainsKey("Retry-After"));
    }

    [Fact]
    public void CloneGraph_Dfs_ClonesCyclicGraphAccurately()
    {
        var solver = new GraphAndIslandSolver();

        // Build cycle graph: 1 -- 2
        //                    |    |
        //                    4 -- 3
        var n1 = new Node(1);
        var n2 = new Node(2);
        var n3 = new Node(3);
        var n4 = new Node(4);

        n1.neighbors = new List<Node> { n2, n4 };
        n2.neighbors = new List<Node> { n1, n3 };
        n3.neighbors = new List<Node> { n2, n4 };
        n4.neighbors = new List<Node> { n1, n3 };

        var clone1 = solver.CloneGraphDfs(n1);

        Assert.NotNull(clone1);
        Assert.NotSame(n1, clone1);
        Assert.Equal(1, clone1.val);
        Assert.Equal(2, clone1.neighbors.Count);

        // Verify deep copy references
        var clone2 = clone1.neighbors.First(n => n.val == 2);
        Assert.NotSame(n2, clone2);
        Assert.Contains(clone2.neighbors, n => n == clone1); // cycle points to clone1, not original n1
    }

    [Fact]
    public void CloneGraph_Bfs_ClonesCyclicGraphAccurately()
    {
        var solver = new GraphAndIslandSolver();

        var n1 = new Node(10);
        var n2 = new Node(20);
        n1.neighbors.Add(n2);
        n2.neighbors.Add(n1);

        var clone1 = solver.CloneGraphBfs(n1);

        Assert.NotNull(clone1);
        Assert.NotSame(n1, clone1);
        Assert.Equal(10, clone1.val);
        Assert.Single(clone1.neighbors);
        Assert.Equal(20, clone1.neighbors[0].val);
        Assert.Same(clone1, clone1.neighbors[0].neighbors[0]);
    }

    [Fact]
    public void CloneGraph_HandlesEdgeCases()
    {
        var solver = new GraphAndIslandSolver();

        Assert.Null(solver.CloneGraphDfs(null));
        Assert.Null(solver.CloneGraphBfs(null));

        // Single isolated node
        var single = new Node(42);
        var clonedSingleDfs = solver.CloneGraphDfs(single);
        Assert.NotNull(clonedSingleDfs);
        Assert.Equal(42, clonedSingleDfs.val);
        Assert.Empty(clonedSingleDfs.neighbors);

        // Self-loop node
        var selfLoop = new Node(99);
        selfLoop.neighbors.Add(selfLoop);
        var clonedSelf = solver.CloneGraphDfs(selfLoop);
        Assert.NotNull(clonedSelf);
        Assert.Single(clonedSelf.neighbors);
        Assert.Same(clonedSelf, clonedSelf.neighbors[0]);
    }

    [Fact]
    public void MaxAreaOfIsland_DfsAndBfs_CalculatesCorrectMaxArea()
    {
        var solver = new GraphAndIslandSolver();

        // Grid with multiple islands: max area is 6
        var grid = new int[][]
        {
            new[] { 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0 },
            new[] { 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 0, 0, 0 },
            new[] { 0, 1, 1, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0 },
            new[] { 0, 1, 0, 0, 1, 1, 0, 0, 1, 0, 1, 0, 0 },
            new[] { 0, 1, 0, 0, 1, 1, 0, 0, 1, 1, 1, 0, 0 },
            new[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0 },
            new[] { 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 0, 0, 0 },
            new[] { 0, 0, 0, 0, 0, 0, 0, 1, 1, 0, 0, 0, 0 }
        };

        var dfsArea = solver.MaxAreaOfIslandDfs(grid);
        var bfsArea = solver.MaxAreaOfIslandBfs(grid);

        Assert.Equal(6, dfsArea);
        Assert.Equal(6, bfsArea);

        // All water
        var waterGrid = new int[][] { new[] { 0, 0 }, new[] { 0, 0 } };
        Assert.Equal(0, solver.MaxAreaOfIslandDfs(waterGrid));
        Assert.Equal(0, solver.MaxAreaOfIslandBfs(waterGrid));

        // Single land
        var singleLand = new int[][] { new[] { 1 } };
        Assert.Equal(1, solver.MaxAreaOfIslandDfs(singleLand));
        Assert.Equal(1, solver.MaxAreaOfIslandBfs(singleLand));
    }
}
