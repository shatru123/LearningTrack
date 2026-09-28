# Day 07 - Senior .NET Interview Questions: Minimal APIs & Endpoint Routing

### Q1: What makes Minimal APIs more performant than MVC Controllers in ASP.NET Core?
- **Short answer**: Minimal APIs bypass the MVC action execution pipeline, eliminate per-request controller activations, and map directly to compiled request delegates.
- **Deeper answer**: MVC controllers require reflection-based discovery, controller factory instantiations, action descriptor cache lookups, model state validation dictionaries, and filter pipeline allocations on every request. Minimal APIs use compiled lambda expressions or source-generated request delegates, drastically reducing memory allocations and instruction counts on the request hot path.
- **Practical example**: A Minimal API endpoint can achieve $15–30\%$ higher throughput (requests per second) and lower memory footprint compared to the equivalent MVC controller action.

---

### Q2: What is the difference between `Results` and `TypedResults` in Minimal APIs?
- **Short answer**: `Results` returns `IResult` (untyped); `TypedResults` returns concrete types (e.g. `Ok<T>`, `Created<T>`), enabling compile-time return type verification and automatic OpenAPI response schema generation.
- **Deeper answer**: `Results.Ok(data)` returns an object implementing `IResult`, forcing Swagger/OpenAPI generators to guess the response schema or require `[ProducesResponseType]` annotations. `TypedResults.Ok(data)` returns `Ok<T>`, which enables the endpoint to return `Results<Ok<T>, NotFound>` union types. Unit tests can assert directly on `result.Result.Value` without running an HTTP server.
- **Practical example**:
```csharp
Results<Ok<UserDto>, NotFound> GetUser(int id) => ...
```

---

### Q3: How does `IEndpointFilter` differ from MVC Action Filters?
- **Short answer**: `IEndpointFilter` applies to individual endpoints or route groups (`MapGroup`), uses modern `ValueTask<object?>`, and works directly with `EndpointFilterInvocationContext` rather than `ActionExecutingContext`.
- **Deeper answer**: MVC action filters are tied to the MVC middleware and controller pipeline. `IEndpointFilter` is part of core routing (`Microsoft.AspNetCore.Http`), meaning it executes closer to the endpoint without loading the MVC framework. It can be attached to route groups (`group.AddEndpointFilter<T>()`), applying uniformly to all child routes.
- **Practical example**: Creating a global validation filter that intercepts all POST/PUT routes in a route group.

---

### Q4: How does `MapGroup` improve code architecture in large Minimal API projects?
- **Short answer**: It allows organizing related endpoints into modular prefixes, sharing common middleware filters, authorization policies, and rate-limiting rules.
- **Deeper answer**: In enterprise applications with hundreds of endpoints, putting everything in `Program.cs` creates unmaintainable monolithic files. `MapGroup` allows dividing routes into feature modules (e.g., `OrderEndpoints.Map(app.MapGroup("/api/orders"))`), attaching authorization (`.RequireAuthorization("Admin")`) and tags (`.WithTags("Orders")`) once to the entire group.
- **Practical example**:
```csharp
var group = app.MapGroup("/api/v1/orders").RequireAuthorization().AddEndpointFilter<AuditFilter>();
```

---

### Q5: How do Minimal APIs support Native AOT (Ahead-Of-Time) compilation?
- **Short answer**: By using the C# Roslyn `RequestDelegateGenerator` (RDG) source generator introduced in .NET 8 to generate parameter binding and routing code at compile time rather than using runtime reflection.
- **Deeper answer**: Traditional MVC relies heavily on runtime reflection and dynamic code generation (`Emit`) for model binding and controller activation, which is incompatible with Native AOT's closed-world assumption. The RequestDelegateGenerator emits C# source code during compilation for every `MapGet`/`MapPost`, allowing instantaneous startup and zero-JIT memory footprints.
- **Practical example**: Compiling an ASP.NET Core Minimal API into a self-contained single-file 15MB binary that boots in under 10 milliseconds.

