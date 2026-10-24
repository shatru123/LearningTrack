namespace Day51.AiLlmFoundations.Tests;

using System;
using System.Collections.Generic;
using Day51.AiLlmFoundations;
using Xunit;

public class Day51Tests
{
    [Fact]
    public void Test1_AttentionEngine_ComputesValidAttentionWeights()
    {
        // 2 tokens, embedding dimension 2
        double[,] Q = { { 1.0, 0.0 }, { 0.0, 1.0 } };
        double[,] K = { { 1.0, 0.0 }, { 0.0, 1.0 } };
        double[,] V = { { 10.0, 20.0 }, { 30.0, 40.0 } };

        double[,] output = TransformerAttentionEngine.ScaledDotProductAttention(Q, K, V, isCausal: false);

        Assert.Equal(2, output.GetLength(0));
        Assert.Equal(2, output.GetLength(1));
        // Token 0 attends mostly to Key 0
        Assert.True(output[0, 0] > 10.0);
        Assert.True(output[1, 1] > 20.0);
    }

    [Fact]
    public void Test2_AttentionEngine_CausalMaskPreventsFutureTokenAttention()
    {
        double[,] Q = { { 1.0, 1.0 }, { 1.0, 1.0 } };
        double[,] K = { { 1.0, 1.0 }, { 1.0, 1.0 } };
        double[,] V = { { 5.0, 5.0 }, { 50.0, 50.0 } };

        double[,] output = TransformerAttentionEngine.ScaledDotProductAttention(Q, K, V, isCausal: true);

        // Token 0 can only attend to position 0 (value 5.0)
        Assert.Equal(5.0, Math.Round(output[0, 0], 2));
        Assert.Equal(5.0, Math.Round(output[0, 1], 2));
    }

    [Fact]
    public void Test3_PositionalEncoding_GeneratesDistinctMonotonicVectors()
    {
        double[,] pe = TransformerAttentionEngine.GeneratePositionalEncoding(seqLen: 4, dModel: 8);

        Assert.Equal(4, pe.GetLength(0));
        Assert.Equal(8, pe.GetLength(1));

        // Position 0 vs Position 1 must differ
        Assert.NotEqual(pe[0, 0], pe[1, 0]);
        // Values must be within [-1, 1] (sin/cos bounds)
        for (int p = 0; p < 4; p++)
        {
            for (int d = 0; d < 8; d++)
            {
                Assert.InRange(pe[p, d], -1.0, 1.0);
            }
        }
    }

    [Fact]
    public void Test4_BpeTokenizer_TrainsAndMergesPairs()
    {
        var tokenizer = new BytePairEncodingTokenizer();
        string corpus = "low lower newest widest lowest";

        tokenizer.Train(corpus, numMerges: 4);
        var tokens = tokenizer.Tokenize("lower lowest");

        Assert.True(tokenizer.VocabSize > 5);
        Assert.Contains(tokens, t => t.Contains("low"));
    }

    [Fact]
    public void Test5_LlmSampling_TemperatureAltersProbabilityDistribution()
    {
        double[] logits = { 2.0, 1.0, 0.5 };

        double[] sharpProb = LlmSamplingEngine.ApplyTemperature(logits, temperature: 0.2);
        double[] flatProb = LlmSamplingEngine.ApplyTemperature(logits, temperature: 2.0);

        // Low temperature sharpens peak
        Assert.True(sharpProb[0] > 0.90);
        // High temperature flattens distribution
        Assert.True(flatProb[0] < 0.60);
        Assert.True(flatProb[1] > 0.20);
    }

    [Fact]
    public void Test6_LlmSampling_TopPNucleusFiltersTails()
    {
        double[] probs = { 0.60, 0.25, 0.10, 0.05 };

        double[] filtered = LlmSamplingEngine.ApplyTopP(probs, p: 0.80);

        // Top-P 0.80 selects 0.60 + 0.25 = 0.85 (2 tokens) and zeroes remaining
        Assert.True(filtered[0] > 0);
        Assert.True(filtered[1] > 0);
        Assert.Equal(0.0, filtered[2]);
        Assert.Equal(0.0, filtered[3]);
        // Sum after re-normalization must be 1.0
        Assert.Equal(1.0, Math.Round(filtered[0] + filtered[1], 5));
    }

    [Fact]
    public void Test7_LlmSampling_RepetitionPenaltyDecreasesLogits()
    {
        double[] logits = { 5.0, 5.0, 5.0 };
        var freq = new Dictionary<int, int> { [0] = 2, [1] = 1 };

        double[] penalized = LlmSamplingEngine.ApplyRepetitionPenalty(
            logits, freq, frequencyPenalty: 0.5, presencePenalty: 0.2);

        // Token 0 penalized: 5 - (2*0.5 + 0.2) = 3.8
        Assert.Equal(3.8, Math.Round(penalized[0], 2));
        // Token 1 penalized: 5 - (1*0.5 + 0.2) = 4.3
        Assert.Equal(4.3, Math.Round(penalized[1], 2));
        // Token 2 untouched: 5.0
        Assert.Equal(5.0, penalized[2]);
    }

    [Theory]
    [InlineData(3, 7, 28)]
    [InlineData(3, 2, 3)]
    [InlineData(1, 1, 1)]
    [InlineData(4, 4, 20)]
    public void Test8_UniquePaths_AllApproachesMatch(int m, int n, int expected)
    {
        int dp2d = UniquePathsAndLcsSolver.UniquePaths2D(m, n);
        int dp1d = UniquePathsAndLcsSolver.UniquePaths1D(m, n);
        long comb = UniquePathsAndLcsSolver.UniquePathsCombinatorics(m, n);

        Assert.Equal(expected, dp2d);
        Assert.Equal(expected, dp1d);
        Assert.Equal((long)expected, comb);
    }

    [Fact]
    public void Test9_LongestCommonSubsequence_LengthAndStringReconstruction()
    {
        string t1 = "abcde";
        string t2 = "ace";

        int length = UniquePathsAndLcsSolver.LongestCommonSubsequenceLength(t1, t2);
        string lcs = UniquePathsAndLcsSolver.GetLcsString(t1, t2);

        Assert.Equal(3, length);
        Assert.Equal("ace", lcs);
    }

    [Fact]
    public void Test10_Lcs_DisjointAndIdenticalStrings()
    {
        Assert.Equal(0, UniquePathsAndLcsSolver.LongestCommonSubsequenceLength("abc", "def"));
        Assert.Equal("", UniquePathsAndLcsSolver.GetLcsString("abc", "def"));

        Assert.Equal(6, UniquePathsAndLcsSolver.LongestCommonSubsequenceLength("csharp", "csharp"));
        Assert.Equal("csharp", UniquePathsAndLcsSolver.GetLcsString("csharp", "csharp"));
    }
}
