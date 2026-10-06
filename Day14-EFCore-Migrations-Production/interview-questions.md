# Day 14: Senior / Staff-Level Interview Questions & Deep Answers

### Q1: Why is running `context.Database.Migrate()` in production `Program.cs` considered an anti-pattern?
- **Short answer**: In multi-replica cloud environments (Kubernetes, ECS), parallel startup causes race conditions, lock deadlocks, and migration table corruption. It also forces web application containers to hold destructive DDL database permissions.
- **Deeper answer**:
  When a deployment scales out, 5–20 pods boot concurrently. Each executes `Migrate()`. Even though EF Core attempts to coordinate via transactions, DDL statements in many databases (like PostgreSQL or MySQL) either do not support transactional rollback or hold catalog locks that cause immediate deadlocks. Furthermore, security compliance (SOC2/PCI-DSS) requires least privilege: running services should only hold DML (`SELECT`, `INSERT`, `UPDATE`, `DELETE`) grants, never DDL (`DROP`, `ALTER`, `TRUNCATE`). If an application container is compromised, having DDL permissions allows attackers to drop entire schemas.

---

### Q2: How does `dotnet ef migrations script --idempotent` achieve idempotency?
- **Short answer**: It wraps each migration's DDL inside a check querying `__EFMigrationsHistory`, executing statements only if the specific `MigrationId` has not yet been recorded.
- **Deeper answer**:
  The generated script creates `__EFMigrationsHistory` if it does not exist. For every migration, it evaluates:
  ```sql
  IF NOT EXISTS (SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261004_Initial')
  BEGIN
      -- DDL commands here
      INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion") VALUES ('20261004_Initial', '8.0.8');
  END;
  ```
  This allows the deployment pipeline to run the exact same script on staging, production, or disaster recovery instances without errors, ensuring the database is always brought to the target migration level safely.

---

### Q3: What is the "Expand-Contract" pattern and why is it mandatory for zero-downtime rolling deployments?
- **Short answer**: It decouples schema changes into two non-breaking phases: expanding the schema first so old and new application versions can co-exist, and contracting (deleting obsolete structures) only after old versions terminate.
- **Deeper answer**:
  During a Kubernetes rolling deployment, old pods (V1) and new pods (V2) run simultaneously for several minutes. If V2 drops or renames a column upon deployment, in-flight requests on V1 will crash instantly with `ColumnNotFoundException`.
  - **Expand**: Add new columns as nullable or with defaults. V2 dual-writes to both columns.
  - **Backfill**: Asynchronous background jobs sync historic data.
  - **Contract**: Once V1 pods are completely terminated and V2 runs 100% of traffic, a subsequent deployment drops the old column.

---

### Q4: How should large table index creation be handled in PostgreSQL and SQL Server production databases?
- **Short answer**: Standard `CREATE INDEX` locks the entire table against concurrent writes. In production, use `CREATE INDEX CONCURRENTLY` in PostgreSQL or `WITH (ONLINE = ON)` in SQL Server.
- **Deeper answer**:
  On a table with 50 million rows, standard index creation can take minutes, blocking all incoming `INSERT`, `UPDATE`, and `DELETE` queries, causing cascading request timeouts across microservices.
  In EF Core, you override the migration:
  ```csharp
  migrationBuilder.CreateIndex(
      name: "IX_Orders_CustomerId",
      table: "Orders",
      column: "CustomerId")
      .Annotation("Npgsql:Concurrent", true);
  ```
  Note: PostgreSQL requires concurrent index operations to execute outside of a multi-statement transaction (`suppressTransaction: true`).

---

### Q5: How do EF Core Migration Bundles (`dotnet ef migrations bundle`) work in containerized CI/CD?
- **Short answer**: A migration bundle compiles migrations and the EF Core runtime into a self-contained, single-file executable that runs without requiring the .NET SDK or source code.
- **Deeper answer**:
  In modern CI/CD:
  1. Build container runs `dotnet ef migrations bundle --output ./bundle --self-contained -r linux-x64`.
  2. The CI/CD pipeline runs `./bundle --connection "Server=..."` as a Kubernetes `Job` or pre-deployment task.
  3. The web application deployment only proceeds if the migration job exits with exit code 0.
  This completely isolates migration credentials and execution from the running web service containers.

