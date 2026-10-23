using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace Day44.KubernetesConfigIngress.Tests;

public class Day44Tests
{
    private readonly KubernetesConfigAndIngressEngine _engine;

    public Day44Tests()
    {
        _engine = new KubernetesConfigAndIngressEngine();
    }

    [Fact]
    public void Test01_ConfigMap_LoadsApplicationSettings()
    {
        var configData = new Dictionary<string, string>
        {
            ["Logging:LogLevel:Default"] = "Information",
            ["Features:EnableOrderStreaming"] = "true",
            ["Features:MaxOrderBatchSize"] = "500"
        };

        _engine.LoadConfigMap(configData);

        Assert.Equal("Information", _engine.GetConfigValue("Logging:LogLevel:Default"));
        Assert.Equal("true", _engine.GetConfigValue("Features:EnableOrderStreaming"));
        Assert.Equal("500", _engine.GetConfigValue("Features:MaxOrderBatchSize"));
    }

    [Fact]
    public void Test02_Secret_DecodesBase64CredentialsCorrectly()
    {
        // "Host=postgres;Database=orders" in base64 is "SG9zdD1wb3N0Z3JlcztEYXRhYmFzZT1vcmRlcnM="
        var secrets = new Dictionary<string, string>
        {
            ["DatabaseConnection"] = "SG9zdD1wb3N0Z3JlcztEYXRhYmFzZT1vcmRlcnM=",
            ["JwtKey"] = "c3VwZXJzZWNyZXRqd3RrZXkyMDI2" // "supersecretjwtkey2026"
        };

        _engine.LoadSecret(secrets);

        Assert.Equal("Host=postgres;Database=orders", _engine.GetConfigValue("DatabaseConnection"));
        Assert.Equal("supersecretjwtkey2026", _engine.GetConfigValue("JwtKey"));
    }

    [Fact]
    public void Test03_Ingress_RoutesPathToTargetService()
    {
        _engine.RegisterIngressRoute(new IngressRoute("api.company.com", "/orders", "order-service", 80, true));
        _engine.RegisterIngressRoute(new IngressRoute("api.company.com", "/inventory", "inventory-service", 80, true));

        var (matched1, target1, path1) = _engine.RouteRequest("api.company.com", "/orders/v1/checkout", true);
        var (matched2, target2, path2) = _engine.RouteRequest("api.company.com", "/inventory/items/sku-100", true);

        Assert.True(matched1);
        Assert.Equal("order-service:80", target1);
        Assert.Equal("/v1/checkout", path1);

        Assert.True(matched2);
        Assert.Equal("inventory-service:80", target2);
        Assert.Equal("/items/sku-100", path2);
    }

    [Fact]
    public void Test04_Ingress_RejectsUnmatchedHostOrPath()
    {
        _engine.RegisterIngressRoute(new IngressRoute("api.company.com", "/orders", "order-service", 80, false));

        var (matchedHost, _, _) = _engine.RouteRequest("rogue.attacker.com", "/orders", true);
        var (matchedPath, _, _) = _engine.RouteRequest("api.company.com", "/unknown-route", true);

        Assert.False(matchedHost);
        Assert.False(matchedPath);
    }

    [Fact]
    public void Test05_HotReload_UpdatesConfigStoreOnAtomicSymlinkSwap()
    {
        var initial = new Dictionary<string, string> { ["LogLevel"] = "Information" };
        _engine.LoadConfigMap(initial);

        var updated = new Dictionary<string, string> { ["LogLevel"] = "Debug" };
        bool swapped = _engine.SimulateHotReload("/etc/config/..data", updated);

        Assert.True(swapped);
        Assert.Equal("Debug", _engine.GetConfigValue("LogLevel"));
    }

    [Fact]
    public void Test06_CountPalindromicSubstrings_CalculatesTotalCount()
    {
        Assert.Equal(3, PalindromicSubstringsAndDecodeWaysSolver.CountSubstrings("abc")); // "a", "b", "c"
        Assert.Equal(6, PalindromicSubstringsAndDecodeWaysSolver.CountSubstrings("aaa")); // "a", "a", "a", "aa", "aa", "aaa"
        Assert.Equal(0, PalindromicSubstringsAndDecodeWaysSolver.CountSubstrings(""));
    }

    [Fact]
    public void Test07_DecodeWays_StandardAndValidSequences()
    {
        Assert.Equal(2, PalindromicSubstringsAndDecodeWaysSolver.NumDecodings("12")); // "AB" (1, 2) or "L" (12)
        Assert.Equal(3, PalindromicSubstringsAndDecodeWaysSolver.NumDecodings("226")); // "BZ", "VF", "BBF"
        Assert.Equal(1, PalindromicSubstringsAndDecodeWaysSolver.NumDecodings("10")); // "J" (10)
    }

    [Fact]
    public void Test08_DecodeWays_InvalidLeadingZeroesAndUnreachableCodes()
    {
        Assert.Equal(0, PalindromicSubstringsAndDecodeWaysSolver.NumDecodings("0"));
        Assert.Equal(0, PalindromicSubstringsAndDecodeWaysSolver.NumDecodings("06"));
        Assert.Equal(0, PalindromicSubstringsAndDecodeWaysSolver.NumDecodings("30")); // 30 is invalid because 'Z' is 26
    }

    [Fact]
    public void Test09_DecodeWays_ConsecutiveValidTransitions()
    {
        Assert.Equal(1, PalindromicSubstringsAndDecodeWaysSolver.NumDecodings("27")); // only "BG" (2, 7) because 27 > 26
        Assert.Equal(2, PalindromicSubstringsAndDecodeWaysSolver.NumDecodings("26")); // "BF" or "Z"
    }

    [Fact]
    public void Test10_ManifestFiles_ValidateK8sConfigurations()
    {
        var k8sDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "k8s");
        var cmPath = Path.Combine(k8sDir, "configmap.yaml");
        var secPath = Path.Combine(k8sDir, "secret.yaml");
        var ingPath = Path.Combine(k8sDir, "ingress.yaml");

        if (File.Exists(cmPath))
        {
            var cm = File.ReadAllText(cmPath);
            Assert.Contains("kind: ConfigMap", cm);
            Assert.Contains("Features__EnableOrderStreaming", cm);
        }

        if (File.Exists(secPath))
        {
            var sec = File.ReadAllText(secPath);
            Assert.Contains("kind: Secret", sec);
            Assert.Contains("type: Opaque", sec);
        }

        if (File.Exists(ingPath))
        {
            var ing = File.ReadAllText(ingPath);
            Assert.Contains("kind: Ingress", ing);
            Assert.Contains("nginx.ingress.kubernetes.io/rewrite-target", ing);
        }
    }
}
