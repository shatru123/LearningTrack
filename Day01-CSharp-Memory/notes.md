# Day 01 - Engineering Notes: C# Memory Internals & Span

## 1. Value Types vs Reference Types: Nuance Over Myth

### Myth
> "Value types are stored on the stack; reference types are stored on the heap."

### Reality
The storage location of a value type is determined entirely by where it is declared and who owns its lifetime:
1. **Local variables of a method**: Stored on the thread's execution stack (or held in CPU registers if optimized by JIT).
2. **Instance fields of a class**: Stored directly inline within the class instance on the managed heap. If class `Order` has an `int Quantity` and a `DateTime CreatedAt`, those 12 bytes live directly in the heap block of `Order`.
3. **Elements of an array**: An array is a reference type on the heap. An `int[]` allocates all `int` elements consecutively on the heap.
4. **Boxed value types**: When cast to `object`, `ValueType`, or an interface, the runtime allocates a box object on the heap containing method table pointer, sync block index, and the value.
5. **Captured variables in closures**: If a local struct or primitive is captured by a lambda or local function, the compiler hoists it to an instance field of a compiler-generated display class on the heap.
6. **Async state machine fields**: Locals in an `async` method spanning an `await` are hoisted into fields of a generated `IAsyncStateMachine` struct/class, which resides on the heap when boxed inside a `Task`.
7. **Static fields**: Live in the AppDomain's loader heap / high-frequency heap.

---

## 2. Anatomy of `Span<T>` and `ReadOnlySpan<T>`

`Span<T>` is declared as a `ref struct`:
```csharp
public readonly ref struct Span<T>
{
    internal readonly ref T _reference;
    private readonly int _length;
}
```

### Why `ref struct`?
A `ref struct` is strictly restricted by the CLR type system:
- Can only live on the execution stack.
- Cannot be boxed to `object` or `ValueType`.
- Cannot implement interfaces (prior to C# 13 `allows ref struct`).
- Cannot be a field of a regular class or normal struct.
- Cannot be an element of an array.
- Cannot be used across `await` or `yield return` boundaries.

These guarantees ensure the interior pointer `_reference` never points to an invalid/reclaimed stack frame or unpinned moved heap memory.

---

## 3. Parsing Comparison

### Traditional String API
```csharp
string[] parts = input.Split('|');
foreach (string part in parts)
{
    string[] kv = part.Split('=');
    // Allocates multiple strings, arrays, and GC tracking overhead
}
```

### ReadOnlySpan API
```csharp
ReadOnlySpan<char> span = input.AsSpan();
while (!span.IsEmpty)
{
    int nextDelimiter = span.IndexOf('|');
    ReadOnlySpan<char> token = nextDelimiter >= 0 ? span.Slice(0, nextDelimiter) : span;
    span = nextDelimiter >= 0 ? span.Slice(nextDelimiter + 1) : ReadOnlySpan<char>.Empty;

    int eqIndex = token.IndexOf('=');
    ReadOnlySpan<char> key = token.Slice(0, eqIndex).Trim();
    ReadOnlySpan<char> value = token.Slice(eqIndex + 1).Trim();

    if (key.SequenceEqual("EventId"))
        eventId = int.Parse(value); // .NET int.Parse supports ReadOnlySpan<char>
}
```
Zero intermediate string allocations. Primitives are parsed directly from memory.

---

## 4. DSA Takeaways
- **Two Sum**: Using a complement lookup in `Dictionary<int, int>` yields single-pass $O(n)$ time complexity vs $O(n^2)$ brute force.
- **Contains Duplicate**: `HashSet<T>.Add()` returns `false` if the item already exists, allowing immediate early exit without double lookups.
