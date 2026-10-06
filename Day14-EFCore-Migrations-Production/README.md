# Day 14: EF Core Migrations & Production Strategy

## Overview
Designing and executing zero-downtime database schema deployment pipelines in high-availability enterprise environments. This module tackles automated CI/CD migration scripts (`dotnet ef migrations script -i`), breaking change identification, expand-contract patterns for multi-replica Kubernetes clusters, advisory locking mechanisms, and DSA: Search in Rotated Sorted Array (LeetCode #33 & #81).

---

## Key Architectural Concepts

### 1. The Anti-Pattern: `DbContext.Database.Migrate()` on Application Startup
Executing migrations inside `Program.cs` during application bootstrap creates catastrophic failure modes:
- **Race Conditions in Multi-Replica Deployments**: When Kubernetes scales 10 pods simultaneously, multiple replicas attempt to apply DDL concurrently, causing deadlocks or corrupted `__EFMigrationsHistory` tables.
- **Excessive Database Permissions**: The web application connection string requires DDL privileges (`ALTER TABLE`, `DROP`, `CREATE`), violating the principle of least privilege.
- **Uncontrolled Rollouts**: If a migration fails mid-way, application pods crash-loop and bring down the entire cluster.

### 2. The Production Solution: Idempotent SQL Scripts (`-i`)
Generate migration SQL artifacts during CI/CD build stages:
```bash
dotnet ef migrations script --idempotent --output ./migrations.sql --context MigrationDbContext
```
The resulting SQL wraps each migration step in an `IF NOT EXISTS` check against `__EFMigrationsHistory`:
- Safe to re-run multiple times without failure.
- Allows DBAs to inspect, benchmark, and approve schema changes before deployment.
- Executed by a dedicated CI/CD deployment runner with elevated DDL credentials, keeping the web application restricted to DML (`SELECT`, `INSERT`, `UPDATE`, `DELETE`).

### 3. Expand-Contract Pattern for Rolling Updates
When making schema modifications (such as renaming or dropping a column):
1. **Phase 1 (Expand)**: Add the new column as nullable. Deploy Phase 1 application which reads from old column and writes to both old and new columns (dual-write).
2. **Phase 2 (Backfill)**: Run an asynchronous background job to backfill historical data from the old column into the new column.
3. **Phase 3 (Switch)**: Deploy Phase 2 application which reads and writes exclusively to the new column.
4. **Phase 4 (Contract)**: Drop the old column in a subsequent migration after all previous replica versions have terminated.

---

## LeetCode Problems Solved

### LeetCode #33: Search in Rotated Sorted Array
- **Algorithm**: Modified Binary Search determining which half of the array is monotonically sorted (`nums[left] <= nums[mid]`).
- **Complexity**: Time: $O(\log N)$, Space: $O(1)$.

### LeetCode #81: Search in Rotated Sorted Array II (Duplicates)
- **Algorithm**: When `nums[left] == nums[mid] == nums[right]`, we cannot know which half is sorted. Safely shrink both ends (`left++`, `right--`).
- **Complexity**: Average Time: $O(\log N)$, Worst Time: $O(N)$, Space: $O(1)$.
