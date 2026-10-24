namespace Day47.TerraformIaC.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using Day47.TerraformIaC;
using Xunit;

public class Day47Tests
{
    [Fact]
    public void Test1_TopologicalSort_ResolvesDependenciesInCorrectOrder()
    {
        var rg = new TerraformResource("azurerm_resource_group", "rg", new() { ["name"] = "rg-prod" });
        var vnet = new TerraformResource("azurerm_virtual_network", "vnet", new() { ["rg_name"] = "azurerm_resource_group.rg.name" });
        var db = new TerraformResource("azurerm_postgresql_flexible_server", "pg", new() { ["vnet_id"] = "azurerm_virtual_network.vnet.id" });

        var order = TerraformGraphBuilder.BuildExecutionOrder(new() { db, rg, vnet });

        Assert.Equal(new[] { "azurerm_resource_group.rg", "azurerm_virtual_network.vnet", "azurerm_postgresql_flexible_server.pg" }, order);
    }

    [Fact]
    public void Test2_TopologicalSort_ThrowsOnCircularDependency()
    {
        var resA = new TerraformResource("res_a", "a", new() { ["ref"] = "res_b.b.id" });
        var resB = new TerraformResource("res_b", "b", new() { ["ref"] = "res_a.a.id" });

        var ex = Assert.Throws<InvalidOperationException>(() => TerraformGraphBuilder.BuildExecutionOrder(new() { resA, resB }));
        Assert.Contains("Circular dependency", ex.Message);
    }

    [Fact]
    public void Test3_RemoteStateLock_PreventsConcurrentAccess()
    {
        var lockManager = new TerraformStateLockManager();

        bool acquired1 = lockManager.TryAcquireLock("process_1", out var lease1);
        bool acquired2 = lockManager.TryAcquireLock("process_2", out var lease2);

        Assert.True(acquired1);
        Assert.False(string.IsNullOrWhiteSpace(lease1));
        Assert.False(acquired2);
        Assert.Empty(lease2);
        Assert.True(lockManager.IsLocked);
    }

    [Fact]
    public void Test4_RemoteStateLock_AllowsAcquisitionAfterRelease()
    {
        var lockManager = new TerraformStateLockManager();

        lockManager.TryAcquireLock("process_1", out var lease1);
        bool released = lockManager.ReleaseLock("process_1", lease1);
        bool acquired2 = lockManager.TryAcquireLock("process_2", out var lease2);

        Assert.True(released);
        Assert.True(acquired2);
        Assert.False(string.IsNullOrWhiteSpace(lease2));
    }

    [Fact]
    public void Test5_PlanGeneration_IdentifiesCreateUpdateDestroy()
    {
        var desired = new List<TerraformResource>
        {
            new("azurerm_resource_group", "rg", new() { ["location"] = "eastus" }),
            new("azurerm_container_app", "app", new() { ["replicas"] = 5 })
        };

        var currentState = new Dictionary<string, Dictionary<string, object>>
        {
            ["azurerm_resource_group.rg"] = new() { ["location"] = "eastus" },
            ["azurerm_redis_cache.redis"] = new() { ["sku"] = "Basic" }
        };

        var plan = TerraformPlanEngine.GeneratePlan(desired, currentState);

        Assert.Contains(plan, p => p.ResourceId == "azurerm_resource_group.rg" && p.Action == PlanAction.NoOp);
        Assert.Contains(plan, p => p.ResourceId == "azurerm_container_app.app" && p.Action == PlanAction.Create);
        Assert.Contains(plan, p => p.ResourceId == "azurerm_redis_cache.redis" && p.Action == PlanAction.Destroy);
    }

    [Fact]
    public void Test6_PlanEngine_MasksSensitiveOutputs()
    {
        var outputs = new List<TerraformOutput>
        {
            new("public_fqdn", "fqdn_expr", IsSensitive: false),
            new("db_conn_str", "conn_expr", IsSensitive: true)
        };

        var computed = new Dictionary<string, string>
        {
            ["public_fqdn"] = "app.azurecontainerapps.io",
            ["db_conn_str"] = "Host=mypsql.postgres.database.azure.com;Password=Secret123;"
        };

        var rendered = TerraformPlanEngine.RenderOutputs(outputs, computed);

        Assert.Equal("app.azurecontainerapps.io", rendered["public_fqdn"]);
        Assert.Equal("(sensitive value)", rendered["db_conn_str"]);
    }

    [Fact]
    public void Test7_DriftDetection_IdentifiesModifiedCloudState()
    {
        var tfState = new Dictionary<string, Dictionary<string, object>>
        {
            ["azurerm_postgresql_flexible_server.pg"] = new() { ["sku"] = "GP_Standard_D2s_v3", ["storage"] = 32768 }
        };

        var liveState = new Dictionary<string, Dictionary<string, object>>
        {
            ["azurerm_postgresql_flexible_server.pg"] = new() { ["sku"] = "GP_Standard_D4s_v3", ["storage"] = 32768 }
        };

        var drifts = TerraformPlanEngine.DetectDrift(tfState, liveState);

        Assert.Single(drifts);
        Assert.Contains("sku", drifts[0]);
        Assert.Contains("GP_Standard_D4s_v3", drifts[0]);
    }

    [Theory]
    [InlineData(new int[] { 10, 9, 2, 5, 3, 7, 101, 18 }, 4)]
    [InlineData(new int[] { 0, 1, 0, 3, 2, 3 }, 4)]
    [InlineData(new int[] { 7, 7, 7, 7, 7 }, 1)]
    [InlineData(new int[] { 1, 3, 6, 7, 9, 4, 10, 5, 6 }, 6)]
    public void Test8_Lis_DpAndPatience_YieldSameResult(int[] nums, int expected)
    {
        int dpResult = LongestIncreasingSubsequenceSolver.SolveDp(nums);
        int patienceResult = LongestIncreasingSubsequenceSolver.SolvePatience(nums);

        Assert.Equal(expected, dpResult);
        Assert.Equal(expected, patienceResult);
    }

    [Fact]
    public void Test9_Lis_EmptyAndSingleElement_HandledGracefully()
    {
        Assert.Equal(0, LongestIncreasingSubsequenceSolver.SolveDp(Array.Empty<int>()));
        Assert.Equal(0, LongestIncreasingSubsequenceSolver.SolvePatience(Array.Empty<int>()));

        Assert.Equal(1, LongestIncreasingSubsequenceSolver.SolveDp(new int[] { 42 }));
        Assert.Equal(1, LongestIncreasingSubsequenceSolver.SolvePatience(new int[] { 42 }));
    }

    [Fact]
    public void Test10_Lis_ReconstructSequence_ProducesStrictlyIncreasingSubsequence()
    {
        int[] nums = new int[] { 10, 22, 9, 33, 21, 50, 41, 60, 80 };
        var actual = LongestIncreasingSubsequenceSolver.GetActualLis(nums);

        Assert.Equal(6, actual.Length); // e.g. [10, 22, 33, 50, 60, 80]
        for (int i = 1; i < actual.Length; i++)
        {
            Assert.True(actual[i] > actual[i - 1]);
        }
    }
}
