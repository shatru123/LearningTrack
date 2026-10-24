namespace Day47.TerraformIaC;

using System;
using System.Collections.Generic;
using System.Linq;

public record TerraformResource(
    string Type,
    string Name,
    Dictionary<string, object> Attributes,
    List<string>? ExplicitDependsOn = null)
{
    public string Id => $"{Type}.{Name}";
}

public record TerraformVariable(string Name, string Type, object? DefaultValue, bool IsSensitive = false);

public record TerraformOutput(string Name, string ValueExpression, bool IsSensitive = false);

public enum PlanAction
{
    NoOp,
    Create,
    Update,
    Destroy
}

public record ResourcePlan(string ResourceId, PlanAction Action, Dictionary<string, object> Changes);

public class TerraformGraphBuilder
{
    public static List<string> BuildExecutionOrder(List<TerraformResource> resources)
    {
        var resourceMap = resources.ToDictionary(r => r.Id, r => r);
        var inDegree = resources.ToDictionary(r => r.Id, _ => 0);
        var adj = resources.ToDictionary(r => r.Id, _ => new List<string>());

        foreach (var res in resources)
        {
            var dependencies = new HashSet<string>(res.ExplicitDependsOn ?? Enumerable.Empty<string>());

            // Scan attributes for implicit dependencies (references like "azurerm_resource_group.rg.name")
            foreach (var kvp in res.Attributes)
            {
                if (kvp.Value is string strVal)
                {
                    foreach (var candidate in resourceMap.Keys)
                    {
                        if (candidate != res.Id && strVal.Contains(candidate, StringComparison.OrdinalIgnoreCase))
                        {
                            dependencies.Add(candidate);
                        }
                    }
                }
            }

            foreach (var dep in dependencies)
            {
                if (!resourceMap.ContainsKey(dep))
                {
                    throw new InvalidOperationException($"Resource '{res.Id}' depends on unknown resource '{dep}'.");
                }
                adj[dep].Add(res.Id);
                inDegree[res.Id]++;
            }
        }

        // Kahn's algorithm for topological sorting
        var queue = new Queue<string>(inDegree.Where(kvp => kvp.Value == 0).Select(kvp => kvp.Key));
        var order = new List<string>();

        while (queue.Count > 0)
        {
            var curr = queue.Dequeue();
            order.Add(curr);

            foreach (var neighbor in adj[curr])
            {
                inDegree[neighbor]--;
                if (inDegree[neighbor] == 0)
                {
                    queue.Enqueue(neighbor);
                }
            }
        }

        if (order.Count != resources.Count)
        {
            throw new InvalidOperationException("Circular dependency detected in Terraform resource graph!");
        }

        return order;
    }
}

public class TerraformStateLockManager
{
    private readonly object _lock = new();
    private string? _currentLockHolder;
    private string? _activeLeaseToken;

    public bool TryAcquireLock(string lockId, out string leaseToken)
    {
        lock (_lock)
        {
            if (_currentLockHolder != null)
            {
                leaseToken = string.Empty;
                return false;
            }

            _currentLockHolder = lockId;
            _activeLeaseToken = Guid.NewGuid().ToString("N");
            leaseToken = _activeLeaseToken;
            return true;
        }
    }

    public bool ReleaseLock(string lockId, string leaseToken)
    {
        lock (_lock)
        {
            if (_currentLockHolder == lockId && _activeLeaseToken == leaseToken)
            {
                _currentLockHolder = null;
                _activeLeaseToken = null;
                return true;
            }
            return false;
        }
    }

    public bool IsLocked => _currentLockHolder != null;
}

public class TerraformPlanEngine
{
    public static List<ResourcePlan> GeneratePlan(
        List<TerraformResource> desiredResources,
        Dictionary<string, Dictionary<string, object>> currentState)
    {
        var plans = new List<ResourcePlan>();
        var desiredMap = desiredResources.ToDictionary(r => r.Id, r => r);

        // Check for Create and Update
        foreach (var res in desiredResources)
        {
            if (!currentState.TryGetValue(res.Id, out var stateAttrs))
            {
                plans.Add(new ResourcePlan(res.Id, PlanAction.Create, res.Attributes));
            }
            else
            {
                var diff = new Dictionary<string, object>();
                foreach (var attr in res.Attributes)
                {
                    if (!stateAttrs.TryGetValue(attr.Key, out var currentVal) || !Equals(attr.Value, currentVal))
                    {
                        diff[attr.Key] = attr.Value;
                    }
                }

                if (diff.Count > 0)
                {
                    plans.Add(new ResourcePlan(res.Id, PlanAction.Update, diff));
                }
                else
                {
                    plans.Add(new ResourcePlan(res.Id, PlanAction.NoOp, new Dictionary<string, object>()));
                }
            }
        }

        // Check for Destroy
        foreach (var stateId in currentState.Keys)
        {
            if (!desiredMap.ContainsKey(stateId))
            {
                plans.Add(new ResourcePlan(stateId, PlanAction.Destroy, currentState[stateId]));
            }
        }

        return plans;
    }

    public static Dictionary<string, string> RenderOutputs(
        List<TerraformOutput> outputs,
        Dictionary<string, string> computedValues)
    {
        var result = new Dictionary<string, string>();
        foreach (var outDef in outputs)
        {
            var rawVal = computedValues.TryGetValue(outDef.Name, out var val) ? val : "<unknown>";
            result[outDef.Name] = outDef.IsSensitive ? "(sensitive value)" : rawVal;
        }
        return result;
    }

    public static List<string> DetectDrift(
        Dictionary<string, Dictionary<string, object>> tfState,
        Dictionary<string, Dictionary<string, object>> liveCloudState)
    {
        var drifts = new List<string>();

        foreach (var (resId, stateAttrs) in tfState)
        {
            if (!liveCloudState.TryGetValue(resId, out var liveAttrs))
            {
                drifts.Add($"Resource '{resId}' exists in tfstate but is missing in live cloud!");
                continue;
            }

            foreach (var (attrKey, stateVal) in stateAttrs)
            {
                if (!liveAttrs.TryGetValue(attrKey, out var liveVal) || !Equals(stateVal, liveVal))
                {
                    drifts.Add($"Drift in '{resId}': attribute '{attrKey}' expected '{stateVal}', found '{liveVal}'.");
                }
            }
        }

        return drifts;
    }
}
