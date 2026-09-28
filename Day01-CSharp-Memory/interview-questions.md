# Day 01 - Senior .NET Interview Questions: C# Memory & Span

### Q1: Do all value types live on the stack in C#?
- **Short answer**: No. Value types live wherever their declaring container lives.
- **Deeper answer**: A value type declared as a local variable lives on the stack. If declared as an instance field of a class, it lives on the managed heap inside that class object. If declared in an array, it lives in the array's heap allocation. If boxed or captured in a lambda closure, it lives on the heap.
- **Practical example**:
```csharp
public class Order { public int OrderId; } // OrderId (int) lives on the heap inside Order instance!
```

---

### Q2: What is `Span<T>` and what problem does it solve in high-throughput systems?
- **Short answer**: `Span<T>` is a `ref struct` representing a contiguous region of arbitrary memory, providing type-safe, bounds-checked, zero-allocation slicing.
- **Deeper answer**: Traditional string and array slicing in .NET creates new array or string objects on the heap. `Span<T>` encapsulates a managed interior pointer (`ref T`) and a length (`int`). It enables zero-copy operations over managed arrays, native unmanaged pointers (`stackalloc`, `NativeMemory`), and string character buffers without triggering GC allocations.
- **Practical example**:
```csharp
ReadOnlySpan<char> slice = rawPayload.AsSpan().Slice(0, 10); // 0 bytes allocated
```

---

### Q3: Why is `Span<T>` declared as a `ref struct` instead of a regular struct?
- **Short answer**: To prevent its interior pointer from escaping the execution stack and pointing to invalid or collected memory.
- **Deeper answer**: Because `Span<T>` can point to a thread's stack memory (e.g. from `stackalloc`), if it could be placed on the heap (inside a class field, boxed, or passed across an `await` boundary), the stack frame could unwind while the heap reference still exists, leading to undefined behavior, memory corruption, and security vulnerabilities.
- **Practical example**: The C# compiler rejects `class CacheItem { public Span<byte> Data; }` with compile error CS8345.

---

### Q4: What is the difference between `Span<T>` and `ReadOnlySpan<T>`?
- **Short answer**: `Span<T>` provides read/write access to mutable memory, whereas `ReadOnlySpan<T>` enforces read-only access and can view immutable memory like `System.String`.
- **Deeper answer**: C# strings are immutable. You cannot create a mutable `Span<char>` directly over a `string` without unsafe memory manipulation. `ReadOnlySpan<char>` can safely view a `string`'s internal buffer (`str.AsSpan()`) without copying.
- **Practical example**:
```csharp
string text = "OrderPlaced";
ReadOnlySpan<char> readOnly = text.AsSpan(); // Valid zero-allocation view
// Span<char> mutable = text.AsSpan(); // Compile Error!
```

---

### Q5: How does boxing impact memory and GC performance in production?
- **Short answer**: Boxing converts a value type to a reference type, forcing a heap allocation and adding a TypeHandle and SyncBlockIndex header (typically 16–24 bytes overhead).
- **Deeper answer**: In tight loops or high-frequency message processing, boxing thousands of value types creates immense Generation 0 garbage collection pressure. It also breaks CPU cache locality because data must be dereferenced through a heap pointer instead of read directly from stack or contiguous memory.
- **Practical example**:
```csharp
int count = 42;
object boxed = count; // Allocates 24 bytes on 64-bit CLR (8-byte header + 8-byte method table + 8-byte payload)
```

---

### Q6: Can a `Span<T>` be passed across an `await` expression in an async method? Why or why not?
- **Short answer**: No. The compiler prohibits `ref struct` types across `await` expressions.
- **Deeper answer**: The C# compiler transforms `async` methods into an `IAsyncStateMachine`. Any local variable that survives an `await` is hoisted into a field of the state machine. If the state machine is boxed onto the heap when an operation goes asynchronous, storing a `ref struct` inside it would violate the stack-only guarantee.
- **Practical example**: Use `Memory<T>` or `ReadOnlyMemory<T>` instead of `Span<T>` when data must survive across `await` expressions.

---

### Q7: What is the difference between `stackalloc` and `ArrayPool<T>.Shared.Rent()`?
- **Short answer**: `stackalloc` allocates memory on the current execution stack frame; `ArrayPool` rents a reusable buffer from the heap.
- **Deeper answer**: `stackalloc` is instant (pointer bump) and has zero GC cost, but risks `StackOverflowException` if the buffer is large or unbounded. `ArrayPool<T>` is safe for large or dynamic buffers, avoids GC allocations by recycling arrays, but requires careful return (`Dispose` / `Return`) to avoid pool starvation or memory leaks.
- **Practical example**:
```csharp
Span<char> smallBuffer = stackalloc char[128]; // Safe for small, known sizes
char[] rented = ArrayPool<char>.Shared.Rent(4096); // Preferred for larger buffers
```

---

### Q8: What is interior pointer tracking in the .NET Garbage Collector?
- **Short answer**: The GC's ability to track and update references that point inside an object rather than to the beginning of the object.
- **Deeper answer**: `Span<T>` uses a managed reference (`ref T`) which can point to the middle of an array or pinned buffer. During GC compaction and relocation, the GC detects interior pointers and updates them accurately relative to the new base address of the moved object.
- **Practical example**: Slicing an array `int[] arr = new int[100]; Span<int> slice = arr.AsSpan(50, 10);` creates an interior pointer to `arr[50]`.

---

### Q9: How does `string.Create` utilize `Span<T>` for zero-allocation string construction?
- **Short answer**: It allocates the exact target string on the heap once and exposes its uninitialized buffer as a `Span<char>` to a callback delegate.
- **Deeper answer**: Traditionally, building strings without fixed concatenation required `StringBuilder` or multiple intermediate strings. `string.Create(length, state, (span, state) => ...)` lets developers populate the newly allocated string buffer in-place using fast Span operations before the string reference is published as immutable.
- **Practical example**:
```csharp
string id = string.Create(8, 1234, (span, val) => {
    val.TryFormat(span, out _);
});
```

---

### Q10: How do you choose between `struct` and `class` when designing domain entities?
- **Short answer**: Use `class` for entities with identity, lifecycle, or large size (>16–24 bytes). Use `struct` for small, immutable, identity-free values that behave like mathematical primitives.
- **Deeper answer**: Passing large structs by value copies their entire byte footprint on every method call, causing CPU cache and memory copying overhead that exceeds reference passing overhead. Structs should typically be immutable (`readonly struct`), small, and avoid holding reference-type fields that complicate GC tracking.
- **Practical example**: `Money(decimal Amount, string Currency)` is a good candidate for `readonly record struct`, whereas `Order` with collections and lifecycle events is a `class`.
