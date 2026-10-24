# Day 49: System Design: Distributed Job Scheduler

## Overview
High-scale architectures require robust background and distributed job scheduling frameworks (e.g. Hangfire Pro, Quartz.NET clustering, Temporal, Cadence) to execute millions of recurring and delayed tasks reliably. Day 49 covers distributed leader election with monotonic fencing tokens, worker node heartbeats and dead node task reassignment, at-least-once task dispatch with idempotency tokens, and LeetCode #42 Trapping Rain Water (Hard) solved via both Two-Pointer and Monotonic Decreasing Stack approaches.

## Key Concepts
- **Leader Election with Fencing Tokens**: Preventing split-brain race conditions when distributed nodes compete to coordinate task schedules.
- **Worker Registry & Liveness Heartbeats**: Monitoring distributed worker node health and automatically reallocating uncompleted tasks upon node failure.
- **Task Dispatch & Idempotency**: Guaranteeing at-least-once delivery with at-most-once execution via unique execution tokens.
- **Retry Backoff with Jitter**: Mitigating thundering herd stampedes when downstream dependencies fail.
- **Algorithm (LC #42)**: Trapping Rain Water using two pointers ($\mathcal{O}(N)$ time, $\mathcal{O}(1)$ space) and monotonic decreasing stack ($\mathcal{O}(N)$ space).
