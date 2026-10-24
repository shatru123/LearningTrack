# Day 48: Staff & Principal Interview Questions

### Q1: Compare Active-Passive (Pilot Light vs Warm Standby) and Active-Active multi-region architectures.
**Answer**:
- **Pilot Light**: Core data is replicated continuously (e.g. database replica, storage sync), but compute resources are off or minimal (e.g. AMIs/container images ready). During disaster recovery, compute must be provisioned. RTO: 15–30 minutes, Low cost.
- **Warm Standby**: Compute runs at a reduced capacity (e.g. 20% replicas) alongside hot database read replicas. During failover, autoscaling ramps compute to 100%. RTO: 3–5 minutes, Moderate cost.
- **Active-Active**: Full compute and multi-primary or multi-write data layers run in 2+ regions simultaneously. Traffic is routed via latency or geoproximity. RTO: < 30 seconds, Highest cost and engineering complexity (handling split-brain and write conflicts).

---

### Q2: What are RTO and RPO, and how do you design systems to meet strict financial SLAs?
**Answer**:
- **Recovery Time Objective (RTO)**: Maximum acceptable duration of downtime before system recovery.
- **Recovery Point Objective (RPO)**: Maximum acceptable age of data lost due to a disaster event.
For strict financial SLAs (e.g., RTO < 1 min, RPO = 0), synchronous multi-region replication (or 3-AZ synchronous quorum like AWS Aurora Global DB or Azure Cosmos DB multi-region strong consistency) is required. If asynchronous replication is used, RPO equals the replication lag; any un-replicated commits at failover time represent permanent data loss.

---

### Q3: How do you prevent split-brain during automated regional failover?
**Answer**:
When primary and secondary regions lose connectivity, the secondary might assume the primary died and self-promote, causing concurrent divergent writes. Mitigations:
1. **Third-Party Quorum / Witness**: A lightweight arbiter in a 3rd independent region or global consensus service (Route 53 ARC / Front Door health check controller) must vote on leader promotion.
2. **Fencing Tokens**: Use epoch-based fencing tokens on shared storage or databases to invalidate requests from a split-brained former primary.
3. **Automated Demotion / STONITH**: Shoot The Other Node In The Head—the orchestration platform revokes IAM permissions or shuts down ingress on the primary before promoting the secondary.

---

### Q4: How do cloud egress charges affect microservices architecture, and how do you optimize them?
**Answer**:
Data egress across availability zones (~$0.01/GB), regions (~$0.02/GB), and the internet (~$0.09/GB) constitutes a major portion of cloud bills. Optimizations:
1. **AZ Affinity**: Co-locate high-bandwidth chatty microservices within the same Availability Zone where possible.
2. **Private Endpoints**: Use Azure Private Link or AWS VPC Endpoints to route traffic internally rather than traversing public IPs.
3. **Edge Caching**: Deploy CloudFront or Azure Front Door with gzip/Brotli compression to cache static assets and API responses at the edge.
4. **Binary Protocols**: Replace large verbose JSON payloads with Protobuf or MessagePack to reduce wire payload sizes by 60–80%.

---

### Q5: How do you architect workloads to safely leverage Spot / Preemptible instances?
**Answer**:
Spot instances offer up to 90% savings but can be terminated by the cloud provider with a 30-to-120-second warning notice. Best practices:
1. **Stateless & Resumable Tasks**: Use Spot for background video transcoding, batch processing, and queue workers (RabbitMQ/SQS), where failed tasks are retried via Dead Letter Queues.
2. **Termination Hook Listeners**: Monitor AWS EC2 Metadata service (`/latest/meta-data/spot/instance-action`) or Azure Scheduled Events to gracefully drain connections and checkpoint progress within the grace period.
3. **Mixed Instance Pools**: Combine 20–30% On-Demand baseline capacity with 70–80% Spot instances across diverse instance types and availability zones.

---

### Q6: How does Azure Cosmos DB or AWS DynamoDB handle multi-region conflict resolution in Active-Active?
**Answer**:
When concurrent writes occur on the same record in different regions:
- **Last-Write-Wins (LWW)**: Uses UTC timestamps to select the latest write. Susceptible to clock skew (NTP drift).
- **Custom Conflict Resolution / Stored Procedures**: Application code merges fields (e.g. merging shopping cart item arrays).
- **CRDTs (Conflict-free Replicated Data Types)**: Commutative operations (incrementing counters, add-only sets) converge mathematically without central coordination.

---

### Q7: Explain the trade-offs of Object Storage Lifecycle Tiering (Hot, Cool, Cold, Archive).
**Answer**:
- **Hot**: Lowest access/retrieval fee, highest storage cost (~$0.018/GB). Ideal for active, frequently accessed data.
- **Cool**: Lower storage cost (~$0.010/GB), small retrieval fee, 30-day minimum retention.
- **Cold**: Even lower storage cost (~$0.0045/GB), moderate retrieval fee, 90-day minimum retention.
- **Archive**: Extremely low storage cost (~$0.00099/GB), highest retrieval fee, hours of retrieval latency (rehydration), 180-day minimum retention.
*Trap*: Prematurely moving frequently accessed data to Archive can incur exorbitant retrieval and early-deletion penalty fees.

---

### Q8: What is Cloud FinOps and how does it integrate into the engineering lifecycle?
**Answer**:
FinOps is the operational framework bringing financial accountability to cloud engineering:
1. **Tagging & Cost Allocation**: Enforcing mandatory tags (`CostCenter`, `Environment`, `ServiceOwner`) via Azure Policy or AWS SCPs.
2. **Unit Economics**: Measuring cost per business transaction (e.g., cost per order processed, cost per active user).
3. **Right-Sizing & Auto-Shutdown**: Automated scripts terminating idle dev/test environments overnight and scaling down overprovisioned VM/container SKUs based on P95 utilization.

---

### Q9: Why must the 1D dynamic programming array for 0/1 Knapsack be traversed backwards?
**Answer**:
In 0/1 Knapsack, each item can be chosen at most once. If we traverse the DP array forwards ($j = num \dots target$):
`dp[j] = dp[j] || dp[j - num]` would use the updated value of `dp[j - num]` from the *same* item iteration, effectively allowing the same item to be reused multiple times (which solves the Unbounded Knapsack problem). Traversing backwards ($j = target \dots num$) guarantees that `dp[j - num]` reflects the state from the *previous* item iteration.

---

### Q10: How can Bitset / BigInteger optimization accelerate Partition Equal Subset Sum?
**Answer**:
The boolean DP array transition can be viewed as shifting a bitmask:
`bitset = bitset | (bitset << num)`
With bitwise operations, a 64-bit integer can compute 64 states simultaneously using CPU SIMD instructions. For target sums up to $10^5$, a bitset evaluates the knapsack in $\mathcal{O}(N \cdot 	ext{Target} / 64)$ operations—yielding a 50x–60x speedup over standard boolean arrays.
