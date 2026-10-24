# Day 51: Staff & Principal Interview Questions

### Q1: Why do Transformers scale quadratically O(N^2) with context length, and how does FlashAttention solve the GPU memory bottleneck?
**Answer**:
Computing $QK^T$ requires calculating an attention score between every pair of tokens in a sequence of length $N$, producing an $N 	imes N$ matrix. In standard implementations:
- $QK^T$ is written to High Bandwidth Memory (HBM).
- Softmax reads and writes the $N 	imes N$ matrix back to HBM.
- Matrix multiplication with $V$ reads from HBM again.
Memory bandwidth (not FLOPS) is the primary bottleneck. **FlashAttention** fuses the attention computation into GPU SRAM using tiling and online softmax scaling, eliminating intermediate $N 	imes N$ reads/writes to HBM and reducing memory from $\mathcal{O}(N^2)$ to $\mathcal{O}(N)$ while achieving 2x–4x wall-clock speedups.

---

### Q2: What is the KV Cache in LLM inference, and why is its memory footprint a critical production concern?
**Answer**:
During autoregressive generation, generating each new token only requires computing $Q$ for the single new token, but it must attend to all previous tokens\' $K$ and $V$ vectors. Without caching, all past tokens must be recomputed at every step ($\mathcal{O}(N^2)$ per generation).
The **KV Cache** stores the Key and Value tensors for all past tokens across all layers.
*Memory Footprint*:
$$	ext{Memory} = 2 	imes 2 	imes 	ext{layers} 	imes 	ext{heads} 	imes d_{	ext{head}} 	imes 	ext{context\_len} 	imes 	ext{batch\_size} 	imes 	ext{bytes\_per\_param}$$
For a 70B model with 4k context and batch size 16 in FP16, KV cache alone can exceed 40 GB of VRAM. Architectures like **Grouped-Query Attention (GQA)** share Key/Value heads across multiple Query heads to reduce KV cache size by 4x–8x.

---

### Q3: Contrast Byte-Pair Encoding (BPE), WordPiece, and SentencePiece tokenization.
**Answer**:
- **BPE (GPT, Llama)**: Bottom-up merge algorithm based on raw frequency counts of adjacent symbol pairs. Operates on bytes to avoid OOV tokens entirely.
- **WordPiece (BERT)**: Similar to BPE, but selects pair merges that maximize the likelihood of the training data according to a unigram language model rather than pure frequency.
- **SentencePiece (T5, Llama)**: Treats whitespace as a regular symbol (e.g. `_`), eliminating language-dependent pre-tokenizers and supporting multilingual scripts natively.

---

### Q4: Explain the mathematical relationship between Temperature and Top-P (Nucleus) sampling.
**Answer**:
- **Temperature ($T$)** scales the raw logits $z_i / T$ *before* the softmax function. It changes the entropy of the probability distribution: low $T$ makes high logits dominate exponentially, whereas high $T$ makes probabilities more uniform.
- **Top-P (Nucleus)** acts *after* softmax. It sorts tokens by probability and discards all tail tokens beyond the cumulative threshold $P$.
*Interaction*: Temperature reshapes the probability curve, which directly affects how many tokens are included in the Top-P cumulative cutoff. High temperature fattens the tail, causing Top-P to include more candidate tokens; low temperature causes Top-P to include fewer tokens.

---

### Q5: How do Frequency and Presence penalties prevent degenerate LLM repetition?
**Answer**:
- **Frequency Penalty**: Proportional to the exact count of times a token has already appeared:
  $$z_i^{\prime} = z_i - (	ext{count}_i 	imes lpha_{	ext{freq}})$$
  Penalizes words increasingly the more often they are repeated.
- **Presence Penalty**: A flat one-time penalty applied if the token has appeared at least once:
  $$z_i^{\prime} = z_i - (\mathbb{I}(	ext{count}_i > 0) 	imes lpha_{	ext{pres}})$$
  Encourages the model to introduce new topics and avoid looping phrases without overly penalizing common grammatical words (like "the", "is").

