# Day 51: AI & LLM Architecture Foundations

## Overview
Day 51 marks the milestone launch of **Phase 3 & Phase 4: Applied AI & LLM Engineering with .NET**. We dissect the foundational internal mechanics of modern Large Language Models (LLMs) and the Transformer architecture: Scaled Dot-Product Attention, Causal Autoregressive Masking, Positional Encoding, Byte-Pair Encoding (BPE) subword tokenization, and generation sampling mechanics (Temperature, Top-P Nucleus, Frequency/Presence penalties), alongside LeetCode #62 Unique Paths and LeetCode #1143 Longest Common Subsequence (LCS).

## Key Concepts
- **Scaled Dot-Product Attention**: $	ext{Attention}(Q, K, V) = 	ext{softmax}(QK^T / \sqrt{d_k})V$ and causal masking preventing attention to future tokens.
- **Positional Embeddings**: Sinusoidal frequency encoding injecting sequence position information into permutation-invariant attention matrices.
- **Tokenization Mechanics**: Byte-Pair Encoding (BPE) subword vocabulary creation, frequency pair merges, and token identification.
- **Generation Sampling**: Temperature sharpening/flattening, Top-P cumulative nucleus filtering, and repetition penalties.
- **Algorithms (LC #62 & #1143)**: Unique Paths via 2D DP, 1D rolling array, and combinatorics; Longest Common Subsequence via 2D table and backtracking string recovery.
