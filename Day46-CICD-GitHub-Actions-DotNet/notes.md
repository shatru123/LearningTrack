# Deep Dive Notes: Enterprise CI/CD with GitHub Actions

## 1. Fast Feedback with GitHub Actions Caching
In .NET pipelines, restore and build times are dominated by downloading NuGet packages.
- Using `actions/cache@v4` on `~/.nuget/packages` keyed by `**/packages.lock.json` drops restore times from 45s to 2s.
- Using Docker Buildx `type=gha` caches container image layers across workflow runs directly in GitHub's internal cache storage.

## 2. Supply Chain Security & Container Signing
Modern cloud-native security requires:
1. **Software Bill of Materials (SBOM)** generated via Syft in SPDX or CycloneDX format.
2. **Container Signing via Cosign / Sigstore**: Keyless signing binding the container image digest to the GitHub Actions OIDC workflow token.
3. **Static Analysis & SAST**: Trivy filesystem and image scanning gating pull requests before deployment.
