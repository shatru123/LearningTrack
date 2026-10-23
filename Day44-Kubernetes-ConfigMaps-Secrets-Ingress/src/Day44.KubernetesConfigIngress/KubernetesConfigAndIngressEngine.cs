using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Day44.KubernetesConfigIngress;

public record IngressRoute(
    string Host,
    string PathPrefix,
    string TargetService,
    int TargetPort,
    bool RewriteEnabled
);

public class KubernetesConfigAndIngressEngine
{
    private readonly Dictionary<string, string> _configurationStore = new();
    private readonly List<IngressRoute> _routes = new();
    private bool _tlsTerminated = false;

    public void LoadConfigMap(Dictionary<string, string> data)
    {
        foreach (var (k, v) in data)
        {
            _configurationStore[k] = v;
        }
    }

    public void LoadSecret(Dictionary<string, string> base64Data)
    {
        foreach (var (k, base64Val) in base64Data)
        {
            var decodedBytes = Convert.FromBase64String(base64Val);
            _configurationStore[k] = Encoding.UTF8.GetString(decodedBytes);
        }
    }

    public string? GetConfigValue(string key) => _configurationStore.GetValueOrDefault(key);

    public void RegisterIngressRoute(IngressRoute route)
    {
        _routes.Add(route);
    }

    public (bool Matched, string? TargetService, string? ForwardedPath) RouteRequest(string host, string requestPath, bool isHttps)
    {
        foreach (var r in _routes)
        {
            if (string.Equals(r.Host, host, StringComparison.OrdinalIgnoreCase))
            {
                if (requestPath.StartsWith(r.PathPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    string forwarded = requestPath;
                    if (r.RewriteEnabled)
                    {
                        forwarded = requestPath.Substring(r.PathPrefix.Length);
                        if (!forwarded.StartsWith("/")) forwarded = "/" + forwarded;
                    }
                    return (true, $"{r.TargetService}:{r.TargetPort}", forwarded);
                }
            }
        }
        return (false, null, null);
    }

    public bool SimulateHotReload(string symlinkDataDir, Dictionary<string, string> updatedData)
    {
        // Simulates Kubelet updating the ..data symlink atomic swap
        if (!symlinkDataDir.EndsWith("..data")) return false;

        foreach (var (k, v) in updatedData)
        {
            _configurationStore[k] = v;
        }
        return true;
    }
}
