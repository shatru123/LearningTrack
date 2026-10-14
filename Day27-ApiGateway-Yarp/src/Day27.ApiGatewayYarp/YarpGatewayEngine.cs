using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Forwarder;

namespace Day27.ApiGatewayYarp;

/// <summary>
/// Status of a downstream microservice destination.
/// </summary>
public enum DestinationHealthStatus
{
    Healthy,
    Degraded,
    Unhealthy
}

/// <summary>
/// Represents a downstream service destination with health tracking.
/// </summary>
public class DownstreamDestination
{
    public string Id { get; }
    public Uri Address { get; }
    public DestinationHealthStatus HealthStatus { get; set; } = DestinationHealthStatus.Healthy;
    public int ActiveConnections { get; private set; }

    public DownstreamDestination(string id, Uri address)
    {
        Id = id;
        Address = address;
    }

    public void IncrementConnections() => Interlocked.Increment(ref _activeConnections);
    public void DecrementConnections() => Interlocked.Decrement(ref _activeConnections);
    private int _activeConnections;
}

/// <summary>
/// Defines transform rules applied to requests passing through the API Gateway.
/// </summary>
public record TransformRule
{
    public Dictionary<string, string> AddHeaders { get; init; } = new();
    public HashSet<string> RemoveHeaders { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public string? PathRemovePrefix { get; init; }
    public string? PathAddPrefix { get; init; }
}

/// <summary>
/// Programmatic YARP Route and Cluster Configuration Builder.
/// </summary>
public class YarpConfigBuilder
{
    private readonly List<RouteConfig> _routes = new();
    private readonly List<ClusterConfig> _clusters = new();

    public YarpConfigBuilder AddCluster(
        string clusterId,
        IReadOnlyDictionary<string, string> destinationAddresses,
        string loadBalancingPolicy = "RoundRobin",
        bool enableHealthChecks = true)
    {
        var destinations = destinationAddresses.ToDictionary(
            kvp => kvp.Key,
            kvp => new DestinationConfig { Address = kvp.Value });

        var cluster = new ClusterConfig
        {
            ClusterId = clusterId,
            LoadBalancingPolicy = loadBalancingPolicy,
            Destinations = destinations,
            HealthCheck = enableHealthChecks ? new HealthCheckConfig
            {
                Active = new ActiveHealthCheckConfig
                {
                    Enabled = true,
                    Interval = TimeSpan.FromSeconds(10),
                    Timeout = TimeSpan.FromSeconds(2),
                    Policy = "ConsecutiveFailures",
                    Path = "/health"
                }
            } : null
        };

        _clusters.Add(cluster);
        return this;
    }

    public YarpConfigBuilder AddRoute(
        string routeId,
        string clusterId,
        string pathPattern,
        IReadOnlyList<IReadOnlyDictionary<string, string>>? transforms = null,
        string? authorizationPolicy = null,
        string? rateLimiterPolicy = null)
    {
        var route = new RouteConfig
        {
            RouteId = routeId,
            ClusterId = clusterId,
            Match = new RouteMatch
            {
                Path = pathPattern
            },
            AuthorizationPolicy = authorizationPolicy,
            RateLimiterPolicy = rateLimiterPolicy,
            Transforms = transforms
        };

        _routes.Add(route);
        return this;
    }

    public (IReadOnlyList<RouteConfig> Routes, IReadOnlyList<ClusterConfig> Clusters) Build()
    {
        return (_routes.AsReadOnly(), _clusters.AsReadOnly());
    }
}

/// <summary>
/// Gateway Token Bucket Rate Limiter partitioned by Client IP or API Key.
/// </summary>
public class GatewayRateLimiter
{
    private readonly int _capacity;
    private readonly double _refillRatePerSecond;
    private readonly ConcurrentDictionary<string, TokenBucket> _clientBuckets = new();

