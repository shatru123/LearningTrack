# Day 02 - Ref Struct Limitations & Memory<T> in Async Pipelines

## Objective
Master the architectural and runtime restrictions of `ref struct` (such as `Span<T>`), understand why they cannot cross asynchronous boundaries, and implement production-grade streaming pipelines using `Memory<T>` and `ReadOnlyMemory<T>` alongside `in` parameters.

## Original Learning Tasks
1. Master `ref struct` limitations & `Memory<T>` async usage
2. DSA: Valid Anagram, Group Anagrams

## Practical Problem
High-performance backend systems often ingest CSV or delimited event payloads over asynchronous network streams:
```text
1001,Arsenal Match,1500,Available
1002,Chelsea Match,2000,SoldOut
1003,Liverpool Match,1800,Available
```
While `Span<T>` provides zero-allocation parsing for synchronous blocks, attempts to use `Span<T>` inside asynchronous pipelines or retain it across `await` boundaries fail at compile time due to stack safety rules. If an engineer doesn't understand the bridge between `Span<T>` and `Memory<T>`, they either revert to allocating strings everywhere or struggle with async compiler errors.

## What I Implemented
1. **TraditionalCsvParser**: Line-by-line parser using `string.Split()` and substrings, demonstrating allocation overhead.
2. **SpanCsvParser**: Zero-allocation synchronous parser slicing `ReadOnlySpan<char>` across lines and columns.
3. **AsyncMemoryCsvParser**: Asynchronous streaming parser using `IAsyncEnumerable<EventTicket>` and `ReadOnlyMemory<char>` that processes batches across `await` boundaries safely.
4. **RefStructLimitations**: Explored and documented `ref struct` compiler restrictions and demonstrated `HeapSafeEventBatch` using `ReadOnlyMemory<char>` for class field storage.
5. **Pass-by-Reference with `in` Parameters**: Implemented `IsAffordable(in EventTicket ticket, in decimal maxBudget)` to pass read-only references and prevent unnecessary struct copying.
6. **DSA Solutions**:
   - `Valid Anagram`: $O(n)$ time, $O(1)$ space character frequency counter using stackalloc.
   - `Group Anagrams`: $O(N \cdot K \log K)$ sorted-key hash table grouping.

## Architecture
```text
                    Asynchronous I/O Stream (Socket / Pipe)
                                      │
                                      ▼
                        ReadOnlyMemory<char> / byte
                        (Heap-safe, can cross await)
                                      │
               ┌──────────────────────┴──────────────────────┐
               │                                             │
      Await Boundary (I/O)                          Synchronous Slice
               │                                             │
               ▼                                             ▼
  Compiler-Generated State Machine            .Span (ref struct on stack)
  (Hoists ReadOnlyMemory<T> safely)          (Ultra-fast zero-allocation tokenizing)
```

## Key Concepts
- **`ref struct` Constraints**: Enforced by the CLR type system to prevent stack tearing. A `ref struct` cannot be boxed, cannot implement interfaces (pre-C# 13), cannot be an array element, cannot be a field of a reference type, and cannot exist across `await` or `yield return` in state machines.
- **`Memory<T>` and `ReadOnlyMemory<T>`**: Standard structs (not `ref struct`) that represent contiguous memory backed by an array, a string, or unmanaged memory. They can safely live on the heap, be passed into async methods, and yield a `Span<T>` via `.Span` for synchronous operations.
- **`in` Parameters**: Passes an argument by reference (`readonly ref`) rather than by value, avoiding copying large structs while guaranteeing callers that the method cannot mutate the value.

## Testing
- Automated xUnit test suite (`CsvEventParserTests`, `DsaExercisesTests`).
- Verifies equality of parsing output between `TraditionalCsvParser` and `SpanCsvParser`.
- Verifies asynchronous streaming using `AsyncMemoryCsvParser` with simulated async network delays.
- Verifies `in` parameter business logic and heap-safe memory encapsulation.
- Verifies anagram algorithms across various test cases.

## Performance Observations
- Slicing `ReadOnlyMemory<T>` is an $O(1)$ index adjustment without data duplication.
- When crossing `await` boundaries, `ReadOnlyMemory<T>` is stored as a 16-byte struct field inside the async state machine without memory leaks or unsafe pointer tracking.

## Production Relevance
- Real-world high-throughput pipelines (e.g., ASP.NET Core Kestrel `PipeReader`, message brokers like Kafka/RabbitMQ consumers) read data into memory buffers. Combining `Memory<T>` for async lifetime management and `.Span` for inner synchronous parsing achieves maximum throughput and minimal GC pause times.

## Common Mistakes
1. Trying to hold a `Span<T>` in a class field or caching dictionary.
2. Passing `Span<T>` into an async method expecting it to survive `await`.
3. Creating defensive copies of `ReadOnlyMemory<T>` when only slicing is required.
4. Using `in` parameters on tiny 4-byte primitives (like `in int`), which actually introduces pointer dereferencing overhead compared to pass-by-value.

## Senior Interview Questions
See [interview-questions.md](file:///Users/shatrughnaambhore/Shatru/Learning/Projects/LearningTrack/Day02-RefStruct-Memory/interview-questions.md) for 10 in-depth architectural questions.

## What I Practically Understood
- How the C# compiler enforces `ref struct` constraints to protect memory safety.
- The dual-tier pattern: use `Memory<T>` for asynchronous transport and storage, and obtain `.Span` for tight synchronous computational loops.
- When `in` parameters actually benefit performance vs. when they degrade performance due to aliasing and pointer dereference overhead.

## What I Need to Read Later
- C# 13 `allows ref struct` anti-constraint and generic ref struct arguments.
- Custom memory pooling via `MemoryManager<T>`.

## Key Takeaways
`Span<T>` provides pure speed on the stack, while `Memory<T>` provides heap-safe flexibility for async workflows. Modern .NET architectures achieve zero-allocation high throughput by orchestrating both types seamlessly.
