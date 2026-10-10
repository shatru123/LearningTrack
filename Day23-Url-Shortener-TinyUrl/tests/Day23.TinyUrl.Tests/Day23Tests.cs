using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace Day23.TinyUrl.Tests;

public class Day23Tests
{
    // ==========================================
    // 1. CAPACITY PLANNING TESTS
    // ==========================================

    [Fact]
    public void CapacityPlanner_CalculatesAccurateThroughputAndStorage_For100MWritesAnd1BReads()
    {
        var plan = CapacityPlanner.Calculate(
            dailyWrites: 100_000_000L,
            dailyReads: 1_000_000_000L,
            peakMultiplier: 3.0,
            cachePercentage: 0.20
        );

        // 100M / 86400 = ~1157.4 QPS
        plan.AverageWriteQps.Should().BeApproximately(1157.4, 1.0);
        plan.PeakWriteQps.Should().BeApproximately(3472.2, 3.0);

        // 1B / 86400 = ~11574.1 QPS
        plan.AverageReadQps.Should().BeApproximately(11574.1, 1.0);
        plan.PeakReadQps.Should().BeApproximately(34722.2, 3.0);

        // 100M * 365 * 500 bytes = 18.25 TB/year
        plan.StorageBytesPerYear.Should().Be(18_250_000_000_000.0);
        plan.StorageBytesPerFiveYears.Should().Be(91_250_000_000_000.0);

        // 20% of 1B reads * 500 bytes = 100 GB cache
        plan.CacheMemoryBytes.Should().Be(100_000_000_000.0);
    }

    // ==========================================
    // 2. SNOWFLAKE ID GENERATOR TESTS
    // ==========================================

    [Fact]
    public void SnowflakeId_GeneratesMonotonicallyIncreasingUniqueIds()
    {
        var generator = new SnowflakeIdGenerator(datacenterId: 2, workerId: 5);
        var ids = new HashSet<long>();
        long prev = -1;

        for (int i = 0; i < 1000; i++)
        {
            var id = generator.NextId();
            id.Should().BeGreaterThan(0, "Snowflake IDs must be positive 64-bit integers");
            id.Should().BeGreaterThan(prev, "Snowflake IDs must monotonically increase");
            ids.Add(id).Should().BeTrue("Each Snowflake ID must be globally unique");
            prev = id;
        }
    }

    [Fact]
    public void SnowflakeId_ExtractsAccurateCreationTimestamp()
    {
        var generator = new SnowflakeIdGenerator(datacenterId: 1, workerId: 1);
        var before = DateTime.UtcNow;
        var id = generator.NextId();
        var after = DateTime.UtcNow;

        var extracted = SnowflakeIdGenerator.ExtractTimestampUtc(id);

        extracted.Should().BeOnOrAfter(before.AddMilliseconds(-10));
        extracted.Should().BeOnOrBefore(after.AddMilliseconds(10));
    }

    [Fact]
    public void SnowflakeId_ThrowsOnClockBackwardsDrift()
    {
        var generator = new SnowflakeIdGenerator(datacenterId: 1, workerId: 1);
        var baseMs = 1770000000000L;

        // Generate at baseMs
        generator.NextId(customTimestampMs: baseMs);

        // Simulate significant backwards drift (1000ms in the past)
        Action act = () => generator.NextId(customTimestampMs: baseMs - 1000);
        act.Should().Throw<InvalidOperationException>()
           .WithMessage("*Clock moved backwards*");
    }

    // ==========================================
    // 3. BASE62 CODEC TESTS
    // ==========================================

    [Theory]
    [InlineData(0L, "0")]
    [InlineData(1L, "1")]
    [InlineData(61L, "Z")]
    [InlineData(62L, "10")]
    [InlineData(123456789L, "8m0Kx")]
    [InlineData(long.MaxValue, "aZl8N0y58M7")]
    public void Base62Codec_RoundTripConversion_IsLossless(long original, string expectedEncoded)
    {
        var encoded = Base62Codec.Encode(original);
        encoded.Should().Be(expectedEncoded);

        var decoded = Base62Codec.Decode(encoded);
        decoded.Should().Be(original);
    }

    // ==========================================
    // 4. TINYURL SERVICE WORKFLOW TESTS
    // ==========================================

