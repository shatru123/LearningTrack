using Day05.MiddlewareApi.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Register factory-activated middleware in DI
builder.Services.AddTransient<RequestLoggingMiddleware>();

var app = builder.Build();

// Middleware Pipeline Ordering:
// 1. Correlation ID (establishes request identity)
app.UseMiddleware<CorrelationIdMiddleware>();

// 2. Exception Handling (catches unhandled exceptions from downstream)
app.UseMiddleware<ExceptionHandlingMiddleware>();

// 3. Request Logging (factory activated via IMiddleware)
app.UseMiddleware<RequestLoggingMiddleware>();

// Endpoints
app.MapGet("/health", () => Results.Ok(new { status = "Healthy", timestampUtc = DateTime.UtcNow }));

app.MapGet("/events", () => Results.Ok(new[]
{
    new { id = 1001, name = "Arsenal vs Chelsea", venue = "Emirates Stadium", price = 1500m },
    new { id = 1002, name = "Liverpool vs Man City", venue = "Anfield", price = 1800m }
}));

app.MapGet("/events/{id:int}", (int id) =>
{
    if (id == 1001)
    {
        return Results.Ok(new { id = 1001, name = "Arsenal vs Chelsea", venue = "Emirates Stadium", price = 1500m });
    }
    return Results.NotFound(new { message = $"Event {id} not found" });
});

app.MapGet("/failure", () =>
{
    throw new InvalidOperationException("Simulated catastrophic failure in pipeline.");
});

app.Run();

// Required for WebApplicationFactory<Program> integration testing in .NET 8
public partial class Program { }
