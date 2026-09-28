# Day 07 - Engineering Notes: Minimal APIs vs Controllers

## 1. Minimal APIs vs Controllers Architectural Comparison

| Dimension | Minimal APIs | Traditional MVC Controllers |
|---|---|---|
| **Underlying Mechanism** | Direct `RequestDelegate` mapped to endpoint | `ControllerActionInvoker`, Action Descriptors, Filter Pipelines |
| **Startup Discovery** | Expressive delegate mapping at startup | Assembly scanning, reflection over all controller classes |
| **Allocations per Request** | Zero controller object allocations | Instantiates new Controller class instance per request |
| **Filter Model** | `IEndpointFilter` | `IActionFilter`, `IResourceFilter`, `IResultFilter` |
| **Return Typing** | `TypedResults` (`Results<Ok<T>, NotFound>`) | `IActionResult` / `ActionResult<T>` |
| **OpenAPI / Swagger** | Inferred automatically from `TypedResults` | Requires `[ProducesResponseType]` annotations |
| **Native AOT Compatibility** | Fully supported via RequestDelegateGenerator | Limited (reflection in model binding and controllers) |

---

## 2. The Power of `TypedResults`

```csharp
// Returns Results<Ok<EventDto>, NotFound<ProblemDetails>>
app.MapGet("/events/{id}", Results<Ok<EventDto>, NotFound<ProblemDetails>> (int id, IEventService service) =>
{
    var evt = service.Find(id);
    return evt != null
        ? TypedResults.Ok(evt)
        : TypedResults.NotFound(new ProblemDetails { Detail = "Event not found" });
});
```

### Key Advantages:
1. **Unit Testability**: The handler returns concrete types (`Ok<EventDto>`, `NotFound<ProblemDetails>`) whose `.Value` or `.StatusCode` can be tested directly without casting or mocking `HttpContext`.
2. **OpenAPI Generation**: Endpoint metadata generators automatically inspect the generic type arguments of `Results<...>` and generate complete OpenAPI 3.0 response schemas for `200` and `404` with correct schemas.

---

## 3. Anatomy of `IEndpointFilter`

```csharp
public class MyEndpointFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        // 1. Pre-execution inspection
        var httpContext = context.HttpContext;
        var arg0 = context.GetArgument<MyDto>(0);

        // 2. Can short-circuit before handler runs!
        if (arg0 == null) return TypedResults.BadRequest();

        // 3. Invoke handler or next filter
        var result = await next(context);

        // 4. Post-execution mutation
        return result;
    }
}
```

---

## 4. DSA Takeaways
- **Best Time to Buy and Sell Stock**:
  - Keep track of the minimum price observed so far (`minPrice = Math.Min(minPrice, price)`).
  - Calculate potential profit on current day (`price - minPrice`).
  - Update `maxProfit = Math.Max(maxProfit, potentialProfit)`.
  - Single pass $O(n)$ time with $O(1)$ space.