    public GatewayRateLimiter(int capacity, double refillRatePerSecond)
    {
        if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
        if (refillRatePerSecond <= 0) throw new ArgumentOutOfRangeException(nameof(refillRatePerSecond));

        _capacity = capacity;
        _refillRatePerSecond = refillRatePerSecond;
    }

    public bool TryConsume(string clientKey, out int remainingTokens, out TimeSpan retryAfter)
    {
        var bucket = _clientBuckets.GetOrAdd(clientKey, _ => new TokenBucket(_capacity, _refillRatePerSecond));
        return bucket.TryConsume(out remainingTokens, out retryAfter);
    }

    private class TokenBucket
    {
        private readonly int _maxTokens;
        private readonly double _refillRate;
        private double _currentTokens;
        private DateTime _lastRefillUtc;
        private readonly object _lock = new();

        public TokenBucket(int maxTokens, double refillRate)
        {
            _maxTokens = maxTokens;
            _refillRate = refillRate;
            _currentTokens = maxTokens;
            _lastRefillUtc = DateTime.UtcNow;
        }

        public bool TryConsume(out int remaining, out TimeSpan retryAfter)
        {
            lock (_lock)
            {
                var now = DateTime.UtcNow;
                var elapsed = (now - _lastRefillUtc).TotalSeconds;
                _currentTokens = Math.Min(_maxTokens, _currentTokens + (elapsed * _refillRate));
                _lastRefillUtc = now;

                if (_currentTokens >= 1.0)
                {
                    _currentTokens -= 1.0;
                    remaining = (int)_currentTokens;
                    retryAfter = TimeSpan.Zero;
                    return true;
                }

                remaining = 0;
                var missingTokens = 1.0 - _currentTokens;
                retryAfter = TimeSpan.FromSeconds(Math.Ceiling(missingTokens / _refillRate));
                return false;
            }
        }
    }
}

/// <summary>
/// High-throughput simulated YARP Reverse Proxy Engine.
/// Handles route dispatching, request transformation, health check filtering, and destination load balancing.
/// </summary>
public class YarpGatewayEngine
{
    private readonly List<RouteConfig> _routes = new();
    private readonly Dictionary<string, List<DownstreamDestination>> _clusterDestinations = new();
    private readonly ConcurrentDictionary<string, int> _clusterRoundRobinIndices = new();
    private readonly GatewayRateLimiter _rateLimiter;
    private readonly TransformRule _globalTransforms;

    public YarpGatewayEngine(GatewayRateLimiter rateLimiter, TransformRule? globalTransforms = null)
    {
        _rateLimiter = rateLimiter;
        _globalTransforms = globalTransforms ?? new TransformRule();
    }

    public void RegisterRoute(RouteConfig route)
    {
        _routes.Add(route);
    }

    public void RegisterCluster(string clusterId, IEnumerable<DownstreamDestination> destinations)
    {
        _clusterDestinations[clusterId] = destinations.ToList();
        _clusterRoundRobinIndices[clusterId] = 0;
    }

