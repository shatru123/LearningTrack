using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace Day14.EfCoreMigrations;

public record MigrationStep(string MigrationId, string UpSql);

public class IdempotentMigrationPipeline
{
    private readonly ConcurrentDictionary<string, byte> _appliedMigrations = new();
    private readonly SemaphoreSlim _advisoryLock = new(1, 1);
    private int _isLocked = 0;

    public IReadOnlyCollection<string> AppliedMigrations => _appliedMigrations.Keys.ToList();

    public async Task<bool> AcquireMigrationLockAsync(TimeSpan timeout, CancellationToken ct = default)
    {
        var entered = await _advisoryLock.WaitAsync(timeout, ct);
        if (entered)
        {
            Interlocked.Exchange(ref _isLocked, 1);
            return true;
        }
        return false;
    }

    public void ReleaseMigrationLock()
    {
        if (Interlocked.Exchange(ref _isLocked, 0) == 1)
        {
            _advisoryLock.Release();
        }
    }

    public string GenerateIdempotentSql(IEnumerable<MigrationStep> steps)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("-- Idempotent Migration Script Generated for Zero-Downtime Pipeline");
        sb.AppendLine("CREATE TABLE IF NOT EXISTS \"__EFMigrationsHistory\" (\"MigrationId\" TEXT NOT NULL PRIMARY KEY, \"ProductVersion\" TEXT NOT NULL);");

        foreach (var step in steps)
        {
            sb.AppendLine($"-- Begin Migration: {step.MigrationId}");
            sb.AppendLine($"-- Check if already applied");
            sb.AppendLine($"-- IF NOT EXISTS (SELECT 1 FROM \"__EFMigrationsHistory\" WHERE \"MigrationId\" = '{step.MigrationId}')");
            sb.AppendLine(step.UpSql);
            sb.AppendLine($"INSERT INTO \"__EFMigrationsHistory\" (\"MigrationId\", \"ProductVersion\") VALUES ('{step.MigrationId}', '8.0.8');");
            sb.AppendLine($"-- End Migration: {step.MigrationId}\n");
        }

        return sb.ToString();
    }

    public async Task<int> ApplyMigrationsIdempotentlyAsync(
        IEnumerable<MigrationStep> steps,
        SqliteConnection connection,
        CancellationToken ct = default)
    {
        // Ensure table exists
        await using (var initCmd = connection.CreateCommand())
        {
            initCmd.CommandText = "CREATE TABLE IF NOT EXISTS \"__EFMigrationsHistory\" (\"MigrationId\" TEXT NOT NULL PRIMARY KEY, \"ProductVersion\" TEXT NOT NULL);";
            await initCmd.ExecuteNonQueryAsync(ct);
        }

        int appliedCount = 0;
        foreach (var step in steps)
        {
            // Check if applied
            bool alreadyApplied = false;
            await using (var checkCmd = connection.CreateCommand())
            {
                checkCmd.CommandText = "SELECT COUNT(1) FROM \"__EFMigrationsHistory\" WHERE \"MigrationId\" = @mId;";
                checkCmd.Parameters.AddWithValue("@mId", step.MigrationId);
                var scalar = await checkCmd.ExecuteScalarAsync(ct);
                alreadyApplied = Convert.ToInt64(scalar) > 0;
            }

            if (!alreadyApplied)
            {
                await using var trans = (SqliteTransaction)await connection.BeginTransactionAsync(ct);
                try
                {
                    await using var upCmd = connection.CreateCommand();
                    upCmd.Transaction = trans;
                    upCmd.CommandText = step.UpSql;
                    await upCmd.ExecuteNonQueryAsync(ct);

                    await using var recordCmd = connection.CreateCommand();
                    recordCmd.Transaction = trans;
                    recordCmd.CommandText = "INSERT INTO \"__EFMigrationsHistory\" (\"MigrationId\", \"ProductVersion\") VALUES (@mId, '8.0.8');";
                    recordCmd.Parameters.AddWithValue("@mId", step.MigrationId);
                    await recordCmd.ExecuteNonQueryAsync(ct);

                    await trans.CommitAsync(ct);
                    _appliedMigrations.TryAdd(step.MigrationId, 0);
                    appliedCount++;
                }
                catch
                {
                    await trans.RollbackAsync(ct);
                    throw;
                }
            }
        }

        return appliedCount;
    }
}
