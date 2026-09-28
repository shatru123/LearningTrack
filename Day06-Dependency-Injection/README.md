# Day 06 - Dependency Injection Lifetimes & Captive Dependencies

## Objective
Deep dive into .NET Dependency Injection lifetimes (Transient, Scoped, Singleton), isolate the dangerous "Captive Dependency" anti-pattern, understand how `ValidateScopes` and `ValidateOnBuild` safeguard production applications, and implement safe scope creation using `IServiceScopeFactory`.

## Original Learning Tasks
1. Deep dive into Transient, Scoped, Singleton & captive dependencies
2. DSA: 3Sum, Container With Most Water

## Practical Problem
A common bug in modern .NET services occurs when a developer registers a background worker or caching service as a `Singleton` and inadvertently injects a `Scoped` service (such as an EF Core `DbContext` or tenant context) directly into its constructor. Because the Singleton never dies, the Scoped service is captured for the entire lifetime of the process. This causes:
1. `DbContext` concurrency crashes (`InvalidOperationException: A second operation was started on this context instance before a previous operation completed`).
2. Memory leaks as the change tracker retains entities indefinitely.
3. Cross-tenant or cross-user data leakage.

## What I Implemented
1. **Lifecycle Tracking**: Created `ITransientOperation`, `IScopedOperation`, and `ISingletonOperation` with `IDisposable` tracking, instance GUIDs, and disposal flags.
2. **EventService Architecture**:
   - `EventRepository`: Scoped data access service.
   - `AvailabilityClient`: Microservice communication client.
   - `EventService`: Scoped business logic service coordinating repository and client.
3. **Captive Dependency Demonstration**:
   - `BadSingletonWithCaptiveDependency`: Captures scoped dependency directly in its constructor.
   - Demonstrated how `ValidateScopes = true` and `ValidateOnBuild = true` catch this failure at application startup.
4. **Resolution via `IServiceScopeFactory`**:
   - `GoodSingletonWithScopeFactory`: Safely creates transient scopes on-demand (`using var scope = _scopeFactory.CreateScope()`), resolves the scoped service within that scope, and guarantees immediate disposal when finished.
5. **xUnit & Moq Test Suite**:
   - Validated lifetime instantiation and disposal guarantees.
   - Verified scope validation behavior under both build-time and runtime conditions.
   - Tested `EventService` logic using `Mock<IEventRepository>` and `Mock<IAvailabilityClient>`.
6. **DSA Solutions**:
   - `3Sum`: Sorted two-pointer solution running in $O(n^2)$ time with duplicate avoidance.
   - `Container With Most Water`: Two-pointer greedy narrowing algorithm running in $O(n)$ time and $O(1)$ space.

## Architecture
```text
                       IServiceCollection / ServiceProvider
                                        │
           ┌────────────────────────────┼────────────────────────────┐
           ▼                            ▼                            ▼
       Transient                      Scoped                     Singleton
    (Always New)               (One Per HTTP Request)       (One Per Application)
           │                            │                            │
   Instance per resolve         Disposed at end              Lives until shutdown
                                of HTTP scope                        │
                                        ▲                            │
                                        │ (NEVER inject directly!)   │
                                        └─────────── X ──────────────┘
                                          Captive Dependency Hazard!
                                                     │
                                                     ▼
                                      Solution: IServiceScopeFactory
                                      (Creates explicit short-lived scope)
```

## Key Concepts
- **Transient**: Created every time requested from the container. Best for lightweight, stateless services.
- **Scoped**: Created once per request scope. Disposed automatically when the scope ends. Mandatory for stateful per-request objects (e.g. `DbContext`, unit of work, current user).
- **Singleton**: Created once on initial resolution. Lives forever. Must be completely thread-safe.
- **Captive Dependency**: Occurs when a service with a longer lifetime injects a service with a shorter lifetime. The shorter-lived service is captured and kept alive for the longer lifetime.
- **`IServiceScopeFactory`**: Allows Singletons to manually generate Scoped containers to execute scoped operations safely without leaking.

## Testing
- Automated test suite (`DependencyInjectionTests`, `DsaExercisesTests`).
- Verifies instance uniqueness for Transient, scope-equality and disposal for Scoped, and global uniqueness for Singleton.
- Confirms that `ValidateScopes = true` throws `AggregateException` / `InvalidOperationException` preventing startup with captive dependencies.
- Confirms that `IServiceScopeFactory` produces isolated scopes with separate instances.
- Verifies `EventService` interactions with Moq.
- Validates 3Sum and Container With Most Water algorithms.

## Performance Observations
- Resolving large object graphs on every request can introduce allocation overhead; designing clean boundaries and caching singletons where thread-safe improves request throughput.
- Enabling `ValidateScopes` adds slight startup verification time but has zero overhead during request runtime.

## Production Relevance
- ASP.NET Core enables `ValidateScopes` and `ValidateOnBuild` by default in the `Development` environment, but turns them off in `Production` for startup performance. Senior engineers should run unit tests with `ValidateScopes = true` to catch captive dependencies in CI/CD pipelines before deployment.

## Common Mistakes
1. Injecting `DbContext` (Scoped) into a BackgroundService or hosted worker (Singleton) constructor.
2. Forgetting to dispose a manually created `IServiceScope` (`using var scope = factory.CreateScope()`), leaking all scoped instances created within it.
3. Writing stateful Singletons without synchronization (locks, thread-safe collections), leading to multi-threaded data races.

## Senior Interview Questions
See [interview-questions.md](file:///Users/shatrughnaambhore/Shatru/Learning/Projects/LearningTrack/Day06-Dependency-Injection/interview-questions.md) for 10 in-depth architectural questions.

## What I Practically Understood
- The exact mechanics of `ValidateScopes` and `ValidateOnBuild` in `Microsoft.Extensions.DependencyInjection`.
- How to properly architect background workers that need to execute scoped database queries using `IServiceScopeFactory`.
- How disposal cascades through child scope providers upon `scope.Dispose()`.

## What I Need to Read Later
- Keyed services (`AddKeyedSingleton`, `AddKeyedScoped`) introduced in .NET 8.
- Custom service provider implementations (Autofac, Lamar) and compiled expression tree resolution in CoreCLR.

## Key Takeaways
Dependency injection lifetimes represent object ownership and concurrency boundaries. Never allow a Singleton to capture a Scoped service directly; use `IServiceScopeFactory` to manage explicit scoped execution.