    [Fact]
    public async Task TinyUrlService_ShortenAndResolve_Success()
    {
        var service = new TinyUrlService(shardCount: 4);
        var longUrl = "https://learningsystem.internal/courses/dotnet-distributed-architecture?module=3";

        var record = await service.ShortenAsync(longUrl);
        record.Should().NotBeNull();
        record.ShortCode.Should().NotBeNullOrEmpty();
        record.LongUrl.Should().Be(longUrl);
        record.ShardId.Should().BeInRange(0, 3);

        // Resolve
        var resolved = await service.ResolveAsync(record.ShortCode);
        resolved.Should().Be(longUrl);

        var stats = service.GetStats(record.ShortCode);
        stats.Should().NotBeNull();
        stats!.ClickCount.Should().Be(1);
    }

    [Fact]
    public async Task TinyUrlService_CustomAlias_HandlesUniquenessAndConflicts()
    {
        var service = new TinyUrlService();
        var longUrl1 = "https://github.com/shatru123/LearningTrack";
        var longUrl2 = "https://google.com";

        var record = await service.ShortenAsync(longUrl1, customAlias: "my-repo");
        record.ShortCode.Should().Be("my-repo");

        var resolved = await service.ResolveAsync("my-repo");
        resolved.Should().Be(longUrl1);

        // Collision attempt with same alias
        Func<Task> act = async () => await service.ShortenAsync(longUrl2, customAlias: "my-repo");
        await act.Should().ThrowAsync<InvalidOperationException>()
                 .WithMessage("*already taken*");
    }

    [Fact]
    public async Task TinyUrlService_Expiration_PurgesExpiredUrls()
    {
        var service = new TinyUrlService();
        var longUrl = "https://temporary-promotion.com/flash-sale";
        var t0 = DateTime.UtcNow;

        var record = await service.ShortenAsync(longUrl, ttl: TimeSpan.FromMinutes(10), customNow: t0);

        // Resolving within validity period
        var active = await service.ResolveAsync(record.ShortCode, customNow: t0.AddMinutes(5));
        active.Should().Be(longUrl);

        // Resolving after TTL expires returns null
        var expired = await service.ResolveAsync(record.ShortCode, customNow: t0.AddMinutes(15));
        expired.Should().BeNull();
    }

    [Fact]
    public async Task TinyUrlService_CacheAside_IncrementsHitsAndMisses()
    {
        var service = new TinyUrlService();
        var longUrl = "https://engineering.blog/redis-internals";

        var record = await service.ShortenAsync(longUrl);

        // First resolve: cache was already warm from write-through -> Cache hit
        await service.ResolveAsync(record.ShortCode);
        service.CacheHits.Should().Be(1);

        // Resolving non-existent short code -> Cache miss
        var missing = await service.ResolveAsync("nonexistent_code_xyz");
        missing.Should().BeNull();
        service.CacheMisses.Should().Be(1);
    }

    // ==========================================
    // 5. DSA: CONSTRUCT TREE FROM PREORDER & INORDER (LC #105)
    // ==========================================

    [Fact]
    public void LC105_BuildTree_ReconstructsTreeCorrectly()
    {
        var solver = new TreeReconstructionSolvers();

        // Standard LeetCode test case:
        // preorder = [3, 9, 20, 15, 7]
        // inorder  = [9, 3, 15, 20, 7]
        // Tree:
        //        3
        //       / \
        //      9   20
        //         /  \
        //        15   7
        int[] preorder = { 3, 9, 20, 15, 7 };
        int[] inorder  = { 9, 3, 15, 20, 7 };

        var root = solver.BuildTree(preorder, inorder);

        root.Should().NotBeNull();
        root!.val.Should().Be(3);
        root.left!.val.Should().Be(9);
        root.left.left.Should().BeNull();
        root.left.right.Should().BeNull();

        root.right!.val.Should().Be(20);
        root.right.left!.val.Should().Be(15);
        root.right.right!.val.Should().Be(7);

        // Edge case: Single node
        var single = solver.BuildTree(new[] { 1 }, new[] { 1 });
        single.Should().NotBeNull();
        single!.val.Should().Be(1);
        single.left.Should().BeNull();
        single.right.Should().BeNull();

        // Edge case: Empty array
        var empty = solver.BuildTree(Array.Empty<int>(), Array.Empty<int>());
        empty.Should().BeNull();
    }
}
