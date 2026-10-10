using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Day22.RateLimiter;

/// <summary>
/// ASP.NET Core Middleware that enforces distributed rate limits across HTTP endpoints.
/// Emits standard RFC 6585 rate limiting headers and returns RFC 7807 ProblemDetails on 429 rejections.
/// </summary>
public class DistributedRateLimiterMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IRateLimiter _limiter;
    private readonly Func<HttpContext, string> _clientKeyResolver;

    public DistributedRateLimiterMiddleware(
        RequestDelegate next,
        IRateLimiter limiter,
        Func<HttpContext, string>? clientKeyResolver = null)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _limiter = limiter ?? throw new ArgumentNullException(nameof(limiter));
        _clientKeyResolver = clientKeyResolver ?? DefaultKeyResolver;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var clientKey = _clientKeyResolver(context);
        var result = _limiter.ShouldAllow(clientKey);

        // Directly inject telemetry rate limit headers
        context.Response.Headers["X-RateLimit-Limit"] = result.Limit.ToString();
        context.Response.Headers["X-RateLimit-Remaining"] = result.Remaining.ToString();
        var resetEpoch = new DateTimeOffset(result.ResetTimeUtc).ToUnixTimeSeconds();
        context.Response.Headers["X-RateLimit-Reset"] = resetEpoch.ToString();

        if (!result.IsAllowed)
        {
            var retryAfterSec = Math.Max(1, (int)Math.Ceiling(result.RetryAfter.TotalSeconds));
            context.Response.Headers["Retry-After"] = retryAfterSec.ToString();
        }

        if (!result.IsAllowed)
        {
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            context.Response.ContentType = "application/problem+json";

            var problem = new
            {
                type = "https://tools.ietf.org/html/rfc6585#section-4",
                title = "Too Many Requests",
                status = 429,
                detail = $"Quota exceeded. Rate limit is {result.Limit} requests. Please retry in {Math.Ceiling(result.RetryAfter.TotalSeconds)} seconds.",
                instance = context.Request.Path.Value,
                retryAfterSeconds = Math.Max(1, (int)Math.Ceiling(result.RetryAfter.TotalSeconds))
            };

            var json = JsonSerializer.Serialize(problem);
            await context.Response.WriteAsync(json);
            return;
        }

        await _next(context);
    }

    public static string DefaultKeyResolver(HttpContext context)
    {
        // 1. Check API Key header
        if (context.Request.Headers.TryGetValue("X-Api-Key", out var apiKey) && !string.IsNullOrWhiteSpace(apiKey))
        {
            return $"api_key:{apiKey}";
        }

        // 2. Check Authorization header
        if (context.Request.Headers.TryGetValue("Authorization", out var authHeader) && !string.IsNullOrWhiteSpace(authHeader))
        {
            return $"auth:{authHeader}";
        }

        // 3. Fallback to client IP
        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown_client";
        return $"ip:{ip}";
    }
}
