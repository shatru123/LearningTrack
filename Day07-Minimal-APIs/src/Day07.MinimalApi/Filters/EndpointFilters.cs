using System.Diagnostics;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Day07.MinimalApi.Filters;

/// <summary>
/// Reusable IEndpointFilter for validating incoming DTOs.
/// Short-circuits the pipeline with a 400 ProblemDetails before invoking the endpoint handler.
/// </summary>
public class ValidationFilter<T> : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var argument = context.Arguments.OfType<T>().FirstOrDefault();
        if (argument == null)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid Request Payload",
                detail: $"Expected payload of type {typeof(T).Name} was not found.");
        }

        if (argument is CreateEventRequest createReq)
        {
            var (isValid, error) = createReq.Validate();
            if (!isValid)
            {
                return TypedResults.Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Validation Failed",
                    detail: error);
            }
        }
        else if (argument is UpdateEventRequest updateReq)
        {
            var (isValid, error) = updateReq.Validate();
            if (!isValid)
            {
                return TypedResults.Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Validation Failed",
                    detail: error);
            }
        }

        return await next(context);
    }
}

/// <summary>
/// IEndpointFilter demonstrating cross-cutting request timing at the endpoint level.
/// </summary>
public class ExecutionTimingFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var sw = Stopwatch.StartNew();

        var result = await next(context);

        sw.Stop();
        context.HttpContext.Response.Headers["X-Endpoint-Time-Ms"] = sw.ElapsedMilliseconds.ToString();

        return result;
    }
}
