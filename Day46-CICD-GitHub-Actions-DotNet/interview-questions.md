# Day 46: Senior / Staff Engineering Interview Q&As

### Q1: How do you structure a high-performance CI/CD pipeline in GitHub Actions to achieve sub-3-minute build and test cycles?
**Answer**:
1. **Parallel Independent Jobs**: Separate linting, testing, and security scanning into independent parallel jobs rather than sequential steps.
2. **Aggressive Cache Invalidation**: Cache `~/.nuget/packages` using `actions/cache@v4` with key `${{ runner.os }}-nuget-${{ hashFiles('**/*.csproj', '**/Directory.Packages.props') }}`.
3. **No-Restore and No-Build Flags**: Pass `--no-restore` to `dotnet build` and `--no-build` to `dotnet test`.
4. **Docker Layer Caching**: Utilize `docker/build-push-action` with `cache-from: type=gha` and `cache-to: type=gha,mode=max`.

### Q2: What is the purpose of Trivy vulnerability scanning, and how should security thresholds be enforced in CI?
**Answer**:
Trivy scans container OS packages (Alpine, Debian, Ubuntu) and application dependencies (NuGet, npm) against CVE databases.
In CI:
- Set `ignore-unfixed: true` to avoid blocking builds on vulnerabilities that vendors haven't patched yet.
- Set `severity: CRITICAL,HIGH` and `exit-code: 1` to fail pull requests only when severe, exploitable CVEs exist.
- Upload scan results in SARIF format (`upload-sarif`) to display inline vulnerability alerts in GitHub's Security tab.

### Q3: How do GitHub Actions OIDC (OpenID Connect) tokens eliminate static cloud credentials?
**Answer**:
Traditional CI pipelines stored long-lived static AWS Access Keys or Azure Service Principal secrets in GitHub repository secrets, which risked credential leakage.
With GitHub Actions OIDC:
1. The GitHub workflow requests a temporary short-lived JWT token signed by GitHub's OIDC Provider.
2. The cloud provider (AWS/Azure/GCP) validates the JWT signature and claims (`sub: repo:owner/repo:ref:refs/heads/main`).
3. The cloud provider assumes a scoped IAM role and returns temporary STS credentials valid for only 1 hour. No permanent secrets are ever stored in GitHub.

### Q4: Explain the dynamic programming recurrence for LeetCode #139 Word Break.
**Answer**:
Let $dp[i]$ be a boolean indicating whether prefix $s[0 \dots i-1]$ can be segmented into words from `wordDict`.
- Base case: $dp[0] = true$ (empty string).
- For $i$ from 1 to $N$:
  For $j$ from $i-1$ down to $\max(0, i - L)$ (where $L$ is the max length of any word in the dictionary):
  If $dp[j] == true$ and $s[j \dots i-1] \in \text{wordSet}$:
  $$dp[i] = true; \quad \text{break}$$
- Result: $dp[N]$.
Optimizing the inner loop with maximum word length $L$ brings the time complexity down from $\mathcal{O}(N^2)$ to $\mathcal{O}(N \cdot L)$.

### Q5: How do Conventional Commits automate Semantic Versioning in CI/CD?
**Answer**:
Conventional Commits define a structured commit syntax:
- `fix:` -> increments **PATCH** version ($1.0.0 \to 1.0.1$) for backwards-compatible bug fixes.
- `feat:` -> increments **MINOR** version ($1.0.0 \to 1.1.0$) for new backwards-compatible features.
- `feat!:`, `fix!:`, or `BREAKING CHANGE:` footer -> increments **MAJOR** version ($1.0.0 \to 2.0.0$) for breaking changes.
Tools like Semantic-Release or GitVersion analyze the commit log between Git tags and automatically tag the repository and publish packages with the exact SemVer version.

### Q6: How do you prevent secret leakage in GitHub Actions console logs?
**Answer**:
1. Any value retrieved from GitHub Secrets (`${{ secrets.MY_SECRET }}`) is automatically masked by the GitHub runner (`***`).
2. Never format secrets into filenames, URLs, or base64-encoded strings without registering the encoded variant with `echo "::add-mask::$ENCODED_SECRET"`.
3. Audit pipeline steps to ensure `set -x` (shell command tracing) is disabled when handling credential variables.

### Q7: What are the risks of using `pull_request_target` vs `pull_request` triggers in GitHub Actions?
**Answer**:
- `pull_request`: Runs in the context of the fork/PR branch without access to repository secrets or write permissions. Safe for open-source repositories.
- `pull_request_target`: Runs in the context of the target base branch with access to repository secrets. If a malicious PR author modifies a script executed by the workflow, they can exfiltrate repository secrets (PwnRequest vulnerability). Never check out and execute code from the PR head inside a `pull_request_target` workflow.

### Q8: What is the difference between multi-platform Docker builds with QEMU vs native builders?
**Answer**:
- **QEMU Emulation**: Uses software CPU instruction emulation (`binfmt_misc`) to build ARM64 images on AMD64 runners. Flexible and requires no extra hardware, but CPU-intensive tasks (like compiling .NET code) can be 5–10x slower.
- **Native Cross-Compilation**: Uses .NET SDK's built-in cross-compilation (`dotnet publish -r linux-arm64`) running natively on AMD64 hardware, achieving full native compilation speed while generating ARM64 binaries.

### Q9: How do you implement automated rollbacks in a GitHub Actions continuous deployment pipeline?
**Answer**:
1. After executing `kubectl rollout restart` or updating the container image tag in Kubernetes, execute:
   `kubectl rollout status deployment/order-api --timeout=120s`.
2. If the rollout status times out or fails health probes, the step fails.
3. In a follow-up step with `if: failure()`, execute:
   `kubectl rollout undo deployment/order-api` to instantly restore the previous known-good ReplicaSet.

### Q10: How does GitHub Container Registry (GHCR) package visibility and permissions integrate with GitHub Actions?
**Answer**:
Workflows access GHCR without external tokens by utilizing the built-in `${{ secrets.GITHUB_TOKEN }}`. Under `permissions:`, the job declares `packages: write` and `contents: read`. Images pushed to `ghcr.io/owner/repo` automatically inherit the repository's access controls (private or public).
