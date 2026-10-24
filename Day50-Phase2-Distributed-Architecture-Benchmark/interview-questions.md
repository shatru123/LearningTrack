# Day 50: Staff & Principal Interview Questions

### Q1: How do you design a disaster recovery chaos drill for a microservices cluster in Kubernetes?
**Answer**:
1. **Steady-State Hypothesis**: Define measurable SLIs/SLOs (e.g. 99.9% HTTP 2xx, P99 latency < 250ms).
2. **Blast Radius Control**: Target a staging or production-canary namespace using Chaos Mesh or LitmusChaos.
3. **Fault Injections**:
   - Random Pod kills to verify ReplicaSet and zero-downtime rolling updates.
   - Network latency/packet loss (100ms delay, 10% packet drop) between services to verify Polly timeouts.
   - Broker partition (disconnect RabbitMQ) to verify Transactional Outbox accumulation.
4. **Verification & Post-Mortem**: Verify alert firing in Prometheus/PagerDuty and validate automated self-healing without operator intervention.

---

### Q2: Compare Eventual Consistency vs Strong Consistency for enterprise financial transactions.
**Answer**:
- **Strong Consistency (ACID / 2PC)**: Immediate consistency across all nodes; high latency, vulnerable to blocking coordinator failure, scales poorly over WAN.
- **Eventual Consistency (BASE / Sagas)**: High availability and throughput; temporary divergence permitted. Compensating transactions reverse failed downstream actions.
*Enterprise Best Practice*: Enforce ACID locally within individual microservice bounded contexts (e.g. reserving funds in Ledger DB) and orchestrate cross-service workflows via asynchronous Sagas with compensating actions.

---

### Q3: Why is P99 latency more important than Average (Mean) latency in distributed systems?
**Answer**:
The mean hides severe outliers. In a microservices architecture where a single user request fans out to 50 parallel downstream RPCs, the probability of the user experiencing at least one P99 slowest call is:
$$1 - (0.99)^{50} pprox 39.5\%$$
Nearly 40% of all user requests will experience the tail latency! Optimizing P99 directly protects user experience and prevents thread-pool starvation.

---

### Q4: How does the Transactional Outbox pattern prevent the dual-write problem?
**Answer**:
Writing to a database and publishing to a message broker in a single operation cannot be atomic without distributed 2PC. If the database commit succeeds but the message broker network drops, data is inconsistent. The Outbox pattern writes both the business entity and the outgoing event into the *same* database within a single local ACID transaction. An independent background worker (Debezium CDC or polling publisher) reads the outbox table and guarantees at-least-once message delivery.

---

### Q5: How do you prevent Saga compensation failures from corrupting state?
**Answer**:
Compensating actions must be designed to **never fail permanently**:
1. **Strict Idempotency**: Compensation commands can be retried indefinitely with identical idempotency keys.
2. **Dead Letter Queue (DLQ) & Human Intervention**: If an unrecoverable failure occurs (e.g. external payment gateway permanent rejection), the saga transitions to a `Suspended` state, alerting operations via PagerDuty for manual reconciliation.
3. **Forward Recovery (Retry to Success)**: Prefer retrying the forward action with exponential backoff if compensation is impossible or expensive.

---

### Q6: What is W3C TraceContext and how does OpenTelemetry propagate it through RabbitMQ?
**Answer**:
The W3C TraceContext standard defines two headers:
- `traceparent`: `00-<trace-id>-<parent-span-id>-<trace-flags>`
- `tracestate`: vendor-specific opaque key-value pairs.
In HTTP, these are transmitted as standard request headers. In RabbitMQ, the OpenTelemetry .NET SDK injects these headers into `IBasicProperties.Headers` as byte arrays. The message consumer extracts them, instantiating a child `Activity` that preserves the distributed trace DAG across asynchronous message queues.

---

### Q7: How does Kubernetes HPA interact with Pod startup and readiness probes?
**Answer**:
1. When HPA scales up replicas based on CPU/custom metrics, new Pods enter the `ContainerCreating` phase.
2. The `startupProbe` buffers slow application initialization without triggering premature liveness restarts.
3. Once the startup probe succeeds, the `readinessProbe` runs. The Pod is *only* added to the Service endpoint slicing and assigned traffic once the readiness probe returns HTTP 200.
4. HPA stabilization windows (e.g. 300s scale-down delay) prevent autoscaling flapping under spiky workloads.

---

### Q8: Contrast Terraform declarative IaC with imperative scripts (Bash, AWS CLI).
**Answer**:
- **Imperative**: Tells the system *how* to achieve a state step-by-step (`aws ec2 run-instances`). Rerunning the script creates duplicate resources unless manual idempotency checks are coded.
- **Declarative**: Tells the system *what* the desired end-state should be (`resource "azurerm_container_app" ...`). Terraform calculates the exact diff between desired code and state, applying only necessary creations, modifications, or deletions idempotently.

---

### Q9: Mathematically analyze the time and space complexity of Merge K Sorted Lists.
**Answer**:
Let $K$ be number of lists and $N$ be total nodes:
- **Min-Heap**:
  - Initial heap construction: $K$ elements $= \mathcal{O}(K)$.
  - Each of the $N$ nodes is extracted once ($\mathcal{O}(\log K)$) and replaced with its next pointer ($\mathcal{O}(\log K)$).
  - Total Time: $\mathcal{O}(N \log K)$. Space: $\mathcal{O}(K)$ for the heap.
- **Divide-and-Conquer**:
  - Number of pairwise merge levels: $\lceil \log_2 K ceil$.
  - At each level, all $N$ nodes are traversed and linked via pointer manipulation.
  - Total Time: $\mathcal{O}(N \log K)$. Auxiliary Space: $\mathcal{O}(1)$ without priority queue memory allocations.

---

### Q10: How do you design an interview response for Phase 2 synthesis: Scaling from 10k to 1M RPS?
**Answer**:
Structure via layered architectural progression:
1. **Edge & Ingress Layer**: Anycast DNS, CDN static caching, Azure Front Door / CloudFront, and YARP API Gateway with rate limiting.
2. **Stateless Compute**: Containerized ASP.NET Core microservices deployed on Kubernetes with HPA and multi-AZ node pools.
3. **Caching Layer**: Redis cluster with distributed lock eviction and Cache-Aside pattern absorbing 90%+ read traffic.
4. **Asynchronous Messaging**: RabbitMQ / Kafka with Transactional Outbox for all mutating side effects.
5. **Database Partitioning**: Primary-replica read splitting, table sharding by tenant/user ID, and Blob storage tiering.
6. **Observability**: OpenTelemetry tracing, Prometheus percentiles, and Grafana alerting ensuring 99.99% availability.
