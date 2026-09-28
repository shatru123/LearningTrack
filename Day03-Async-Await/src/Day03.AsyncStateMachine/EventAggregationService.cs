namespace Day03.AsyncStateMachine;

public record DiscoveredEvent(int EventId, string Title);
public record CatalogDetails(int EventId, string Venue, string Performer);
public record AvailabilityInfo(int EventId, int RemainingTickets, decimal CurrentPrice);
public record CustomerProfile(int CustomerId, string Tier, decimal DiscountRate);

public record AggregatedEventPackage(
    DiscoveredEvent Event,
    CatalogDetails Catalog,
    AvailabilityInfo Availability,
    CustomerProfile Customer,
    long ElapsedMilliseconds);

public class SimulatedApiException : Exception
{
    public string ServiceName { get; }

    public SimulatedApiException(string serviceName, string message) : base($"[{serviceName}] {message}")
    {
        ServiceName = serviceName;
    }
}

public interface IEventDependencies
{
    Task<DiscoveredEvent> GetDiscoveryAsync(int eventId, CancellationToken ct = default);
    Task<CatalogDetails> GetCatalogAsync(int eventId, CancellationToken ct = default);
    Task<AvailabilityInfo> GetAvailabilityAsync(int eventId, CancellationToken ct = default);
    Task<CustomerProfile> GetCustomerProfileAsync(int customerId, CancellationToken ct = default);
    ValueTask<AvailabilityInfo> GetCachedAvailabilityAsync(int eventId);
}

public class MockEventDependencies : IEventDependencies
{
    private readonly int _baseDelayMs;
    private readonly Dictionary<int, AvailabilityInfo> _cache = new();

    public MockEventDependencies(int baseDelayMs = 15)
    {
        _baseDelayMs = baseDelayMs;
        _cache[1001] = new AvailabilityInfo(1001, 45, 120m);
    }

    public async Task<DiscoveredEvent> GetDiscoveryAsync(int eventId, CancellationToken ct = default)
    {
        await Task.Delay(_baseDelayMs, ct).ConfigureAwait(false);
        return new DiscoveredEvent(eventId, "Arsenal vs Chelsea");
    }

    public async Task<CatalogDetails> GetCatalogAsync(int eventId, CancellationToken ct = default)
    {
        await Task.Delay(_baseDelayMs + 5, ct).ConfigureAwait(false);
        return new CatalogDetails(eventId, "Emirates Stadium", "Premier League");
    }

    public async Task<AvailabilityInfo> GetAvailabilityAsync(int eventId, CancellationToken ct = default)
    {
        await Task.Delay(_baseDelayMs + 10, ct).ConfigureAwait(false);
        return new AvailabilityInfo(eventId, 42, 150m);
    }

    public async Task<CustomerProfile> GetCustomerProfileAsync(int customerId, CancellationToken ct = default)
    {
        await Task.Delay(_baseDelayMs, ct).ConfigureAwait(false);
        return new CustomerProfile(customerId, "Gold", 0.15m);
    }

    // Demonstrates ValueTask: Synchronous fast-path avoids heap allocation of Task<T>
    public ValueTask<AvailabilityInfo> GetCachedAvailabilityAsync(int eventId)
    {
        if (_cache.TryGetValue(eventId, out var cached))
        {
            return new ValueTask<AvailabilityInfo>(cached); // 0 bytes allocated
        }

        return new ValueTask<AvailabilityInfo>(GetAvailabilityAsync(eventId));
    }
}

public class EventAggregationService
{
    private readonly IEventDependencies _dependencies;

    public EventAggregationService(IEventDependencies dependencies)
    {
        _dependencies = dependencies;
    }

    /// <summary>
    /// Version 1: Sequential Execution.
    /// Each API is awaited one after another.
    /// Latency = Sum(Delays).
    /// </summary>
    public async Task<AggregatedEventPackage> AggregateSequentialAsync(int eventId, int customerId)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        var discovery = await _dependencies.GetDiscoveryAsync(eventId).ConfigureAwait(false);
        var catalog = await _dependencies.GetCatalogAsync(eventId).ConfigureAwait(false);
        var availability = await _dependencies.GetAvailabilityAsync(eventId).ConfigureAwait(false);
        var customer = await _dependencies.GetCustomerProfileAsync(customerId).ConfigureAwait(false);

