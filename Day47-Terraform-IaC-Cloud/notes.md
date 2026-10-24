# Technical Notes: Terraform IaC & State Management

### 1. Terraform Dependency DAG Resolution
Terraform builds a Directed Acyclic Graph (DAG) of all resources. If Resource B references `azurerm_resource_group.rg.name`, an edge `rg -> B` is added. Kahn's topological sort determines execution tiers:
- Tier 0: Resources with in-degree 0 (Resource Groups, VPCs).
- Tier 1: Subnets, Route Tables, Log Workspaces.
- Tier 2: Managed Databases, Container App Environments.
- Tier 3: Container Apps, Ingress, DNS records.

### 2. State File Locking Mechanics
Concurrent `terraform apply` executions against the same state file cause state truncation and corrupt resource mapping. Remote backends enforce distributed locks:
- **Azure Blob Storage**: Blob lease acquisition (`x-ms-lease-action: acquire`).
- **AWS S3**: DynamoDB table with `LockID` primary key and TTL.
- Without a valid lease token, Terraform halts with exit code 1.

### 3. LeetCode #300: Longest Increasing Subsequence
- **DP Approach**: `dp[i] = max(dp[j] + 1)` for $j < i$ and `nums[j] < nums[i]`. Time: $\mathcal{O}(N^2)$, Space: $\mathcal{O}(N)$.
- **Patience Sorting**: Maintain an array of active tails of increasing subsequences. For each element `x`, binary search finds the smallest tail $\ge x$. If found, replace it; if none, append `x`. Time: $\mathcal{O}(N \log N)$, Space: $\mathcal{O}(N)$.
