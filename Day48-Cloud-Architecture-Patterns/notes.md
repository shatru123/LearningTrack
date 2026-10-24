# Technical Notes: Cloud Architecture Patterns & Cost Optimization

### 1. Multi-Region Failover Topologies
- **Active-Passive (Warm Standby)**: Primary region processes 100% of write traffic; secondary region hosts scaled-down compute and read replicas. Failover requires DNS record update + compute scale-out. RTO: 3–10 minutes, RPO: Asynchronous replication lag (seconds).
- **Active-Active (Dual-Region)**: Both regions process active reads and writes. Requires distributed conflict resolution (CRDTs, Last-Write-Wins, or partitioned tenancy). RTO: < 1 minute, RPO: Near zero.

### 2. Cloud Cost & Egress Bandwidth Economics
- **Compute Commitments**: On-Demand provides flexibility; 1-year and 3-year Savings Plans / Reserved Instances yield 40%–60% discounts; Spot instances provide up to 75%–90% discounts for fault-tolerant workers.
- **Egress Bandwidth Tax**: Public internet egress is charged at ~$0.09/GB; inter-region traffic at ~$0.02/GB; inter-AZ at ~$0.01/GB. Routing intra-cloud traffic via **Private Endpoints (Azure Private Link / AWS PrivateLink)** eliminates public internet traversal and reduces egress fees to zero.

### 3. LeetCode #416: Partition Equal Subset Sum
- Target sum $S = 	ext{totalSum} / 2$. If $	ext{totalSum}$ is odd, return `false`.
- **State Transition**: `dp[j] = dp[j] || dp[j - num]`.
- Reverse traversal in 1D array ($j = 	ext{target} \dots 	ext{num}$) ensures each element is used at most once (0/1 Knapsack property).
