namespace Day39.DistributedConsensus;

public class QuorumUnavailableException : Exception
{
    public QuorumUnavailableException(string message) : base(message) { }
}

public record ReplicaValue<T>(T Value, long Version, long TimestampTicks);

public class ReplicaNode<T>
{
    public string NodeId { get; }
    public bool IsPartitioned { get; set; } = false;
    private readonly Dictionary<string, ReplicaValue<T>> _store = new();
    private readonly object _lock = new();

    public ReplicaNode(string nodeId)
    {
        NodeId = nodeId;
    }

    public bool TryWrite(string key, T value, long version, long timestampTicks)
    {
        if (IsPartitioned) return false;

        lock (_lock)
        {
            if (!_store.TryGetValue(key, out var current) || version >= current.Version)
            {
                _store[key] = new ReplicaValue<T>(value, version, timestampTicks);
                return true;
            }
            return false;
        }
    }

    public bool TryRead(string key, out ReplicaValue<T>? value)
    {
        value = null;
        if (IsPartitioned) return false;

        lock (_lock)
        {
            if (_store.TryGetValue(key, out var val))
            {
                value = val;
                return true;
            }
            return true; // Read succeeded, value is null
        }
    }
}

public class QuorumCluster<T>
{
    public int N => Nodes.Count;
    public int W { get; }
    public int R { get; }
    public IReadOnlyList<ReplicaNode<T>> Nodes { get; }
    public bool IsStronglyConsistent => (R + W) > N;

    private long _globalVersion = 0;

    public QuorumCluster(int nodeCount, int writeQuorum, int readQuorum)
    {
        if (writeQuorum > nodeCount || readQuorum > nodeCount)
            throw new ArgumentException("Quorum cannot exceed total node count");

        W = writeQuorum;
        R = readQuorum;

        var list = new List<ReplicaNode<T>>();
        for (int i = 1; i <= nodeCount; i++)
        {
            list.Add(new ReplicaNode<T>($"node-{i}"));
        }
        Nodes = list;
    }

    public async Task<bool> WriteAsync(string key, T value)
    {
        long version = Interlocked.Increment(ref _globalVersion);
        long timestamp = DateTime.UtcNow.Ticks;

        int acks = 0;
        foreach (var node in Nodes)
        {
            if (node.TryWrite(key, value, version, timestamp))
            {
                acks++;
            }
        }

        if (acks < W)
        {
            throw new QuorumUnavailableException(
                $"Write quorum failed: Required {W} acks but received {acks} from {N} nodes.");
        }

        return await Task.FromResult(true);
    }

    public async Task<T?> ReadAsync(string key, bool triggerReadRepair = true)
    {
        var responses = new List<(ReplicaNode<T> Node, ReplicaValue<T>? Value)>();

        foreach (var node in Nodes)
        {
            if (node.TryRead(key, out var val))
            {
                responses.Add((node, val));
            }
        }

        if (responses.Count < R)
        {
            throw new QuorumUnavailableException(
                $"Read quorum failed: Required {R} responses but received {responses.Count} from {N} nodes.");
        }

        // Filter valid non-null values
        var validValues = responses
            .Where(r => r.Value != null)
            .Select(r => r.Value!)
            .ToList();

        if (validValues.Count == 0)
        {
            return default;
        }

        // Conflict resolution: highest version and latest timestamp (Last-Write-Wins)
        var latest = validValues
            .OrderByDescending(v => v.Version)
            .ThenByDescending(v => v.TimestampTicks)
            .First();

        // Read Repair: asynchronously update any node that returned stale data
        if (triggerReadRepair)
        {
            foreach (var (node, val) in responses)
            {
                if (val == null || val.Version < latest.Version)
                {
                    node.TryWrite(key, latest.Value, latest.Version, latest.TimestampTicks);
                }
            }
        }

        return await Task.FromResult(latest.Value);
    }
}

public static class PacelcClassifier
{
    public static string Classify(string systemName)
    {
        return systemName.ToLowerInvariant() switch
        {
            "spanner" or "cockroachdb" => "PC/EC (Consistent on Partition, Consistent on Normal)",
            "cassandra" or "dynamodb" => "PA/EL (Available on Partition, Low Latency on Normal)",
            "mongodb" => "PC/EC (Primary Read/Write) or PA/EL (Secondary Read)",
            _ => "Configurable"
        };
    }
}