---

### Q6: How does advisory locking prevent concurrent migration execution across distributed workers?
- **Short answer**: Advisory locks provide an explicit, application-defined lock in the database catalog that prevents multiple workers from executing migration logic simultaneously.
- **Deeper answer**:
  Unlike row locks, advisory locks do not lock specific records. In PostgreSQL, `SELECT pg_advisory_lock(app_hash)` or `pg_try_advisory_lock()` reserves a 64-bit integer lock. If a pod crashes, PostgreSQL automatically releases session-level advisory locks when the connection drops. This guarantees that even if three deployment jobs trigger at once, only one executes DDL while the others wait or gracefully exit.

---

### Q7: What are the risks of adding a `NOT NULL` column to an existing table with millions of rows?
- **Short answer**: Without a default value, the database must validate or fail on all existing rows, taking table-level write locks. In older database engines, it rewrites every data page on disk.
- **Deeper answer**:
  In SQL Server 2012+ and PostgreSQL 11+, adding a `NOT NULL` column with a constant `DEFAULT` is metadata-only (fast). However:
  1. If added without a default value, it immediately fails because existing rows evaluate to `NULL`.
  2. If the default is volatile (e.g., `DEFAULT NEWID()`), the engine must generate a value for every row on disk, causing massive disk I/O, WAL expansion, and replication lag.
  **Best Practice**: Add the column as `NULL`, backfill in batches, and alter column to `NOT NULL` once populated.

---

### Q8: How do you handle schema rollbacks safely in a zero-downtime architecture?
- **Short answer**: Never roll back the database schema in production. Instead, fix forward with a new migration or roll back only application code that was designed to support the expanded schema.
- **Deeper answer**:
  Down migrations (`Down()`) often involve dropping newly added columns or tables. If the new code wrote legitimate customer data to those columns during the 10 minutes it was live, running `Down()` causes irrecoverable data loss. Under the Expand-Contract model, because schema expansions are strictly backward-compatible, rolling back application code from V2 to V1 requires zero database changes because V1 never depended on the newly added fields.

---

### Q9: In LeetCode #33 (Search in Rotated Sorted Array), how do we guarantee $O(\log N)$ time complexity?
- **Short answer**: At every step of binary search, at least one half of the array (left to mid, or mid to right) is guaranteed to be strictly sorted.
- **Deeper answer**:
  When an array is rotated at a pivot, splitting at `mid` always divides it into one normally sorted subarray and one rotated subarray.
  1. If `nums[left] <= nums[mid]`, the left subarray is sorted. We check if `nums[left] <= target < nums[mid]`. If yes, search left; else search right.
  2. If `nums[left] > nums[mid]`, the right subarray is guaranteed to be sorted. We check if `nums[mid] < target <= nums[right]`. If yes, search right; else search left.
  Because half the search space is eliminated on each iteration, time is strictly $O(\log N)$.

---

### Q10: Why does duplicate elements in LeetCode #81 degrade binary search from $O(\log N)$ to $O(N)$?
- **Short answer**: When `nums[left] == nums[mid] == nums[right]`, it is impossible to determine whether the left or right half contains the pivot or target without scanning.
- **Deeper answer**:
  Consider `[1, 1, 1, 2, 1, 1]` vs `[1, 2, 1, 1, 1, 1]`.
  In both cases, `nums[0] == 1`, `nums[mid] == 1`, and `nums[5] == 1`.
  The value 2 could be located either to the right or to the left of `mid`. No constant-time mathematical comparison can distinguish which partition is sorted. The only safe action is `left++` and `right--`. In the worst case where all elements are identical except one (e.g., `[1, 1, 1, 1, 2]`), the algorithm decrements one by one, resulting in $O(N)$ linear time.
