# Day 14 - Engineering Notes: EF Core Migrations & Production Strategy

## 1. Zero-Downtime Migration Pipeline Architecture

### Migration Deployment Strategies Matrix

| Strategy | When to Use | Pros | Cons |
|---|---|---|---|
| **`Database.Migrate()` on Startup** | Local development only | Zero friction | Pod race conditions, high DDL DB privileges required, crash loops |
| **Idempotent SQL Script (`-i`)** | Standard CI/CD Pipelines | Auditable by DBAs, transactional, reproducible | Requires CI/CD execution pipeline |
| **Migration Bundles (`dotnet ef migrations bundle`)** | Containerized Kubernetes Jobs | Self-contained executable, no .NET SDK required on runner | Slightly larger artifact |

---

## 2. DDL Locking & Concurrency Hazards

### Exclusive Locks During DDL
In PostgreSQL and SQL Server, DDL operations take heavy schema locks:
- `ALTER TABLE ... ADD COLUMN ... NOT NULL` without a default value takes an `ACCESS EXCLUSIVE` lock in older database engines and rewrites the entire table heap.
- `CREATE INDEX` takes a table-level write lock. In production, always use `CREATE INDEX CONCURRENTLY` (PostgreSQL) or `WITH (ONLINE = ON)` (SQL Server Enterprise).

### Application Advisory Locking
When multi-replica services must synchronize schema application, use database-native advisory locks:
- PostgreSQL: `SELECT pg_advisory_lock(123456);`
- SQL Server: `sp_getapplock @Resource = 'DbMigration', @LockMode = 'Exclusive';`

---

## 3. The Expand-Contract (Parallel-Run) Pattern

```
Timeline:
V1 App  ----[Reads/Writes Old Col]------------------> Terminated
V1.5 App                        ----[Dual-Write Both]---> Terminated
V2 App                                              ----[Reads/Writes New Col]---->
Database: [Old Col] -------------> [Add New Col] ------> [Backfill] -----------> [Drop Old Col]
```

1. **Step 1 (Expand)**: Add the new column as `NULL`. Old application continues running without disruption.
2. **Step 2 (Dual Write)**: Update application to read from Old and write to both Old and New.
3. **Step 3 (Backfill)**: Backfill historical rows in small batches (e.g., 5,000 rows per transaction) to prevent lock escalation.
4. **Step 4 (Cutover)**: Point read logic to New column.
5. **Step 5 (Contract)**: Drop Old column once no replicas read from it.

---

## 4. Modified Binary Search on Rotated Arrays (LC #33 & #81)

```csharp
int mid = left + (right - left) / 2;
if (nums[mid] == target) return mid;

if (nums[left] <= nums[mid]) // Left half is monotonically increasing
{
    if (nums[left] <= target && target < nums[mid])
        right = mid - 1;
    else
        left = mid + 1;
}
else // Right half is monotonically increasing
{
    if (nums[mid] < target && target <= nums[right])
        left = mid + 1;
    else
        right = mid - 1;
}
```
When duplicates exist, `nums[left] == nums[mid] == nums[right]` destroys our ability to tell which side is sorted. Pruning `left++` and `right--` recovers correctness with $O(N)$ worst-case.
