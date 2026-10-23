using Xunit;

namespace Day39.DistributedConsensus.Tests;

public class Day39Tests
{
    [Fact]
    public void QuorumCluster_StrongConsistency_WhenRPlusWGreaterThanN()
    {
        var strongCluster = new QuorumCluster<string>(nodeCount: 5, writeQuorum: 3, readQuorum: 3); // 3+3=6 > 5
        var weakCluster = new QuorumCluster<string>(nodeCount: 5, writeQuorum: 2, readQuorum: 2);   // 2+2=4 <= 5

        Assert.True(strongCluster.IsStronglyConsistent);
        Assert.False(weakCluster.IsStronglyConsistent);
    }

    [Fact]
    public async Task QuorumCluster_WriteAndReadQuorum_SucceedsUnderHealthyCluster()
    {
        var cluster = new QuorumCluster<string>(nodeCount: 3, writeQuorum: 2, readQuorum: 2);

        await cluster.WriteAsync("user:101", "Alice");
        var readVal = await cluster.ReadAsync("user:101");

        Assert.Equal("Alice", readVal);
    }

    [Fact]
    public async Task QuorumCluster_WriteQuorum_ThrowsQuorumUnavailable_WhenPartitioned()
    {
        var cluster = new QuorumCluster<string>(nodeCount: 3, writeQuorum: 2, readQuorum: 2);

        // Partition 2 of the 3 nodes
        cluster.Nodes[0].IsPartitioned = true;
        cluster.Nodes[1].IsPartitioned = true;

        await Assert.ThrowsAsync<QuorumUnavailableException>(async () =>
        {
            await cluster.WriteAsync("key", "val");
        });
    }

    [Fact]
    public async Task QuorumCluster_ReadRepair_HealsStaleReplicaNode()
    {
        var cluster = new QuorumCluster<string>(nodeCount: 3, writeQuorum: 2, readQuorum: 2);

        // Node 3 is partitioned during write
        cluster.Nodes[2].IsPartitioned = true;
        await cluster.WriteAsync("balance:99", "500.00");

        // Partition heals
        cluster.Nodes[2].IsPartitioned = false;

        // Node 2 is now temporarily offline, but Node 1 and Node 3 are online
        cluster.Nodes[1].IsPartitioned = true;

        // Reading triggers Read Repair from Node 1 (latest) to Node 3 (stale)
        var readVal = await cluster.ReadAsync("balance:99", triggerReadRepair: true);
        Assert.Equal("500.00", readVal);

        // Verify Node 3 now has the healed value
        cluster.Nodes[2].TryRead("balance:99", out var node3Val);
        Assert.NotNull(node3Val);
        Assert.Equal("500.00", node3Val.Value);
    }

    [Fact]
    public void PacelcClassifier_ReturnsCorrectClassifications()
    {
        Assert.Contains("PC/EC", PacelcClassifier.Classify("Spanner"));
        Assert.Contains("PA/EL", PacelcClassifier.Classify("Cassandra"));
        Assert.Contains("PA/EL", PacelcClassifier.Classify("DynamoDB"));
    }

    [Fact]
    public void WordSearch_WordExists_ReturnsTrue()
    {
        char[][] board =
        [
            ['A', 'B', 'C', 'E'],
            ['S', 'F', 'C', 'S'],
            ['A', 'D', 'E', 'E']
        ];

        Assert.True(WordSearchAndPhoneSolver.Exist(board, "ABCCED"));
        Assert.True(WordSearchAndPhoneSolver.Exist(board, "SEE"));
    }

    [Fact]
    public void WordSearch_WordDoesNotExist_ReturnsFalse()
    {
        char[][] board =
        [
            ['A', 'B', 'C', 'E'],
            ['S', 'F', 'C', 'S'],
            ['A', 'D', 'E', 'E']
        ];

        Assert.False(WordSearchAndPhoneSolver.Exist(board, "ABCB"));
    }

    [Fact]
    public void WordSearch_SameCellCannotBeReused_ReturnsFalse()
    {
        char[][] board =
        [
            ['A', 'B'],
            ['C', 'D']
        ];

        // "ABA" would require reusing cell 'A'
        Assert.False(WordSearchAndPhoneSolver.Exist(board, "ABA"));
    }

    [Fact]
    public void LetterCombinations_StandardTwoDigits_ReturnsNineCombinations()
    {
        var result = WordSearchAndPhoneSolver.LetterCombinations("23");

        // 3 * 3 = 9 combinations
        Assert.Equal(9, result.Count);
        Assert.Contains("ad", result);
        Assert.Contains("ae", result);
        Assert.Contains("af", result);
        Assert.Contains("bd", result);
        Assert.Contains("cd", result);
        Assert.Contains("cf", result);
    }

    [Fact]
    public void LetterCombinations_EmptyDigits_ReturnsEmptyList()
    {
        var result = WordSearchAndPhoneSolver.LetterCombinations("");
        Assert.Empty(result);
    }
}
