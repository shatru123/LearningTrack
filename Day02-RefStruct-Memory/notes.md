# Day 02 - Engineering Notes: Ref Struct Limitations & Memory<T>

## 1. Why `ref struct` Cannot Cross Async Boundaries

When a C# method is marked `async`, the compiler performs a lower-level rewrite:
1. It generates a state machine structure implementing `IAsyncStateMachine`.
2. All parameters and local variables that need to survive across an `await` expression are hoisted into fields of this state machine.
3. When the awaited operation is incomplete, the state machine is boxed or captured on the managed heap (inside `AsyncTaskMethodBuilder` / `TaskPromise`).
4. If a `ref struct` (such as `Span<T>`) were permitted as a field of that state machine, a stack-bound reference would escape to the managed heap!
5. When the continuation resumes (often on a different ThreadPool thread), the original stack frame is long gone. Accessing the interior pointer would lead to **stack tearing**, memory corruption, or hard crashes.

```text
Thread 1 (Initial Call Stack)                ThreadPool Thread 2 (Continuation)
┌─────────────────────────────────┐         ┌─────────────────────────────────┐
│ Method Frame:                   │         │ Continuation Frame:             │
│  Span<byte> points here ────┐   │         │                                 │
└─────────────────────────────┼───┘         │                                 │
                              │             │                                 │
                              ▼             │                                 │
                        Stack Memory        │                                 │
                       (Deallocated         │  State Machine on Heap          │
                        after await!)       │  Attempts to access pointer ──X │
                                            └─────────────────────────────────┘
```

---

## 2. The Architectural Role of `Memory<T>` and `ReadOnlyMemory<T>`

`Memory<T>` is a regular `struct`, not a `ref struct`:
```csharp
public readonly struct Memory<T>
{
    private readonly object? _object; // Could be T[], string, or MemoryManager<T>
    private readonly int _index;
    private readonly int _length;
}
```
Because it does not hold a raw unmanaged stack interior pointer, `Memory<T>`:
- Can be stored as a field in a class or standard struct.
- Can be boxed or stored in collections (`List<Memory<byte>>`).
- Can survive across `await` expressions in async methods.
- Exposes a `.Span` property to obtain a temporary `Span<T>` on the current execution stack when synchronous work needs to happen.

---

## 3. The `in` Parameter Modifier: Pass-By-Reference for Read-Only Semantics

- **Declaration**: `void Process(in LargeStruct data)`
- **Behavior**: The runtime passes a pointer/reference to `data` instead of making a 32-byte or 64-byte value copy on the stack.
- **Immutability**: The compiler rejects any code within `Process` that attempts to mutate fields of `data`.
- **Defensive Copies**: If `LargeStruct` is NOT declared as `readonly struct`, calling any method or property on `data` causes the compiler to create a defensive copy on the stack to prevent hidden mutations! Always mark structs passed by `in` as `readonly struct`.
- **Warning for small types**: Never use `in int` or `in bool`. Passing a 64-bit reference address and dereferencing it is more expensive than copying a 4-byte integer.

---

## 4. DSA Takeaways
- **Valid Anagram**: A fixed-size array of 26 integers on the stack (`stackalloc int[26]`) tracks character delta counts in a single pass ($O(n)$ time, $O(1)$ memory).
- **Group Anagrams**: Sorting each string ($O(K \log K)$) yields a canonical signature to bucket anagrams into a `Dictionary<string, List<string>>`.
