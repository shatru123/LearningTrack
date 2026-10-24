# Day 48: Cloud Architecture Patterns (AWS/Azure)

## Overview
Enterprise distributed systems running in public cloud providers (AWS, Azure) must guarantee business continuity across catastrophic regional outages while optimizing cloud spend. Day 48 explores multi-region failover (Active-Passive vs Active-Active), database replication lag bounds, blob storage lifecycle tiering, cloud compute and egress cost optimization, and LeetCode #416 Partition Equal Subset Sum (0/1 Knapsack variation).

## Key Concepts
- **Multi-Region Failover Architecture**: Warm Standby / Pilot Light vs Active-Active dual-write topologies with Azure Front Door / AWS Route 53 health probing.
- **RTO & RPO Metrics**: Calculating Recovery Time Objective (RTO) and Recovery Point Objective (RPO) based on DNS propagation and asynchronous replication lag.
- **Storage Lifecycle Policy**: Transitioning unaccessed objects through Hot, Cool, Cold, and Archive tiers.
- **FinOps & Cost Optimization**: Maximizing compute savings through Reserved Instances and Spot instances; eliminating data egress bandwidth taxes via Private Endpoints.
- **Algorithm (LC #416)**: Partition Equal Subset Sum solved via 0/1 knapsack 2D DP table and space-optimized 1D boolean array.