---

### Q6: What are Rotary Position Embeddings (RoPE) and why have they replaced Sinusoidal/Absolute Embeddings?
**Answer**:
Absolute sinusoidal embeddings add a position-dependent vector to token embeddings before the first layer: $x_{pos} = x + PE_{pos}$. However, attention depends on *relative* distance between tokens ($m - n$), not absolute position.
**RoPE** rotates the Query and Key vectors in 2D complex coordinate subspaces by an angle proportional to position:
$$R_{\Theta, m} Q_m$$
The inner product $(R_m Q)^T (R_n K) = Q^T R_{n - m} K$ naturally encodes relative distance $(m - n)$. RoPE offers superior context length extrapolation and has become the universal standard across Llama, Mistral, and DeepSeek.

---

### Q7: Why do we use Layer Normalization (RMSNorm) rather than Batch Normalization in Transformers?
**Answer**:
- **BatchNorm** normalizes across the batch dimension. It fails in NLP/LLMs because input sentence lengths vary dynamically within a batch, and during single-sample autoregressive inference (batch size 1), variance is zero.
- **LayerNorm / RMSNorm** normalizes across the feature/embedding dimension for *each individual token independently*. It is invariant to batch size and sequence length. RMSNorm (Root Mean Square Normalization) further omits mean-centering, reducing computational overhead without loss of model quality.

---

### Q8: What is the purpose of the Feed-Forward Network (FFN) layer in a Transformer block?
**Answer**:
Self-Attention is purely an aggregation and routing mechanism: it mixes information across tokens but performs linear combinations. The **Feed-Forward Network (FFN)** consists of two linear transformations with a non-linear activation (GeLU or SwiGLU) in between:
$$	ext{FFN}(x) = \sigma(x W_1) W_2$$
Typically, the hidden dimension is expanded by 4x ($d_{	ext{ffn}} = 4 \cdot d_{	ext{model}}$). The FFN acts as a key-value associative memory where factual knowledge and linguistic patterns learned during pretraining are stored.

---

### Q9: Explain how Unique Paths can be solved in O(1) space using Combinatorics.
**Answer**:
In an $M 	imes N$ grid, any path from $(0, 0)$ to $(M - 1, N - 1)$ requires making exactly $(M - 1)$ downward steps and $(N - 1)$ rightward steps. The total number of steps is fixed:
$$T = (M - 1) + (N - 1) = M + N - 2$$
The problem reduces to choosing $(M - 1)$ down steps out of $T$ total steps:
$$inom{M + N - 2}{M - 1} = rac{(M + N - 2)!}{(M - 1)! (N - 1)!}$$
This can be computed in $\mathcal{O}(\min(M, N))$ time and $\mathcal{O}(1)$ auxiliary space by multiplying sequentially and dividing iteratively to prevent 64-bit integer overflow.

---

### Q10: How do you optimize Longest Common Subsequence to O(min(M, N)) auxiliary space?
**Answer**:
In the 2D DP transition:
$$	ext{dp}[i, j] = egin{cases} 	ext{dp}[i-1, j-1] + 1 & 	ext{if } text1[i] = text2[j] \ \max(	ext{dp}[i-1, j], 	ext{dp}[i, j-1]) & 	ext{otherwise} \end{cases}$$
Computing row $i$ only depends on the current row $i$ and the immediately preceding row $i - 1$.
- We can maintain just two rows of length $N$: `prev[N + 1]` and `curr[N + 1]`.
- Furthermore, we can ensure $N \le M$ by swapping strings if $M < N$.
- This reduces auxiliary space from $\mathcal{O}(M 	imes N)$ to $\mathcal{O}(\min(M, N))$.
*Note*: While space is optimized to $\mathcal{O}(\min(M, N))$ for calculating length, reconstructing the exact LCS string still requires the full 2D table or Hirschberg\'s divide-and-conquer algorithm.
