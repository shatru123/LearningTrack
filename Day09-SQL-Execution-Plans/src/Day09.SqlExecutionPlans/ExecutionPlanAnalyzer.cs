using System.Text.RegularExpressions;

namespace Day09.SqlExecutionPlans;

public enum PlanNodeType
{
    SeqScan,
    IndexScan,
    IndexOnlyScan,
    BitmapIndexScan,
    BitmapHeapScan,
    NestedLoop,
    HashJoin,
    MergeJoin,
    Sort,
    Materialize,
    Unknown
}

public sealed record PlanBuffers(
    long SharedHit = 0,
    long SharedRead = 0,
    long SharedWritten = 0,
    long TempRead = 0,
    long TempWritten = 0)
{
    public long TotalSharedBlocks => SharedHit + SharedRead;
    public double CacheHitRatioPercent =>
        TotalSharedBlocks == 0 ? 100.0 : Math.Round((double)SharedHit / TotalSharedBlocks * 100.0, 2);
    public long TotalDiskBytesRead => SharedRead * 8192; // 8KB PostgreSQL pages
}

public sealed record PlanCost(
    double StartupCost,
    double TotalCost,
    long EstimatedRows,
    int RowWidth);

public sealed record ExecutionMetrics(
    double ActualStartupTimeMs,
    double ActualTotalTimeMs,
    long ActualRows,
    int Loops);

public sealed class ExecutionPlanNode
{
    public required PlanNodeType NodeType { get; init; }
    public string? RelationName { get; init; }
    public string? IndexName { get; init; }
    public string? FilterCondition { get; init; }
    public string? IndexCondition { get; init; }
    public required PlanCost Cost { get; init; }
    public ExecutionMetrics? Metrics { get; init; }
    public PlanBuffers Buffers { get; init; } = new();
    public List<ExecutionPlanNode> Children { get; init; } = [];

    public double EstimationSkewRatio =>
        Cost.EstimatedRows == 0 || Metrics == null ? 1.0 :
        Math.Round((double)Metrics.ActualRows / Cost.EstimatedRows, 2);

    public bool HasWorkMemSpill => Buffers.TempRead > 0 || Buffers.TempWritten > 0;
}

public sealed record PlanDiagnostic(
    string Severity, // "Critical", "Warning", "Info"
    string Category,
    string Message,
    string Recommendation);

public static class ExecutionPlanAnalyzer
{
    public static List<PlanDiagnostic> Analyze(ExecutionPlanNode rootNode)
    {
        var diagnostics = new List<PlanDiagnostic>();
        AnalyzeNodeRecursive(rootNode, diagnostics);
        return diagnostics;
    }

    private static void AnalyzeNodeRecursive(ExecutionPlanNode node, List<PlanDiagnostic> diagnostics)
    {
        // 1. Check for unindexed Sequential Scan on substantial row sets
        if (node.NodeType == PlanNodeType.SeqScan && (node.Metrics?.ActualRows ?? node.Cost.EstimatedRows) > 1000)
        {
            diagnostics.Add(new PlanDiagnostic(
                Severity: "Warning",
                Category: "IndexOptimization",
                Message: $"Sequential Scan on table '{node.RelationName ?? "unknown"}' processed {node.Metrics?.ActualRows ?? node.Cost.EstimatedRows:N0} rows.",
                Recommendation: $"Consider creating a B-Tree index on the columns evaluated in filter: '{node.FilterCondition ?? "WHERE condition"}' to avoid full table scans."));
        }

        // 2. Check for Hash Join / Sort WorkMem spill to temporary disk files
        if (node.HasWorkMemSpill)
        {
            diagnostics.Add(new PlanDiagnostic(
                Severity: "Critical",
                Category: "MemoryConfiguration",
                Message: $"Node '{node.NodeType}' spilled to disk: {node.Buffers.TempRead} blocks read, {node.Buffers.TempWritten} blocks written.",
                Recommendation: "Increase 'work_mem' for this session or query to allow in-memory hash table / sorting and eliminate disk I/O."));
        }

        // 3. Check for low buffer cache hit ratio
        if (node.Buffers.TotalSharedBlocks > 100 && node.Buffers.CacheHitRatioPercent < 85.0)
        {
            diagnostics.Add(new PlanDiagnostic(
                Severity: "Warning",
                Category: "BufferCache",
                Message: $"Low buffer cache hit ratio ({node.Buffers.CacheHitRatioPercent}%) on '{node.RelationName ?? node.NodeType.ToString()}'. {node.Buffers.SharedRead} blocks read from disk.",
                Recommendation: "Evaluate 'shared_buffers' allocation or inspect if cold queries / unindexed lookups are causing excessive physical storage reads."));
        }

        // 4. Check for severe cardinality estimation skew (>10x or <0.1x)
        if (node.Metrics != null && (node.EstimationSkewRatio > 10.0 || node.EstimationSkewRatio < 0.1))
        {
            diagnostics.Add(new PlanDiagnostic(
                Severity: "Warning",
                Category: "StaleStatistics",
                Message: $"Significant estimation skew on '{node.NodeType}': Planner estimated {node.Cost.EstimatedRows} rows but actual count was {node.Metrics.ActualRows} (Skew: {node.EstimationSkewRatio}x).",
                Recommendation: $"Run 'ANALYZE {node.RelationName ?? ""}' to refresh table statistics and rebuild the pg_statistic histogram."));
        }

        // 5. Index Scan vs Index Only Scan distinction
        if (node.NodeType == PlanNodeType.IndexScan && node.Buffers.SharedHit > 500 && node.Buffers.SharedRead == 0)
        {
            diagnostics.Add(new PlanDiagnostic(
                Severity: "Info",
                Category: "CoveringIndex",
                Message: $"Index Scan on '{node.IndexName}' required table heap lookups.",
                Recommendation: "Consider including required projection columns in an 'INCLUDE (...)' clause to convert this into an Index Only Scan."));
        }

        foreach (var child in node.Children)
        {
            AnalyzeNodeRecursive(child, diagnostics);
        }
    }

