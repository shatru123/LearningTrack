using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Day14.EfCoreMigrations;

public enum ChangeSeverity
{
    Safe,
    Warning,
    Breaking
}

public record SchemaChangeEvaluation(
    string Statement,
    ChangeSeverity Severity,
    string Reason,
    string RecommendedMitigation);

public class MigrationSafetyAnalyzer
{
    private static readonly Regex DropTableRegex = new(@"\bDROP\s+TABLE\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex DropColumnRegex = new(@"\bALTER\s+TABLE\s+.*?\bDROP\s+COLUMN\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex RenameColumnRegex = new(@"\bRENAME\s+COLUMN\b|\bsp_rename\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex AddNotNullNoDefaultRegex = new(@"\bADD\s+(?!COLUMN\s+)?(\w+)\s+[^;]*?\bNOT\s+NULL\b(?!\s+DEFAULT)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex CreateIndexWithoutConcurrentRegex = new(@"\bCREATE\s+(?:UNIQUE\s+)?INDEX\s+(?!CONCURRENTLY\b)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public IReadOnlyList<SchemaChangeEvaluation> AnalyzeSql(string sqlScript)
    {
        var results = new List<SchemaChangeEvaluation>();
        var statements = sqlScript.Split(';', StringSplitOptions.RemoveEmptyEntries);

        foreach (var raw in statements)
        {
            var stmt = raw.Trim();
            if (string.IsNullOrWhiteSpace(stmt)) continue;

            if (DropTableRegex.IsMatch(stmt))
            {
                results.Add(new SchemaChangeEvaluation(
                    stmt,
                    ChangeSeverity.Breaking,
                    "Immediate data loss and fatal errors for in-flight service queries.",
                    "Adopt Expand-Contract: Stop writing to table, archive data, release contract migration later."));
            }
            else if (DropColumnRegex.IsMatch(stmt))
            {
                results.Add(new SchemaChangeEvaluation(
                    stmt,
                    ChangeSeverity.Breaking,
                    "Old application replicas querying the column will crash immediately.",
                    "Expand-Contract: Deprecate property in code first, verify no replicas use column, then drop."));
            }
            else if (RenameColumnRegex.IsMatch(stmt))
            {
                results.Add(new SchemaChangeEvaluation(
                    stmt,
                    ChangeSeverity.Breaking,
                    "Renaming breaks backward compatibility for running replicas during rolling deployment.",
                    "Add new column, dual-write to both in application code, backfill existing rows, then contract old column."));
            }
            else if (AddNotNullNoDefaultRegex.IsMatch(stmt))
            {
                results.Add(new SchemaChangeEvaluation(
                    stmt,
                    ChangeSeverity.Breaking,
                    "Adding NOT NULL column without DEFAULT locks large tables and fails if table has rows.",
                    "Add as nullable first, populate data/default in batches, then alter to NOT NULL with validation."));
            }
            else if (CreateIndexWithoutConcurrentRegex.IsMatch(stmt) && stmt.Contains("INDEX", StringComparison.OrdinalIgnoreCase))
            {
                results.Add(new SchemaChangeEvaluation(
                    stmt,
                    ChangeSeverity.Warning,
                    "Standard CREATE INDEX takes ACCESS EXCLUSIVE lock on PostgreSQL, blocking all writes.",
                    "Use CREATE INDEX CONCURRENTLY in production to avoid table write locks."));
            }
            else
            {
                results.Add(new SchemaChangeEvaluation(
                    stmt,
                    ChangeSeverity.Safe,
                    "Non-destructive backward-compatible schema expansion.",
                    "Safe for rolling deployments."));
            }
        }

        return results;
    }

    public bool IsDeploymentSafe(IReadOnlyList<SchemaChangeEvaluation> evaluations)
    {
        return !evaluations.Any(e => e.Severity == ChangeSeverity.Breaking);
    }
}
