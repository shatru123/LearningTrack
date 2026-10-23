# Day 43: Senior / Staff Engineering Interview Q&As

### Q1: Describe the end-to-end lifecycle of a `kubectl apply -f deployment.yaml` command.
**Answer**:
1. `kubectl` validates manifest client-side and transmits an HTTP POST/PUT to `kube-apiserver`.
2. `kube-apiserver` runs authentication (TLS client cert/OIDC), authorization (RBAC), and admission controllers (Mutating and Validating Webhooks).
3. The manifest is written into `etcd` with a new monotonically increasing revision number.
4. The `DeploymentController` detects the new Deployment resource via an API watch stream and creates a `ReplicaSet`.
5. The `ReplicaSetController` creates unbound Pods (where `nodeName` is null).
6. The `kube-scheduler` filters nodes with sufficient CPU/memory requests, scores them, and assigns the pod to a node by updating the Pod resource's `nodeName`.
7. The `kubelet` on the selected worker node detects the scheduled Pod via local API watch, instructs the Container Runtime (containerd/CRI-O) to pull the image and create the container sandbox, attaches volumes, and begins periodic probe executions.

### Q2: What is the difference between IPVS mode and iptables mode in `kube-proxy`?
**Answer**:
`iptables` uses sequential rule chains. For clusters with thousands of services, packet traversal complexity is $\mathcal{O}(N)$, causing latency degradation and high kernel CPU usage on updates.
`IPVS` (IP Virtual Server) is implemented in Linux kernel Netfilter using hash tables with $\mathcal{O}(1)$ lookup complexity, supporting advanced load-balancing algorithms (least connection, weighted response) and scaling seamlessly to 50,000+ services.

### Q3: How do you achieve zero-downtime deployments with Kubernetes RollingUpdate?
**Answer**:
1. Configure `maxUnavailable: 0` and `maxSurge: 25%` (or `1`) so Kubernetes never destroys an existing pod until a replacement pod is confirmed healthy.
2. Configure a robust `readinessProbe` with `initialDelaySeconds` and `periodSeconds`. Kubernetes will NOT register a new pod in the Service Endpoints until the readiness probe passes.
3. Configure `terminationGracePeriodSeconds` (e.g. 30s) and handle SIGTERM in the ASP.NET Core application (`IHostApplicationLifetime.ApplicationStopping`) to complete in-flight HTTP requests before process termination.

### Q4: Why is it a security best practice to configure `readOnlyRootFilesystem: true` in production Pods?
**Answer**:
`readOnlyRootFilesystem: true` prevents an attacker who exploits an application vulnerability (such as remote code execution or arbitrary file upload) from installing malicious binaries, modifying system shared libraries, or writing cron persistence mechanisms to the root filesystem. Any temporary write operations must be explicitly directed to mounted `emptyDir` memory volumes.

### Q5: How do Kubernetes Resource Requests differ from Resource Limits?
**Answer**:
- **Requests**: Used by `kube-scheduler` to place pods on nodes that have sufficient allocatable resources. Guarantees minimum reservation.
- **Limits**: Enforced by Linux kernel `cgroups`. If a container exceeds its memory limit, the Linux OOM-killer immediately terminates the process (Exit code 137). If it exceeds CPU limit, the CFS (Completely Fair Scheduler) throttles container CPU execution slices without terminating the process.

### Q6: What happens when `etcd` loses quorum in a multi-master Kubernetes control plane?
**Answer**:
`etcd` requires a strict majority quorum ($Q = \lfloor N/2 \rfloor + 1$). In a 3-node cluster, if 2 nodes fail, quorum is lost. When quorum is lost:
- `kube-apiserver` rejects all write, update, and deletion requests.
- Worker nodes and existing running Pods continue executing normally and serving traffic.
- No auto-scaling (HPA), rolling deployments, or failed pod restarts can be scheduled until `etcd` quorum is restored.

### Q7: Explain the center-expansion algorithm for LeetCode #5 Longest Palindromic Substring.
**Answer**:
A palindrome mirrors around its center. For a string of length $N$, there are $2N - 1$ potential centers:
- $N$ single character centers for odd palindromes (e.g., `"aba"` centered at `'b'`).
- $N - 1$ between-character centers for even palindromes (e.g., `"abba"` centered between `'b'` and `'b'`).
We iterate over every center and expand pointers outwards while characters match. This achieves $\mathcal{O}(N^2)$ time complexity and $\mathcal{O}(1)$ auxiliary space, avoiding the $\mathcal{O}(N^2)$ memory table required by classic dynamic programming.

### Q8: What is the purpose of `externalTrafficPolicy: Local` on a LoadBalancer or NodePort service?
**Answer**:
By default (`externalTrafficPolicy: Cluster`), node-level kube-proxy forwards external traffic to pods residing on other nodes, incurring a second network hop and masking the client's source IP with the node's SNAT IP.
Setting `externalTrafficPolicy: Local` preserves the original client IP and ensures traffic is only routed to pods running on the receiving node, dropping the extra network hop. Nodes with zero local pods fail the load balancer health check and receive no traffic.

### Q9: What is `revisionHistoryLimit` in a Kubernetes Deployment?
**Answer**:
`revisionHistoryLimit` dictates how many old ReplicaSets (and their corresponding rollout revision definitions) are retained in `etcd`. Retaining old ReplicaSets enables instant rollback via `kubectl rollout undo deployment/order-api`, but retaining too many degrades `etcd` performance and bloats API response payloads. A typical production setting is 5 to 10 revisions.

### Q10: How does DNS resolution work for a headless service in Kubernetes?
**Answer**:
A headless service sets `spec.clusterIP: None`. Instead of returning a single virtual ClusterIP, CoreDNS returns multiple `A` records containing the direct IP addresses of all ready backend Pods, enabling client-side load balancing, leader election (e.g. ZooKeeper, Kafka), and direct peer-to-peer clustering.
