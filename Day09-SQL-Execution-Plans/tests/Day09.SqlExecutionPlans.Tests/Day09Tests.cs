using FluentAssertions;
using Xunit;

namespace Day09.SqlExecutionPlans.Tests;

public class ExecutionPlanAnalyzerTests
{
    [Fact]
    public void ParsePlanNode_ShouldExtractCosts_AndMetrics_AndBuffers()
    {
        // Arrange
        const string planLine = "Seq Scan on orders  (cost=0.00..3285.00 rows=150000 width=64) (actual time=0.015..18.520 rows=150000 loops=1)";
        const string bufferLine = "Buffers: shared hit=2800 read=485 written=0";

        // Act
        var node = ExecutionPlanAnalyzer.ParsePlanNode(planLine, bufferLine);

        // Assert
        node.NodeType.Should().Be(PlanNodeType.SeqScan);
        node.RelationName.Should().Be("orders");
        node.Cost.StartupCost.Should().Be(0.00);
        node.Cost.TotalCost.Should().Be(3285.00);
        node.Cost.EstimatedRows.Should().Be(150000);
        node.Cost.RowWidth.Should().Be(64);
        node.Metrics.Should().NotBeNull();
        node.Metrics!.ActualRows.Should().Be(150000);
        node.Metrics.ActualTotalTimeMs.Should().Be(18.520);
        node.Buffers.SharedHit.Should().Be(2800);
        node.Buffers.SharedRead.Should().Be(485);
        node.Buffers.TotalSharedBlocks.Should().Be(3285);
        node.Buffers.CacheHitRatioPercent.Should().BeApproximately(85.24, 0.05);
        node.Buffers.TotalDiskBytesRead.Should().Be(485 * 8192);
    }

    [Fact]
    public void Analyze_ShouldFlagWarning_WhenLargeSequentialScanDetected()
    {
        // Arrange
        var root = new ExecutionPlanNode
        {
            NodeType = PlanNodeType.SeqScan,
            RelationName = "customers",
            FilterCondition = "(country = 'US'::text)",
            Cost = new PlanCost(0.0, 1500.0, 50000, 32),
            Metrics = new ExecutionMetrics(0.02, 12.5, 50000, 1)
        };

        // Act
        var diagnostics = ExecutionPlanAnalyzer.Analyze(root);

        // Assert
        diagnostics.Should().ContainSingle(d =>
            d.Category == "IndexOptimization" &&
            d.Severity == "Warning" &&
            d.Message.Contains("Sequential Scan on table 'customers'") &&
            d.Recommendation.Contains("country = 'US'::text"));
    }

    [Fact]
    public void Analyze_ShouldFlagCritical_WhenWorkMemSpillsToDisk()
    {
        // Arrange
        var root = new ExecutionPlanNode
        {
            NodeType = PlanNodeType.HashJoin,
            Cost = new PlanCost(100.0, 5000.0, 20000, 48),
            Buffers = new PlanBuffers(SharedHit: 1000, SharedRead: 200, TempRead: 120, TempWritten: 120)
        };

        // Act
        var diagnostics = ExecutionPlanAnalyzer.Analyze(root);

        // Assert
        diagnostics.Should().ContainSingle(d =>
            d.Category == "MemoryConfiguration" &&
            d.Severity == "Critical" &&
            d.Message.Contains("spilled to disk") &&
            d.Recommendation.Contains("work_mem"));
    }

    [Fact]
    public void Analyze_ShouldFlagWarning_WhenCardinalityEstimationIsSeverelySkewed()
    {
        // Arrange: Planner estimated 100 rows, but actual was 10,000 rows (100x skew)
        var root = new ExecutionPlanNode
        {
            NodeType = PlanNodeType.IndexScan,
            RelationName = "transactions",
            IndexName = "idx_transactions_status",
            Cost = new PlanCost(0.42, 25.0, 100, 16),
            Metrics = new ExecutionMetrics(0.01, 8.4, 10000, 1)
        };

        // Act
        var diagnostics = ExecutionPlanAnalyzer.Analyze(root);

        // Assert
        diagnostics.Should().Contain(d =>
            d.Category == "StaleStatistics" &&
            d.Severity == "Warning" &&
            d.Recommendation.Contains("ANALYZE transactions"));
    }

    [Fact]
    public void Analyze_ShouldSuggestCoveringIndex_ForIndexScanWithHighHeapFetches()
    {
        // Arrange
        var root = new ExecutionPlanNode
        {
            NodeType = PlanNodeType.IndexScan,
            RelationName = "products",
            IndexName = "idx_products_sku",
            Cost = new PlanCost(0.28, 8.5, 800, 24),
            Buffers = new PlanBuffers(SharedHit: 800, SharedRead: 0)
        };

        // Act
        var diagnostics = ExecutionPlanAnalyzer.Analyze(root);

        // Assert
        diagnostics.Should().Contain(d =>
            d.Category == "CoveringIndex" &&
            d.Severity == "Info" &&
            d.Recommendation.Contains("INCLUDE"));
    }
}

public class CharacterReplacementSolverTests
{
    [Theory]
    [InlineData("ABAB", 2, 4)]
    [InlineData("AABABBA", 1, 4)]
    [InlineData("AAAA", 2, 4)]
    [InlineData("A", 0, 1)]
    [InlineData("ABACD", 2, 4)]
    [InlineData("ABBB", 0, 3)]
    [InlineData("ABCDE", 5, 5)]
    [InlineData("BAAAB", 2, 5)]
    public void CharacterReplacement_ShouldReturnMaxWindowLength(string s, int k, int expected)
    {
        // Act
        int result = CharacterReplacementSolver.CharacterReplacement(s, k);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void CharacterReplacement_EmptyString_ShouldReturnZero()
    {
        CharacterReplacementSolver.CharacterReplacement("", 2).Should().Be(0);
    }
}
