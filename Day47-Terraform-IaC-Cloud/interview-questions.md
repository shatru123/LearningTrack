# Day 47: Staff & Principal Interview Questions

### Q1: How does Terraform manage distributed state locking, and what happens during a network partition?
**Answer**:
Terraform acquires a state lock before running `plan` or `apply` by interacting with the remote backend (e.g. AWS DynamoDB item lock or Azure Blob Storage lease). The lock holds a unique MD5/UUID lease token, host information, and creation timestamp. If a network partition interrupts the client mid-apply, the lock remains orphaned. Engineers must inspect the execution logs to verify no background provisioning is active and manually run `terraform force-unlock <LOCK-ID>` to release the lease.

---

### Q2: Why is storing secrets in Terraform state dangerous, and how do modern architectures mitigate this?
**Answer**:
Terraform state files (`terraform.tfstate`) store resource attributes—including database passwords, private keys, and API tokens—in unencrypted plaintext within the JSON payload. Mitigations include:
1. Server-side encryption with customer-managed KMS keys (CMK) and strict RBAC on the storage container.
2. Ephemeral secrets via HashiCorp Vault or Azure Key Vault, retrieving dynamic credentials at runtime rather than baking them into Terraform resources.
3. Marking outputs as `sensitive = true` to prevent console and CI/CD log leakage.

---

### Q3: How do you handle configuration drift between live cloud resources and Terraform state?
**Answer**:
Drift occurs when resources are modified out-of-band via cloud consoles or CLIs. Terraform detects drift during `terraform plan` by refreshing state against cloud APIs. To remediate:
- **Adopt live change**: Update Terraform HCL configuration to match new cloud values.
- **Overwrite live change**: Run `terraform apply` to force the cloud back to the declared configuration.
- **Import existing resources**: Use `terraform import` or Terraform 1.5+ `import { ... }` blocks to bring untracked resources under IaC management.

---

### Q4: Explain the difference between implicit and explicit dependencies in Terraform.
**Answer**:
- **Implicit Dependency**: Created automatically when a resource attribute references an attribute of another resource (e.g., `subnet_id = azurerm_subnet.snet.id`). Terraform automatically infers that `snet` must be created before the dependent resource.
- **Explicit Dependency**: Specified using the `depends_on = [ ... ]` meta-argument. Used when dependencies exist outside direct attribute references, such as waiting for IAM role propagation or database initialization scripts.

---

### Q5: How do you structure Terraform code to minimize blast radius in enterprise environments?
**Answer**:
Avoid monolithic state files. Divide infrastructure into decoupled state layers with independent workspaces or directories:
1. **Foundation Layer**: Networking, VPC, Subnets, DNS, Transit Gateways.
2. **Data Layer**: PostgreSQL Flexible Server, Redis caches, S3/Blob storage.
3. **Application Compute Layer**: Container Apps, Kubernetes clusters, Lambdas.
Cross-state data is shared using `terraform_remote_state` data sources or Cloud Parameter Stores/Key Vaults.

---

### Q6: What is the purpose of Terraform modules, and how are version constraints enforced?
**Answer**:
Modules encapsulate reusable infrastructure blueprints. In production, modules are published to a private registry or Git repository with SemVer tags. Root configurations enforce strict version constraints:
```hcl
module "postgres" {
  source  = "git::https://github.com/org/tf-modules.git//postgres?ref=v2.4.1"
  version = "~> 2.4.0"
}
```
This prevents unexpected breaking changes when module maintainers release new versions.

---

### Q7: What are Terraform provisioners (`local-exec`, `remote-exec`) and why are they considered a last resort?
**Answer**:
Provisioners execute shell commands or configuration management scripts upon resource creation. They are anti-patterns because:
1. They break declarative idempotency.
2. They do not track destroyed resources cleanly.
3. Failures leave resources in a tainted state.
Cloud-init, VM extensions, Kubernetes manifest controllers, and container entrypoints should be favored over provisioners.

---

### Q8: How does Atlantis or Terraform Cloud enhance CI/CD pull request workflows?
**Answer**:
Atlantis and Terraform Cloud automate PR-driven IaC:
1. When a PR is opened, a bot runs `terraform plan` and comments the exact diff directly on the GitHub PR.
2. Locks the workspace branch to prevent concurrent PR conflicts.
3. Team leads review the plan diff and comment `atlantis apply` to execute changes only after approval.
4. Ensures single-source-of-truth applies directly from the authorized CI runner with credential isolation.

---

### Q9: Contrast the mathematical proofs of O(N^2) DP vs O(N log N) Patience Sorting for LIS.
**Answer**:
- **O(N^2) DP**: Optimal substructure states that the LIS ending at index $i$ is $1 + \max(dp[j])$ for all $j < i$ where $nums[j] < nums[i]$. Checking all $j$ takes $\sum_{i=1}^{N} i = rac{N(N-1)}{2} = \mathcal{O}(N^2)$ comparisons.
- **Patience Sorting**: Elements represent playing cards dealt onto piles. A card can be placed on top of the leftmost pile whose top card is $\ge x$; if none exists, a new pile is created. The top cards of piles form a strictly increasing array at all times. Binary search finds the destination pile in $\mathcal{O}(\log K)$ time. Processing $N$ elements takes $\mathcal{O}(N \log N)$. The number of piles at termination equals the LIS length.

---

### Q10: How do you reconstruct the actual LIS sequence from Patience Sorting?
**Answer**:
While patience sorting keeps an array of pile tails of length $L$, the tails array itself does not necessarily represent a valid subsequence (as elements are overwritten). To reconstruct:
1. Maintain an array `tailsIdx` storing indices in `nums` corresponding to pile tops.
2. Maintain a `parent[i]` array storing the index of the top card of the pile immediately to the left when `nums[i]` was placed.
3. At the end, backtrack from `tailsIdx[L - 1]` through `parent` pointers to recover the exact elements in reverse order.
