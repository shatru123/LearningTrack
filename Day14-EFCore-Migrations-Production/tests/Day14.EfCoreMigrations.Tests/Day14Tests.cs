using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Day14.EfCoreMigrations.Tests;

public class Day14Tests
{
    [Theory]
    [InlineData(new int[] { 4, 5, 6, 7, 0, 1, 2 }, 0, 4)]
    [InlineData(new int[] { 4, 5, 6, 7, 0, 1, 2 }, 3, -1)]
    [InlineData(new int[] { 1 }, 0, -1)]
    [InlineData(new int[] { 1 }, 1, 0)]
    [InlineData(new int[] { 3, 1 }, 1, 1)]
    [InlineData(new int[] { 5, 1, 3 }, 5, 0)]
    public void LeetCode33_Search_RotatedDistinctArray_FindsCorrectIndex(int[] nums, int target, int expected)
    {
        var result = RotatedArraySearcher.Search(nums, target);
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(new int[] { 2, 5, 6, 0, 0, 1, 2 }, 0, true)]
    [InlineData(new int[] { 2, 5, 6, 0, 0, 1, 2 }, 3, false)]
    [InlineData(new int[] { 1, 0, 1, 1, 1 }, 0, true)]
    [InlineData(new int[] { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 2, 1, 1, 1, 1, 1 }, 2, true)]
    [InlineData(new int[] { 1, 1, 1, 1 }, 2, false)]
    [InlineData(new int[] { 3, 1, 1 }, 3, true)]
    public void LeetCode81_SearchWithDuplicates_RotatedArray_ReturnsExpected(int[] nums, int target, bool expected)
    {
        var result = RotatedArraySearcher.SearchWithDuplicates(nums, target);
        result.Should().Be(expected);
    }

    [Fact]
    public void MigrationSafetyAnalyzer_DetectsBreakingChanges()
    {
        var analyzer = new MigrationSafetyAnalyzer();
        var sql = @"
            ALTER TABLE Users DROP COLUMN LegacyToken;
            DROP TABLE TempAudit;
            ALTER TABLE Orders RENAME COLUMN OldStatus TO NewStatus;
            ALTER TABLE Accounts ADD Balance DECIMAL NOT NULL;
            CREATE INDEX idx_users_email ON Users(Email);
            ALTER TABLE Users ADD Nickname TEXT NULL;
        ";

        var evaluations = analyzer.AnalyzeSql(sql);

        evaluations.Should().HaveCount(6);
        evaluations.Count(e => e.Severity == ChangeSeverity.Breaking).Should().Be(4);
        evaluations.Count(e => e.Severity == ChangeSeverity.Warning).Should().Be(1);
        evaluations.Count(e => e.Severity == ChangeSeverity.Safe).Should().Be(1);

        analyzer.IsDeploymentSafe(evaluations).Should().BeFalse();
    }

    [Fact]
    public void MigrationSafetyAnalyzer_ApprovesSafeExpandStep()
    {
        var analyzer = new MigrationSafetyAnalyzer();
        var safeSql = @"
            CREATE TABLE FeatureFlags (Id INT, Name TEXT);
            ALTER TABLE Users ADD PreferencesJson TEXT NULL;
        ";

        var evaluations = analyzer.AnalyzeSql(safeSql);
        analyzer.IsDeploymentSafe(evaluations).Should().BeTrue();
    }

    [Fact]
    public async Task IdempotentMigrationPipeline_AppliesOnlyOnce()
    {
        using var conn = new SqliteConnection("DataSource=:memory:");
        await conn.OpenAsync();

        var pipeline = new IdempotentMigrationPipeline();
        var steps = new[]
        {
            new MigrationStep("20261004_CreateUsers", "CREATE TABLE Users (Id INTEGER PRIMARY KEY, Username TEXT NOT NULL);"),
            new MigrationStep("20261004_AddEmail", "ALTER TABLE Users ADD COLUMN Email TEXT NULL;")
        };

        // First execution -> 2 applied
        var appliedFirst = await pipeline.ApplyMigrationsIdempotentlyAsync(steps, conn);
        appliedFirst.Should().Be(2);

        // Second execution -> 0 applied (idempotency verified)
        var appliedSecond = await pipeline.ApplyMigrationsIdempotentlyAsync(steps, conn);
        appliedSecond.Should().Be(0);

        // Verify Users table works
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO Users (Id, Username, Email) VALUES (1, 'alice', 'alice@test.com');";
        await cmd.ExecuteNonQueryAsync();

        cmd.CommandText = "SELECT COUNT(*) FROM Users;";
        var count = Convert.ToInt32(await cmd.ExecuteScalarAsync());
        count.Should().Be(1);
    }

    [Fact]
    public async Task MigrationPipeline_AdvisoryLock_PreventsSimultaneousRuns()
    {
        var pipeline = new IdempotentMigrationPipeline();

        var acquired1 = await pipeline.AcquireMigrationLockAsync(TimeSpan.FromMilliseconds(100));
        acquired1.Should().BeTrue();

        // Second attempt times out immediately
        var acquired2 = await pipeline.AcquireMigrationLockAsync(TimeSpan.FromMilliseconds(50));
        acquired2.Should().BeFalse();

        pipeline.ReleaseMigrationLock();

        // Third attempt succeeds after release
        var acquired3 = await pipeline.AcquireMigrationLockAsync(TimeSpan.FromMilliseconds(100));
        acquired3.Should().BeTrue();
        pipeline.ReleaseMigrationLock();
    }

    [Fact]
    public void GenerateIdempotentSql_IncludesHistoryTableChecks()
    {
        var pipeline = new IdempotentMigrationPipeline();
        var steps = new[]
        {
            new MigrationStep("20261004_Init", "CREATE TABLE Items (Id INT);")
        };

        var script = pipeline.GenerateIdempotentSql(steps);
        script.Should().Contain("__EFMigrationsHistory");
        script.Should().Contain("20261004_Init");
    }
}
