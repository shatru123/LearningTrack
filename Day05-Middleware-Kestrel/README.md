# Day 05 - ASP.NET Core Custom Middleware & Kestrel Server Internals

## Objective
Build custom asynchronous middleware components using both convention-based (`RequestDelegate`) and factory-activated (`IMiddleware`) patterns, implement cross-cutting concerns (correlation tracking, RFC 7807 `ProblemDetails` exception handling, request logging), understand pipeline ordering, and dissect request flow from Kestrel to endpoints.

## Original Learning Tasks
1. Build custom async middleware with RequestDelegate & factory activation
2. DSA: Valid Palindrome, Two Sum II (Input Array Is Sorted)

## Practical Problem
In microservice architectures, unhandled exceptions that return raw HTML stack traces leak internal secrets, break frontend JSON parsers, and violate security standards. Furthermore, without a uniform correlation ID flowing across downstream services, debugging distributed transaction failures and performance bottlenecks in log aggregators (Elasticsearch, Datadog) is nearly impossible.

## What I Implemented
1. **CorrelationIdMiddleware**: Convention-based middleware using `RequestDelegate`. Extracts or generates `X-Correlation-ID`, attaches it to `context.TraceIdentifier` and outgoing HTTP headers.
2. **ExceptionHandlingMiddleware**: Global exception filter catching unhandled downstream exceptions and returning standardized RFC 7807 `application/problem+json` with HTTP 500 status and correlation IDs.
3. **RequestLoggingMiddleware**: Factory-activated middleware implementing `IMiddleware`, resolved from the DI container with scoped lifecycle support, measuring exact request duration.
4. **Program.cs API Pipeline**: Configured middleware ordering and endpoints (`/health`, `/events`, `/events/{id}`, `/failure`).
5. **WebApplicationFactory Integration Tests**: End-to-end HTTP tests validating correlation propagation, status codes, and JSON problem details.
6. **DSA Solutions**:
   - `Valid Palindrome`: Two-pointer string validation in $O(n)$ time and $O(1)$ space.
   - `Two Sum II`: Two-pointer search on sorted array in $O(n)$ time and $O(1)$ space.

## Architecture
```text
                      Incoming TCP Socket (OS Network Stack)
                                        │
                                        ▼
                              Kestrel Web Server
                      (Libuv / Socket Transport Layer)
                                        │
                         HttpContext Created & Pooled
                                        │
                                        ▼
    ┌──────────────────────── Middleware Pipeline ────────────────────────┐
    │                                                                     │
    │  1. CorrelationIdMiddleware (Sets X-Correlation-ID & TraceId)       │
    │                      │                                              │
    │                      ▼                                              │
    │  2. ExceptionHandlingMiddleware (try { await next() } catch)        │
    │                      │                                              │
    │                      ▼                                              │
    │  3. RequestLoggingMiddleware (IMiddleware factory activation)       │
    │                      │                                              │
    │                      ▼                                              │
    │  4. RoutingMiddleware (Matches URL to Endpoint Metadata)            │
    │                      │                                              │
    │                      ▼                                              │
    │  5. Endpoint Execution (MapGet delegate / Filters)                  │
    │                                                                     │
    └─────────────────────────────────────────────────────────────────────┘
```

## Key Concepts
- **Kestrel Web Server**: High-performance, cross-platform web server based on non-blocking asynchronous socket I/O (`System.IO.Pipelines`).
- **Convention-based Middleware**: Initialized once at application startup. Its constructor accepts `RequestDelegate next`. It cannot accept scoped services in its constructor (causes captive dependency), but can accept them in `InvokeAsync(HttpContext context, IScopedService scoped)`.
- **Factory-activated Middleware (`IMiddleware`)**: Implements `IMiddleware` interface. Registered in DI (Transient or Scoped) and resolved by `IMiddlewareFactory` per request. Enables constructor injection of scoped dependencies.
- **Middleware Ordering**: Order matters fundamentally! Exception handling middleware must be registered before logging and endpoint execution so that it surrounds downstream execution in its `try/catch` block.

## Testing
- Automated integration test suite (`MiddlewarePipelineTests`, `DsaExercisesTests`) using `WebApplicationFactory<Program>`.
- Verifies generation and propagation of correlation IDs.
- Validates exception interception and RFC 7807 `ProblemDetails` output on `/failure`.
- Verifies two-pointer palindrome and sorted two-sum algorithms.

## Performance Observations
- Using convention-based middleware for singleton operations avoids per-request DI resolution overhead.
- `HttpContext` is pooled by Kestrel; storing references to `HttpContext` beyond the request lifetime leads to data corruption across requests.

## Production Relevance
- Standardized error contracts (`ProblemDetails`) prevent breaking API gateways and mobile clients. Correlation IDs are the foundation of distributed tracing across microservices (OpenTelemetry, W3C Trace Context).

## Common Mistakes
1. Registering exception handling middleware at the *end* of the pipeline (catches nothing!).
2. Injecting a Scoped service into the *constructor* of convention-based middleware (becomes a captive dependency!).
3. Calling `await next(context)` multiple times or modifying response headers after `await next(context)` when headers have already been sent (`Response.HasStarted == true`).
4. Writing raw exception stack traces to client responses in production.

## Senior Interview Questions
See [interview-questions.md](file:///Users/shatrughnaambhore/Shatru/Learning/Projects/LearningTrack/Day05-Middleware-Kestrel/interview-questions.md) for 10 in-depth architectural questions.

## What I Practically Understood
- The fundamental difference between convention-based and `IMiddleware` factory activation in ASP.NET Core.
- Why modifying response headers after `next(context)` crashes if response streaming has started.
- How Kestrel passes requests through the `RequestDelegate` chain via Russian-doll wrapping.

## What I Need to Read Later
- HTTP/3 and QUIC transport support in Kestrel.
- Rate limiting middleware (`System.Threading.RateLimiting`) in ASP.NET Core.

## Key Takeaways
ASP.NET Core's middleware pipeline is an asynchronous chain of responsibility. Mastering middleware ordering, lifetime activation, and exception isolation is essential for building production-grade microservices.
