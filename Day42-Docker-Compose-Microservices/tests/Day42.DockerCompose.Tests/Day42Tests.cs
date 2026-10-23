using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace Day42.DockerCompose.Tests;

public class Day42Tests
{
    private readonly string _composeYaml;
    private readonly DockerComposeValidatorEngine _engine;

    public Day42Tests()
    {
        var composePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "docker-compose.microservices.yml");
        if (File.Exists(composePath))
        {
            _composeYaml = File.ReadAllText(composePath);
        }
        else
        {
            _composeYaml = "postgres:\nredis:\nrabbitmq:\njaeger:\norder-api:\nmicroservices-net:\npgdata:\nredisdata:\nrabbitdata:";
        }
        _engine = new DockerComposeValidatorEngine();
    }

    [Fact]
    public void Test01_Compose_DefinesAllRequiredMicroservices()
    {
        var config = _engine.Parse(_composeYaml);

        Assert.True(config.Services.ContainsKey("postgres"));
        Assert.True(config.Services.ContainsKey("redis"));
        Assert.True(config.Services.ContainsKey("rabbitmq"));
        Assert.True(config.Services.ContainsKey("jaeger"));
        Assert.True(config.Services.ContainsKey("order-api"));
    }

    [Fact]
    public void Test02_Compose_ConfiguresCustomBridgeNetworkAndVolumes()
    {
        var config = _engine.Parse(_composeYaml);

        Assert.Contains("microservices-net", config.Networks);
        Assert.Contains("pgdata", config.Volumes);
        Assert.Contains("redisdata", config.Volumes);
        Assert.Contains("rabbitdata", config.Volumes);
    }

    [Fact]
    public void Test03_Compose_HealthChecksConfiguredForDatabasesAndBrokers()
    {
        var config = _engine.Parse(_composeYaml);

        var pg = config.Services["postgres"];
        Assert.NotNull(pg.HealthCheck);
        Assert.Contains("pg_isready", pg.HealthCheck.TestCommand);

        var redis = config.Services["redis"];
        Assert.NotNull(redis.HealthCheck);
        Assert.Contains("redis-cli", redis.HealthCheck.TestCommand);

        var rmq = config.Services["rabbitmq"];
        Assert.NotNull(rmq.HealthCheck);
        Assert.Contains("rabbitmq-diagnostics", rmq.HealthCheck.TestCommand);
    }

    [Fact]
    public void Test04_Compose_OrderApiWaitsForServiceHealthy()
    {
        var config = _engine.Parse(_composeYaml);
        var api = config.Services["order-api"];

        Assert.Equal("service_healthy", api.DependsOn["postgres"]);
        Assert.Equal("service_healthy", api.DependsOn["redis"]);
        Assert.Equal("service_healthy", api.DependsOn["rabbitmq"]);
        Assert.Equal("service_started", api.DependsOn["jaeger"]);
    }

    [Fact]
    public void Test05_PortCollisionDetector_IdentifiesConflictingHostPorts()
    {
        var s1 = new ServiceDefinition("svc1", "img1", null, new List<string> { "5432:5432" }, new(), new(), new(), new(), null);
        var s2 = new ServiceDefinition("svc2", "img2", null, new List<string> { "5432:5433" }, new(), new(), new(), new(), null);

        var collisions = _engine.DetectPortCollisions(new[] { s1, s2 });

        Assert.Single(collisions);
        Assert.Contains("Port collision on host port 5432", collisions[0]);
    }

    [Fact]
    public void Test06_TopologicalSort_OrdersDependenciesBeforeDependentApi()
    {
        var config = _engine.Parse(_composeYaml);
        var order = _engine.GetTopologicalStartupOrder(config);

        int pgIndex = order.IndexOf("postgres");
        int redisIndex = order.IndexOf("redis");
        int rmqIndex = order.IndexOf("rabbitmq");
        int apiIndex = order.IndexOf("order-api");

        Assert.True(pgIndex < apiIndex);
        Assert.True(redisIndex < apiIndex);
        Assert.True(rmqIndex < apiIndex);
    }

    [Fact]
    public void Test07_EnvVarSubstitution_UsesValueOrFallback()
    {
        var template = "Host=${POSTGRES_HOST:-localhost};Database=${POSTGRES_DB:-masterdb};Password=${POSTGRES_PASS:-secret}";
        var env = new Dictionary<string, string>
        {
            ["POSTGRES_HOST"] = "production-cluster"
        };

        var resolved = _engine.SubstituteEnvironmentVariables(template, env);

        Assert.Equal("Host=production-cluster;Database=masterdb;Password=secret", resolved);
    }

    [Fact]
    public void Test08_HouseRobber_StandardLinearStreet_ReturnsOptimalMax()
    {
        int[] street1 = { 1, 2, 3, 1 };
        int[] street2 = { 2, 7, 9, 3, 1 };

        Assert.Equal(4, HouseRobberSolver.Rob(street1));
        Assert.Equal(12, HouseRobberSolver.Rob(street2));
    }

    [Fact]
    public void Test09_HouseRobberII_CircularStreet_PreventsFirstAndLastCollision()
    {
        int[] street1 = { 2, 3, 2 }; // Robbing 3 is optimal because 2 and 2 are adjacent
        int[] street2 = { 1, 2, 3, 1 }; // Rob 2 + 1 = 3 or 1 + 3 = 4 (circular forbids 1 and 1 together)
        int[] street3 = { 1, 2, 3 };

        Assert.Equal(3, HouseRobberSolver.RobCircular(street1));
        Assert.Equal(4, HouseRobberSolver.RobCircular(street2));
        Assert.Equal(3, HouseRobberSolver.RobCircular(street3));
    }

    [Fact]
    public void Test10_HouseRobber_EdgeCases_HandledGracefully()
    {
        Assert.Equal(0, HouseRobberSolver.Rob(Array.Empty<int>()));
        Assert.Equal(0, HouseRobberSolver.RobCircular(Array.Empty<int>()));

        int[] single = { 42 };
        Assert.Equal(42, HouseRobberSolver.Rob(single));
        Assert.Equal(42, HouseRobberSolver.RobCircular(single));

        int[] pair = { 10, 25 };
        Assert.Equal(25, HouseRobberSolver.Rob(pair));
        Assert.Equal(25, HouseRobberSolver.RobCircular(pair));
    }
}
