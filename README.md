# 100-Day .NET + AI Engineering Track

[![.NET 8](https://img.shields.io/badge/.NET-8.0-blue.svg)](https://dotnet.microsoft.com/)
[![Status](https://img.shields.io/badge/Roadmap-Days%201--23%20Completed-brightgreen.svg)](#completed-curriculum-days-123)
[![Tests](https://img.shields.io/badge/Tests-265%20Passed-brightgreen.svg)](#running-builds-and-tests)

Practical engineering implementations, performance benchmarks, systems architecture labs, and Data Structures & Algorithms (DSA) exercises across the 100-Day .NET + AI Engineering journey.

---

## Relationship Between LearningOS and LearningTrack

- **[LearningOS](https://learning-ttf6.onrender.com/)**: Learning planner and progress tracker. Answers: *"What should I learn next?"*
- **[LearningTrack](https://github.com/shatru123/LearningTrack)**: Practical learning repository. Answers: *"What did I actually build while learning it?"*

---

## Status

**Days 1–23: Completed**

Detailed status table: [roadmap/progress.md](roadmap/progress.md)  
Roadmap overview: [roadmap/100-day-roadmap.md](roadmap/100-day-roadmap.md)

---

## 🌐 Quick Access: Interactive Web Knowledge Hub

Read complete daily guides, deep architectural notes, interview questions, and copy C# code snippets directly from your phone or browser:

- **Instant Live Preview**: [Open Knowledge Hub via HTMLPreview](https://htmlpreview.github.io/?https://github.com/shatru123/LearningTrack/blob/main/index.html)
- **GitHub Pages Portal** *(once enabled)*: [https://shatru123.github.io/LearningTrack/](https://shatru123.github.io/LearningTrack/)

---

## Completed Curriculum (Days 1–23)

Each day includes complete practical implementations, xUnit test suites, comprehensive engineering notes, and practical scenarios.

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
| **Day 09** | SQL Execution Plans & Tuning | PostgreSQL `EXPLAIN (ANALYZE, BUFFERS)` node parser, cost models, buffer hit ratios, disk spill detection, cardinality skew alerts, DSA (LeetCode #424 Longest Repeating Character Replacement) | [Day09-SQL-Execution-Plans](Day09-SQL-Execution-Plans/) • [README](Day09-SQL-Execution-Plans/README.md) • [Notes](Day09-SQL-Execution-Plans/notes.md) • [Interview Qs](Day09-SQL-Execution-Plans/interview-questions.md) |
| **Day 10** | SQL Transactions, ACID & MVCC | ANSI SQL isolation levels, PostgreSQL MVCC `xmin`/`xmax`/snapshot visibility, anomaly simulation (Dirty Read, Non-Repeatable Read, Phantom Read, Write Skew), DSA (LeetCode #20 Valid Parentheses, LeetCode #155 Min Stack) | [Day10-Transactions-ACID](Day10-Transactions-ACID/) • [README](Day10-Transactions-ACID/README.md) • [Notes](Day10-Transactions-ACID/notes.md) • [Interview Qs](Day10-Transactions-ACID/interview-questions.md) |
| **Day 11** | EF Core Change Tracking Mechanics | Snapshot vs Notification tracking (`INotifyPropertyChanged`/`Changing`), `DetectChanges()` inspection, `AsNoTracking` vs `AsNoTrackingWithIdentityResolution`, DSA (LeetCode #150 Evaluate Reverse Polish Notation, LeetCode #739 Daily Temperatures) | [Day11-EFCore-ChangeTracking](Day11-EFCore-ChangeTracking/) • [README](Day11-EFCore-ChangeTracking/README.md) • [Notes](Day11-EFCore-ChangeTracking/notes.md) • [Interview Qs](Day11-EFCore-ChangeTracking/interview-questions.md) |
| **Day 12** | EF Core Split Queries & Interceptors | Cartesian explosion prevention (`.AsSplitQuery()` vs `.AsSingleQuery()`), query auditing & slow query logging with `DbCommandInterceptor`, DSA (LeetCode #704 Binary Search, LeetCode #74 Search a 2D Matrix) | [Day12-EFCore-SplitQueries-Interceptors](Day12-EFCore-SplitQueries-Interceptors/) • [README](Day12-EFCore-SplitQueries-Interceptors/README.md) • [Notes](Day12-EFCore-SplitQueries-Interceptors/notes.md) • [Interview Qs](Day12-EFCore-SplitQueries-Interceptors/interview-questions.md) |
| **Day 13** | Dapper Performance & Streaming | Multi-mapping 1:N relations, buffered vs unbuffered queries, async streaming with `IAsyncEnumerable<T>`, Dapper vs EF Core raw SQL profiling, DSA (LeetCode #153 Find Minimum in Rotated Sorted Array & #154 Duplicates) | [Day13-Dapper-Performance](Day13-Dapper-Performance/) • [README](Day13-Dapper-Performance/README.md) • [Notes](Day13-Dapper-Performance/notes.md) • [Interview Qs](Day13-Dapper-Performance/interview-questions.md) |
| **Day 14** | EF Core Migrations & Production Strategy | Idempotent SQL deployment (`dotnet ef migrations script -i`), breaking change analysis, Expand-Contract pattern, advisory locking, DSA (LeetCode #33 Search in Rotated Sorted Array & #81 Duplicates) | [Day14-EFCore-Migrations-Production](Day14-EFCore-Migrations-Production/) • [README](Day14-EFCore-Migrations-Production/README.md) • [Notes](Day14-EFCore-Migrations-Production/notes.md) • [Interview Qs](Day14-EFCore-Migrations-Production/interview-questions.md) |
| **Day 15** | Channels & Concurrency in C# | `System.Threading.Channels` producer-consumer pipeline, bounded buffer backpressure policies, graceful shutdown, DSA (LeetCode #206 Reverse Linked List & #21 Merge Two Sorted Lists) | [Day15-Channels-Concurrency](Day15-Channels-Concurrency/) • [README](Day15-Channels-Concurrency/README.md) • [Notes](Day15-Channels-Concurrency/notes.md) • [Interview Qs](Day15-Channels-Concurrency/interview-questions.md) |
| **Day 16** | Distributed Locking & Synchronization | `Interlocked` lock-free atomic CAS, `SemaphoreSlim` async throttled leases, `ReaderWriterLockSlim`, Redlock distributed consensus & fencing tokens, DSA (LeetCode #143 Reorder List & #19 Remove Nth From End) | [Day16-Synchronization-Locks](Day16-Synchronization-Locks/) • [README](Day16-Synchronization-Locks/README.md) • [Notes](Day16-Synchronization-Locks/notes.md) • [Interview Qs](Day16-Synchronization-Locks/interview-questions.md) |
| **Day 17** | Introduction to Redis & Cache Patterns | Redis connection resilience, Cache-Aside, Write-Through, Write-Behind batch flushing, Cache Stampede (Mutex & XFetch algorithm), DSA (LeetCode #226 Invert Binary Tree & #104 Maximum Depth) | [Day17-Redis-Cache-Patterns](Day17-Redis-Cache-Patterns/) • [README](Day17-Redis-Cache-Patterns/README.md) • [Notes](Day17-Redis-Cache-Patterns/notes.md) • [Interview Qs](Day17-Redis-Cache-Patterns/interview-questions.md) |
| **Day 18** | Redis Data Structures & Memory Eviction Policies | Strings (SDS), Hashes, Sets (SINTER), Sorted Sets (ZSET leaderboards & rate limiting), Bitmaps (DAU), HyperLogLog (12KB cardinality), LRU/LFU/Volatile-TTL eviction simulator, DSA (LeetCode #543 Diameter of Binary Tree & #110 Balanced Binary Tree) | [Day18-Redis-DataStructures-Eviction](Day18-Redis-DataStructures-Eviction/) • [README](Day18-Redis-DataStructures-Eviction/README.md) • [Notes](Day18-Redis-DataStructures-Eviction/notes.md) • [Interview Qs](Day18-Redis-DataStructures-Eviction/interview-questions.md) |
| **Day 19** | Redis Distributed Locking with RedLock | Multi-instance RedLock consensus, quorum validation ($\lfloor N/2 \rfloor + 1$), clock drift & skew, auto-renewal lease watchdog, fencing tokens, DSA (LeetCode #100 Same Tree & #572 Subtree of Another Tree with Merkle Hashing) | [Day19-Redis-RedLock-DistributedLocking](Day19-Redis-RedLock-DistributedLocking/) • [README](Day19-Redis-RedLock-DistributedLocking/README.md) • [Notes](Day19-Redis-RedLock-DistributedLocking/notes.md) • [Interview Qs](Day19-Redis-RedLock-DistributedLocking/interview-questions.md) |
| **Day 20** | Redis Pub/Sub & Redis Streams | Redis Streams Radix tree of listpacks, Consumer groups, ACK, Pending Entries List (PEL), `XCLAIM` fault recovery, Pub/Sub broadcast, DSA (LeetCode #235 Lowest Common Ancestor of a BST & #102 Binary Tree Level Order Traversal) | [Day20-Redis-Streams-PubSub](Day20-Redis-Streams-PubSub/) • [README](Day20-Redis-Streams-PubSub/README.md) • [Notes](Day20-Redis-Streams-PubSub/notes.md) • [Interview Qs](Day20-Redis-Streams-PubSub/interview-questions.md) |
| **Day 21** | System Design: Distributed Cache Architecture | Ketama consistent hash ring with virtual nodes ($2^{32}-1$), two-tier caching (L1 MemoryCache + L2 Redis), hot-key suffix scattering, single-flight mutex & XFetch stampede protection, DSA (LeetCode #199 Binary Tree Right Side View & #1448 Count Good Nodes in Binary Tree) | [Day21-Distributed-Cache-Architecture](Day21-Distributed-Cache-Architecture/) • [README](Day21-Distributed-Cache-Architecture/README.md) • [Notes](Day21-Distributed-Cache-Architecture/notes.md) • [Interview Qs](Day21-Distributed-Cache-Architecture/interview-questions.md) |
| **Day 22** | System Design: Rate Limiter Design | Token Bucket, Leaky Bucket, Fixed Window, Sliding Window Log, Sliding Window Counter, ASP.NET Core distributed rate limiter middleware, standard headers, RFC 7807 429, DSA (LeetCode #98 Validate BST & #230 Kth Smallest Element in BST) | [Day22-Rate-Limiter-Design](Day22-Rate-Limiter-Design/) • [README](Day22-Rate-Limiter-Design/README.md) • [Notes](Day22-Rate-Limiter-Design/notes.md) • [Interview Qs](Day22-Rate-Limiter-Design/interview-questions.md) |
| **Day 23** | System Design: URL Shortener (TinyURL) | Capacity planning (100M writes, 1B reads), Twitter Snowflake 64-bit ID generator, loss-less Base62 codec, database sharding by Hash(ID), Cache-Aside Redis layer, HTTP 302 analytics, DSA (LeetCode #105 Construct Binary Tree from Preorder & Inorder) | [Day23-Url-Shortener-TinyUrl](Day23-Url-Shortener-TinyUrl/) • [README](Day23-Url-Shortener-TinyUrl/README.md) • [Notes](Day23-Url-Shortener-TinyUrl/notes.md) • [Interview Qs](Day23-Url-Shortener-TinyUrl/interview-questions.md) |

---

## Running Builds and Tests

To restore, build, and run the automated test suite across all modules:

```bash
dotnet restore
dotnet build
dotnet test LearningTrack.sln
```

All 265 automated unit and integration tests validate the implementations across Days 1–23.
