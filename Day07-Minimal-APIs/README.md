# Day 07 - ASP.NET Core Minimal APIs, Endpoint Routing, TypedResults & Filters

## Objective
Master .NET Minimal APIs, route groups (`MapGroup`), strongly typed results (`TypedResults`), cross-cutting validation and observability with `IEndpointFilter`, OpenAPI metadata enrichment, and contrast Minimal API internals against traditional MVC Controllers.

## Original Learning Tasks
1. Endpoint routing, IEndpointFilter, TypedResults, and validation
2. DSA: Best Time to Buy and Sell Stock

## Practical Problem
Traditional ASP.NET Core MVC Controllers incur significant overhead: reflection-based action discovery, heavy controller context allocations, model binding filters, and untyped `IActionResult` returns that obscure OpenAPI response schemas. In high-performance microservices, teams need lightweight routing with zero controller bloat, compile-time type safety via `TypedResults`, and composable endpoint filters.

## What I Implemented
1. **Event Minimal API**:
   - `MapGroup("/api/events")`: Route prefixing and unified filter application.
   - `GET /api/events`: Query string filtering (`?status=...`).
   - `GET /api/events/{id}`: Route parameter binding.
   - `POST /api/events`: JSON request body binding with automatic `201 Created` Location header.
   - `PUT /api/events/{id}`: Update operation returning strongly-typed `Results<Ok, NotFound, ProblemHttpResult>`.
   - `DELETE /api/events/{id}`: Returns `204 NoContent` or `404 NotFound`.
2. **IEndpointFilter Implementations**:
   - `ValidationFilter<T>`: Reusable filter intercepting incoming DTOs, validating constraints, and short-circuiting with RFC 7807 `ProblemDetails` on validation failure.
   - `ExecutionTimingFilter`: Attaches custom `X-Endpoint-Time-Ms` response header measuring handler execution.
3. **TypedResults Union Types**:
   - Strongly-typed compile-time result unions (`Results<Ok<T>, NotFound<ProblemDetails>, ProblemHttpResult>`) that automatically populate Swagger/OpenAPI response schemas without manual `[ProducesResponseType]` attributes.
4. **WebApplicationFactory Integration Tests**:
   - End-to-end integration tests validating CRUD lifecycles, filter short-circuiting, and response headers.
5. **DSA Solution**:
   - `MaxProfit`: Single-pass running minimum price and maximum profit tracker in $O(n)$ time and $O(1)$ space.

## Architecture
```text
                             HTTP Request (Kestrel)
                                       │
                                       ▼
                             Endpoint Routing Match
                                       │
                         Endpoint Filter Pipeline
                         ┌─────────────────────────────┐
                         │ 1. ExecutionTimingFilter    │
                         │             │               │
                         │             ▼               │
                         │ 2. ValidationFilter<T>      │
                         │   (Fails fast if invalid)   │
                         └─────────────┬───────────────┘
                                       │ (Valid)
                                       ▼
                              Endpoint Handler
                         (Func<..., IResult> Delegate)
                                       │
                                       ▼
                              IEventManager (Service)
                                       │
                                       ▼
                       TypedResults (Strongly Typed IResult)
```

## Key Concepts
- **`MapGroup`**: Enables modular endpoint organization, shared route prefixes, shared authorization policies, and shared endpoint filters across related route handlers.
- **`TypedResults` vs `Results`**: `Results.Ok(...)` returns `IResult` (boxing return type and requiring reflection for Swagger). `TypedResults.Ok(...)` returns concrete `Ok<T>`, enabling compile-time return type checking and automatic OpenAPI schema extraction.
- **`IEndpointFilter`**: Replaces MVC action filters in Minimal APIs. Can inspect handler arguments, short-circuit execution, and mutate responses before or after handler invocation.
- **Minimal APIs vs. Controllers**: Minimal APIs compile directly to `RequestDelegate` invocation graphs at startup, bypassing controller activation, action descriptor lookups, and model binding reflection trees.

## Testing
- Automated integration test suite (`MinimalApiTests`, `DsaExercisesTests`) using `WebApplicationFactory<Program>`.
- Validates full CRUD lifecycles (Create -> Read -> Update -> Delete -> Verify 404).
- Confirms validation filter short-circuiting on negative prices with `400 Bad Request`.
- Confirms timing filter header presence (`X-Endpoint-Time-Ms`).
- Validates stock profit calculation across varied market patterns.

## Performance Observations
- Minimal APIs eliminate controller activation allocations (`ControllerBase` instantiation, action filter arrays).
- `TypedResults` avoid boxing allocations associated with untyped `IActionResult`.

## Production Relevance
- Minimal APIs are the standard for high-performance .NET microservices, cloud-native serverless functions (AWS Lambda, Azure Functions), and containerized APIs where startup time, memory footprint, and RPS throughput are critical.

## Common Mistakes
1. Using untyped `Results.Ok()` instead of `TypedResults.Ok()` in unit-testable APIs.
2. Writing validation logic directly inside endpoint handler bodies rather than encapsulating it in reusable `IEndpointFilter` classes.
3. Forgetting route parameter constraints (e.g. `{id:int}`), leading to routing ambiguity.

## Senior Interview Questions
See [interview-questions.md](file:///Users/shatrughnaambhore/Shatru/Learning/Projects/LearningTrack/Day07-Minimal-APIs/interview-questions.md) for 10 in-depth architectural questions.

## What I Practically Understood
- How `IEndpointFilter` chains wrap endpoint delegate invocation.
- How `TypedResults` union types (`Results<T1, T2, ...>`) provide compile-time safety for multiple HTTP status codes.
- How `MapGroup` eliminates repetitive routing prefixes and middleware attachment.

## What I Need to Read Later
- Source-generated Minimal APIs (`Microsoft.AspNetCore.Http.RequestDelegateGenerator`) for Native AOT compilation.
- OpenApi document transformers and scalar API documentation in .NET 8 / 9.

## Key Takeaways
Minimal APIs are not just syntactically cleaner; they represent an architectural shift in ASP.NET Core toward lean, delegate-based, allocation-reduced endpoint routing that natively supports Native AOT.
