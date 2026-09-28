namespace Day05.MiddlewareApi.Middleware;

/// <summary>
/// Convention-based middleware using RequestDelegate.
/// Inspects or creates 'X-Correlation-ID' header, sets context.TraceIdentifier,
/// and attaches header to outgoing HTTP response.
/// </summary>
public class CorrelationIdMiddleware
{
    public const string CorrelationHeaderName = "X-Correlation-ID";
    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        string correlationId;
        if (context.Request.Headers.TryGetValue(CorrelationHeaderName, out var existingCorrelationId) &&
            !string.IsNullOrWhiteSpace(existingCorrelationId))
        {
            correlationId = existingCorrelationId.ToString();
        }
        else
        {
            correlationId = Guid.NewGuid().ToString("N");
        }

        context.TraceIdentifier = correlationId;
        context.Response.Headers[CorrelationHeaderName] = correlationId;

        await _next(context);
    }
}
