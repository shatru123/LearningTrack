# Day 05 - Engineering Notes: Middleware Pipeline & Kestrel

## 1. Request Flow: From Socket to Endpoint

```text
OS Socket -> Kestrel Sockets/IO -> HttpContext -> Middleware Pipeline -> Endpoint
```
1. **Kestrel Transport Layer**: Uses `SocketTransport` and `System.IO.Pipelines` to read bytes from network sockets into pooled memory without blocking threads.
2. **HttpContext Allocation**: Kestrel allocates an `HttpContext` instance from an internal object pool (`DefaultHttpContext`).
3. **Pipeline Dispatch**: The pipeline is built at application startup by chaining delegates:
   `Func<RequestDelegate, RequestDelegate>`.
   The final delegate is invoked for each request.

---

## 2. Convention-Based vs IMiddleware (Factory-Activated)

### Convention-Based Middleware
- **Signature**: Class with `InvokeAsync(HttpContext context)` (or with scoped services as additional parameters).
- **Lifetime**: Created **once** at startup (effectively a Singleton).
- **Dependency Rule**: **NEVER** inject scoped services (like `DbContext`) into its constructor! Doing so makes the scoped service a captive dependency that never gets disposed.
- **Injecting Scoped Services**: Inject scoped services as method parameters in `InvokeAsync(HttpContext context, IScopedService scoped)`.

### Factory-Activated (`IMiddleware`)
- **Signature**: Implements `IMiddleware` interface: `InvokeAsync(HttpContext context, RequestDelegate next)`.
- **Lifetime**: Managed by the DI container (`services.AddTransient<MyMiddleware>()`).
- **Activation**: Resolved per-request by `IMiddlewareFactory`.
- **Benefit**: Scoped services can be safely injected directly into the constructor.

---

## 3. The `Response.HasStarted` Trap

When streaming data or sending headers to the client:
```csharp
await _next(context);
// WARNING: At this point, context.Response.HasStarted may be true!
// Calling context.Response.Headers.Add(...) will throw InvalidOperationException!
```
Any header modifications (like `X-Correlation-ID`) MUST happen **before** calling `await _next(context)`.

---

## 4. DSA Takeaways
- **Valid Palindrome**: Two-pointer convergence skipping non-alphanumeric characters validates palindromes in $O(n)$ time with $0$ extra heap allocation.
- **Two Sum II**: In a sorted array, if `arr[left] + arr[right] < target`, incrementing `left` increases the sum; if `> target`, decrementing `right` decreases the sum ($O(n)$ time, $O(1)$ space).
