# Day 47: Infrastructure as Code with Terraform

## Overview
Enterprise systems rely on declarative Infrastructure as Code (IaC) to eliminate configuration drift and automate cloud infrastructure provisioning. Day 47 explores Terraform HCL syntax, resource directed acyclic graphs (DAG), remote state storage with distributed state locking (Azure Blob Storage / AWS S3 + DynamoDB), and LeetCode #300 Longest Increasing Subsequence (LIS) solved via both $\mathcal{O}(N^2)$ DP and $\mathcal{O}(N \log N)$ Patience Sorting.

## Key Concepts
- **HCL Grammar & Providers**: Declaring providers (`azurerm`, `aws`), resources, variables, locals, and outputs.
- **Dependency Graph & Topological Sort**: Evaluating implicit and explicit `depends_on` relationships via Kahn's algorithm.
- **State Management & Remote Locking**: Lease-based state locking preventing split-brain corruption during team collaboration.
- **Drift Detection & Plan Diffs**: Comparing desired code vs `terraform.tfstate` vs actual cloud control plane.
- **Algorithm (LC #300)**: Patience sorting with binary search for $\mathcal{O}(N \log N)$ LIS determination.
