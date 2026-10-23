# Day 44: Senior / Staff Engineering Interview Q&As

### Q1: How do ConfigMaps mounted as environment variables differ from ConfigMaps mounted as volume files?
**Answer**:
- **Environment Variables**: Injected at container startup. If the ConfigMap is modified in Kubernetes, the container's environment variables do **not** update until the Pod is restarted (`kubectl rollout restart`).
- **Volume Mounts**: Mounted into the filesystem via atomic symlink swap (`..data`). Kubelet synchronizes changes automatically (typically within 60–90 seconds). Applications using file watchers (such as .NET `reloadOnChange: true` in `ConfigurationBuilder`) can hot-reload changes without container restarts.

### Q2: Why are standard Kubernetes Secrets not secure by default, and how should they be protected in production?
**Answer**:
Standard Kubernetes Secrets are merely Base64-encoded strings stored in plaintext within `etcd`. Anyone with read RBAC permissions or access to `etcd` disk backups can view them.
In production:
1. Enable `etcd` Encryption at Rest via `KMS` (AWS KMS, Azure Key Vault, HashiCorp Vault).
2. Use **HashiCorp Vault Secrets Store CSI Driver** or **External Secrets Operator** to synchronize secrets directly into Pod memory without persisting plaintext in `etcd`.
3. Use **Bitnami Sealed Secrets** to safely commit asymmetrically encrypted secrets to Git.

### Q3: How does NGINX Ingress handle TLS termination and certificate management?
**Answer**:
1. The TLS certificate and private key are stored in a Kubernetes Secret of type `kubernetes.io/tls` with `tls.crt` and `tls.key`.
2. The Ingress resource references this secret in `spec.tls[*].secretName`.
3. The Ingress Controller loads the certificate, terminates the incoming client TLS handshake, decrypts HTTP requests, and forwards plaintext HTTP traffic (or re-encrypted internal mTLS) to backend ClusterIP pods.
4. Tools like `cert-manager` automate ACME (Let's Encrypt) challenge solving and certificate renewal.

### Q4: Explain the dynamic programming state transition for LeetCode #91 Decode Ways.
**Answer**:
Let $dp[i]$ be the number of ways to decode string prefix of length $i$:
- **Single digit** ($1 \le s[i-1] \le 9$): $dp[i] += dp[i-1]$.
- **Two digits** ($10 \le s[i-2..i-1] \le 26$): $dp[i] += dp[i-2]$.
- **Edge cases**: If $s[0] == '0'$, return 0 immediately. If an invalid two-digit sequence occurs (e.g. `"30"` or `"00"`), no transitions apply and result becomes 0.
Space complexity is optimized to $\mathcal{O}(1)$ by retaining only `prev1` ($dp[i-1]$) and `prev2` ($dp[i-2]$).

### Q5: What is the purpose of the `nginx.ingress.kubernetes.io/rewrite-target` annotation?
**Answer**:
Microservices frequently expect routes at root (`/checkout`), while the external API Gateway or Ingress exposes them namespaced under a prefix (`/orders/checkout`).
The `rewrite-target: /$2` annotation together with path regex `/orders(/|$)(.*)` strips the `/orders` prefix before proxying the request to the backend service.

### Q6: How do you configure ASP.NET Core to read mounted Kubernetes ConfigMaps and Secrets?
**Answer**:
Use `AddKeyPerFile`:
```csharp
builder.Configuration
    .AddKeyPerFile(directoryPath: "/etc/config", optional: true, reloadOnChange: true)
    .AddKeyPerFile(directoryPath: "/etc/secrets", optional: true, reloadOnChange: false);
```
Each file inside the directory becomes a configuration key whose value is the file's content.

### Q7: What is the difference between Ingress and Gateway API in modern Kubernetes?
**Answer**:
- **Ingress**: Mature Layer 7 routing specification (`networking.k8s.io/v1`), but limited in advanced routing (canary releases, header-based routing, weighted splitting) which required vendor-specific annotations.
- **Gateway API**: The modern successor (`gateway.networking.k8s.io`), providing role-oriented interfaces (`GatewayClass`, `Gateway`, `HTTPRoute`) with first-class support for multi-tenancy, cross-namespace routing, canary traffic splitting, and Layer 4/7 protocols.

### Q8: What happens if an Ingress resource specifies a host that does not match the client request's HTTP Host header or SNI?
**Answer**:
The Ingress Controller either routes the request to the `default-backend` (which typically returns HTTP `404 Not Found`) or rejects the connection if strict host routing is configured.

### Q9: How can you protect sensitive configuration values from appearing in container crash dumps or logs?
**Answer**:
1. Mount secrets as files in memory (`tmpfs` volume) instead of environment variables, since environment variables are printed by `/proc/[pid]/environ` and logging tools.
2. Configure structured logging redaction in .NET using Microsoft's `Microsoft.Extensions.Compliance.Redaction` to mask PII and credentials.
3. Configure `automountServiceAccountToken: false` on Pods that do not require Kubernetes API access.

### Q10: How does LeetCode #647 Palindromic Substrings differ from LeetCode #5 Longest Palindromic Substring?
**Answer**:
While LC #5 seeks the maximum length palindrome, LC #647 counts every valid palindromic substring across the entire string. Both can be solved in $\mathcal{O}(N^2)$ time using the center-expansion technique, where every step outward in expansion increments the global palindrome counter by 1.
