# Day 02 - Senior .NET Interview Questions: ref struct & Memory<T>

### Q1: Why can't `Span<T>` be used across an `await` statement in C#?
- **Short answer**: Because `Span<T>` is a `ref struct` that could point to stack memory; hoisting it into an async state machine on the managed heap could lead to stack tearing and memory corruption.
- **Deeper answer**: When an async method pauses at an `await` that doesn't complete synchronously, its state machine is preserved on the heap. If `Span<T>` were permitted in that state machine, its interior pointer would reference stack memory that is invalidated when the initial thread unwinds. The continuation could then execute on a different thread with an invalid pointer.
- **Practical example**: Use `Memory<T>` or `ReadOnlyMemory<T>` to store buffer state across `await`, and access `.Span` only within synchronous code blocks between awaits.

---

### Q2: What is the internal memory layout of `Memory<T>` compared to `Span<T>`?
- **Short answer**: `Span<T>` contains a managed interior pointer (`ref T`) and length (`int`). `Memory<T>` contains an object reference (`object?`), starting offset (`int`), and length (`int`).
- **Deeper answer**: Because `Memory<T>` refers to the owning heap object (such as an array, string, or `MemoryManager<T>`) rather than an ephemeral interior pointer directly on the stack, the GC can track the root object safely on the heap. Calling `.Span` performs the pointer calculation dynamically for stack-only usage.
- **Practical example**:
```csharp
ReadOnlyMemory<byte> memory = new byte[1024]; // Safe on heap
ReadOnlySpan<byte> span = memory.Span; // Safe on stack only
```

---

### Q3: What is the purpose of the `in` parameter modifier and what is its performance trap?
- **Short answer**: `in` passes an argument by reference for read-only access. Its trap is hidden defensive copying when used with non-`readonly` structs.
- **Deeper answer**: If a struct is not declared with the `readonly struct` modifier, the C# compiler cannot prove that accessing its properties or methods won't mutate its internal state. Therefore, the compiler silently creates a temporary defensive copy on the stack before invoking any member, destroying any performance gain.
- **Practical example**:
```csharp
public struct MutablePoint { public int X; public int GetX() => X; }
public void Process(in MutablePoint p) { p.GetX(); /* Compiler inserts defensive copy! */ }
```

---

### Q4: When should you NOT use the `in` parameter modifier?
- **Short answer**: Do not use `in` for primitive types (e.g., `int`, `float`, `bool`) or small structs ($\le 16$ bytes).
- **Deeper answer**: On 64-bit architectures, passing by reference requires pushing an 8-byte memory pointer onto the stack. Reading the value requires an extra pointer dereference. For types that fit in a 32-bit or 64-bit CPU register, passing by value is faster and avoids dereferencing overhead.
- **Practical example**: Prefer `void Add(int a, int b)` over `void Add(in int a, in int b)`.

---

### Q5: How do `System.IO.Pipelines` leverage `ReadOnlySequence<T>` and `Memory<T>`?
- **Short answer**: `System.IO.Pipelines` breaks network socket streams into linked segments of memory, exposing them as `ReadOnlySequence<byte>` without continuous heap reallocation.
- **Deeper answer**: In high-throughput servers like Kestrel, incoming data arrives in variable-sized buffers. `ReadOnlySequence<byte>` can span multiple distinct memory blocks (`ReadOnlyMemory<byte>`). Code can parse contiguous chunks as `ReadOnlySpan<byte>` without reallocating a single monolithic array.
- **Practical example**:
```csharp
ReadResult result = await pipeReader.ReadAsync();
ReadOnlySequence<byte> buffer = result.Buffer;
// Inspect buffer without copying
pipeReader.AdvanceTo(buffer.Start, buffer.End);
```

---

### Q6: Can a `ref struct` implement an interface in modern C#?
- **Short answer**: Prior to C# 13, no. In C# 13 (.NET 9), ref structs can implement interfaces if the interface is constrained with `allows ref struct`.
- **Deeper answer**: Historically, calling an interface method on a struct requires boxing it to the interface type, which is forbidden for `ref struct`. In C# 13, the anti-constraint `where T : allows ref struct` allows generic code and interfaces to work with ref structs without boxing.
- **Practical example**: In C# 12 / .NET 8, `public ref struct MySpan : IDisposable` compiles only if `Dispose()` is implemented via duck-typing pattern (a public `void Dispose()` method) rather than explicit interface implementation.

---

### Q7: What is `MemoryManager<T>` and when is it necessary in production?
- **Short answer**: An abstract base class used to wrap custom or unmanaged memory (like native memory, memory-mapped files, or hardware buffers) inside a safe `Memory<T>`.
- **Deeper answer**: When integrating with native C libraries or GPU/DMA buffers, you cannot wrap unmanaged memory into a standard `Memory<T>` directly. Subclassing `MemoryManager<T>` allows you to implement pinning, span retrieval, and custom disposal lifecycle while exposing idiomatic `Memory<T>` to downstream .NET code.
- **Practical example**: Wrapping memory mapped files or native ring buffers in zero-copy messaging architectures.

---

### Q8: What is the difference between `ReadOnlySpan<T>.Slice()` and `ReadOnlyMemory<T>.Slice()`?
- **Short answer**: Both perform $O(1)$ zero-allocation slicing by adjusting the offset and length, but `ReadOnlySpan<T>` produces a stack-only `ref struct`, while `ReadOnlyMemory<T>` produces a heap-safe struct.
- **Deeper answer**: `Span.Slice()` adjusts the internal interior pointer `_reference + (offset * sizeof(T))` and decrements length. `Memory.Slice()` simply calculates `newIndex = _index + offset` and updates length, retaining the original root object reference.
- **Practical example**: Slicing a 1MB payload into 100 individual records across asynchronous tasks requires `ReadOnlyMemory<T>.Slice()`.

---

### Q9: Why is `stackalloc` dangerous if used without length validation?
- **Short answer**: Unbounded `stackalloc` can cause a `StackOverflowException`, which terminates the process immediately and cannot be caught by a `try/catch` block.
- **Deeper answer**: The execution stack is small (typically 1MB on Windows, 1.5MB on Linux/macOS). If user-supplied input dictates the size of `stackalloc char[userSize]`, a malicious or large payload will blow the stack, causing an unrecoverable crash (denial of service). Always guard with a threshold (e.g. $\le 256$ elements) and fallback to `ArrayPool<T>.Shared`.
- **Practical example**:
```csharp
Span<char> buffer = size <= 256 ? stackalloc char[size] : (rented = ArrayPool<char>.Shared.Rent(size));
```

---

### Q10: How does `ArrayPool<T>.Shared` interact with `Memory<T>` in async pipelines?
- **Short answer**: You rent an array from the pool, wrap it in `Memory<T>`, slice it to the valid data length, process it asynchronously, and return the underlying array in a `finally` block.
- **Deeper answer**: Because rented arrays may be larger than the requested size, `Memory<T>.Slice(0, actualBytes)` restricts consumer visibility to the valid portion. Returning the array prematurely before an `await` finishes will lead to race conditions where other threads overwrite active data.
- **Practical example**:
```csharp
byte[] rented = ArrayPool<byte>.Shared.Rent(4096);
try {
    int read = await stream.ReadAsync(rented.AsMemory(0, 4096));
    await ProcessDataAsync(rented.AsMemory(0, read));
} finally {
    ArrayPool<byte>.Shared.Return(rented);
}
```
