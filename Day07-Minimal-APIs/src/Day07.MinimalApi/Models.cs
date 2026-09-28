using System.Collections.Concurrent;

namespace Day07.MinimalApi;

public record Event(int Id, string Name, string Venue, decimal Price, string Status);

public record CreateEventRequest(string Name, string Venue, decimal Price, string Status)
{
    public (bool IsValid, string? ErrorMessage) Validate()
    {
        if (string.IsNullOrWhiteSpace(Name)) return (false, "Name is required.");
        if (string.IsNullOrWhiteSpace(Venue)) return (false, "Venue is required.");
        if (Price <= 0) return (false, "Price must be greater than zero.");
        if (string.IsNullOrWhiteSpace(Status)) return (false, "Status is required.");
        return (true, null);
    }
}

public record UpdateEventRequest(string Name, string Venue, decimal Price, string Status)
{
    public (bool IsValid, string? ErrorMessage) Validate()
    {
        if (string.IsNullOrWhiteSpace(Name)) return (false, "Name is required.");
        if (string.IsNullOrWhiteSpace(Venue)) return (false, "Venue is required.");
        if (Price <= 0) return (false, "Price must be greater than zero.");
        if (string.IsNullOrWhiteSpace(Status)) return (false, "Status is required.");
        return (true, null);
    }
}

public record EventResponse(int Id, string Name, string Venue, decimal Price, string Status);

public interface IEventManager
{
    IEnumerable<EventResponse> GetAll(string? status = null);
    EventResponse? GetById(int id);
    EventResponse Create(CreateEventRequest request);
    EventResponse? Update(int id, UpdateEventRequest request);
    bool Delete(int id);
}

public class InMemoryEventManager : IEventManager
{
    private readonly ConcurrentDictionary<int, Event> _store = new();
    private int _idSequence = 1000;

    public InMemoryEventManager()
    {
        Create(new CreateEventRequest("Arsenal vs Chelsea", "Emirates Stadium", 1500m, "Available"));
        Create(new CreateEventRequest("Liverpool vs Man City", "Anfield", 1800m, "SoldOut"));
    }

    public IEnumerable<EventResponse> GetAll(string? status = null)
    {
        var query = _store.Values.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(e => e.Status.Equals(status, StringComparison.OrdinalIgnoreCase));
        }

        return query.Select(e => new EventResponse(e.Id, e.Name, e.Venue, e.Price, e.Status));
    }

    public EventResponse? GetById(int id)
    {
        if (_store.TryGetValue(id, out var evt))
        {
            return new EventResponse(evt.Id, evt.Name, evt.Venue, evt.Price, evt.Status);
        }
        return null;
    }

    public EventResponse Create(CreateEventRequest request)
    {
        int id = Interlocked.Increment(ref _idSequence);
        var evt = new Event(id, request.Name, request.Venue, request.Price, request.Status);
        _store[id] = evt;
        return new EventResponse(evt.Id, evt.Name, evt.Venue, evt.Price, evt.Status);
    }

    public EventResponse? Update(int id, UpdateEventRequest request)
    {
        if (!_store.ContainsKey(id)) return null;

        var updated = new Event(id, request.Name, request.Venue, request.Price, request.Status);
        _store[id] = updated;
        return new EventResponse(updated.Id, updated.Name, updated.Venue, updated.Price, updated.Status);
    }

    public bool Delete(int id)
    {
        return _store.TryRemove(id, out _);
    }
}
