using Day28.ResilientHttpRefitPolly;
using Polly.CircuitBreaker;
using Polly.Timeout;
using Xunit;

namespace Day28.ResilientHttpRefitPolly.Tests;

public class Day28Tests
{
    [Fact]
    public void RefitContract_ModelSerializationAndInvariants()
    {
        var inventoryDto = new InventoryItemDto("SKU-PRO-MAX", 45, 1299.99m, "WH-SEATTLE-01");
        Assert.Equal("SKU-PRO-MAX", inventoryDto.Sku);
        Assert.Equal(45, inventoryDto.AvailableStock);
        Assert.Equal(1299.99m, inventoryDto.UnitPrice);

        var reservationRequest = new StockReservationRequest("SKU-PRO-MAX", 2, "ORD-7788");
        Assert.Equal("ORD-7788", reservationRequest.OrderReference);

        var reservationResponse = new StockReservationResponse(Guid.NewGuid(), "SKU-PRO-MAX", 2, true, null);
        Assert.True(reservationResponse.IsSuccess);
        Assert.Null(reservationResponse.ErrorMessage);
    }

    [Fact]
    public async Task PollyPipeline_ExecutesSuccessfulCallWithoutRetries()
    {
        var pipeline = ResiliencePipelineFactory.CreateStandardHttpPipeline(maxRetries: 3);
        int executionCount = 0;

        var result = await pipeline.ExecuteAsync(async ct =>
        {
            executionCount++;
            await Task.Yield();
            return "SUCCESS";
        });

        Assert.Equal("SUCCESS", result);
        Assert.Equal(1, executionCount);
    }

    [Fact]
    public async Task PollyPipeline_RetriesOnTransientHttpExceptions_AndSucceeds()
    {
        var pipeline = ResiliencePipelineFactory.CreateStandardHttpPipeline(
            maxRetries: 3,
            initialDelay: TimeSpan.FromMilliseconds(5));

        int executionCount = 0;

        var result = await pipeline.ExecuteAsync(async ct =>
        {
            executionCount++;
            if (executionCount < 3)
            {
                throw new HttpRequestException("Transient network glitch (HTTP 503).");
            }
            await Task.Yield();
            return "RECOVERED";
        });

        Assert.Equal("RECOVERED", result);
        Assert.Equal(3, executionCount);
    }

    [Fact]
    public async Task PollyPipeline_ExhaustsRetries_WhenFailurePersists()
    {
        var pipeline = ResiliencePipelineFactory.CreateStandardHttpPipeline(
            maxRetries: 2,
            initialDelay: TimeSpan.FromMilliseconds(5));

        int executionCount = 0;

        await Assert.ThrowsAsync<HttpRequestException>(async () =>
        {
            await pipeline.ExecuteAsync(async ct =>
            {
                executionCount++;
                await Task.Yield();
                throw new HttpRequestException("Downstream server dead.");
            });
        });

        // Initial attempt + 2 retries = 3 total attempts
        Assert.Equal(3, executionCount);
    }

    [Fact]
    public async Task PollyPipeline_CircuitBreaker_OpensAfterThreshold()
    {
        var monitor = new CircuitBreakerStateMonitor();
        var pipeline = ResiliencePipelineFactory.CreateStandardHttpPipeline(
            monitor: monitor,
            maxRetries: 0, // Isolate circuit breaker from retry loops
            circuitBreakDuration: TimeSpan.FromSeconds(2));

        // Drive failures to breach minimum throughput (4) and failure ratio (0.5)
        for (int i = 0; i < 4; i++)
        {
            try
            {
                await pipeline.ExecuteAsync(async ct =>
                {
                    await Task.Yield();
                    throw new HttpRequestException("Consecutive hard failure");
                });
            }
            catch (HttpRequestException) { }
        }

        Assert.Equal(CircuitState.Open, monitor.CurrentState);
        Assert.True(monitor.OpenTransitionCount >= 1);

        // Immediate subsequent request must fail fast with BrokenCircuitException
        await Assert.ThrowsAsync<BrokenCircuitException>(async () =>
        {
            await pipeline.ExecuteAsync(async ct =>
            {
                await Task.Yield();
                return "Should not reach here";
            });
        });
    }