    /// <summary>
    /// Processes an incoming request through Rate Limiter, Route Matching, Transform Pipeline, and Load Balancer.
    /// </summary>
    public GatewayProxyResult Forward(GatewayRequest request)
    {
        // 1. Rate Limiting Check
        if (!_rateLimiter.TryConsume(request.ClientIp, out var remainingTokens, out var retryAfter))
        {
            return new GatewayProxyResult(
                StatusCode: 429,
                ErrorMessage: "Rate limit exceeded. Try again later.",
                TransformedHeaders: new Dictionary<string, string>
                {
                    ["Retry-After"] = ((int)retryAfter.TotalSeconds).ToString(),
                    ["X-RateLimit-Remaining"] = "0"
                });
        }

        // 2. Route Matching
        var matchedRoute = FindMatchingRoute(request.Path);
        if (matchedRoute == null)
        {
            return new GatewayProxyResult(
                StatusCode: 404,
                ErrorMessage: $"No matching YARP route found for path: {request.Path}");
        }

        // 3. Cluster & Healthy Destination Selection
        if (!_clusterDestinations.TryGetValue(matchedRoute.ClusterId ?? string.Empty, out var destinations))
        {
            return new GatewayProxyResult(
                StatusCode: 502,
                ErrorMessage: $"Target cluster '{matchedRoute.ClusterId}' not configured.");
        }

        var healthyDestinations = destinations
            .Where(d => d.HealthStatus == DestinationHealthStatus.Healthy)
            .ToList();

        if (healthyDestinations.Count == 0)
        {
            return new GatewayProxyResult(
                StatusCode: 503,
                ErrorMessage: $"All destinations for cluster '{matchedRoute.ClusterId}' are unhealthy.");
        }

        // Round-Robin selection over healthy destinations
        var index = _clusterRoundRobinIndices.AddOrUpdate(
            matchedRoute.ClusterId!,
            0,
            (_, curr) => (curr + 1) % healthyDestinations.Count);
        var targetDestination = healthyDestinations[index % healthyDestinations.Count];

        // 4. Request Transformation
        var transformedHeaders = ApplyHeaderTransforms(request.Headers);
        transformedHeaders["X-RateLimit-Remaining"] = remainingTokens.ToString();
        transformedHeaders["X-Forwarded-Host"] = request.Host;
        transformedHeaders["X-Forwarded-For"] = request.ClientIp;
        if (!transformedHeaders.ContainsKey("X-Correlation-ID"))
        {
            transformedHeaders["X-Correlation-ID"] = Guid.NewGuid().ToString("N");
        }

        var transformedPath = ApplyPathTransforms(request.Path, matchedRoute);

        return new GatewayProxyResult(
            StatusCode: 200,
            DestinationId: targetDestination.Id,
            DestinationUri: new Uri(targetDestination.Address, transformedPath.TrimStart('/')),
            TransformedPath: transformedPath,
            TransformedHeaders: transformedHeaders);
    }

    private RouteConfig? FindMatchingRoute(string path)
    {
        foreach (var route in _routes)
        {
            if (string.IsNullOrEmpty(route.Match.Path)) continue;

            // Simplified prefix & wildcards matching (e.g. /api/orders/{**catch-all} or /api/orders)
            var pattern = route.Match.Path
                .Replace("{**catch-all}", ".*")
                .Replace("{**remainder}", ".*");

            if (Regex.IsMatch(path, $"^{pattern}$", RegexOptions.IgnoreCase))
            {
                return route;
            }
        }
        return null;
    }

    private Dictionary<string, string> ApplyHeaderTransforms(IReadOnlyDictionary<string, string> incomingHeaders)
    {
        var headers = new Dictionary<string, string>(incomingHeaders, StringComparer.OrdinalIgnoreCase);

        // Strip disallowed headers
        foreach (var removeKey in _globalTransforms.RemoveHeaders)
        {
            headers.Remove(removeKey);
        }

        // Add configured headers
        foreach (var (k, v) in _globalTransforms.AddHeaders)
        {
            headers[k] = v;
        }

        return headers;
    }

    private string ApplyPathTransforms(string incomingPath, RouteConfig route)
    {
        var path = incomingPath;

        if (_globalTransforms.PathRemovePrefix != null && path.StartsWith(_globalTransforms.PathRemovePrefix, StringComparison.OrdinalIgnoreCase))
        {
            path = path[_globalTransforms.PathRemovePrefix.Length..];
        }

        if (_globalTransforms.PathAddPrefix != null)
        {
            path = _globalTransforms.PathAddPrefix.TrimEnd('/') + "/" + path.TrimStart('/');
        }

        return string.IsNullOrEmpty(path) ? "/" : path;
    }
}

public record GatewayRequest(
    string Path,
    string Method,
    string Host,
    string ClientIp,
    Dictionary<string, string> Headers);

public record GatewayProxyResult(
    int StatusCode,
    string? DestinationId = null,
    Uri? DestinationUri = null,
    string? TransformedPath = null,
    Dictionary<string, string>? TransformedHeaders = null,
    string? ErrorMessage = null);
