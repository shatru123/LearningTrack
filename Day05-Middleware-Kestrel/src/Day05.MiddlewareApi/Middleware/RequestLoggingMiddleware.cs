using System.Diagnostics;

namespace Day05.MiddlewareApi.Middleware;

/// <summary>
/// Factory-activated middleware implementing IMiddleware.
/// Unlike convention-based middleware (which is instantiated once at startup),
/// factory middleware is resolved from the DI container (typically Transient or Scoped),
/// enabling scoped dependency injection directly into the middleware class.
/// </summary>
public class RequestLoggingMiddleware : IMiddleware
{
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(ILogger<RequestLoggingMiddleware> logger)
    {
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        var sw = Stopwatch.StartNew();
        string method = context.Request.Method;
        string path = context.Request.Path;
        string traceId = context.TraceIdentifier;

        try
        {
            await next(context);
        }
        finally
        {
            sw.Stop();
            int statusCode = context.Response.StatusCode;
            long elapsedMs = sw.ElapsedMilliseconds;

            _logger.LogInformation(
                "HTTP {Method} {Path} responded {StatusCode} in {ElapsedMs}ms [TraceId: {TraceId}]",
                method, path, statusCode, elapsedMs, traceId);
        }
    }
}
