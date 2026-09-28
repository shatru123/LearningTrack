# Day 03 - Senior .NET Interview Questions: Async/Await & State Machine

### Q1: What happens under the hood when a method is marked with `async`?
- **Short answer**: Roslyn generates a compiler state machine implementing `IAsyncStateMachine` and transforms local variables spanning awaits into fields of that struct.
- **Deeper answer**: The original method body becomes a stub that instantiates `AsyncTaskMethodBuilder<T>`, calls `.Start(ref stateMachine)`, and returns the builder's Task. The state machine's `MoveNext()` method divides execution into states separated by `await` expressions, attaching continuations when awaiters are incomplete.
- **Practical example**: A single `await Task.Delay(10)` creates a state machine with states `-1` (running), `0` (suspended waiting for delay), and `-2` (completed).

---

### Q2: Why is `async void` considered dangerous, and what is its only valid use case?
- **Short answer**: `async void` methods cannot be awaited, and unhandled exceptions crash the entire process. Its only valid use case is UI event handlers (e.g. `button_Click(object sender, EventArgs e)`).
- **Deeper answer**: Because `async void` does not return a `Task`, the caller has no handle to track completion or catch exceptions. Unhandled exceptions are posted directly to the `SynchronizationContext` or rethrown on the ThreadPool, terminating the application.
- **Practical example**: In ASP.NET Core middleware or backend services, always return `Task` or `Task<T>`, never `async void`.

---

### Q3: What is the difference between `Task.Run` and `Task.Factory.StartNew`?
- **Short answer**: `Task.Run(fn)` is a simplified, safe wrapper around `Task.Factory.StartNew` with `TaskScheduler.Default` and `TaskCreationOptions.DenyChildAttach`.
- **Deeper answer**: `Task.Factory.StartNew` has dangerous defaults: it uses the ambient `TaskScheduler.Current` (which might be a UI or custom scheduler) and does not automatically unwrap nested tasks if the delegate is async (`Task<Task<T>>`), requiring `.Unwrap()`. `Task.Run` avoids these pitfalls.
- **Practical example**: Always use `Task.Run` for offloading CPU-bound operations to the ThreadPool.

---

### Q4: How does `ConfigureAwait(false)` work and why is it recommended for library code?
- **Short answer**: It instructs the awaiter not to marshal the continuation back to the original `SynchronizationContext` or `TaskScheduler`.
- **Deeper answer**: In desktop UI (WPF/WinForms) or legacy ASP.NET (pre-Core), requests captured a synchronization context. Resuming on that context causes UI thread bottlenecks and potential deadlocks if someone blocks synchronously on `.Result`. `ConfigureAwait(false)` schedules the continuation on any available ThreadPool thread, reducing overhead and preventing deadlocks.
- **Practical example**: In ASP.NET Core, there is no `SynchronizationContext`, so `ConfigureAwait(false)` is technically redundant in controllers, but still recommended in reusable NuGet libraries.

---

### Q5: What happens when an exception is thrown in `Task.WhenAll`?
- **Short answer**: `Task.WhenAll` waits for all tasks to complete or fault and aggregates all exceptions into an `AggregateException`. However, `await Task.WhenAll(...)` only unwraps and rethrows the *first* exception.
- **Deeper answer**: If 4 out of 5 tasks throw exceptions, `await whenAllTask` throws only the first one. The remaining 3 exceptions will be swallowed unless you inspect `whenAllTask.Exception.InnerExceptions`.
- **Practical example**:
```csharp
Task all = Task.WhenAll(t1, t2);
try { await all; }
catch {
    foreach(var ex in all.Exception.InnerExceptions) Log(ex);
}
```

---

### Q6: When should you use `ValueTask<T>` instead of `Task<T>`, and what are the strict rules around it?
- **Short answer**: Use `ValueTask<T>` when an operation often completes synchronously. Rules: Never await a `ValueTask` more than once, never call `.Result` before it completes, and do not await concurrently.
- **Deeper answer**: A `Task<T>` always allocates an object on the heap even if completed synchronously (unless using a cached Task). A `ValueTask<T>` is a struct that incurs $0$ heap allocation on synchronous paths. If backed by an `IValueTaskSource`, reusing or multiple-awaiting it will corrupt the backing object pool.
- **Practical example**: A read-through cache method that hits in-memory cache $95\%$ of the time is the ideal candidate for `ValueTask<T>`.

---

### Q7: What is ThreadPool starvation and how does sync-over-async cause it?
- **Short answer**: Blocking thread pool threads on asynchronous tasks (using `.Result` or `.Wait()`) exhausts available threads, causing catastrophic queuing and latency spikes.
- **Deeper answer**: If 50 incoming HTTP requests block their threads waiting for database queries to finish, and the database queries need ThreadPool threads to execute their continuations, all workers are blocked. The ThreadPool injects new threads slowly (typically 1–2 per second), resulting in request timeouts and server unresponsiveness.
- **Practical example**: `var data = GetDataAsync().Result;` inside an ASP.NET Core controller under high load causes ThreadPool starvation.

---

### Q8: How does `CancellationToken.Register` differ from passing the token directly into async APIs?
- **Short answer**: Passing the token lets the underlying async method handle cancellation natively. `Register` attaches a custom callback delegate invoked when cancellation occurs.
- **Deeper answer**: `token.Register()` allocates a registration node and must be disposed (`using var reg = token.Register(...)`) to avoid memory leaks if the token outlives the scope. It is primarily used to bridge legacy non-cancellable APIs (e.g. closing a socket or stream when cancelled).
- **Practical example**:
```csharp
using var reg = ct.Register(() => socket.Close());
```

---

### Q9: What is `TaskCompletionSource<T>` and what is the importance of `TaskCreationOptions.RunContinuationsAsynchronously`?
- **Short answer**: It manually controls the lifecycle of a `Task`. `RunContinuationsAsynchronously` prevents continuations from executing inline on the thread calling `SetResult`.
- **Deeper answer**: Without this option, when `tcs.SetResult()` is called, any code awaiting `tcs.Task` immediately runs on the caller's thread. If that continuation does heavy work or blocks, it hijacks the producer thread, risking deadlocks and stack overflow.
- **Practical example**:
```csharp
var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
```

---

### Q10: How do asynchronous local variables (`AsyncLocal<T>`) propagate across `await` boundaries?
- **Short answer**: Through the ambient `ExecutionContext`, which flows from the calling thread to the continuation thread.
- **Deeper answer**: When an async method yields at an await, the current `ExecutionContext` is captured. When the continuation resumes on a ThreadPool worker, the captured `ExecutionContext` is restored, allowing ambient context like trace IDs, correlation IDs, and tenant info to persist across threads.
- **Practical example**: `IHttpContextAccessor` in ASP.NET Core uses `AsyncLocal<HttpContext>` internally to make the current request accessible across async calls.