        sw.Stop();
        return new AggregatedEventPackage(discovery, catalog, availability, customer, sw.ElapsedMilliseconds);
    }

    /// <summary>
    /// Version 2: Parallel Execution with Task.WhenAll.
    /// All APIs run concurrently on ThreadPool workers.
    /// Latency = Max(Delays).
    /// </summary>
    public async Task<AggregatedEventPackage> AggregateParallelAsync(int eventId, int customerId)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        var discoveryTask = _dependencies.GetDiscoveryAsync(eventId);
        var catalogTask = _dependencies.GetCatalogAsync(eventId);
        var availabilityTask = _dependencies.GetAvailabilityAsync(eventId);
        var customerTask = _dependencies.GetCustomerProfileAsync(customerId);

        await Task.WhenAll(discoveryTask, catalogTask, availabilityTask, customerTask).ConfigureAwait(false);

        sw.Stop();
        return new AggregatedEventPackage(
            discoveryTask.Result,
            catalogTask.Result,
            availabilityTask.Result,
            customerTask.Result,
            sw.ElapsedMilliseconds);
    }

    /// <summary>
    /// Version 3: Parallel Execution with CancellationToken propagation.
    /// Cancels all pending tasks immediately when token signals.
    /// </summary>
    public async Task<AggregatedEventPackage> AggregateWithCancellationAsync(
        int eventId, int customerId, CancellationToken ct)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        var discoveryTask = _dependencies.GetDiscoveryAsync(eventId, ct);
        var catalogTask = _dependencies.GetCatalogAsync(eventId, ct);
        var availabilityTask = _dependencies.GetAvailabilityAsync(eventId, ct);
        var customerTask = _dependencies.GetCustomerProfileAsync(customerId, ct);

        await Task.WhenAll(discoveryTask, catalogTask, availabilityTask, customerTask).ConfigureAwait(false);

        sw.Stop();
        return new AggregatedEventPackage(
            discoveryTask.Result,
            catalogTask.Result,
            availabilityTask.Result,
            customerTask.Result,
            sw.ElapsedMilliseconds);
    }

    /// <summary>
    /// Version 4: Timeout Handling with Task.WaitAsync.
    /// Safely times out if downstream services exceed threshold.
    /// </summary>
    public async Task<AggregatedEventPackage> AggregateWithTimeoutAsync(
        int eventId, int customerId, TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout);
        return await AggregateWithCancellationAsync(eventId, customerId, cts.Token).ConfigureAwait(false);
    }

    /// <summary>
    /// Version 5: Multi-Failure Handling.
    /// Demonstrates why awaiting Task.WhenAll unrolls only the FIRST exception,
    /// and how to inspect allTasks.Exception to capture ALL failure reasons.
    /// </summary>
    public async Task<(AggregatedEventPackage? Package, List<Exception> Errors)> AggregateWithResilientFailuresAsync(
        Task<DiscoveredEvent> t1,
        Task<CatalogDetails> t2,
        Task<AvailabilityInfo> t3,
        Task<CustomerProfile> t4)
    {
        var allTasks = Task.WhenAll(t1, t2, t3, t4);
        var errors = new List<Exception>();

        try
        {
            await allTasks.ConfigureAwait(false);
            var package = new AggregatedEventPackage(t1.Result, t2.Result, t3.Result, t4.Result, 0);
            return (package, errors);
        }
        catch (Exception)
        {
            // IMPORTANT: 'await allTasks' only rethrows the FIRST exception encountered.
            // allTasks.Exception contains the complete AggregateException holding all inner exceptions!
            if (allTasks.Exception != null)
            {
                errors.AddRange(allTasks.Exception.InnerExceptions);
            }
            return (null, errors);
        }
    }
}
