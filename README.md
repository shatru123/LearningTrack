# 100-Day .NET + AI Engineering Track

[![.NET 8](https://img.shields.io/badge/.NET-8.0-blue.svg)](https://dotnet.microsoft.com/)
[![Status](https://img.shields.io/badge/Recovery%20Sprint-Days%201--8%20Completed-brightgreen.svg)](#recovery-sprint-days-18)

Practical engineering implementations, performance benchmarks, systems architecture labs, and Data Structures & Algorithms (DSA) exercises across the 100-Day .NET + AI Engineering journey.

---

## Relationship Between LearningOS and LearningTrack

- **[LearningOS](https://learning-ttf6.onrender.com/)**: Learning planner and progress tracker. Answers: *"What should I learn next?"*
- **[LearningTrack](https://github.com/shatru123/LearningTrack)**: Practical learning repository. Answers: *"What did I actually build while learning it?"*

---

## Status

**Days 1–8 Recovery: Completed**

**Next:**  
**Day 9** — Continue the original 100-day LearningOS roadmap.

Detailed status table: [roadmap/progress.md](roadmap/progress.md)  
Roadmap overview: [roadmap/100-day-roadmap.md](roadmap/100-day-roadmap.md)

---

## Recovery Sprint (Days 1–8)

Each day includes complete practical implementations, xUnit test suites, comprehensive engineering notes, and 10 senior-level interview questions.

| Day | Topic | Key Implementations & Concepts | Links |
|---|---|---|---|
| **Day 01** | C# Memory Model, Span & ReadOnlySpan | High-Performance Event Parser (Traditional vs Span vs ReadOnlySpan), Stack vs Heap reality, Allocations, DSA (Two Sum, Contains Duplicate) | [Day01-CSharp-Memory](Day01-CSharp-Memory/) • [README](Day01-CSharp-Memory/README.md) • [Notes](Day01-CSharp-Memory/notes.md) • [Interview Qs](Day01-CSharp-Memory/interview-questions.md) |
| **Day 02** | ref struct & Memory&lt;T&gt; in Async | CSV Parser, ref struct limitations, async boundary safety, `Memory<T>`, `ReadOnlyMemory<T>`, `in` parameters, DSA (Valid Anagram, Group Anagrams) | [Day02-RefStruct-Memory](Day02-RefStruct-Memory/) • [README](Day02-RefStruct-Memory/README.md) • [Notes](Day02-RefStruct-Memory/notes.md) • [Interview Qs](Day02-RefStruct-Memory/interview-questions.md) |
| **Day 03** | Async/Await & AsyncStateMachine | Compiler-generated `IAsyncStateMachine`, Event Aggregation Service (Sequential vs Parallel, Cancellation, Timeout, Failure aggregation, `ValueTask`), DSA (Top K Frequent Elements) | [Day03-Async-Await](Day03-Async-Await/) • [README](Day03-Async-Await/README.md) • [Notes](Day03-Async-Await/notes.md) • [Interview Qs](Day03-Async-Await/interview-questions.md) |
| **Day 04** | Garbage Collection Laboratory | Gen 0/1/2, LOH (85KB+), Server vs Workstation GC, Background GC, GCSettings, allocation benchmarking with `GC.GetAllocatedBytesForCurrentThread()`, DSA (Product of Array Except Self) | [Day04-Garbage-Collection](Day04-Garbage-Collection/) • [README](Day04-Garbage-Collection/README.md) • [Notes](Day04-Garbage-Collection/notes.md) • [Interview Qs](Day04-Garbage-Collection/interview-questions.md) |
| **Day 05** | Middleware & Kestrel Server | Custom async middleware, Correlation ID (`X-Correlation-ID`), RFC 7807 `ProblemDetails` exception handling, Request logging, RequestDelegate vs `IMiddleware` factory activation, Kestrel pipeline ordering, DSA (Valid Palindrome, Two Sum II) | [Day05-Middleware-Kestrel](Day05-Middleware-Kestrel/) • [README](Day05-Middleware-Kestrel/README.md) • [Notes](Day05-Middleware-Kestrel/notes.md) • [Interview Qs](Day05-Middleware-Kestrel/interview-questions.md) |
| **Day 06** | Dependency Injection Lifetimes | Transient, Scoped, Singleton lifecycle tracking, Captive Dependency problem reproduction, `ValidateScopes` / `ValidateOnBuild`, `IServiceScopeFactory`, Moq unit tests, DSA (3Sum, Container With Most Water) | [Day06-Dependency-Injection](Day06-Dependency-Injection/) • [README](Day06-Dependency-Injection/README.md) • [Notes](Day06-Dependency-Injection/notes.md) • [Interview Qs](Day06-Dependency-Injection/interview-questions.md) |
| **Day 07** | Minimal APIs & Endpoint Routing | `MapGroup`, `TypedResults` union types, `IEndpointFilter` for validation & execution timing, OpenAPI metadata, CRUD lifecycle, Minimal APIs vs Controllers, DSA (Best Time to Buy and Sell Stock) | [Day07-Minimal-APIs](Day07-Minimal-APIs/) • [README](Day07-Minimal-APIs/README.md) • [Notes](Day07-Minimal-APIs/notes.md) • [Interview Qs](Day07-Minimal-APIs/interview-questions.md) |
| **Day 08** | PostgreSQL B-Tree Indexing | 8KB page layout, PageHeader, ItemId/Linp pointers, `pageinspect` metadata, `EXPLAIN (ANALYZE, BUFFERS)` 4 cases (Seq Scan, Index Scan, Covering Index-Only Scan with INCLUDE, Poorly Designed Index), Dapper repository, DSA (Longest Substring Without Repeating Characters) | [Day08-SQL-Indexes](Day08-SQL-Indexes/) • [README](Day08-SQL-Indexes/README.md) • [Notes](Day08-SQL-Indexes/notes.md) • [Interview Qs](Day08-SQL-Indexes/interview-questions.md) |

---

## Running Builds and Tests

To restore, build, and run the automated test suite across all modules:

```bash
dotnet restore
dotnet build
dotnet test
```

All 76 automated unit and integration tests validate the implementations across Days 1–8.
