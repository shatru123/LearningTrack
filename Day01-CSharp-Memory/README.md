# Day 01 - C# Memory Model, Span<T>, and ReadOnlySpan<T>

## Objective
Understand the reality of C# memory allocation, debunk common myths around value vs. reference types and stack vs. heap, and master zero-allocation string parsing using `Span<char>` and `ReadOnlySpan<char>`.

## Original Learning Tasks
1. Study C# Value Types vs Reference Types, Stack vs Heap
2. Implement `Span<T>` and `ReadOnlySpan<T>` parsing
3. DSA: Two Sum, Contains Duplicate

## Practical Problem
In high-throughput event processing pipelines (such as ticketing gateways, message brokers, and financial feeds), parsing string payloads like:
`EventId=1001|Name=Arsenal Match|Venue=Emirates Stadium|Price=1500|Status=Available`
using traditional `string.Split()` and `Substring()` allocates massive numbers of short-lived heap objects. This puts severe pressure on Generation 0 garbage collections, causing unpredictable p99 latency spikes and memory fragmentation.

## What I Implemented
1. **TraditionalStringEventParser**: Baseline parser using `string.Split('|')`, `string.Split('=')`, and `string.Trim()`. Causes 8+ distinct heap allocations per parsed payload.
2. **SpanEventParser**: Zero-allocation mutable parser operating directly over stack-allocated or rented buffers (`Span<char>`).
3. **ReadOnlySpanEventParser**: Production-grade zero-allocation parser operating over immutable string memory via `ReadOnlySpan<char>`.
4. **MemoryModelLab**: Practical proof debunking the myth that "all value types live on the stack" by demonstrating struct placement in class fields, boxed objects, closures, arrays, and async state machines.
5. **DSA Solutions**:
   - `TwoSum`: Optimal single-pass hash map solution ($O(n)$ time, $O(n)$ space).
   - `ContainsDuplicate`: Early-exit hash set solution ($O(n)$ time, $O(n)$ space).

## Architecture
```text
Payload Input (string / native memory / socket buffer)
                      │
        ┌─────────────┴─────────────┐
        ▼                           ▼
Traditional Parser          Span / ReadOnlySpan Parser
(string.Split, Substring)   (ref struct, stack-only pointer + length)
        │                           │
  Multiple Heap Allocations   Zero Intermediate Heap Allocations
  (string[], sub-strings)     (Slices point directly to payload bytes)
        │                           │
        ▼                           ▼
  Gen 0 GC Pressure           Zero GC Pressure / Max Throughput
```

## Key Concepts
- **Value Types vs. Reference Types**: Value types copy data by value (unless passed by `ref`/`in`); reference types pass references to objects managed by the GC.
- **Where Value Types Live**: Value types live wherever their execution context dictates. Local variables live on the stack; struct fields inside a class live on the heap inside that class instance; boxed structs live on the heap; closure-captured struct variables live in the heap-allocated closure class.
- **Span<T>**: A `ref struct` representing a contiguous region of arbitrary memory (stack, native heap, managed heap). Encapsulates an interior pointer (`ref T`) and length (`int`).
- **ReadOnlySpan<T>**: Provides read-only access to contiguous memory without defensive copying.

## Testing
- Automated xUnit test suite (`EventParserTests`, `DsaExercisesTests`).
- Verifies parsing equivalence between traditional and Span implementations.
- Validates edge cases: boundary conditions, empty spans, malformed key-value pairs.
- Validates DSA solutions across varied input sizes and duplicate patterns.

## Performance Observations
- Traditional parsing creates allocations for:
  - Splitting on `|`: 1 array + 5 substring allocations.
  - Splitting on `=`: 5 arrays + 10 substring allocations.
  - Totaling 21 allocations per record.
- `ReadOnlySpan<char>` slicing creates 0 intermediate allocations. It parses primitive numbers directly using `int.Parse(ReadOnlySpan<char>)` and `decimal.Parse(ReadOnlySpan<char>)`.

## Production Relevance
- In services processing 50,000+ messages per second, eliminating 21 allocations per message eliminates over 1,000,000 Gen 0 object allocations every second, drastically smoothing out garbage collection pauses and stabilizing p99 API latencies.

## Common Mistakes
1. Assuming `struct` always lives on the stack.
2. Storing `Span<T>` in a class field or boxing it (compiler prevents this because `Span<T>` is a `ref struct`).
3. Calling `.ToString()` on every Span slice during parsing, which completely defeats zero-allocation benefits.
4. Using `Span<T>` across `await` boundaries (ref structs cannot be hoisted into compiler-generated async state machines).

## Senior Interview Questions
See [interview-questions.md](file:///Users/shatrughnaambhore/Shatru/Learning/Projects/LearningTrack/Day01-CSharp-Memory/interview-questions.md) for 10 in-depth architectural questions.

## What I Practically Understood
- How `ref struct` guarantees that `Span<T>` cannot escape the execution stack frame.
- How memory slicing works under the hood via interior pointers (`ref byte _reference` + `int _length`).
- How to parse primitives directly from character spans without `string` conversion.

## What I Need to Read Later
- CLR internal struct layout (Sequential vs Auto layout and struct memory packing/alignment).
- Vectorized string operations with SIMD via `SearchValues<T>` in .NET 8.

## Key Takeaways
Zero-allocation programming in C# does not require writing unsafe C-style pointer code. `Span<T>` and `ReadOnlySpan<T>` provide type-safe, bounds-checked, high-performance abstractions that yield dramatic throughput and latency improvements in backend services.
