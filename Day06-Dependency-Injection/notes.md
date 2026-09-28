# Day 06 - Engineering Notes: Dependency Injection & Captive Dependencies

## 1. Service Lifetime Comparison

| Lifetime | Registration | Creation Trigger | Disposal Point | Concurrency Safety Requirement |
|---|---|---|---|---|
| **Transient** | `AddTransient<T>` | Every `.GetService<T>()` call | When parent scope/container is disposed | None (isolated instance) |
| **Scoped** | `AddScoped<T>` | Once per `IServiceScope` | When the `IServiceScope` is disposed | Must be safe within the single thread / request |
| **Singleton** | `AddSingleton<T>` | Once upon initial resolution | When application host shuts down | **Must be 100% thread-safe** across all concurrent threads |

---

## 2. Anatomy of the Captive Dependency Problem

```csharp
// Anti-Pattern:
services.AddScoped<IOrderRepository, OrderRepository>();
services.AddSingleton<OrderBatchWorker>(); // Injects IOrderRepository in ctor!
```

### Why it causes production catastrophes:
1. `OrderBatchWorker` is created once.
2. The `OrderRepository` injected into it is never disposed because the worker never dies.
3. If `OrderRepository` holds an EF Core `DbContext`:
   - Any background task or concurrent timer ticks calling the repository will access the same `DbContext` concurrently from multiple threads -> **Crash (`A second operation was started on this context...`)**.
   - The `DbContext` change tracker grows indefinitely, holding onto every entity ever queried -> **Memory leak (`OutOfMemoryException`)**.

---

## 3. The Correct Architecture: `IServiceScopeFactory`

```csharp
public class OrderBatchWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;

    public OrderBatchWorker(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            // Create an isolated scope per execution unit / message
            using (var scope = _scopeFactory.CreateScope())
            {
                var repo = scope.ServiceProvider.GetRequiredService<IOrderRepository>();
                await repo.ProcessPendingOrdersAsync();
            } // Repository and DbContext are cleanly disposed here!

            await Task.Delay(5000, stoppingToken);
        }
    }
}
```

---

## 4. Enabling Scope Validation in Unit Tests & CI

In ASP.NET Core:
```csharp
builder.Host.UseDefaultServiceProvider((context, options) =>
{
    options.ValidateScopes = true;
    options.ValidateOnBuild = true;
});
```
This forces the container to verify all singleton call sites at startup and fail fast during automated tests rather than in production.

---

## 5. DSA Takeaways
- **3Sum**: Sort the array ($O(n \log n)$). Fix element $i$, then use two pointers ($left, right$) to find pairs summing to $-nums[i]$. Skipping duplicate adjacent values prevents duplicate triplets ($O(n^2)$ time, $O(1)$ space).
- **Container With Most Water**: Two pointers at extremes ($0$ and $n-1$). Area is $(right - left) \times \min(h_l, h_r)$. Always advance the pointer pointing to the shorter line because keeping the shorter line cannot yield a larger area with a smaller width ($O(n)$ time, $O(1)$ space).
