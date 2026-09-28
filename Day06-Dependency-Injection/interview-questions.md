# Day 06 - Senior .NET Interview Questions: Dependency Injection & Lifetimes

### Q1: What is a Captive Dependency in .NET and how does it manifest in production?
- **Short answer**: A captive dependency occurs when a service with a longer lifetime (e.g. Singleton) injects a service with a shorter lifetime (e.g. Scoped).
- **Deeper answer**: Because the singleton survives for the lifetime of the application, the scoped dependency it holds is never disposed and is effectively promoted to a singleton. If that scoped dependency is stateful or non-thread-safe (such as EF Core `DbContext`), concurrent requests access the same instance simultaneously, resulting in multi-threading crashes, stale data, and memory leaks.
- **Practical example**: Injecting `DbContext` into a `BackgroundService` constructor causes `InvalidOperationException: A second operation was started on this context instance before a previous operation completed`.

---

### Q2: How do `ValidateScopes` and `ValidateOnBuild` differ in `ServiceProviderOptions`?
- **Short answer**: `ValidateScopes` checks that singletons do not directly or indirectly consume scoped services. `ValidateOnBuild` verifies that all registered services can be created at container build time.
- **Deeper answer**: `ValidateScopes = true` intercepts service resolution calls (or build-time call sites) and throws an exception if a scoped service is requested from the root provider or from a singleton. `ValidateOnBuild = true` eagerly iterates through all registered service descriptors at startup, verifying that all constructors can be resolved, detecting circular dependencies or missing registrations before the first request arrives.
- **Practical example**:
```csharp
var provider = services.BuildServiceProvider(new ServiceProviderOptions {
    ValidateScopes = true,
    ValidateOnBuild = true
});
```

---

### Q3: Why is `IServiceProvider` considered a Service Locator anti-pattern when injected into business logic?
- **Short answer**: It hides class dependencies, makes unit testing difficult, and bypasses compile-time dependency verification.
- **Deeper answer**: When a class takes `IServiceProvider` and calls `provider.GetRequiredService<T>()` internally, consumers cannot determine what dependencies the class actually requires without reading its internal code. Constructor injection explicitly advertises dependencies in the class contract, facilitating easy mocking with Moq or NSubstitute.
- **Practical example**: Injected `IServiceProvider` makes `new OrderProcessor(provider)` opaque, whereas `new OrderProcessor(IRepository repo, IPaymentGateway payment)` is explicit and testable.

---

### Q4: When is Transient lifetime preferred over Scoped?
- **Short answer**: For small, lightweight, stateless helper services that do not need to share state within a single HTTP request.
- **Deeper answer**: Transient services are created every time they are requested. If multiple services within the same request need independent instances that do not interfere with each other or if the class is completely stateless (e.g. calculation utilities, builders), Transient avoids keeping references inside the request scope list until scope disposal.
- **Practical example**: Custom validation rules, mathematical processors, or lightweight formatting components.

---

### Q5: What happens to `IDisposable` and `IAsyncDisposable` instances created by the DI container?
- **Short answer**: The DI container retains a reference to every disposable instance it creates and disposes them when the owning scope is disposed.
- **Deeper answer**: If a Transient service implements `IDisposable`, the container tracks it in its internal disposal list. If hundreds of transient disposable objects are resolved within a long-running scope (or the root container), they are not garbage collected until that scope is disposed, causing unexpected memory retention.
- **Practical example**: Resolving thousands of transient `IDisposable` objects in a loop from a single scope causes memory accumulation until the loop finishes and the scope is disposed.

---

### Q6: How do you safely consume a Scoped service inside a Singleton or Hosted Service?
- **Short answer**: Inject `IServiceScopeFactory`, call `CreateScope()`, resolve the scoped service from `scope.ServiceProvider`, and dispose the scope when finished.
- **Deeper answer**: The scope factory creates a child `IServiceProvider` with its own isolated scope cache. When `scope.Dispose()` is called (typically via a `using` statement), all scoped services created within that scope are cleanly disposed, ensuring no memory leaks and zero cross-thread state sharing.
- **Practical example**:
```csharp
using var scope = _scopeFactory.CreateScope();
var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
await db.SaveChangesAsync();
```

---

### Q7: Can a Scoped service inject a Transient service? What about a Singleton injecting a Transient service?
- **Short answer**: A Scoped service can inject a Transient service safely. A Singleton injecting a Transient service will turn that Transient into a singleton!
- **Deeper answer**: When a Singleton injects a Transient service, that Transient is instantiated once when the Singleton is created and held for the application lifetime. If the Transient service assumed it would be created fresh every time or is not thread-safe, bugs will occur.
- **Practical example**: Injecting a non-thread-safe transient serializer into a singleton API gateway client.

---

### Q8: What are Keyed Services introduced in .NET 8?
- **Short answer**: A native feature in `Microsoft.Extensions.DependencyInjection` allowing registration and resolution of dependencies by key/name without third-party containers.
- **Deeper answer**: Prior to .NET 8, resolving different implementations of an interface (e.g., `AzureBlobStorage` vs `AmazonS3Storage` for `IStorageService`) required custom factories or libraries like Autofac. .NET 8 allows `services.AddKeyedSingleton<IStorageService, S3Storage>("s3")` and injection via `[FromKeyedServices("s3")] IStorageService storage`.
- **Practical example**:
```csharp
app.MapGet("/download", ([FromKeyedServices("s3")] IStorageService storage) => ...);
```

---

### Q9: How does ASP.NET Core manage the request scope lifetime?
- **Short answer**: Kestrel / the host creates a new `IServiceScope` via `IServiceScopeFactory` at the beginning of each HTTP request and disposes it at the end of the request.
- **Deeper answer**: In `HttpProtocol.ProcessRequestsAsync()`, the server creates a request scope and assigns `HttpContext.RequestServices = scope.ServiceProvider`. When the HTTP response pipeline completes and the response body is flushed, the scope is disposed, triggering `Dispose()` on all scoped services and any transient disposable services created during that request.
- **Practical example**: All controllers and minimal API handlers resolve dependencies directly from `HttpContext.RequestServices`.

---

### Q10: Why should you avoid resolving services from `app.Services` (Root Provider) in HTTP handlers?
- **Short answer**: Resolving scoped services from `app.Services` bypasses the HTTP request scope, which throws an exception if `ValidateScopes` is enabled, or resolves un-scoped instances that never get disposed.
- **Deeper answer**: `app.Services` is the root container. If you resolve a scoped service from the root container, it will either throw `InvalidOperationException` (if scope validation is on) or become rooted in the root container's disposal list, living forever and acting as an accidental singleton. Always resolve from `HttpContext.RequestServices`.
- **Practical example**: Use `context.RequestServices.GetRequiredService<T>()` instead of `builder.Services.BuildServiceProvider().GetRequiredService<T>()`.
