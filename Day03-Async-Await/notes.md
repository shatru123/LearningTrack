# Day 03 - Engineering Notes: Async/Await & State Machine Mechanics

## 1. Compiler Transformation of Async Methods

When you compile an `async` method, Roslyn converts it into:
1. **A Stub Method**:
   - Creates an instance of the compiler-generated struct `IAsyncStateMachine`.
   - Initializes `AsyncTaskMethodBuilder<T>.Create()`.
   - Calls `builder.Start(ref stateMachine)`.
   - Returns `builder.Task`.
2. **The State Machine Struct**:
   - Contains an `int <>1__state` field initialized to `-1`.
   - Contains fields for hoisted method parameters and locals spanning `await`.
   - Contains `TaskAwaiter` fields for active awaiters.
   - Contains the `MoveNext()` method containing a state switch statement.

```csharp
public void MoveNext()
{
    try
    {
        if (state == 0) goto PostAwait;
        // Pre-await work...
        awaiter = task.GetAwaiter();
        if (!awaiter.IsCompleted)
        {
            state = 0;
            builder.AwaitUnsafeOnCompleted(ref awaiter, ref this);
            return; // Release current thread back to pool!
        }
    PostAwait:
        result = awaiter.GetResult();
        builder.SetResult(finalResult);
    }
    catch (Exception ex)
    {
        state = -2;
        builder.SetException(ex);
    }
}
```

---

## 2. Awaiter Protocol & Continuations

An object can be `await`ed in C# if it satisfies the awaitable pattern:
1. Exposes a `GetAwaiter()` method.
2. The awaiter implements `INotifyCompletion` or `ICriticalNotifyCompletion`.
3. The awaiter exposes `bool IsCompleted { get; }`.
4. The awaiter exposes `T GetResult()`.

If `awaiter.IsCompleted` is `true` at the moment of evaluation (e.g. `Task.FromResult` or cached result), `MoveNext()` does not yield! It immediately calls `GetResult()` synchronously without context switches or ThreadPool queuing.

---

## 3. Exception Aggregation in Task.WhenAll

When multiple parallel tasks fail:
```csharp
Task allTasks = Task.WhenAll(t1, t2, t3);
try {
    await allTasks;
} catch (Exception ex) {
    // WARNING: 'ex' is ONLY the first exception encountered!
    // The exceptions from the other tasks are buried in allTasks.Exception!
}
```
In production, log or inspect `allTasks.Exception.InnerExceptions` to avoid blind spots during outages.

---

## 4. Task vs ValueTask Decision Matrix

| Dimension | `Task<T>` | `ValueTask<T>` |
|---|---|---|
| **Type** | Reference type (Class on Heap) | Value type (Struct) |
| **Synchronous Allocation** | Allocates $\approx 72+$ bytes (unless using cached Task) | Zero allocations ($0$ bytes) |
| **Awaiting Rules** | Can be awaited multiple times, concurrently | **Must be awaited exactly once** |
| **When to Use** | Asynchronous operations that frequently yield (I/O, network) | Operations that complete synchronously $\ge 80\%$ of the time (cache, buffer) |

---

## 5. DSA Takeaways
- **Top K Frequent Elements**: Using `PriorityQueue<int, int>` as a min-heap maintains the largest $K$ elements in $O(N \log K)$ time, outperforming full array sorting ($O(N \log N)$).
