using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Day33.DistributedIdempotency.Tests;

public class Day33Tests
{
    private IdempotencyDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<IdempotencyDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new IdempotencyDbContext(options);
    }

    [Fact]
    public async Task IdempotencyManager_FirstExecution_ExecutesAndStoresResult()
    {
        using var context = CreateDbContext();
        var manager = new IdempotencyManager(context);
        var key = "order-tx-101";
        var payload = "{\"orderId\": 101, \"amount\": 250.00}";
        int callCount = 0;

        var result = await manager.ExecuteAsync(key, payload, async () =>
        {
            callCount++;
            await Task.Delay(10);
            return new IdempotentResult(201, "{\"status\": \"created\", \"id\": 101}");
        });

        Assert.Equal(1, callCount);
        Assert.False(result.FromCache);
        Assert.Equal(201, result.StatusCode);
        Assert.Contains("created", result.ResponseBody);

        var record = await context.IdempotencyRecords.FindAsync(key);
        Assert.NotNull(record);
        Assert.Equal("Completed", record.Status);
        Assert.Equal(201, record.ResponseStatusCode);
    }

    [Fact]
    public async Task IdempotencyManager_DuplicateRequest_ReturnsCachedResult_WithoutReExecuting()
    {
        using var context = CreateDbContext();
        var manager = new IdempotencyManager(context);
        var key = "order-tx-102";
        var payload = "{\"orderId\": 102, \"amount\": 99.99}";
        int callCount = 0;

        // First call
        var firstResult = await manager.ExecuteAsync(key, payload, async () =>
        {
            callCount++;
            return new IdempotentResult(200, "{\"tx\": \"ok\"}");
        });

        // Second call with same key and payload
        var secondResult = await manager.ExecuteAsync(key, payload, async () =>
        {
            callCount++;
            return new IdempotentResult(200, "{\"tx\": \"should not run\"}");
        });

        Assert.Equal(1, callCount);
        Assert.False(firstResult.FromCache);
        Assert.True(secondResult.FromCache);
        Assert.Equal("{\"tx\": \"ok\"}", secondResult.ResponseBody);
    }

    [Fact]
    public async Task IdempotencyManager_PayloadMismatch_ThrowsIdempotencyPayloadMismatchException()
    {
        using var context = CreateDbContext();
        var manager = new IdempotencyManager(context);
        var key = "order-tx-103";
        var payload1 = "{\"orderId\": 103, \"amount\": 100.00}";
        var payload2 = "{\"orderId\": 103, \"amount\": 500.00}";

        await manager.ExecuteAsync(key, payload1, async () =>
        {
            return new IdempotentResult(200, "{\"status\": \"ok\"}");
        });

        await Assert.ThrowsAsync<IdempotencyPayloadMismatchException>(async () =>
        {
            await manager.ExecuteAsync(key, payload2, async () =>
            {
                return new IdempotentResult(200, "{\"status\": \"tampered\"}");
            });
        });
    }

    [Fact]
    public async Task IdempotencyManager_InFlightConcurrency_ThrowsIdempotencyConflictException()
    {
        using var context = CreateDbContext();
        var key = "order-tx-104";
        var payload = "{\"orderId\": 104}";

        // Seed an in-flight record with active lease
        context.IdempotencyRecords.Add(new IdempotencyRecord
        {
            IdempotencyKey = key,
            RequestPayloadHash = IdempotencyManager.ComputeSha256(payload),
            Status = "Processing",
            CreatedAtUtc = DateTime.UtcNow,
            LockedUntilUtc = DateTime.UtcNow.AddMinutes(5)
        });
        await context.SaveChangesAsync();

        var manager = new IdempotencyManager(context);

        var ex = await Assert.ThrowsAsync<IdempotencyConflictException>(async () =>
        {
            await manager.ExecuteAsync(key, payload, async () =>
            {
                return new IdempotentResult(200, "{}");
            });
        });

        Assert.Contains("in progress", ex.Message);
    }

    [Fact]
    public async Task IdempotencyManager_ExpiredLease_RecoversAndExecutes()
    {
        using var context = CreateDbContext();
        var key = "order-tx-105";
        var payload = "{\"orderId\": 105}";

        // Seed a crashed worker's record whose lease expired 10 minutes ago
        context.IdempotencyRecords.Add(new IdempotencyRecord
        {
            IdempotencyKey = key,
            RequestPayloadHash = IdempotencyManager.ComputeSha256(payload),
            Status = "Processing",
            CreatedAtUtc = DateTime.UtcNow.AddMinutes(-20),
            LockedUntilUtc = DateTime.UtcNow.AddMinutes(-10)
        });
        await context.SaveChangesAsync();

        var manager = new IdempotencyManager(context);
        int executed = 0;

        var result = await manager.ExecuteAsync(key, payload, async () =>
        {
            executed++;
            return new IdempotentResult(200, "{\"recovered\": true}");
        });

        Assert.Equal(1, executed);
        Assert.False(result.FromCache);
        Assert.Equal("{\"recovered\": true}", result.ResponseBody);

        var updatedRecord = await context.IdempotencyRecords.FindAsync(key);
        Assert.NotNull(updatedRecord);
        Assert.Equal("Completed", updatedRecord.Status);
    }

    [Fact]
    public async Task IdempotencyManager_FailedOperation_MarksRecordFailedAndRethrows()
    {
        using var context = CreateDbContext();
        var manager = new IdempotencyManager(context);
        var key = "order-tx-106";
        var payload = "{\"orderId\": 106}";

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await manager.ExecuteAsync(key, payload, async () =>
            {
                throw new InvalidOperationException("Downstream payment service down");
            });
        });

        var record = await context.IdempotencyRecords.FindAsync(key);
        Assert.NotNull(record);
        Assert.Equal("Failed", record.Status);
        Assert.Null(record.LockedUntilUtc);
    }

    [Fact]
    public async Task IdempotencyManager_EmptyIdempotencyKey_ExecutesDirectlyWithoutCache()
    {
        using var context = CreateDbContext();
        var manager = new IdempotencyManager(context);
        int count = 0;

        var res1 = await manager.ExecuteAsync("", "{}", async () =>
        {
            count++;
            return new IdempotentResult(200, "non-idempotent-1");
        });

        var res2 = await manager.ExecuteAsync(null!, "{}", async () =>
        {
            count++;
            return new IdempotentResult(200, "non-idempotent-2");
        });

        Assert.Equal(2, count);
        Assert.False(res1.FromCache);
        Assert.False(res2.FromCache);
        Assert.Empty(await context.IdempotencyRecords.ToListAsync());
    }

    [Fact]
    public void KthLargest_Quickselect_StandardArray_ReturnsCorrectElement()
    {
        int[] nums = [3, 2, 1, 5, 6, 4];
        int k = 2;

        int result = KthLargestSolver.FindKthLargestQuickselect(nums, k);

        Assert.Equal(5, result);
    }

    [Fact]
    public void KthLargest_Quickselect_WithDuplicates_ReturnsCorrectElement()
    {
        int[] nums = [3, 2, 3, 1, 2, 4, 5, 5, 6];
        int k = 4;

        int result = KthLargestSolver.FindKthLargestQuickselect(nums, k);

        Assert.Equal(4, result);
    }

    [Fact]
    public void KthLargest_MinHeap_MatchesQuickselectResult_AcrossMultipleInputs()
    {
        var testCases = new (int[] nums, int k)[]
        {
            ([1], 1),
            ([7, 10, 4, 3, 20, 15], 3),
            ([99, 99, 99, 100, 100], 1),
            ([99, 99, 99, 100, 100], 2),
            ([99, 99, 99, 100, 100], 5),
            ([-1, -5, -2, -8, 0], 2)
        };

        foreach (var (nums, k) in testCases)
        {
            int[] numsCopyForHeap = (int[])nums.Clone();
            int[] numsCopyForSelect = (int[])nums.Clone();

            int heapResult = KthLargestSolver.FindKthLargestMinHeap(numsCopyForHeap, k);
            int selectResult = KthLargestSolver.FindKthLargestQuickselect(numsCopyForSelect, k);

            Assert.Equal(heapResult, selectResult);
        }
    }
}