    [Fact]
    public async Task PollyPipeline_Timeout_AbortsSlowOperations()
    {
        var pipeline = ResiliencePipelineFactory.CreateStandardHttpPipeline(
            maxRetries: 0,
            attemptTimeout: TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAsync<TimeoutRejectedException>(async () =>
        {
            await pipeline.ExecuteAsync(async ct =>
            {
                await Task.Delay(500, ct);
                return "Never reached";
            });
        });
    }

    [Fact]
    public async Task PollyPipeline_Hedging_ExecutesSpeculativeParallelAttempt()
    {
        var hedgedPipeline = ResiliencePipelineFactory.CreateHedgedPipeline<string>(
            hedgingDelay: TimeSpan.FromMilliseconds(40),
            maxHedgedAttempts: 2);

        int attemptsStarted = 0;

        var result = await hedgedPipeline.ExecuteAsync(async ct =>
        {
            var attemptId = Interlocked.Increment(ref attemptsStarted);
            if (attemptId == 1)
            {
                // Primary attempt hangs
                await Task.Delay(300, ct);
                return "Primary Slow Result";
            }

            // Hedged secondary attempt responds fast
            await Task.Yield();
            return "Hedged Fast Result";
        });

        Assert.Equal("Hedged Fast Result", result);
        Assert.True(attemptsStarted >= 2);
    }

    [Fact]
    public void PacificAtlantic_DfsAndBfs_StandardContinent_CalculatesCorrectIntersection()
    {
        var solver = new PacificAtlanticSolver();

        // LeetCode #417 standard test grid:
        // Pacific (top & left), Atlantic (bottom & right)
        var heights = new int[][]
        {
            new[] { 1, 2, 2, 3, 5 },
            new[] { 3, 2, 3, 4, 4 },
            new[] { 2, 4, 5, 3, 1 },
            new[] { 6, 7, 1, 4, 5 },
            new[] { 5, 1, 1, 2, 4 }
        };

        var dfsResult = solver.PacificAtlanticDfs(heights);
        var bfsResult = solver.PacificAtlanticBfs(heights);

        // Expected intersection coordinates:
        // [0,4], [1,3], [1,4], [2,2], [3,0], [3,1], [4,0]
        Assert.Equal(7, dfsResult.Count);
        Assert.Equal(7, bfsResult.Count);

        // Verify specific known intersections
        Assert.Contains(dfsResult, coord => coord[0] == 0 && coord[1] == 4);
        Assert.Contains(dfsResult, coord => coord[0] == 2 && coord[1] == 2);
        Assert.Contains(dfsResult, coord => coord[0] == 4 && coord[1] == 0);

        Assert.Contains(bfsResult, coord => coord[0] == 0 && coord[1] == 4);
        Assert.Contains(bfsResult, coord => coord[0] == 2 && coord[1] == 2);
        Assert.Contains(bfsResult, coord => coord[0] == 4 && coord[1] == 0);
    }

    [Fact]
    public void PacificAtlantic_SingleCell_ReachesBothOceans()
    {
        var solver = new PacificAtlanticSolver();
        var heights = new int[][] { new[] { 10 } };

        var dfsResult = solver.PacificAtlanticDfs(heights);
        var bfsResult = solver.PacificAtlanticBfs(heights);

        Assert.Single(dfsResult);
        Assert.Equal(0, dfsResult[0][0]);
        Assert.Equal(0, dfsResult[0][1]);

        Assert.Single(bfsResult);
        Assert.Equal(0, bfsResult[0][0]);
        Assert.Equal(0, bfsResult[0][1]);
    }

    [Fact]
    public void PacificAtlantic_HandlesEdgeCases()
    {
        var solver = new PacificAtlanticSolver();

        Assert.Empty(solver.PacificAtlanticDfs(null));
        Assert.Empty(solver.PacificAtlanticBfs(null));
        Assert.Empty(solver.PacificAtlanticDfs(Array.Empty<int[]>()));
        Assert.Empty(solver.PacificAtlanticBfs(Array.Empty<int[]>()));

        // Flat plateau: all cells reach both oceans
        var flatHeights = new int[][]
        {
            new[] { 1, 1 },
            new[] { 1, 1 }
        };
        var flatDfs = solver.PacificAtlanticDfs(flatHeights);
        var flatBfs = solver.PacificAtlanticBfs(flatHeights);

        Assert.Equal(4, flatDfs.Count);
        Assert.Equal(4, flatBfs.Count);
    }
}
