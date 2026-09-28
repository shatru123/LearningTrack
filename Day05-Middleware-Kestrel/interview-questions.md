# Day 05 - Senior .NET Interview Questions: Middleware & Kestrel

### Q1: What is the architectural difference between convention-based and factory-activated (`IMiddleware`) middleware?
- **Short answer**: Convention-based middleware is instantiated once at startup (Singleton lifecycle); factory middleware (`IMiddleware`) is resolved from the DI container per request.
- **Deeper answer**: Convention-based middleware takes `RequestDelegate next` in its constructor and cannot have scoped dependencies injected into its constructor without causing captive dependencies. Factory middleware implements `IMiddleware`, takes `next` as a method argument in `InvokeAsync`, and can safely inject transient or scoped services directly into its constructor.
- **Practical example**: Injecting `DbContext` into the constructor of convention middleware creates a captive dependency; with `IMiddleware`, `DbContext` can be injected into the constructor safely.

---

### Q2: Why does middleware order matter in ASP.NET Core? Give a concrete example.
- **Short answer**: Middleware executes in the order it is registered in `Program.cs`. Surrounding concerns (exception handling, authentication) must wrap inner execution.
- **Deeper answer**: The middleware pipeline is a nested Russian-doll structure. If `ExceptionHandlingMiddleware` is registered *after* `Routing` or endpoint execution, any exception thrown in downstream endpoints will bypass the exception handler and bubble up directly to Kestrel, returning raw 500 errors to the client.
- **Practical example**:
```csharp
app.UseMiddleware<ExceptionHandlingMiddleware>(); // Catches all below
app.UseAuthentication();                         // Establishes ClaimsPrincipal
app.UseAuthorization();                          // Enforces policy
app.MapControllers();                            // Executes action
```

---

### Q3: What happens if you try to modify `context.Response.Headers` after calling `await next(context)`?
- **Short answer**: An `InvalidOperationException` is thrown: "Headers are read-only, response has already started."
- **Deeper answer**: Once the endpoint or downstream middleware starts writing bytes to the response body stream, Kestrel flushes the HTTP headers to the network socket (`Response.HasStarted = true`). After this point, no HTTP headers or status codes can be modified.
- **Practical example**: Always attach correlation IDs or security headers *before* calling `await _next(context)`.

---

### Q4: How does Kestrel achieve high throughput compared to traditional IIS / HttpListener?
- **Short answer**: Through non-blocking asynchronous socket I/O built on `System.IO.Pipelines`, zero-copy buffer pooling, and minimal abstraction overhead.
- **Deeper answer**: Kestrel avoids thread-per-request models. It uses OS-level epoll (Linux) or kqueue (macOS) / IOCP (Windows) via Libuv or managed sockets. Request reading and response writing happen over reusable pooled memory buffers (`MemoryPool<byte>`), virtually eliminating GC allocations on the request hot path.
- **Practical example**: Kestrel can process hundreds of thousands of plaintext requests per second per machine on standard commodity hardware.

---

### Q5: What is the RFC 7807 standard and why should production APIs adopt it?
- **Short answer**: RFC 7807 defines `ProblemDetails`, a standard machine-readable JSON structure for reporting HTTP API errors.
- **Deeper answer**: Without RFC 7807, APIs return arbitrary error structures (`{ "error": "msg" }` vs `{ "message": "msg" }` vs raw text), forcing API clients to write brittle parsing logic. ProblemDetails standardizes `type`, `title`, `status`, `detail`, `instance`, and custom extensions (like `traceId`).
- **Practical example**:
```json
{
  "status": 500,
  "title": "An unexpected server error occurred.",
  "detail": "Database connection timed out.",
  "traceId": "d7a71141-8ddb-4e17"
}
```

---

### Q6: Can you resolve a Scoped service inside a convention-based middleware? If yes, how?
- **Short answer**: Yes, by adding the Scoped service as a parameter to the `InvokeAsync` method rather than the constructor.
- **Deeper answer**: At runtime, ASP.NET Core inspects the parameters of `InvokeAsync` via reflection/compiled expressions and resolves them from the current request's `HttpContext.RequestServices` (the request scope).
- **Practical example**:
```csharp
public async Task InvokeAsync(HttpContext context, IOrderRepository scopedRepo) {
    // scopedRepo is correctly scoped to this specific HTTP request
    await _next(context);
}
```

---

### Q7: Why is storing `HttpContext` in a static field or background task hazardous?
- **Short answer**: `HttpContext` is not thread-safe and is pooled/recycled by Kestrel as soon as the request completes.
- **Deeper answer**: After the response finishes, Kestrel resets the `DefaultHttpContext` instance and returns it to the object pool. If a background task holds a reference to it, it will observe corrupted or completely different request data from a subsequent user's HTTP request, creating severe security vulnerabilities.
- **Practical example**: If a background fire-and-forget task needs request data, extract the specific values (e.g. `userId`, `correlationId`) into immutable local variables before firing the task.

---

### Q8: What is the difference between `EndpointRoutingMiddleware` and `EndpointMiddleware`?
- **Short answer**: `UseRouting()` matches the request URL to an endpoint and attaches metadata to `HttpContext`. `UseEndpoints()` executes the matched endpoint delegate.
- **Deeper answer**: Splitting routing into two phases allows middleware positioned between `UseRouting` and `UseEndpoints` (like `UseAuthentication`, `UseAuthorization`, and `UseCors`) to inspect endpoint metadata (e.g. `[Authorize]`, `[AllowAnonymous]`) before deciding whether to allow the request to proceed.
- **Practical example**: Authorization middleware checks `context.GetEndpoint()?.Metadata.GetMetadata<AuthorizeAttribute>()`.

---

### Q9: How does the Correlation ID pattern facilitate observability in microservices?
- **Short answer**: It generates a unique identifier at the API gateway and propagates it across all downstream HTTP, gRPC, and message queues to stitch distributed logs together.
- **Deeper answer**: In a distributed system, a single user click may trigger calls across 10 distinct microservices. By propagating `X-Correlation-ID` across HTTP headers and logging it with every log message (via Serilog/OpenTelemetry log scopes), engineers can query `correlationId: xyz` and view the entire causal chain of events.
- **Practical example**: Sending `X-Correlation-ID: 7a9b-4c2d` in outgoing `HttpClient` calls via a `DelegatingHandler`.

---

### Q10: How does ASP.NET Core short-circuit the middleware pipeline?
- **Short answer**: By not calling `await next(context)` and returning directly from `InvokeAsync`.
- **Deeper answer**: When a middleware decides the request cannot proceed (e.g., authentication failure, rate limit exceeded, static file found and served), it writes the response directly and returns without invoking `next(context)`. Execution immediately unwinds back up through the previous middleware layers.
- **Practical example**: Static file middleware returns the cached file and terminates the pipeline without routing or hitting endpoint handlers.
