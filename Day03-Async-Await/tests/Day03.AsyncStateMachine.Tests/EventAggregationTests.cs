using Xunit;
using Day03.AsyncStateMachine;

namespace Day03.AsyncStateMachine.Tests;

public class EventAggregationTests
{
    private readonly MockEventDependencies _dependencies = new(baseDelayMs: 10);

    [Fact]
    public async Task Sequential_ExecutesAllServices_ProducesValidPackage()
    {
        var service = new EventAggregationService(_dependencies);
        var package = await service.AggregateSequentialAsync(1001, 501);

        Assert.NotNull(package);
        Assert.Equal("Arsenal vs Chelsea", package.Event.Title);
        Assert.Equal("Emirates Stadium", package.Catalog.Venue);
        Assert.Equal(42, package.Availability.RemainingTickets);
        Assert.Equal("Gold", package.Customer.Tier);
        // Sequential latency should reflect cumulative delays
        Assert.True(package.ElapsedMilliseconds >= 30, $"Elapsed was {package.ElapsedMilliseconds}");
    }

    [Fact]
    public async Task Parallel_CompletesFasterThanSequential()
    {
        var service = new EventAggregationService(_dependencies);

        var sequentialPkg = await service.AggregateSequentialAsync(1001, 501);
        var parallelPkg = await service.AggregateParallelAsync(1001, 501);

        Assert.Equal(sequentialPkg.Event, parallelPkg.Event);
        Assert.Equal(sequentialPkg.Catalog, parallelPkg.Catalog);
        Assert.Equal(sequentialPkg.Availability, parallelPkg.Availability);
        Assert.Equal(sequentialPkg.Customer, parallelPkg.Customer);

        // Parallel duration should be roughly max delay rather than sum of delays
        Assert.True(parallelPkg.ElapsedMilliseconds <= sequentialPkg.ElapsedMilliseconds);
    }

    [Fact]
    public async Task CancellationToken_AbortsExecution_WhenCancelled()
    {
        var service = new EventAggregationService(_dependencies);
        using var cts = new CancellationTokenSource();
        cts.Cancel(); // Cancel immediately

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.AggregateWithCancellationAsync(1001, 501, cts.Token));
    }

    [Fact]
    public async Task Timeout_ThrowsOperationCanceledException_WhenExceeded()
    {
        var service = new EventAggregationService(_dependencies);

        // Sub-millisecond timeout that is guaranteed to expire before 10ms delays
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.AggregateWithTimeoutAsync(1001, 501, TimeSpan.FromMilliseconds(1)));
    }

    [Fact]
    public async Task FailureHandling_CapturesAllAggregatedExceptions()
    {
        var service = new EventAggregationService(_dependencies);

        var failingTask1 = Task.FromException<DiscoveredEvent>(new SimulatedApiException("Discovery", "Service Unavailable 503"));
        var failingTask2 = Task.FromException<CatalogDetails>(new SimulatedApiException("Catalog", "Gateway Timeout 504"));
        var okTask3 = Task.FromResult(new AvailabilityInfo(1001, 10, 50m));
        var okTask4 = Task.FromResult(new CustomerProfile(501, "Silver", 0.05m));

        var (package, errors) = await service.AggregateWithResilientFailuresAsync(failingTask1, failingTask2, okTask3, okTask4);

        Assert.Null(package);
        Assert.Equal(2, errors.Count);
        Assert.Contains(errors, e => e.Message.Contains("Discovery"));
        Assert.Contains(errors, e => e.Message.Contains("Catalog"));
    }

    [Fact]
    public async Task CachedAvailability_ValueTask_AvoidsTaskAllocation()
    {
        var valueTask = _dependencies.GetCachedAvailabilityAsync(1001);

        // Synchronous fast-path check
        Assert.True(valueTask.IsCompletedSuccessfully);

        var result = await valueTask;
        Assert.Equal(1001, result.EventId);
        Assert.Equal(45, result.RemainingTickets);
    }

    [Fact]
    public async Task ManualStateMachine_ExecutesCorrectly()
    {
        int result = await AsyncStateMachineDissection.ExecuteManualStateMachineAsync(50);
        Assert.Equal(100, result);
    }

    [Fact]
    public async Task TaskCompletionSource_AdaptsCallbackCorrectly()
    {
        Task<string> task = AsyncStateMachineDissection.FromLegacyCallbackAsync(callback =>
        {
            Task.Run(() => callback("SuccessPayload"));
        });

        string result = await task;
        Assert.Equal("SuccessPayload", result);
    }
}
