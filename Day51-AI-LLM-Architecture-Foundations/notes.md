# Technical Notes: Transformer Architecture & LLM Foundations

### 1. Scaled Dot-Product & Causal Attention Mechanics
- **Scale Factor $\sqrt{d_k}$**: For large projection dimensions $d_k$, the dot product $QK^T$ grows large in magnitude, pushing the softmax function into regions with near-zero gradients (vanishing gradient problem). Dividing by $\sqrt{d_k}$ stabilizes variance to 1.0.
- **Causal Masking**: In decoder-only architectures (GPT, Llama, Mistral), token $i$ must not attend to token $j > i$. Applying an upper-triangular mask of $-\infty$ ensures that $\exp(-\infty) = 0$ after softmax, preserving strict autoregression.

### 2. Tokenization & Sampling Mechanics
- **Byte-Pair Encoding (BPE)**: Subword tokenization iteratively merges the most frequent adjacent character/token pairs. Balances vocabulary size against out-of-vocabulary (OOV) tokens and compression efficiency.
- **Temperature ($T$)**: Modulates logit sharpness $P_i = \exp(z_i / T) / \sum_j \exp(z_j / T)$. Low $T 	o 0$ approaches argmax greedy decoding; high $T > 1$ flattens distribution.
- **Top-P (Nucleus Sampling)**: Dynamically truncates vocabulary to the smallest set of tokens whose cumulative probability $\ge P$, adapting candidate pool size based on model confidence.

### 3. LeetCode #62 & #1143
- **Unique Paths**: `dp[i, j] = dp[i - 1, j] + dp[i, j - 1]`. Space-optimizable to 1D array of size $\min(M, N)$. Closed-form combinatorics: $inom{M + N - 2}{M - 1}$.
- **Longest Common Subsequence**: If `text1[i] == text2[j]`, `dp[i, j] = dp[i-1, j-1] + 1`; else `dp[i, j] = max(dp[i-1, j], dp[i, j-1])`. Backtracking from $(M, N)$ recovers the LCS string.
