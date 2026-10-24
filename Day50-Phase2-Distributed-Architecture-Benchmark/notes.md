# Technical Notes: Phase 2 Milestone Benchmark & Architecture Review

### 1. Phase 2 Architecture Review Synthesis
Across Days 26 to 50, the architecture stack spans:
- **Resilience**: Polly circuit breakers prevent cascade failures across microservices.
- **Consistency**: The Transactional Outbox Pattern and Saga State Machines decouple atomic business transactions from distributed asynchronous messaging.
- **Observability**: OpenTelemetry standardizes W3C distributed trace propagation (`traceparent`), correlated across logs, Prometheus metrics, and Jaeger spans.
- **Orchestration**: Kubernetes Deployments, Probes, HPA, and Ingress manage containerized life cycles backed by declarative Terraform IaC.

### 2. LeetCode #23: Merge K Sorted Lists
Let $K$ be the number of linked lists and $N$ be the total number of nodes across all lists:
- **Min-Heap Approach**: Insert the head of each list into a priority queue of size $K$. Dequeue the smallest node, append it to the merged list, and enqueue its `next` pointer. Time: $\mathcal{O}(N \log K)$, Space: $\mathcal{O}(K)$.
- **Divide-and-Conquer**: Pairwise merge adjacent lists in rounds ($K 	o K/2 	o K/4 \dots 	o 1$). Total rounds: $\log K$. Each round processes $N$ nodes. Time: $\mathcal{O}(N \log K)$, Space: $\mathcal{O}(1)$ auxiliary space.
