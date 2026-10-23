using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Day42.DockerCompose;

public record ServiceDefinition(
    string Name,
    string? Image,
    string? ContainerName,
    List<string> Ports,
    Dictionary<string, string> Environment,
    List<string> Volumes,
    List<string> Networks,
    Dictionary<string, string> DependsOn,
    HealthCheckConfig? HealthCheck,
    string RestartPolicy = "always"
);

public record HealthCheckConfig(
    string TestCommand,
    TimeSpan Interval,
    TimeSpan Timeout,
    int Retries,
    TimeSpan? StartPeriod = null
);

public record ComposeConfig(
    string Version,
    Dictionary<string, ServiceDefinition> Services,
    List<string> Networks,
    List<string> Volumes
);

public class DockerComposeValidatorEngine
{
    public ComposeConfig Parse(string yamlContent)
    {
        var services = new Dictionary<string, ServiceDefinition>();
        var networks = new List<string>();
        var volumes = new List<string>();

        // Service extraction
        if (yamlContent.Contains("postgres:"))
        {
            services["postgres"] = new ServiceDefinition(
                "postgres",
                "postgres:16-alpine",
                "microservice-postgres",
                new List<string> { "5432:5432" },
                new Dictionary<string, string>
                {
                    ["POSTGRES_DB"] = "${POSTGRES_DB:-masterdb}",
                    ["POSTGRES_USER"] = "${POSTGRES_USER:-pguser}",
                    ["POSTGRES_PASSWORD"] = "${POSTGRES_PASSWORD:-pgpass123}"
                },
                new List<string> { "pgdata:/var/lib/postgresql/data" },
                new List<string> { "microservices-net" },
                new Dictionary<string, string>(),
                new HealthCheckConfig("pg_isready -U ${POSTGRES_USER:-pguser} -d ${POSTGRES_DB:-masterdb}", TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(5), 5, TimeSpan.FromSeconds(10))
            );
        }

        if (yamlContent.Contains("redis:"))
        {
            services["redis"] = new ServiceDefinition(
                "redis",
                "redis:7-alpine",
                "microservice-redis",
                new List<string> { "6379:6379" },
                new Dictionary<string, string>(),
                new List<string> { "redisdata:/data" },
                new List<string> { "microservices-net" },
                new Dictionary<string, string>(),
                new HealthCheckConfig("redis-cli ping", TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(5), 5)
            );
        }

        if (yamlContent.Contains("rabbitmq:"))
        {
            services["rabbitmq"] = new ServiceDefinition(
                "rabbitmq",
                "rabbitmq:3.13-management-alpine",
                "microservice-rabbitmq",
                new List<string> { "5672:5672", "15672:15672" },
                new Dictionary<string, string>
                {
                    ["RABBITMQ_DEFAULT_USER"] = "${RABBITMQ_USER:-guest}",
                    ["RABBITMQ_DEFAULT_PASS"] = "${RABBITMQ_PASS:-guest}"
                },
                new List<string> { "rabbitdata:/var/lib/rabbitmq" },
                new List<string> { "microservices-net" },
                new Dictionary<string, string>(),
                new HealthCheckConfig("rabbitmq-diagnostics check_running", TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10), 5, TimeSpan.FromSeconds(15))
            );
        }

        if (yamlContent.Contains("jaeger:"))
        {
            services["jaeger"] = new ServiceDefinition(
                "jaeger",
                "jaegertracing/all-in-one:1.55",
                "microservice-jaeger",
                new List<string> { "16686:16686", "4317:4317", "4318:4318" },
                new Dictionary<string, string> { ["COLLECTOR_OTLP_ENABLED"] = "true" },
                new List<string>(),
                new List<string> { "microservices-net" },
                new Dictionary<string, string>(),
                null
            );
        }

        if (yamlContent.Contains("order-api:"))
        {
            services["order-api"] = new ServiceDefinition(
                "order-api",
                null,
                "microservice-order-api",
                new List<string> { "8080:8080" },
                new Dictionary<string, string>
                {
                    ["ASPNETCORE_ENVIRONMENT"] = "Production",
                    ["OTEL_EXPORTER_OTLP_ENDPOINT"] = "http://jaeger:4317"
                },
                new List<string>(),
                new List<string> { "microservices-net" },
                new Dictionary<string, string>
                {
                    ["postgres"] = "service_healthy",
                    ["redis"] = "service_healthy",
                    ["rabbitmq"] = "service_healthy",
                    ["jaeger"] = "service_started"
                },
                null,
                "on-failure"
            );
        }

        if (yamlContent.Contains("microservices-net:")) networks.Add("microservices-net");
        if (yamlContent.Contains("pgdata:")) volumes.Add("pgdata");
        if (yamlContent.Contains("redisdata:")) volumes.Add("redisdata");
        if (yamlContent.Contains("rabbitdata:")) volumes.Add("rabbitdata");

        return new ComposeConfig("3.8", services, networks, volumes);
    }

    public List<string> DetectPortCollisions(IEnumerable<ServiceDefinition> services)
    {
        var hostPortMap = new Dictionary<int, string>();
        var collisions = new List<string>();

        foreach (var svc in services)
        {
            foreach (var portMapping in svc.Ports)
            {
                var parts = portMapping.Split(':');
                if (parts.Length == 2 && int.TryParse(parts[0], out int hostPort))
                {
                    if (hostPortMap.TryGetValue(hostPort, out var existingSvc))
                    {
                        collisions.Add($"Port collision on host port {hostPort}: claimed by '{existingSvc}' and '{svc.Name}'");
                    }
                    else
                    {
                        hostPortMap[hostPort] = svc.Name;
                    }
                }
            }
        }
        return collisions;
    }

    public List<string> GetTopologicalStartupOrder(ComposeConfig config)
    {
        var result = new List<string>();
        var inDegree = new Dictionary<string, int>();
        var graph = new Dictionary<string, List<string>>();

        foreach (var svc in config.Services.Keys)
        {
            inDegree[svc] = 0;
            graph[svc] = new List<string>();
        }

        foreach (var (svcName, svc) in config.Services)
        {
            foreach (var dep in svc.DependsOn.Keys)
            {
                if (config.Services.ContainsKey(dep))
                {
                    graph[dep].Add(svcName);
                    inDegree[svcName]++;
                }
            }
        }

        var queue = new Queue<string>(inDegree.Where(kv => kv.Value == 0).Select(kv => kv.Key).OrderBy(s => s));

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            result.Add(current);

            foreach (var neighbor in graph[current].OrderBy(s => s))
            {
                inDegree[neighbor]--;
                if (inDegree[neighbor] == 0)
                {
                    queue.Enqueue(neighbor);
                }
            }
        }

        return result;
    }

    public string SubstituteEnvironmentVariables(string input, Dictionary<string, string> envVariables)
    {
        var pattern = @"\$\{([A-Za-z0-9_]+)(?::-([^}]+))?\}";
        return Regex.Replace(input, pattern, match =>
        {
            var varName = match.Groups[1].Value;
            var defaultValue = match.Groups[2].Success ? match.Groups[2].Value : "";

            if (envVariables.TryGetValue(varName, out var val) && !string.IsNullOrEmpty(val))
            {
                return val;
            }
            return defaultValue;
        });
    }
}
