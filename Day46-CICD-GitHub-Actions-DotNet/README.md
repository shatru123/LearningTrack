# Day 46: CI/CD with GitHub Actions for .NET

## Overview
Day 46 focuses on building enterprise-grade **Continuous Integration and Continuous Deployment (CI/CD)** pipelines for .NET applications using **GitHub Actions**, automated **Trivy security scanning**, and **GitHub Container Registry (GHCR)** image publishing.

## Production Pipeline Architecture
- **Lint & Format**: Enforces Roslyn code analyzers and format consistency via `dotnet format --verify-no-changes`.
- **Test with Code Coverage**: Executes test suites and collects code coverage using XPlat coverage collectors.
- **Trivy Security Gate**: Scans container layers and NuGet dependencies, failing the pipeline if High/Critical vulnerabilities (CVSS $\ge 7.0$) are present with available fixes.
- **Multi-Arch Docker Buildx**: Builds and pushes multi-architecture images (`linux/amd64`, `linux/arm64`) with GitHub Actions cache backends (`type=gha`).
- **Semantic Versioning**: Automatically computes SemVer releases (`vMajor.Minor.Patch`) based on Conventional Commits.

## DSA Problem: LeetCode #139 (Word Break)
- 1D Dynamic Programming with boolean array $dp[i]$ indicating if substring $s[0 \dots i-1]$ can be partitioned into dictionary words.
- Max word length pruning optimizing inner loop to $\mathcal{O}(N \cdot L)$ time.

## Test Verification
Run the 10 automated unit tests:
```bash
dotnet test tests/Day46.CiCdGitHubActions.Tests/Day46.CiCdGitHubActions.Tests.csproj
```
