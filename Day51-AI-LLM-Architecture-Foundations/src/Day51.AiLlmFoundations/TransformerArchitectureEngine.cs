namespace Day51.AiLlmFoundations;

using System;
using System.Collections.Generic;
using System.Linq;

public class TransformerAttentionEngine
{
    /// <summary>
    /// Computes Scaled Dot-Product Attention: Softmax(Q * K^T / sqrt(d_k)) * V
    /// Supports causal masking for autoregressive decoders.
    /// </summary>
    public static double[,] ScaledDotProductAttention(
        double[,] Q,
        double[,] K,
        double[,] V,
        bool isCausal = false)
    {
        int seqLenQ = Q.GetLength(0);
        int dK = Q.GetLength(1);
        int seqLenK = K.GetLength(0);
        int dV = V.GetLength(1);

        double scale = Math.Sqrt(dK);
        double[,] scores = new double[seqLenQ, seqLenK];

        // 1. Q * K^T / sqrt(d_k)
        for (int i = 0; i < seqLenQ; i++)
        {
            for (int j = 0; j < seqLenK; j++)
            {
                if (isCausal && j > i)
                {
                    scores[i, j] = -1e9; // Causal mask
                    continue;
                }

                double dot = 0;
                for (int d = 0; d < dK; d++)
                {
                    dot += Q[i, d] * K[j, d];
                }
                scores[i, j] = dot / scale;
            }
        }

        // 2. Softmax per row
        double[,] attentionWeights = new double[seqLenQ, seqLenK];
        for (int i = 0; i < seqLenQ; i++)
        {
            double maxScore = double.NegativeInfinity;
            for (int j = 0; j < seqLenK; j++)
            {
                if (scores[i, j] > maxScore) maxScore = scores[i, j];
            }

            double sumExp = 0;
            for (int j = 0; j < seqLenK; j++)
            {
                attentionWeights[i, j] = Math.Exp(scores[i, j] - maxScore);
                sumExp += attentionWeights[i, j];
            }

            for (int j = 0; j < seqLenK; j++)
            {
                attentionWeights[i, j] /= sumExp;
            }
        }

        // 3. AttentionWeights * V
        double[,] output = new double[seqLenQ, dV];
        for (int i = 0; i < seqLenQ; i++)
        {
            for (int v = 0; v < dV; v++)
            {
                double sum = 0;
                for (int j = 0; j < seqLenK; j++)
                {
                    sum += attentionWeights[i, j] * V[j, v];
                }
                output[i, v] = sum;
            }
        }

        return output;
    }

    /// <summary>
    /// Generates Sinusoidal Positional Encoding matrix of shape [seqLen, dModel].
    /// </summary>
    public static double[,] GeneratePositionalEncoding(int seqLen, int dModel)
    {
        double[,] pe = new double[seqLen, dModel];
        for (int pos = 0; pos < seqLen; pos++)
        {
            for (int i = 0; i < dModel; i += 2)
            {
                double denominator = Math.Pow(10000, (double)i / dModel);
                pe[pos, i] = Math.Sin(pos / denominator);
                if (i + 1 < dModel)
                {
                    pe[pos, i + 1] = Math.Cos(pos / denominator);
                }
            }
        }
        return pe;
    }
}

public class BytePairEncodingTokenizer
{
    private readonly Dictionary<string, int> _vocab = new();
    private readonly List<(string, string)> _merges = new();

    public void Train(string text, int numMerges)
    {
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                        .Select(w => string.Join(' ', w.ToCharArray()) + " </w>")
                        .ToList();

        // Initialize base vocab
        int nextId = 0;
        foreach (var c in text.Distinct().Where(c => c != ' '))
        {
            if (!_vocab.ContainsKey(c.ToString())) _vocab[c.ToString()] = nextId++;
        }
        if (!_vocab.ContainsKey("</w>")) _vocab["</w>"] = nextId++;

        for (int m = 0; m < numMerges; m++)
        {
            var pairCounts = new Dictionary<(string, string), int>();

            foreach (var word in words)
            {
                var symbols = word.Split(' ');
                for (int i = 0; i < symbols.Length - 1; i++)
                {
                    var pair = (symbols[i], symbols[i + 1]);
                    pairCounts[pair] = pairCounts.GetValueOrDefault(pair) + 1;
                }
            }

            if (pairCounts.Count == 0) break;

            var bestPair = pairCounts.OrderByDescending(kvp => kvp.Value).First().Key;
            _merges.Add(bestPair);
            string mergedToken = bestPair.Item1 + bestPair.Item2;
            _vocab[mergedToken] = nextId++;

            // Replace in words
            string target = $"{bestPair.Item1} {bestPair.Item2}";
            for (int i = 0; i < words.Count; i++)
            {
                words[i] = words[i].Replace(target, mergedToken);
            }
        }
    }

    public List<string> Tokenize(string text)
    {
        var tokens = new List<string>();
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        foreach (var w in words)
        {
            string current = string.Join(' ', w.ToCharArray()) + " </w>";
            foreach (var (p1, p2) in _merges)
            {
                current = current.Replace($"{p1} {p2}", $"{p1}{p2}");
            }
            tokens.AddRange(current.Split(' '));
        }

        return tokens;
    }

    public int VocabSize => _vocab.Count;
}

public class LlmSamplingEngine
{
    /// <summary>
    /// Applies Temperature scaling to raw logits.
    /// </summary>
    public static double[] ApplyTemperature(double[] logits, double temperature)
    {
        if (temperature <= 0.001) temperature = 0.001; // Guard against divide-by-zero
        double[] scaled = new double[logits.Length];
        for (int i = 0; i < logits.Length; i++)
        {
            scaled[i] = logits[i] / temperature;
        }
        return Softmax(scaled);
    }

    /// <summary>
    /// Applies Top-P (Nucleus) filtering: zeroes out probabilities outside cumulative threshold p.
    /// </summary>
    public static double[] ApplyTopP(double[] probabilities, double p)
    {
        int n = probabilities.Length;
        var indexed = probabilities.Select((prob, idx) => (prob, idx))
                                   .OrderByDescending(x => x.prob)
                                   .ToList();

        double cumulative = 0;
        var filtered = new double[n];
        double filteredSum = 0;

        foreach (var (prob, idx) in indexed)
        {
            filtered[idx] = prob;
            filteredSum += prob;
            cumulative += prob;
            if (cumulative >= p) break;
        }

        // Renormalize
        for (int i = 0; i < n; i++)
        {
            filtered[i] /= filteredSum;
        }

        return filtered;
    }

    /// <summary>
    /// Applies Frequency & Presence repetition penalty to logits.
    /// </summary>
    public static double[] ApplyRepetitionPenalty(
        double[] logits,
        Dictionary<int, int> tokenFrequency,
        double frequencyPenalty,
        double presencePenalty)
    {
        double[] penalized = (double[])logits.Clone();
        foreach (var (tokenId, count) in tokenFrequency)
        {
            if (tokenId >= 0 && tokenId < penalized.Length)
            {
                penalized[tokenId] -= (count * frequencyPenalty + (count > 0 ? presencePenalty : 0));
            }
        }
        return penalized;
    }

    private static double[] Softmax(double[] values)
    {
        double max = values.Max();
        double sum = values.Sum(v => Math.Exp(v - max));
        return values.Select(v => Math.Exp(v - max) / sum).ToArray();
    }
}