    /// <summary>
    /// Parses a standard PostgreSQL EXPLAIN (ANALYZE, BUFFERS) text line block into structured ExecutionPlanNode.
    /// </summary>
    public static ExecutionPlanNode ParsePlanNode(string planLine, string? bufferLine = null)
    {
        // Example: "Seq Scan on users  (cost=0.00..1834.00 rows=100000 width=72) (actual time=0.012..14.230 rows=100000 loops=1)"
        var nodeType = DetectNodeType(planLine);
        var relation = ExtractRegex(planLine, @"on (\w+)");
        var index = ExtractRegex(planLine, @"using (\w+)");

        var costMatch = Regex.Match(planLine, @"cost=([\d\.]+)\.\.([\d\.]+)\s+rows=(\d+)\s+width=(\d+)");
        double startupCost = costMatch.Success ? double.Parse(costMatch.Groups[1].Value) : 0.0;
        double totalCost = costMatch.Success ? double.Parse(costMatch.Groups[2].Value) : 0.0;
        long estRows = costMatch.Success ? long.Parse(costMatch.Groups[3].Value) : 0;
        int width = costMatch.Success ? int.Parse(costMatch.Groups[4].Value) : 0;

        var actualMatch = Regex.Match(planLine, @"actual time=([\d\.]+)\.\.([\d\.]+)\s+rows=(\d+)\s+loops=(\d+)");
        ExecutionMetrics? metrics = null;
        if (actualMatch.Success)
        {
            metrics = new ExecutionMetrics(
                ActualStartupTimeMs: double.Parse(actualMatch.Groups[1].Value),
                ActualTotalTimeMs: double.Parse(actualMatch.Groups[2].Value),
                ActualRows: long.Parse(actualMatch.Groups[3].Value),
                Loops: int.Parse(actualMatch.Groups[4].Value));
        }

        var buffers = new PlanBuffers();
        if (!string.IsNullOrWhiteSpace(bufferLine))
        {
            // Example: "Buffers: shared hit=421 read=12 written=3 temp read=5 written=5"
            long hit = ParseBufferVal(bufferLine, @"shared hit=(\d+)");
            long read = ParseBufferVal(bufferLine, @"read=(\d+)");
            long written = ParseBufferVal(bufferLine, @"written=(\d+)");
            long tempRead = ParseBufferVal(bufferLine, @"temp read=(\d+)");
            long tempWritten = ParseBufferVal(bufferLine, @"temp.*written=(\d+)");
            buffers = new PlanBuffers(hit, read, written, tempRead, tempWritten);
        }

        return new ExecutionPlanNode
        {
            NodeType = nodeType,
            RelationName = relation,
            IndexName = index,
            Cost = new PlanCost(startupCost, totalCost, estRows, width),
            Metrics = metrics,
            Buffers = buffers
        };
    }

    private static PlanNodeType DetectNodeType(string line)
    {
        if (line.Contains("Index Only Scan")) return PlanNodeType.IndexOnlyScan;
        if (line.Contains("Bitmap Index Scan")) return PlanNodeType.BitmapIndexScan;
        if (line.Contains("Bitmap Heap Scan")) return PlanNodeType.BitmapHeapScan;
        if (line.Contains("Index Scan")) return PlanNodeType.IndexScan;
        if (line.Contains("Seq Scan")) return PlanNodeType.SeqScan;
        if (line.Contains("Nested Loop")) return PlanNodeType.NestedLoop;
        if (line.Contains("Hash Join")) return PlanNodeType.HashJoin;
        if (line.Contains("Merge Join")) return PlanNodeType.MergeJoin;
        if (line.Contains("Sort")) return PlanNodeType.Sort;
        if (line.Contains("Materialize")) return PlanNodeType.Materialize;
        return PlanNodeType.Unknown;
    }

    private static string? ExtractRegex(string input, string pattern)
    {
        var match = Regex.Match(input, pattern);
        return match.Success ? match.Groups[1].Value : null;
    }

    private static long ParseBufferVal(string input, string pattern)
    {
        var match = Regex.Match(input, pattern);
        return match.Success ? long.Parse(match.Groups[1].Value) : 0;
    }
}
