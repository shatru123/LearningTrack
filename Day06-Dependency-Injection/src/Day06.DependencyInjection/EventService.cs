using Microsoft.Extensions.DependencyInjection;

namespace Day06.DependencyInjection;

public interface IDisposableTracker
{
    Guid InstanceId { get; }
    bool IsDisposed { get; }
}

public interface ITransientOperation : IDisposableTracker { }
public interface IScopedOperation : IDisposableTracker { }
public interface ISingletonOperation : IDisposableTracker { }

public class TransientOperation : ITransientOperation, IDisposable
{
    public Guid InstanceId { get; } = Guid.NewGuid();
    public bool IsDisposed { get; private set; }

    public void Dispose()
    {
        IsDisposed = true;
    }
}

public class ScopedOperation : IScopedOperation, IDisposable
{
    public Guid InstanceId { get; } = Guid.NewGuid();
    public bool IsDisposed { get; private set; }

    public void Dispose()
    {
        IsDisposed = true;
    }
}

public class SingletonOperation : ISingletonOperation, IDisposable
{
    public Guid InstanceId { get; } = Guid.NewGuid();
    public bool IsDisposed { get; private set; }

    public void Dispose()
    {
        IsDisposed = true;
    }
}

public record EventDto(int Id, string Title, decimal Price, int AvailableSeats);

public interface IEventRepository : IDisposableTracker
{
    Task<EventDto?> GetEventByIdAsync(int id);
}

public class EventRepository : IEventRepository, IDisposable
{
    public Guid InstanceId { get; } = Guid.NewGuid();
    public bool IsDisposed { get; private set; }

    public Task<EventDto?> GetEventByIdAsync(int id)
    {
        if (id == 1001)
            return Task.FromResult<EventDto?>(new EventDto(1001, "El Clasico", 250m, 120));
        return Task.FromResult<EventDto?>(null);
    }

    public void Dispose()
    {
        IsDisposed = true;
    }
}

public interface IAvailabilityClient
{
    Task<int> CheckRealtimeSeatsAsync(int eventId);
}

public class AvailabilityClient : IAvailabilityClient
{
    public Task<int> CheckRealtimeSeatsAsync(int eventId) => Task.FromResult(45);
}

public interface IEventService
{
    Task<EventDto?> GetEventAsync(int id);
}

public class EventService : IEventService
{
    private readonly IEventRepository _repository;
    private readonly IAvailabilityClient _availabilityClient;

    public EventService(IEventRepository repository, IAvailabilityClient availabilityClient)
    {
        _repository = repository;
        _availabilityClient = availabilityClient;
    }

    public async Task<EventDto?> GetEventAsync(int id)
    {
        var evt = await _repository.GetEventByIdAsync(id);
        if (evt == null) return null;

        int realtimeSeats = await _availabilityClient.CheckRealtimeSeatsAsync(id);
        return evt with { AvailableSeats = realtimeSeats };
    }
}

/// <summary>
/// Demonstrates the Captive Dependency Anti-Pattern:
/// A Singleton directly injecting a Scoped service in its constructor.
/// </summary>
public class BadSingletonWithCaptiveDependency
{
    public IScopedOperation CapturedScopedInstance { get; }

    public BadSingletonWithCaptiveDependency(IScopedOperation scoped)
    {
        CapturedScopedInstance = scoped;
    }
}

/// <summary>
/// Demonstrates the Correct Solution using IServiceScopeFactory:
/// The Singleton injects IServiceScopeFactory, creating an explicit scope on-demand
/// and safely disposing the scoped service when work finishes.
/// </summary>
public class GoodSingletonWithScopeFactory
{
    private readonly IServiceScopeFactory _scopeFactory;

    public GoodSingletonWithScopeFactory(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public Guid ExecuteInScopedContext()
    {
        using var scope = _scopeFactory.CreateScope();
        var scopedOp = scope.ServiceProvider.GetRequiredService<IScopedOperation>();
        return scopedOp.InstanceId;
    }
}
