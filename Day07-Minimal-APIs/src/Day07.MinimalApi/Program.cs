using Day07.MinimalApi;
using Day07.MinimalApi.Filters;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

// Register dependencies
builder.Services.AddSingleton<IEventManager, InMemoryEventManager>();
builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();

// Endpoint Routing with MapGroup
var eventsGroup = app.MapGroup("/api/events")
    .AddEndpointFilter<ExecutionTimingFilter>();

// GET /api/events?status={status}
eventsGroup.MapGet("/", (string? status, IEventManager manager) =>
{
    var list = manager.GetAll(status).ToList();
    return TypedResults.Ok(list);
})
.WithName("GetAllEvents")
.WithSummary("Retrieve all events with optional status filter");

// GET /api/events/{id}
eventsGroup.MapGet("/{id:int}", Results<Ok<EventResponse>, NotFound<ProblemDetails>> (int id, IEventManager manager) =>
{
    var evt = manager.GetById(id);
    if (evt == null)
    {
        return TypedResults.NotFound(new ProblemDetails
        {
            Status = StatusCodes.Status404NotFound,
            Title = "Event Not Found",
            Detail = $"Event with ID {id} does not exist."
        });
    }

    return TypedResults.Ok(evt);
})
.WithName("GetEventById");

// POST /api/events
eventsGroup.MapPost("/", Results<Created<EventResponse>, ProblemHttpResult> (CreateEventRequest request, IEventManager manager) =>
{
    var created = manager.Create(request);
    return TypedResults.Created($"/api/events/{created.Id}", created);
})
.AddEndpointFilter<ValidationFilter<CreateEventRequest>>()
.WithName("CreateEvent");

// PUT /api/events/{id}
eventsGroup.MapPut("/{id:int}", Results<Ok<EventResponse>, NotFound<ProblemDetails>, ProblemHttpResult> (
    int id, UpdateEventRequest request, IEventManager manager) =>
{
    var updated = manager.Update(id, request);
    if (updated == null)
    {
        return TypedResults.NotFound(new ProblemDetails
        {
            Status = StatusCodes.Status404NotFound,
            Title = "Event Not Found",
            Detail = $"Cannot update non-existent event {id}."
        });
    }

    return TypedResults.Ok(updated);
})
.AddEndpointFilter<ValidationFilter<UpdateEventRequest>>()
.WithName("UpdateEvent");

// DELETE /api/events/{id}
eventsGroup.MapDelete("/{id:int}", Results<NoContent, NotFound<ProblemDetails>> (int id, IEventManager manager) =>
{
    bool deleted = manager.Delete(id);
    if (!deleted)
    {
        return TypedResults.NotFound(new ProblemDetails
        {
            Status = StatusCodes.Status404NotFound,
            Title = "Event Not Found",
            Detail = $"Cannot delete non-existent event {id}."
        });
    }

    return TypedResults.NoContent();
})
.WithName("DeleteEvent");

app.Run();

// Required for WebApplicationFactory<Program> in integration tests
public partial class Program { }