---

### Q6: How does parameter binding work in Minimal APIs without `[FromBody]`, `[FromQuery]`, etc.?
- **Short answer**: Minimal APIs use convention-based heuristics: route parameters match route templates, services registered in DI are injected, complex types are bound from JSON body, and primitive types are bound from query strings.
- **Deeper answer**: The binding order is:
  1. Route values matching template `{id}`.
  2. Services from DI container (or `[FromKeyedServices]`).
  3. `HttpContext`, `HttpRequest`, `HttpResponse`, `ClaimsPrincipal`, `CancellationToken`.
  4. Special types (`IFormFile`, `IFormCollection`).
  5. Types implementing `TryParse` or `BindAsync` static methods.
  6. Complex record/class types bound from JSON request body.
- **Practical example**: A parameter `int id` matching `/events/{id}` binds to route; `IEventService service` binds to DI; `CreateEventRequest req` binds to JSON body automatically.

---

### Q7: What is the `BindAsync` and `TryParse` pattern in Minimal APIs?
- **Short answer**: Custom parameter binding mechanisms where a type defines a public static `BindAsync` or `TryParse` method to parse itself from the HTTP context or route string.
- **Deeper answer**: Instead of writing custom model binders in MVC, a record or class can implement `public static ValueTask<T?> BindAsync(HttpContext context)` to inspect headers, cookies, or queries, or `public static bool TryParse(string? value, out T result)`. This makes custom binding logic self-contained within the domain/DTO type.
- **Practical example**:
```csharp
public record PagingParameters(int Page, int Size) {
    public static ValueTask<PagingParameters> BindAsync(HttpContext context) { ... }
}
```

---

### Q8: When would you still choose MVC Controllers over Minimal APIs?
- **Short answer**: When maintaining legacy applications, when rendering server-side Razor views (`ViewResult`), or when utilizing complex legacy third-party MVC filter libraries.
- **Deeper answer**: For pure REST/HTTP microservices in modern .NET, Minimal APIs are Microsoft's recommended default. Controllers are primarily necessary if you are building server-rendered UI (Razor Pages / MVC Views with `TagHelpers`) or if an existing enterprise codebase has extensive custom action filter hierarchies that would be costly to rewrite as `IEndpointFilter`.
- **Practical example**: A hybrid application serving both HTML views and REST API endpoints.

---

### Q9: How do you unit test Minimal API endpoint handlers in isolation?
- **Short answer**: By extracting the handler into a named method or static delegate returning `TypedResults`, or by using `WebApplicationFactory<Program>` for integration testing.
- **Deeper answer**: Because `TypedResults.Ok(data)` returns a concrete `Ok<T>` type, you can unit-test the handler method directly without spinning up any test server: `var result = EventEndpoints.GetById(1001, mockService.Object); Assert.Equal(1001, result.Result.Value.Id);`.
- **Practical example**:
```csharp
[Fact]
public void Handler_Returns_Ok_With_Value() {
    var result = (Ok<EventResponse>)MyHandler(1, mockService.Object);
    Assert.Equal(1, result.Value.Id);
}
```

---

### Q10: How does `EndpointMetadata` work in Minimal APIs?
- **Short answer**: Metadata provides information about the endpoint (authorization, OpenAPI specs, CORS, filters) attached to the route's `Endpoint` object.
- **Deeper answer**: Methods like `.RequireAuthorization()`, `.WithTags()`, `.Produces<T>()`, and `.WithSummary()` append metadata objects to the endpoint's `EndpointBuilder.Metadata` collection. Downstream middleware (such as Authorization or OpenAPI generators) inspect this metadata to enforce rules or generate API documentation.
- **Practical example**: `app.MapGet("/secure", ...).RequireAuthorization("AdminPolicy");` attaches an `AuthorizeData` instance to the endpoint's metadata collection.
