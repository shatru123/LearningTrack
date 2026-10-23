#!/usr/bin/env python3
import os
import json
import html
import re

DAYS_INFO = [
    {"num": 1, "dir": "Day01-CSharp-Memory", "title": "C# Memory Model, Span & ReadOnlySpan", "category": "Memory & Performance"},
    {"num": 2, "dir": "Day02-RefStruct-Memory", "title": "ref struct & Memory<T> in Async Pipelines", "category": "Memory & Performance"},
    {"num": 3, "dir": "Day03-Async-Await", "title": "Async/Await & AsyncStateMachine Compiler Lowering", "category": "Async & Concurrency"},
    {"num": 4, "dir": "Day04-Garbage-Collection", "title": "Garbage Collection Laboratory & Tuning", "category": "Memory & Performance"},
    {"num": 5, "dir": "Day05-Middleware-Kestrel", "title": "ASP.NET Core Middleware & Kestrel Server", "category": "Web Architecture"},
    {"num": 6, "dir": "Day06-Dependency-Injection", "title": "Dependency Injection Lifetimes & Captive Dependencies", "category": "Architecture & Patterns"},
    {"num": 7, "dir": "Day07-Minimal-APIs", "title": "Minimal APIs, Endpoint Filters & TypedResults", "category": "Web Architecture"},
    {"num": 8, "dir": "Day08-SQL-Indexes", "title": "PostgreSQL B-Tree Indexing & 8KB Page Layout", "category": "SQL & Relational DB"},
    {"num": 9, "dir": "Day09-SQL-Execution-Plans", "title": "SQL Execution Plans & Index Tuning", "category": "SQL & Relational DB"},
    {"num": 10, "dir": "Day10-Transactions-ACID", "title": "SQL Transactions, ACID & MVCC Isolation Anomaly Simulator", "category": "SQL & Relational DB"},
    {"num": 11, "dir": "Day11-EFCore-ChangeTracking", "title": "EF Core Change Tracking Mechanics & Optimization", "category": "EF Core & ORM"},
    {"num": 12, "dir": "Day12-EFCore-SplitQueries-Interceptors", "title": "EF Core Split Queries & DbCommandInterceptor", "category": "EF Core & ORM"},
    {"num": 13, "dir": "Day13-Dapper-Performance", "title": "High-Performance Data Access with Dapper & Rotated Array Search", "category": "Dapper & Performance"},
    {"num": 14, "dir": "Day14-EFCore-Migrations-Production", "title": "EF Core Migrations & Production Strategy", "category": "EF Core & DevOps"},
    {"num": 15, "dir": "Day15-Channels-Concurrency", "title": "Channels & High-Throughput Concurrency in C#", "category": ".NET & Concurrency"},
    {"num": 16, "dir": "Day16-Synchronization-Locks", "title": "Distributed Locking & Concurrency Primitives", "category": "Architecture & Systems"},
    {"num": 17, "dir": "Day17-Redis-Cache-Patterns", "title": "Introduction to Redis & Cache Patterns", "category": "Redis & Distributed Systems"},
    {"num": 18, "dir": "Day18-Redis-DataStructures-Eviction", "title": "Redis Data Structures & Memory Eviction Policies", "category": "Redis & Distributed Systems"},
    {"num": 19, "dir": "Day19-Redis-RedLock-DistributedLocking", "title": "Redis Distributed Locking with RedLock", "category": "Redis & Distributed Systems"},
    {"num": 20, "dir": "Day20-Redis-Streams-PubSub", "title": "Redis Pub/Sub & Redis Streams", "category": "Redis & Distributed Systems"},
    {"num": 21, "dir": "Day21-Distributed-Cache-Architecture", "title": "System Design: Distributed Cache Architecture", "category": "System Design & DSA"},
    {"num": 22, "dir": "Day22-Rate-Limiter-Design", "title": "System Design: Rate Limiter Design", "category": "System Design & Distributed Systems"},
    {"num": 23, "dir": "Day23-Url-Shortener-TinyUrl", "title": "System Design: URL Shortener (TinyURL)", "category": "System Design & Distributed Systems"},
    {"num": 24, "dir": "Day24-Advanced-SQL-Window-Partitions", "title": "Advanced SQL: CTEs, Window Functions & Partitions", "category": "SQL & Relational DB"},
    {"num": 25, "dir": "Day25-Phase1-Milestone-Assessment", "title": "Phase 1 Review & Milestone Assessment", "category": "Architecture, Review & Benchmarks"},
    {"num": 26, "dir": "Day26-Microservices-BoundedContexts", "title": "Microservices Principles & Bounded Contexts", "category": "Microservices & DDD"},
    {"num": 27, "dir": "Day27-ApiGateway-Yarp", "title": "API Gateway Architecture with YARP", "category": "Microservices & YARP"},
    {"num": 28, "dir": "Day28-Resilient-Http-Refit-Polly", "title": "Resilient Service-to-Service HTTP with Refit & Polly", "category": "Microservices & Refit"},
    {"num": 29, "dir": "Day29-RabbitMQ-Core-Exchanges-Queues", "title": "RabbitMQ Core Concepts: Exchanges & Queues", "category": "RabbitMQ & Messaging"},
    {"num": 30, "dir": "Day30-RabbitMQ-DLQ-MassTransit", "title": "RabbitMQ Dead-Letter Queues & MassTransit", "category": "RabbitMQ & Messaging"},
    {"num": 31, "dir": "Day31-Transactional-Outbox-Pattern", "title": "The Transactional Outbox Pattern", "category": "Distributed Systems"},
    {"num": 32, "dir": "Day32-Saga-Orchestration-MassTransit", "title": "Saga Pattern: Orchestration vs Choreography", "category": "Distributed Systems"},
    {"num": 33, "dir": "Day33-Distributed-Idempotency", "title": "Distributed Idempotency & De-duplication", "category": "Distributed Systems"},
    {"num": 34, "dir": "Day34-OpenTelemetry-DistributedTracing", "title": "OpenTelemetry in .NET: Distributed Tracing", "category": "Observability & Tracing"},
    {"num": 35, "dir": "Day35-Observability-Prometheus-Grafana-Jaeger", "title": "Observability Stack: Prometheus, Grafana & Jaeger", "category": "Observability & Metrics"},
    {"num": 36, "dir": "Day36-SystemDesign-Notification-Service", "title": "System Design: Notification Service", "category": "System Design & Queues"},
    {"num": 37, "dir": "Day37-SystemDesign-Chat-System", "title": "System Design: High-Throughput Chat System", "category": "System Design & WebSockets"},
    {"num": 38, "dir": "Day38-SystemDesign-Video-Streaming", "title": "System Design: Video Streaming System", "category": "System Design & ABR"},
    {"num": 39, "dir": "Day39-CAP-PACELC-DistributedSystems", "title": "CAP Theorem & PACELC in Practice", "category": "Distributed Consensus"},
    {"num": 40, "dir": "Day40-EventSourcing-CQRS", "title": "Event Sourcing & CQRS Foundations", "category": "Event Sourcing & CQRS"},
    {"num": 41, "dir": "Day41-Docker-DotNet-Cgroups", "title": "Docker Deep Dive for .NET Services", "category": "DevOps & Containers"},
]

def get_day_svg(day_num):
    """Returns a rich, modern SVG architectural diagram for each day's core concept."""
    if day_num == 1:
        return """
        <svg viewBox="0 0 900 360" width="100%" height="auto" xmlns="http://www.w3.org/2000/svg">
          <defs>
            <linearGradient id="gradStack" x1="0%" y1="0%" x2="100%" y2="100%"><stop offset="0%" stop-color="#1e3a8a"/><stop offset="100%" stop-color="#0f172a"/></linearGradient>
            <linearGradient id="gradHeap" x1="0%" y1="0%" x2="100%" y2="100%"><stop offset="0%" stop-color="#064e3b"/><stop offset="100%" stop-color="#0f172a"/></linearGradient>
            <linearGradient id="gradSpan" x1="0%" y1="0%" x2="100%" y2="0%"><stop offset="0%" stop-color="#38bdf8"/><stop offset="100%" stop-color="#818cf8"/></linearGradient>
            <filter id="glow"><feGaussianBlur stdDeviation="3" result="coloredBlur"/><feMerge><feMergeNode in="coloredBlur"/><feMergeNode in="SourceGraphic"/></feMerge></filter>
          </defs>
          <rect width="900" height="360" rx="12" fill="#0b0f19" stroke="#1e293b" stroke-width="2"/>
          <text x="450" y="32" fill="#f8fafc" font-size="16" font-weight="bold" text-anchor="middle">C# Memory Model: Stack vs Heap & Span&lt;T&gt; Zero-Allocation Slicing</text>
          
          <!-- Stack Frame -->
          <rect x="50" y="60" width="340" height="260" rx="10" fill="url(#gradStack)" stroke="#3b82f6" stroke-width="1.5"/>
          <text x="70" y="90" fill="#60a5fa" font-size="15" font-weight="bold">THREAD STACK FRAME</text>
          <rect x="70" y="110" width="300" height="70" rx="6" fill="#1e293b" stroke="#475569"/>
          <text x="85" y="135" fill="#f8fafc" font-size="13" font-weight="bold">Local ref struct: Span&lt;char&gt;</text>
          <text x="85" y="155" fill="#94a3b8" font-size="11">ref T _pointer (8-byte pointer) | int _length: 6</text>
          <text x="85" y="170" fill="#38bdf8" font-size="11">Stack allocated • Cannot escape frame</text>

          <rect x="70" y="200" width="300" height="90" rx="6" fill="#1e293b" stroke="#475569"/>
          <text x="85" y="225" fill="#f8fafc" font-size="13" font-weight="bold">Primitive Value Types</text>
          <text x="85" y="248" fill="#94a3b8" font-size="12">int count = 42 (4 bytes)</text>
          <text x="85" y="268" fill="#94a3b8" font-size="12">double price = 99.99 (8 bytes)</text>

          <!-- Managed Heap -->
          <rect x="510" y="60" width="340" height="260" rx="10" fill="url(#gradHeap)" stroke="#10b981" stroke-width="1.5"/>
          <text x="530" y="90" fill="#34d399" font-size="15" font-weight="bold">MANAGED HEAP (GC)</text>
          <rect x="530" y="110" width="300" height="180" rx="6" fill="#1e293b" stroke="#475569"/>
          <text x="545" y="135" fill="#f8fafc" font-size="13" font-weight="bold">Heap Object: System.String</text>
          <rect x="545" y="145" width="270" height="24" rx="4" fill="#0f172a"/><text x="555" y="161" fill="#cbd5e1" font-size="11">SyncBlock Index (8 bytes)</text>
          <rect x="545" y="175" width="270" height="24" rx="4" fill="#0f172a"/><text x="555" y="191" fill="#cbd5e1" font-size="11">MethodTable Pointer (8 bytes)</text>
          <rect x="545" y="205" width="270" height="24" rx="4" fill="#0f172a"/><text x="555" y="221" fill="#cbd5e1" font-size="11">String Length (4 bytes) = 11</text>
          <rect x="545" y="235" width="270" height="40" rx="4" fill="#1e3a8a" stroke="#38bdf8"/><text x="555" y="260" fill="#38bdf8" font-size="13" font-weight="bold">"OrderPlaced" (UTF-16 buffer)</text>

          <!-- Pointer Arrow -->
          <path d="M 370 145 C 440 145, 460 255, 540 255" fill="none" stroke="#38bdf8" stroke-width="3" stroke-dasharray="6,4" filter="url(#glow)"/>
          <polygon points="545,255 535,250 535,260" fill="#38bdf8"/>
          <text x="445" y="190" fill="#38bdf8" font-size="12" font-weight="bold" text-anchor="middle">Zero-Copy Interior Pointer</text>
        </svg>
        """
    elif day_num == 2:
        return """
        <svg viewBox="0 0 900 360" width="100%" height="auto" xmlns="http://www.w3.org/2000/svg">
          <rect width="900" height="360" rx="12" fill="#0b0f19" stroke="#1e293b" stroke-width="2"/>
          <text x="450" y="32" fill="#f8fafc" font-size="16" font-weight="bold" text-anchor="middle">ref struct Stack Invariant vs Memory&lt;T&gt; Across Async Boundaries</text>
          
          <rect x="50" y="65" width="370" height="250" rx="10" fill="#1e293b" stroke="#ef4444" stroke-width="1.5"/>
          <text x="70" y="95" fill="#f87171" font-size="15" font-weight="bold">SYNCHRONOUS ONLY: ref struct (Span&lt;T&gt;)</text>
          <rect x="70" y="115" width="330" height="70" rx="6" fill="#0f172a" stroke="#334155"/>
          <text x="85" y="140" fill="#f8fafc" font-size="12">Guaranteed on execution stack</text>
          <text x="85" y="160" fill="#ef4444" font-size="12">❌ Cannot cross 'await' (CS4013)</text>
          <rect x="70" y="200" width="330" height="95" rx="6" fill="#0f172a" stroke="#334155"/>
          <text x="85" y="225" fill="#94a3b8" font-size="11">Why? Async state machine hoists locals to heap.</text>
          <text x="85" y="245" fill="#94a3b8" font-size="11">Placing ref struct on heap is strictly forbidden</text>
          <text x="85" y="265" fill="#94a3b8" font-size="11">by the CLR type safety invariants.</text>

          <rect x="480" y="65" width="370" height="250" rx="10" fill="#1e293b" stroke="#10b981" stroke-width="1.5"/>
          <text x="500" y="95" fill="#34d399" font-size="15" font-weight="bold">ASYNC SAFE: Memory&lt;T&gt; &amp; ReadOnlyMemory&lt;T&gt;</text>
          <rect x="500" y="115" width="330" height="70" rx="6" fill="#0f172a" stroke="#334155"/>
          <text x="515" y="140" fill="#f8fafc" font-size="12">Regular struct (holds object _target, int _index, int _length)</text>
          <text x="515" y="160" fill="#10b981" font-size="12">✅ Safe to cross 'await' &amp; live in heap state machine</text>
          <rect x="500" y="200" width="330" height="95" rx="6" fill="#0f172a" stroke="#334155"/>
          <text x="515" y="225" fill="#38bdf8" font-size="11">Thread 1: Read HTTP Chunk into Memory&lt;byte&gt;</text>
          <text x="515" y="245" fill="#f59e0b" font-size="11">⚡ await socket.ReadAsync() [Stack frame drops]</text>
          <text x="515" y="265" fill="#38bdf8" font-size="11">Thread 4: Resumes from ThreadPool with Memory intact!</text>
        </svg>
        """
    elif day_num == 3:
        return """
        <svg viewBox="0 0 900 360" width="100%" height="auto" xmlns="http://www.w3.org/2000/svg">
          <rect width="900" height="360" rx="12" fill="#0b0f19" stroke="#1e293b" stroke-width="2"/>
          <text x="450" y="32" fill="#f8fafc" font-size="16" font-weight="bold" text-anchor="middle">Roslyn Async/Await Compiler Lowering &amp; IAsyncStateMachine</text>
          
          <rect x="50" y="65" width="240" height="250" rx="8" fill="#1e293b" stroke="#3b82f6"/>
          <text x="65" y="95" fill="#60a5fa" font-size="14" font-weight="bold">1. Method Call</text>
          <text x="65" y="125" fill="#cbd5e1" font-size="12">public async Task&lt;int&gt;</text>
          <text x="65" y="145" fill="#cbd5e1" font-size="12">GetOrderAsync()</text>
          <path d="M 65 170 L 270 170" stroke="#475569"/>
          <text x="65" y="195" fill="#94a3b8" font-size="11">Compiler generates:</text>
          <text x="65" y="215" fill="#38bdf8" font-size="11">struct &lt;GetOrderAsync&gt;d__1</text>
          <text x="65" y="235" fill="#38bdf8" font-size="11">: IAsyncStateMachine</text>

          <rect x="330" y="65" width="240" height="250" rx="8" fill="#1e293b" stroke="#8b5cf6"/>
          <text x="345" y="95" fill="#c084fc" font-size="14" font-weight="bold">2. State Transitions</text>
          <rect x="345" y="115" width="210" height="40" rx="4" fill="#0f172a"/><text x="355" y="140" fill="#e2e8f0" font-size="11">state = -1 (Initial / Running)</text>
          <rect x="345" y="165" width="210" height="40" rx="4" fill="#0f172a"/><text x="355" y="190" fill="#f59e0b" font-size="11">state = 0 (Suspended at await)</text>
          <rect x="345" y="215" width="210" height="40" rx="4" fill="#0f172a"/><text x="355" y="240" fill="#10b981" font-size="11">state = -2 (Completed)</text>

          <rect x="610" y="65" width="240" height="250" rx="8" fill="#1e293b" stroke="#10b981"/>
          <text x="625" y="95" fill="#34d399" font-size="14" font-weight="bold">3. Execution Engine</text>
          <text x="625" y="125" fill="#cbd5e1" font-size="11">AsyncTaskMethodBuilder</text>
          <text x="625" y="145" fill="#94a3b8" font-size="11">• Synchronous completion?</text>
          <text x="625" y="165" fill="#10b981" font-size="11">  Return completed Task (0 alloc)</text>
          <text x="625" y="195" fill="#94a3b8" font-size="11">• Incomplete awaiter?</text>
          <text x="625" y="215" fill="#f59e0b" font-size="11">  Box struct state machine</text>
          <text x="625" y="235" fill="#f59e0b" font-size="11">  Attach MoveNext continuation</text>
        </svg>
        """
    elif day_num == 4:
        return """
        <svg viewBox="0 0 900 360" width="100%" height="auto" xmlns="http://www.w3.org/2000/svg">
          <rect width="900" height="360" rx="12" fill="#0b0f19" stroke="#1e293b" stroke-width="2"/>
          <text x="450" y="32" fill="#f8fafc" font-size="16" font-weight="bold" text-anchor="middle">.NET Garbage Collection: Generational Heaps &amp; LOH / POH</text>
          
          <rect x="50" y="65" width="220" height="250" rx="8" fill="#1e293b" stroke="#3b82f6"/>
          <text x="70" y="95" fill="#60a5fa" font-size="14" font-weight="bold">GEN 0 (Ephemeral)</text>
          <text x="70" y="125" fill="#cbd5e1" font-size="11">Short-lived objects (Strings, DTOs)</text>
          <text x="70" y="145" fill="#38bdf8" font-size="11">Very frequent, sub-millisecond</text>
          <text x="70" y="170" fill="#94a3b8" font-size="11">Surviving objects promote ➔ Gen 1</text>
          <rect x="70" y="200" width="180" height="90" rx="4" fill="#0f172a"/>
          <text x="80" y="230" fill="#64748b" font-size="10">Mark Phase ➔ Scan roots</text>
          <text x="80" y="250" fill="#64748b" font-size="10">Sweep Phase ➔ Reclaim dead</text>
          <text x="80" y="270" fill="#64748b" font-size="10">Compact Phase ➔ Shift memory</text>

          <rect x="300" y="65" width="180" height="250" rx="8" fill="#1e293b" stroke="#8b5cf6"/>
          <text x="320" y="95" fill="#c084fc" font-size="14" font-weight="bold">GEN 1 (Buffer)</text>
          <text x="320" y="125" fill="#cbd5e1" font-size="11">Intermediate buffer</text>
          <text x="320" y="150" fill="#94a3b8" font-size="11">Surviving objects ➔ Gen 2</text>

          <rect x="510" y="65" width="180" height="250" rx="8" fill="#1e293b" stroke="#f59e0b"/>
          <text x="530" y="95" fill="#fbbf24" font-size="14" font-weight="bold">GEN 2 (Long-Lived)</text>
          <text x="530" y="125" fill="#cbd5e1" font-size="11">Singletons, static caches</text>
          <text x="530" y="150" fill="#f59e0b" font-size="11">Full GC collection</text>
          <text x="530" y="175" fill="#94a3b8" font-size="11">Highest latency cost</text>

          <rect x="710" y="65" width="150" height="120" rx="8" fill="#1e293b" stroke="#ec4899"/>
          <text x="720" y="90" fill="#f472b6" font-size="12" font-weight="bold">LOH (&gt; 85,000 B)</text>
          <text x="720" y="115" fill="#cbd5e1" font-size="10">Large arrays/buffers</text>
          <text x="720" y="135" fill="#ec4899" font-size="10">No compaction</text>

          <rect x="710" y="195" width="150" height="120" rx="8" fill="#1e293b" stroke="#06b6d4"/>
          <text x="720" y="220" fill="#22d3ee" font-size="12" font-weight="bold">POH (Pinned)</text>
          <text x="720" y="245" fill="#cbd5e1" font-size="10">Native interop &amp; I/O</text>
          <text x="720" y="265" fill="#22d3ee" font-size="10">GC cannot relocate</text>
        </svg>
        """
    elif day_num == 5:
        return """
        <svg viewBox="0 0 900 360" width="100%" height="auto" xmlns="http://www.w3.org/2000/svg">
          <rect width="900" height="360" rx="12" fill="#0b0f19" stroke="#1e293b" stroke-width="2"/>
          <text x="450" y="32" fill="#f8fafc" font-size="16" font-weight="bold" text-anchor="middle">ASP.NET Core Middleware Pipeline: The Russian Doll Model</text>
          
          <rect x="50" y="65" width="800" height="250" rx="12" fill="#111827" stroke="#3b82f6" stroke-dasharray="4,4"/>
          <text x="70" y="95" fill="#60a5fa" font-size="13" font-weight="bold">KESTREL HTTP PIPELINE (HttpContext)</text>
          
          <rect x="80" y="115" width="200" height="170" rx="8" fill="#1e293b" stroke="#38bdf8"/>
          <text x="95" y="140" fill="#38bdf8" font-size="12" font-weight="bold">1. CorrelationId</text>
          <text x="95" y="165" fill="#cbd5e1" font-size="10">IN: Set X-Correlation-ID</text>
          <text x="95" y="235" fill="#34d399" font-size="10">OUT: Append header</text>

          <rect x="310" y="115" width="200" height="170" rx="8" fill="#1e293b" stroke="#f59e0b"/>
          <text x="325" y="140" fill="#fbbf24" font-size="12" font-weight="bold">2. ExceptionHandling</text>
          <text x="325" y="165" fill="#cbd5e1" font-size="10">IN: try { await next() }</text>
          <text x="325" y="235" fill="#f87171" font-size="10">OUT: catch ➔ RFC 7807</text>

          <rect x="540" y="115" width="200" height="170" rx="8" fill="#1e293b" stroke="#10b981"/>
          <text x="555" y="140" fill="#34d399" font-size="12" font-weight="bold">3. Endpoint Handler</text>
          <text x="555" y="165" fill="#cbd5e1" font-size="10">IN: Route execute</text>
          <text x="555" y="195" fill="#a7f3d0" font-size="11" font-weight="bold">Execute API Action</text>
          <text x="555" y="235" fill="#cbd5e1" font-size="10">OUT: Return 200 OK</text>

          <!-- Flow arrows -->
          <path d="M 280 160 L 310 160" stroke="#38bdf8" stroke-width="2"/>
          <path d="M 510 160 L 540 160" stroke="#f59e0b" stroke-width="2"/>
          <path d="M 540 230 L 510 230" stroke="#34d399" stroke-width="2"/>
          <path d="M 310 230 L 280 230" stroke="#34d399" stroke-width="2"/>
        </svg>
        """
    elif day_num == 6:
        return """
        <svg viewBox="0 0 900 360" width="100%" height="auto" xmlns="http://www.w3.org/2000/svg">
          <rect width="900" height="360" rx="12" fill="#0b0f19" stroke="#1e293b" stroke-width="2"/>
          <text x="450" y="32" fill="#f8fafc" font-size="16" font-weight="bold" text-anchor="middle">Dependency Injection: Service Lifetimes &amp; Captive Dependency Danger</text>
          
          <rect x="50" y="65" width="230" height="250" rx="8" fill="#1e293b" stroke="#3b82f6"/>
          <text x="70" y="95" fill="#60a5fa" font-size="14" font-weight="bold">TRANSIENT</text>
          <text x="70" y="125" fill="#cbd5e1" font-size="11">Created every time resolved</text>
          <text x="70" y="150" fill="#94a3b8" font-size="11">Lightweight, stateless</text>

          <rect x="310" y="65" width="230" height="250" rx="8" fill="#1e293b" stroke="#10b981"/>
          <text x="330" y="95" fill="#34d399" font-size="14" font-weight="bold">SCOPED</text>
          <text x="330" y="125" fill="#cbd5e1" font-size="11">1 instance per HTTP request</text>
          <text x="330" y="150" fill="#94a3b8" font-size="11">DbContext, Unit of Work</text>

          <rect x="570" y="65" width="280" height="250" rx="8" fill="#1e293b" stroke="#f59e0b"/>
          <text x="590" y="95" fill="#fbbf24" font-size="14" font-weight="bold">SINGLETON</text>
          <text x="590" y="125" fill="#cbd5e1" font-size="11">1 instance per application lifecycle</text>
          
          <rect x="590" y="150" width="240" height="145" rx="6" fill="#450a0a" stroke="#ef4444"/>
          <text x="605" y="175" fill="#f87171" font-size="12" font-weight="bold">⚠️ Captive Dependency</text>
          <text x="605" y="195" fill="#fca5a5" font-size="10">Singleton injects Scoped service</text>
          <text x="605" y="215" fill="#fca5a5" font-size="10">➔ Scoped service captured forever!</text>
          <text x="605" y="235" fill="#fca5a5" font-size="10">➔ Stale DbContext &amp; Memory Leaks</text>
          <text x="605" y="265" fill="#6ee7b7" font-size="10">Fix: Use IServiceScopeFactory</text>
        </svg>
        """
    elif day_num == 7:
        return """
        <svg viewBox="0 0 900 360" width="100%" height="auto" xmlns="http://www.w3.org/2000/svg">
          <rect width="900" height="360" rx="12" fill="#0b0f19" stroke="#1e293b" stroke-width="2"/>
          <text x="450" y="32" fill="#f8fafc" font-size="16" font-weight="bold" text-anchor="middle">ASP.NET Core Minimal APIs: Route Tree Matching &amp; Endpoint Filters</text>
          
          <rect x="50" y="65" width="240" height="250" rx="8" fill="#1e293b" stroke="#3b82f6"/>
          <text x="70" y="95" fill="#60a5fa" font-size="14" font-weight="bold">1. RDG Route Matching</text>
          <text x="70" y="125" fill="#cbd5e1" font-size="11">Source-generated RDG</text>
          <text x="70" y="145" fill="#38bdf8" font-size="11">No MVC controller reflection</text>
          <text x="70" y="175" fill="#94a3b8" font-size="11">Fast Radix Tree traversal</text>

          <rect x="320" y="65" width="260" height="250" rx="8" fill="#1e293b" stroke="#8b5cf6"/>
          <text x="340" y="95" fill="#c084fc" font-size="14" font-weight="bold">2. IEndpointFilter Pipeline</text>
          <rect x="340" y="115" width="220" height="50" rx="4" fill="#0f172a"/><text x="350" y="145" fill="#cbd5e1" font-size="11">Filter 1: Stopwatch Timing</text>
          <rect x="340" y="175" width="220" height="50" rx="4" fill="#0f172a"/><text x="350" y="205" fill="#fbbf24" font-size="11">Filter 2: Fluent Validation</text>
          <text x="340" y="255" fill="#f87171" font-size="11">Short-circuit: 400 Bad Request</text>

          <rect x="610" y="65" width="240" height="250" rx="8" fill="#1e293b" stroke="#10b981"/>
          <text x="630" y="95" fill="#34d399" font-size="14" font-weight="bold">3. TypedResults Union</text>
          <text x="630" y="125" fill="#cbd5e1" font-size="11">Results&lt;Ok&lt;T&gt;, NotFound&gt;</text>
          <text x="630" y="155" fill="#10b981" font-size="11">Compile-time type safety</text>
          <text x="630" y="185" fill="#38bdf8" font-size="11">Automated OpenAPI schemas</text>
        </svg>
        """
    elif day_num == 8:
        return """
        <svg viewBox="0 0 900 360" width="100%" height="auto" xmlns="http://www.w3.org/2000/svg">
          <rect width="900" height="360" rx="12" fill="#0b0f19" stroke="#1e293b" stroke-width="2"/>
          <text x="450" y="32" fill="#f8fafc" font-size="16" font-weight="bold" text-anchor="middle">PostgreSQL Relational Storage: 8KB Page Layout &amp; B-Tree Index</text>
          
          <rect x="50" y="65" width="400" height="250" rx="8" fill="#1e293b" stroke="#3b82f6"/>
          <text x="70" y="95" fill="#60a5fa" font-size="14" font-weight="bold">8KB HEAP PAGE INTERNALS</text>
          <rect x="70" y="110" width="360" height="30" fill="#1e3a8a"/><text x="80" y="130" fill="#93c5fd" font-size="11">PageHeaderData (24 bytes): pd_lsn, pd_lower, pd_upper</text>
          <rect x="70" y="145" width="360" height="30" fill="#064e3b"/><text x="80" y="165" fill="#a7f3d0" font-size="11">ItemId[] Array (Line pointers: offset + length) ➔ Grows Down ▼</text>
          <rect x="70" y="180" width="360" height="40" fill="#0f172a" stroke="#334155" stroke-dasharray="2,2"/><text x="180" y="205" fill="#64748b" font-size="11">FREE SPACE GAP</text>
          <rect x="70" y="225" width="360" height="35" fill="#312e81"/><text x="80" y="247" fill="#c7d2fe" font-size="11">HeapTuple 2 (grows Up ▲ from page bottom)</text>
          <rect x="70" y="265" width="360" height="35" fill="#312e81"/><text x="80" y="287" fill="#c7d2fe" font-size="11">HeapTuple 1: [xmin][xmax][infomask][Data Columns]</text>

          <rect x="490" y="65" width="360" height="250" rx="8" fill="#1e293b" stroke="#10b981"/>
          <text x="510" y="95" fill="#34d399" font-size="14" font-weight="bold">B-TREE INDEX NAVIGATION</text>
          <rect x="620" y="115" width="100" height="35" rx="4" fill="#0f172a" stroke="#38bdf8"/><text x="645" y="137" fill="#38bdf8" font-size="11">ROOT</text>
          <rect x="530" y="180" width="120" height="35" rx="4" fill="#0f172a" stroke="#818cf8"/><text x="555" y="202" fill="#818cf8" font-size="11">Internal L1</text>
          <rect x="690" y="180" width="120" height="35" rx="4" fill="#0f172a" stroke="#818cf8"/><text x="715" y="202" fill="#818cf8" font-size="11">Internal L2</text>
          <rect x="510" y="250" width="100" height="35" rx="4" fill="#064e3b" stroke="#34d399"/><text x="525" y="272" fill="#34d399" font-size="11">Leaf (ctid)</text>
          <rect x="630" y="250" width="100" height="35" rx="4" fill="#064e3b" stroke="#34d399"/><text x="645" y="272" fill="#34d399" font-size="11">Leaf (ctid)</text>
          <rect x="750" y="250" width="100" height="35" rx="4" fill="#064e3b" stroke="#34d399"/><text x="765" y="272" fill="#34d399" font-size="11">Leaf (ctid)</text>
        </svg>
        """
    elif day_num == 9:
        return """
        <svg viewBox="0 0 900 360" width="100%" height="auto" xmlns="http://www.w3.org/2000/svg">
          <rect width="900" height="360" rx="12" fill="#0b0f19" stroke="#1e293b" stroke-width="2"/>
          <text x="450" y="32" fill="#f8fafc" font-size="16" font-weight="bold" text-anchor="middle">PostgreSQL EXPLAIN (ANALYZE, BUFFERS) Execution Plan Tree</text>
          
          <rect x="330" y="60" width="240" height="60" rx="8" fill="#1e293b" stroke="#3b82f6"/>
          <text x="350" y="85" fill="#60a5fa" font-size="13" font-weight="bold">HASH JOIN</text>
          <text x="350" y="105" fill="#94a3b8" font-size="11">Cost: 15.2..450.8 | Rows: 1,200</text>

          <line x1="390" y1="120" x2="250" y2="170" stroke="#475569" stroke-width="2"/>
          <line x1="510" y1="120" x2="650" y2="170" stroke="#475569" stroke-width="2"/>

          <rect x="130" y="170" width="250" height="70" rx="8" fill="#1e293b" stroke="#10b981"/>
          <text x="145" y="195" fill="#34d399" font-size="12" font-weight="bold">INDEX SCAN (orders_cust_id_idx)</text>
          <text x="145" y="215" fill="#94a3b8" font-size="10">Buffers: shared hit=428 read=0 (100% RAM)</text>
          <text x="145" y="230" fill="#38bdf8" font-size="10">Fast Selective Lookup</text>

          <rect x="520" y="170" width="250" height="70" rx="8" fill="#1e293b" stroke="#f59e0b"/>
          <text x="535" y="195" fill="#fbbf24" font-size="12" font-weight="bold">HASH ➔ SEQ SCAN (customers)</text>
          <text x="535" y="215" fill="#94a3b8" font-size="10">Buffers: shared hit=18 read=2</text>
          <text x="535" y="230" fill="#f87171" font-size="10">work_mem check: In-Memory (No Spill)</text>

          <rect x="130" y="270" width="640" height="55" rx="6" fill="#0f172a" stroke="#334155"/>
          <text x="150" y="295" fill="#38bdf8" font-size="12" font-weight="bold">Diagnostic Checks:</text>
          <text x="150" y="313" fill="#cbd5e1" font-size="11">✅ Buffer Hit Ratio: 99.5%  |  ✅ Work_Mem: No Disk Spill  |  ✅ Cardinality Skew: 1.05x (Healthy)</text>
        </svg>
        """
    elif day_num == 10:
        return """
        <svg viewBox="0 0 900 360" width="100%" height="auto" xmlns="http://www.w3.org/2000/svg">
          <rect width="900" height="360" rx="12" fill="#0b0f19" stroke="#1e293b" stroke-width="2"/>
          <text x="450" y="32" fill="#f8fafc" font-size="16" font-weight="bold" text-anchor="middle">PostgreSQL MVCC Multi-Version Tuple Chaining &amp; Snapshot Visibility</text>
          
          <rect x="60" y="70" width="340" height="150" rx="8" fill="#1e293b" stroke="#3b82f6"/>
          <text x="80" y="95" fill="#60a5fa" font-size="13" font-weight="bold">TUPLE VERSION 1 (Old)</text>
          <text x="80" y="120" fill="#94a3b8" font-size="11">xmin: 100 (Created by Tx 100)</text>
          <text x="80" y="140" fill="#f87171" font-size="11">xmax: 105 (Superseded by Tx 105)</text>
          <text x="80" y="165" fill="#cbd5e1" font-size="12">Data: Alice | Balance: $500</text>
          <text x="80" y="195" fill="#38bdf8" font-size="11">ctid ➔ points to Tuple V2</text>

          <path d="M 400 145 L 490 145" stroke="#38bdf8" stroke-width="3" stroke-dasharray="4,4"/>
          <polygon points="495,145 485,140 485,150" fill="#38bdf8"/>

          <rect x="500" y="70" width="340" height="150" rx="8" fill="#1e293b" stroke="#10b981"/>
          <text x="520" y="95" fill="#34d399" font-size="13" font-weight="bold">TUPLE VERSION 2 (Current)</text>
          <text x="520" y="120" fill="#34d399" font-size="11">xmin: 105 (Created by Tx 105)</text>
          <text x="520" y="140" fill="#94a3b8" font-size="11">xmax: 0 (Live tuple, un-deleted)</text>
          <text x="520" y="165" fill="#cbd5e1" font-size="12">Data: Alice | Balance: $750</text>
          <text x="520" y="195" fill="#34d399" font-size="11">ctid ➔ points to self</text>

          <rect x="60" y="245" width="780" height="80" rx="6" fill="#0f172a" stroke="#334155"/>
          <text x="80" y="270" fill="#fbbf24" font-size="12" font-weight="bold">Snapshot Isolation Rule:</text>
          <text x="80" y="292" fill="#cbd5e1" font-size="11">• Tx started at timestamp 102 ➔ Sees Tuple V1 (Tx 105 was not yet committed). Readers never block writers!</text>
          <text x="80" y="312" fill="#cbd5e1" font-size="11">• Tx started at timestamp 108 ➔ Sees Tuple V2. Vacuum reclaims V1 only after Tx 102 completes.</text>
        </svg>
        """
    elif day_num == 11:
        return """
        <svg viewBox="0 0 900 360" width="100%" height="auto" xmlns="http://www.w3.org/2000/svg">
          <rect width="900" height="360" rx="12" fill="#0b0f19" stroke="#1e293b" stroke-width="2"/>
          <text x="450" y="32" fill="#f8fafc" font-size="16" font-weight="bold" text-anchor="middle">EF Core Change Tracking &amp; AsNoTrackingWithIdentityResolution</text>
          
          <rect x="50" y="65" width="370" height="250" rx="8" fill="#1e293b" stroke="#ef4444"/>
          <text x="70" y="95" fill="#f87171" font-size="14" font-weight="bold">AsNoTracking() (Standard)</text>
          <text x="70" y="120" fill="#94a3b8" font-size="11">Orders.AsNoTracking().Include(o => o.Customer)</text>
          <rect x="70" y="135" width="330" height="45" rx="4" fill="#0f172a"/><text x="85" y="162" fill="#cbd5e1" font-size="11">Order 1 ➔ Customer Instance #A (0x10A)</text>
          <rect x="70" y="190" width="330" height="45" rx="4" fill="#0f172a"/><text x="85" y="217" fill="#cbd5e1" font-size="11">Order 2 ➔ Customer Instance #B (0x20B)</text>
          <text x="70" y="260" fill="#f87171" font-size="12">❌ ReferenceEquals is FALSE!</text>
          <text x="70" y="280" fill="#94a3b8" font-size="11">Duplicate objects created on heap for same ID.</text>

          <rect x="480" y="65" width="370" height="250" rx="8" fill="#1e293b" stroke="#10b981"/>
          <text x="500" y="95" fill="#34d399" font-size="14" font-weight="bold">AsNoTrackingWithIdentityResolution()</text>
          <text x="500" y="120" fill="#94a3b8" font-size="11">Uses query-scoped ephemeral identity map</text>
          <rect x="500" y="135" width="330" height="45" rx="4" fill="#0f172a"/><text x="515" y="162" fill="#cbd5e1" font-size="11">Order 1 ➔ Customer Instance #A (0x10A)</text>
          <rect x="500" y="190" width="330" height="45" rx="4" fill="#0f172a"/><text x="515" y="217" fill="#cbd5e1" font-size="11">Order 2 ➔ Reuses Instance #A (0x10A)</text>
          <text x="500" y="260" fill="#34d399" font-size="12">✅ ReferenceEquals is TRUE!</text>
          <text x="500" y="280" fill="#94a3b8" font-size="11">Consistent object graph with zero tracking overhead.</text>
        </svg>
        """
    elif day_num == 12:
        return """
        <svg viewBox="0 0 900 360" width="100%" height="auto" xmlns="http://www.w3.org/2000/svg">
          <rect width="900" height="360" rx="12" fill="#0b0f19" stroke="#1e293b" stroke-width="2"/>
          <text x="450" y="32" fill="#f8fafc" font-size="16" font-weight="bold" text-anchor="middle">Cartesian Product Explosion vs. EF Core Split Queries (.AsSplitQuery())</text>
          
          <rect x="50" y="65" width="370" height="250" rx="8" fill="#1e293b" stroke="#ef4444"/>
          <text x="70" y="95" fill="#f87171" font-size="14" font-weight="bold">SINGLE QUERY (.AsSingleQuery())</text>
          <text x="70" y="120" fill="#94a3b8" font-size="11">Orders ➔ LEFT JOIN Items ➔ LEFT JOIN Shipments</text>
          <rect x="70" y="140" width="330" height="85" rx="6" fill="#450a0a" stroke="#ef4444"/>
          <text x="85" y="165" fill="#fca5a5" font-size="12" font-weight="bold">Cartesian Row Multiplication:</text>
          <text x="85" y="188" fill="#fca5a5" font-size="11">10 Orders × 20 Items × 4 Shipments</text>
          <text x="85" y="210" fill="#ffffff" font-size="14" font-weight="bold">= 800 Redundant Rows Transferred!</text>
          <text x="70" y="255" fill="#ef4444" font-size="11">❌ High network latency, buffer bloat &amp; Gen 2 GC</text>

          <rect x="480" y="65" width="370" height="250" rx="8" fill="#1e293b" stroke="#10b981"/>
          <text x="500" y="95" fill="#34d399" font-size="14" font-weight="bold">SPLIT QUERY (.AsSplitQuery())</text>
          <text x="500" y="120" fill="#94a3b8" font-size="11">Emits 3 independent queries over connection</text>
          <rect x="500" y="140" width="330" height="85" rx="6" fill="#064e3b" stroke="#10b981"/>
          <text x="515" y="165" fill="#a7f3d0" font-size="11">Query 1: Orders (10 rows)</text>
          <text x="515" y="185" fill="#a7f3d0" font-size="11">Query 2: Items (200 rows)</text>
          <text x="515" y="205" fill="#a7f3d0" font-size="11">Query 3: Shipments (40 rows)</text>
          <text x="500" y="250" fill="#34d399" font-size="13" font-weight="bold">= 250 Total Rows (68.7% Network Reduction!)</text>
          <text x="500" y="275" fill="#94a3b8" font-size="11">Intercepted via DbCommandInterceptor</text>
        </svg>
        """
    elif day_num == 13:
        return """
        <svg viewBox="0 0 900 360" width="100%" height="auto" xmlns="http://www.w3.org/2000/svg">
          <rect width="900" height="360" rx="12" fill="#0b0f19" stroke="#1e293b" stroke-width="2"/>
          <text x="450" y="32" fill="#f8fafc" font-size="16" font-weight="bold" text-anchor="middle">Dapper Architecture: DynamicMethod IL Generation &amp; Streaming</text>
          
          <rect x="50" y="65" width="230" height="250" rx="8" fill="#1e293b" stroke="#3b82f6"/>
          <text x="70" y="95" fill="#60a5fa" font-size="14" font-weight="bold">1. SQL &amp; Parameters</text>
          <text x="70" y="125" fill="#cbd5e1" font-size="11">conn.QueryAsync&lt;Order&gt;(sql)</text>
          <text x="70" y="150" fill="#94a3b8" font-size="11">Executes on raw ADO.NET</text>
          <text x="70" y="180" fill="#38bdf8" font-size="11">Multi-mapping with splitOn</text>
          <text x="70" y="205" fill="#94a3b8" font-size="10">Deduplicates 1:N relations</text>

          <rect x="320" y="65" width="260" height="250" rx="8" fill="#1e293b" stroke="#8b5cf6"/>
          <text x="340" y="95" fill="#c084fc" font-size="14" font-weight="bold">2. DynamicMethod IL</text>
          <rect x="340" y="115" width="220" height="50" rx="4" fill="#0f172a"/><text x="350" y="145" fill="#cbd5e1" font-size="11">Emits MSIL at runtime</text>
          <rect x="340" y="175" width="220" height="50" rx="4" fill="#0f172a"/><text x="350" y="205" fill="#34d399" font-size="11">Caches Func&lt;IDataReader, T&gt;</text>
          <text x="340" y="255" fill="#c084fc" font-size="11">Zero reflection on repeat queries</text>

          <rect x="620" y="65" width="230" height="250" rx="8" fill="#1e293b" stroke="#10b981"/>
          <text x="640" y="95" fill="#34d399" font-size="14" font-weight="bold">3. Memory Modes</text>
          <rect x="640" y="115" width="190" height="55" rx="4" fill="#0f172a"/><text x="650" y="137" fill="#fbbf24" font-size="11" font-weight="bold">buffered: true</text><text x="650" y="157" fill="#94a3b8" font-size="10">Eager list in memory</text>
          <rect x="640" y="185" width="190" height="55" rx="4" fill="#0f172a"/><text x="650" y="207" fill="#34d399" font-size="11" font-weight="bold">IAsyncEnumerable&lt;T&gt;</text><text x="650" y="227" fill="#94a3b8" font-size="10">Non-allocating streaming</text>
          <text x="640" y="275" fill="#34d399" font-size="11">O(1) memory for millions of rows</text>
        </svg>
        """
    elif day_num == 14:
        return """
        <svg viewBox="0 0 900 360" width="100%" height="auto" xmlns="http://www.w3.org/2000/svg">
          <rect width="900" height="360" rx="12" fill="#0b0f19" stroke="#1e293b" stroke-width="2"/>
          <text x="450" y="32" fill="#f8fafc" font-size="16" font-weight="bold" text-anchor="middle">Zero-Downtime Migration Architecture &amp; Expand-Contract Pipeline</text>
          
          <!-- Phase 1: Expand -->
          <rect x="40" y="65" width="250" height="250" rx="8" fill="#1e293b" stroke="#3b82f6"/>
          <text x="60" y="95" fill="#60a5fa" font-size="14" font-weight="bold">Phase 1: Expand</text>
          <rect x="55" y="115" width="220" height="40" rx="4" fill="#0f172a"/><text x="65" y="140" fill="#cbd5e1" font-size="11">Add New Column (NULL)</text>
          <rect x="55" y="165" width="220" height="50" rx="4" fill="#0f172a"/><text x="65" y="185" fill="#34d399" font-size="11">App V1.5: Dual Write</text><text x="65" y="205" fill="#94a3b8" font-size="10">Writes to Old &amp; New Column</text>
          <text x="60" y="250" fill="#94a3b8" font-size="11">Idempotent script: -i</text>
          <text x="60" y="275" fill="#38bdf8" font-size="11">Checks __EFMigrationsHistory</text>

          <!-- Phase 2: Backfill -->
          <rect x="325" y="65" width="250" height="250" rx="8" fill="#1e293b" stroke="#8b5cf6"/>
          <text x="345" y="95" fill="#c084fc" font-size="14" font-weight="bold">Phase 2: Backfill</text>
          <rect x="340" y="115" width="220" height="50" rx="4" fill="#0f172a"/><text x="350" y="137" fill="#cbd5e1" font-size="11">Async Background Job</text><text x="350" y="155" fill="#94a3b8" font-size="10">Batch 5,000 rows / tx</text>
          <rect x="340" y="175" width="220" height="50" rx="4" fill="#0f172a"/><text x="350" y="197" fill="#fbbf24" font-size="11">Advisory Lock Sync</text><text x="350" y="215" fill="#94a3b8" font-size="10">Prevents concurrent DDL</text>
          <text x="345" y="260" fill="#c084fc" font-size="11">No table lock escalation</text>
          <text x="345" y="285" fill="#94a3b8" font-size="10">Zero impact on read traffic</text>

          <!-- Phase 3: Contract -->
          <rect x="610" y="65" width="250" height="250" rx="8" fill="#1e293b" stroke="#10b981"/>
          <text x="630" y="95" fill="#34d399" font-size="14" font-weight="bold">Phase 3: Contract</text>
          <rect x="625" y="115" width="220" height="50" rx="4" fill="#0f172a"/><text x="635" y="137" fill="#34d399" font-size="11">App V2: Reads New Col</text><text x="635" y="155" fill="#94a3b8" font-size="10">All V1 pods terminated</text>
          <rect x="625" y="175" width="220" height="50" rx="4" fill="#0f172a"/><text x="635" y="197" fill="#ef4444" font-size="11">Drop Old Column</text><text x="635" y="215" fill="#94a3b8" font-size="10">Metadata lock safe</text>
          <text x="630" y="260" fill="#34d399" font-size="11">Zero-Downtime achieved</text>
          <text x="630" y="285" fill="#94a3b8" font-size="10">Clean final schema</text>
        </svg>
        """
    elif day_num == 15:
        return """
        <svg viewBox="0 0 900 360" width="100%" height="auto" xmlns="http://www.w3.org/2000/svg">
          <rect width="900" height="360" rx="12" fill="#0b0f19" stroke="#1e293b" stroke-width="2"/>
          <text x="450" y="32" fill="#f8fafc" font-size="16" font-weight="bold" text-anchor="middle">System.Threading.Channels: Bounded Producer-Consumer Pipeline</text>
          
          <!-- Producers -->
          <rect x="40" y="65" width="220" height="250" rx="8" fill="#1e293b" stroke="#3b82f6"/>
          <text x="60" y="95" fill="#60a5fa" font-size="14" font-weight="bold">Concurrent Producers</text>
          <rect x="55" y="115" width="190" height="40" rx="4" fill="#0f172a"/><text x="65" y="140" fill="#cbd5e1" font-size="11">Producer #1 (API)</text>
          <rect x="55" y="165" width="190" height="40" rx="4" fill="#0f172a"/><text x="65" y="190" fill="#cbd5e1" font-size="11">Producer #2 (Worker)</text>
          <rect x="55" y="215" width="190" height="40" rx="4" fill="#0f172a"/><text x="65" y="240" fill="#cbd5e1" font-size="11">Producer #N (Event)</text>
          <text x="60" y="285" fill="#38bdf8" font-size="11">channel.Writer.WriteAsync()</text>

          <!-- Bounded Queue -->
          <rect x="290" y="65" width="320" height="250" rx="8" fill="#1e293b" stroke="#f59e0b"/>
          <text x="310" y="95" fill="#fbbf24" font-size="14" font-weight="bold">Bounded Channel Buffer</text>
          <text x="310" y="120" fill="#94a3b8" font-size="11">Capacity: N | Lock-Free Ring Buffer</text>
          
          <rect x="310" y="140" width="50" height="50" rx="6" fill="#0f172a" stroke="#10b981"/><text x="325" y="170" fill="#34d399" font-size="12">Item 1</text>
          <rect x="370" y="140" width="50" height="50" rx="6" fill="#0f172a" stroke="#10b981"/><text x="385" y="170" fill="#34d399" font-size="12">Item 2</text>
          <rect x="430" y="140" width="50" height="50" rx="6" fill="#0f172a" stroke="#10b981"/><text x="445" y="170" fill="#34d399" font-size="12">Item 3</text>
          <rect x="490" y="140" width="50" height="50" rx="6" fill="#0f172a" stroke="#3b82f6"/><text x="505" y="170" fill="#60a5fa" font-size="12">Wait...</text>
          <rect x="550" y="140" width="45" height="50" rx="6" fill="#0f172a" stroke="#64748b"/><text x="560" y="170" fill="#94a3b8" font-size="12">Free</text>

          <rect x="310" y="210" width="280" height="50" rx="4" fill="#0f172a"/>
          <text x="320" y="230" fill="#fbbf24" font-size="11" font-weight="bold">Backpressure FullMode:</text>
          <text x="320" y="248" fill="#94a3b8" font-size="10">Wait | DropOldest | DropWrite</text>
          <text x="310" y="290" fill="#fbbf24" font-size="11">Zero Allocation on reader handoff</text>

          <!-- Consumers -->
          <rect x="640" y="65" width="220" height="250" rx="8" fill="#1e293b" stroke="#10b981"/>
          <text x="660" y="95" fill="#34d399" font-size="14" font-weight="bold">Concurrent Consumers</text>
          <rect x="655" y="115" width="190" height="40" rx="4" fill="#0f172a"/><text x="665" y="140" fill="#cbd5e1" font-size="11">Consumer #1</text>
          <rect x="655" y="165" width="190" height="40" rx="4" fill="#0f172a"/><text x="665" y="190" fill="#cbd5e1" font-size="11">Consumer #2</text>
          <rect x="655" y="215" width="190" height="40" rx="4" fill="#0f172a"/><text x="665" y="240" fill="#cbd5e1" font-size="11">Consumer #M</text>
          <text x="660" y="285" fill="#34d399" font-size="11">channel.Reader.ReadAllAsync()</text>
        </svg>
        """
    elif day_num == 16:
        return """
        <svg viewBox="0 0 900 360" width="100%" height="auto" xmlns="http://www.w3.org/2000/svg">
          <rect width="900" height="360" rx="12" fill="#0b0f19" stroke="#1e293b" stroke-width="2"/>
          <text x="450" y="32" fill="#f8fafc" font-size="16" font-weight="bold" text-anchor="middle">Synchronization Primitives &amp; Distributed Locking Hierarchy</text>
          
          <!-- In-Process Lock-Free -->
          <rect x="40" y="65" width="250" height="250" rx="8" fill="#1e293b" stroke="#3b82f6"/>
          <text x="60" y="95" fill="#60a5fa" font-size="14" font-weight="bold">1. Interlocked (Hardware)</text>
          <rect x="55" y="115" width="220" height="45" rx="4" fill="#0f172a"/><text x="65" y="137" fill="#cbd5e1" font-size="11">CPU Bus Lock (CMPXCHG)</text><text x="65" y="152" fill="#94a3b8" font-size="10">Latency: ~5-10 ns</text>
          <rect x="55" y="170" width="220" height="45" rx="4" fill="#0f172a"/><text x="65" y="192" fill="#34d399" font-size="11">CompareExchange Loop</text><text x="65" y="207" fill="#94a3b8" font-size="10">Optimistic state updates</text>
          <text x="60" y="250" fill="#38bdf8" font-size="11">Lock-free atomic counters</text>
          <text x="60" y="275" fill="#94a3b8" font-size="10">Zero thread suspension</text>

          <!-- In-Process Async Throttle -->
          <rect x="325" y="65" width="250" height="250" rx="8" fill="#1e293b" stroke="#8b5cf6"/>
          <text x="345" y="95" fill="#c084fc" font-size="14" font-weight="bold">2. SemaphoreSlim &amp; RWLock</text>
          <rect x="340" y="115" width="220" height="45" rx="4" fill="#0f172a"/><text x="350" y="137" fill="#c084fc" font-size="11">SemaphoreSlim.WaitAsync()</text><text x="350" y="152" fill="#94a3b8" font-size="10">Cooperative async waiter queue</text>
          <rect x="340" y="170" width="220" height="45" rx="4" fill="#0f172a"/><text x="350" y="192" fill="#fbbf24" font-size="11">ReaderWriterLockSlim</text><text x="350" y="207" fill="#94a3b8" font-size="10">Shared read, exclusive write</text>
          <text x="345" y="250" fill="#c084fc" font-size="11">Disposable Lease Pattern</text>
          <text x="345" y="275" fill="#94a3b8" font-size="10">Prevents permit leakage</text>

          <!-- Distributed Lock -->
          <rect x="610" y="65" width="250" height="250" rx="8" fill="#1e293b" stroke="#10b981"/>
          <text x="630" y="95" fill="#34d399" font-size="14" font-weight="bold">3. Distributed Lock &amp; Fencing</text>
          <rect x="625" y="115" width="220" height="45" rx="4" fill="#0f172a"/><text x="635" y="137" fill="#34d399" font-size="11">Redis Redlock Quorum</text><text x="635" y="152" fill="#94a3b8" font-size="10">SET resource token NX PX ttl</text>
          <rect x="625" y="170" width="220" height="45" rx="4" fill="#0f172a"/><text x="635" y="192" fill="#ef4444" font-size="11">Fencing Token (Monotonic)</text><text x="635" y="207" fill="#94a3b8" font-size="10">Rejects GC-paused zombie writes</text>
          <text x="630" y="250" fill="#34d399" font-size="11">Multi-pod mutual exclusion</text>
          <text x="630" y="275" fill="#94a3b8" font-size="10">Guarantees distributed safety</text>
        </svg>
        """
    elif day_num == 17:
        return """
        <svg viewBox="0 0 900 360" width="100%" height="auto" xmlns="http://www.w3.org/2000/svg">
          <rect width="900" height="360" rx="12" fill="#0b0f19" stroke="#1e293b" stroke-width="2"/>
          <text x="450" y="32" fill="#f8fafc" font-size="16" font-weight="bold" text-anchor="middle">Redis Architecture, Caching Topologies &amp; Stampede Protection</text>
          
          <!-- Topologies -->
          <rect x="40" y="65" width="260" height="250" rx="8" fill="#1e293b" stroke="#3b82f6"/>
          <text x="60" y="95" fill="#60a5fa" font-size="14" font-weight="bold">Cache Topologies</text>
          <rect x="55" y="115" width="230" height="40" rx="4" fill="#0f172a"/><text x="65" y="135" fill="#38bdf8" font-size="11">Cache-Aside (Lazy)</text><text x="65" y="148" fill="#94a3b8" font-size="9">Read cache; on miss query DB</text>
          <rect x="55" y="165" width="230" height="40" rx="4" fill="#0f172a"/><text x="65" y="185" fill="#34d399" font-size="11">Write-Through</text><text x="65" y="198" fill="#94a3b8" font-size="9">Sync write cache + DB together</text>
          <rect x="55" y="215" width="230" height="40" rx="4" fill="#0f172a"/><text x="65" y="235" fill="#c084fc" font-size="11">Write-Behind (Write-Back)</text><text x="65" y="248" fill="#94a3b8" font-size="9">Async queue batch DB flush</text>
          <text x="60" y="285" fill="#94a3b8" font-size="11">Singleton ConnectionMultiplexer</text>

          <!-- Cache Stampede / Thundering Herd -->
          <rect x="330" y="65" width="270" height="250" rx="8" fill="#1e293b" stroke="#ef4444"/>
          <text x="350" y="95" fill="#f87171" font-size="14" font-weight="bold">Cache Stampede Hazards</text>
          <rect x="345" y="115" width="240" height="60" rx="4" fill="#0f172a"/>
          <text x="355" y="135" fill="#ef4444" font-size="11" font-weight="bold">Key Expiration under Load</text>
          <text x="355" y="152" fill="#cbd5e1" font-size="10">5,000 req/sec hit DB simultaneously</text>
          <text x="355" y="167" fill="#94a3b8" font-size="10">DB CPU spikes to 100%, cascading outage</text>
          
          <rect x="345" y="185" width="240" height="60" rx="4" fill="#0f172a"/>
          <text x="355" y="205" fill="#fbbf24" font-size="11" font-weight="bold">Cache Avalanche &amp; Jitter</text>
          <text x="355" y="222" fill="#cbd5e1" font-size="10">All keys expire at midnight</text>
          <text x="355" y="237" fill="#94a3b8" font-size="10">Mitigate: TTL +/- random jitter</text>
          <text x="350" y="285" fill="#f87171" font-size="11">Prevent single-point failure</text>

          <!-- Mitigation: Mutex & XFetch -->
          <rect x="630" y="65" width="230" height="250" rx="8" fill="#1e293b" stroke="#10b981"/>
          <text x="650" y="95" fill="#34d399" font-size="14" font-weight="bold">Mitigation Solutions</text>
          <rect x="645" y="115" width="200" height="55" rx="4" fill="#0f172a"/>
          <text x="655" y="135" fill="#60a5fa" font-size="11" font-weight="bold">1. Mutex (Single-Flight)</text>
          <text x="655" y="155" fill="#94a3b8" font-size="10">1 worker loads DB; others await</text>
          
          <rect x="645" y="180" width="200" height="55" rx="4" fill="#0f172a"/>
          <text x="655" y="200" fill="#34d399" font-size="11" font-weight="bold">2. XFetch Algorithm</text>
          <text x="655" y="215" fill="#cbd5e1" font-size="9">delta * beta * -ln(rand) &gt; expiry</text>
          <text x="655" y="228" fill="#94a3b8" font-size="9">Probabilistic background refresh</text>
          <text x="650" y="275" fill="#34d399" font-size="11">Zero DB stampede guaranteed</text>
        </svg>
        """
    elif day_num == 18:
        return """
        <svg viewBox="0 0 920 380" width="100%" height="auto" xmlns="http://www.w3.org/2000/svg">
          <defs>
            <linearGradient id="d18GradBg" x1="0%" y1="0%" x2="100%" y2="100%">
              <stop offset="0%" stop-color="#0b0f19"/><stop offset="100%" stop-color="#111827"/>
            </linearGradient>
            <linearGradient id="d18GradDS" x1="0%" y1="0%" x2="100%" y2="100%">
              <stop offset="0%" stop-color="#1e293b"/><stop offset="100%" stop-color="#0f172a"/>
            </linearGradient>
            <linearGradient id="d18GradEvict" x1="0%" y1="0%" x2="100%" y2="100%">
              <stop offset="0%" stop-color="#31101b"/><stop offset="100%" stop-color="#180c14"/>
            </linearGradient>
            <linearGradient id="d18GradTree" x1="0%" y1="0%" x2="100%" y2="100%">
              <stop offset="0%" stop-color="#064e3b"/><stop offset="100%" stop-color="#0b1e19"/>
            </linearGradient>
            <filter id="d18Glow"><feGaussianBlur stdDeviation="3" result="blur"/><feMerge><feMergeNode in="blur"/><feMergeNode in="SourceGraphic"/></feMerge></filter>
          </defs>
          <rect width="920" height="380" rx="12" fill="url(#d18GradBg)" stroke="#1e293b" stroke-width="2"/>
          <text x="460" y="32" fill="#f8fafc" font-size="16" font-weight="bold" text-anchor="middle">Redis Data Structures Architecture &amp; Memory Eviction Mechanics</text>

          <!-- 1. Six Core Data Structures Column -->
          <rect x="30" y="60" width="280" height="300" rx="10" fill="url(#d18GradDS)" stroke="#38bdf8" stroke-width="1.5"/>
          <text x="50" y="88" fill="#38bdf8" font-size="14" font-weight="bold">Core Data Structures</text>
          
          <rect x="45" y="105" width="250" height="34" rx="5" fill="#0f172a" stroke="#334155"/>
          <text x="55" y="122" fill="#60a5fa" font-size="11" font-weight="bold">Strings (SDS)</text>
          <text x="55" y="133" fill="#94a3b8" font-size="9">len, alloc, buf[] | INCRBY atomic counters</text>

          <rect x="45" y="145" width="250" height="34" rx="5" fill="#0f172a" stroke="#334155"/>
          <text x="55" y="162" fill="#34d399" font-size="11" font-weight="bold">Hashes (ListPack / Dict)</text>
          <text x="55" y="173" fill="#94a3b8" font-size="9">Field granularity | Zero full JSON deserialize</text>

          <rect x="45" y="185" width="250" height="34" rx="5" fill="#0f172a" stroke="#334155"/>
          <text x="55" y="202" fill="#c084fc" font-size="11" font-weight="bold">Sets (IntSet / HashTable)</text>
          <text x="55" y="213" fill="#94a3b8" font-size="9">O(1) deduplication | SINTER mutual tags</text>

          <rect x="45" y="225" width="250" height="34" rx="5" fill="#0f172a" stroke="#334155"/>
          <text x="55" y="242" fill="#fbbf24" font-size="11" font-weight="bold">Sorted Sets (SkipList)</text>
          <text x="55" y="253" fill="#94a3b8" font-size="9">O(log N) Leaderboards &amp; Sliding-window rate limit</text>

          <rect x="45" y="265" width="250" height="34" rx="5" fill="#0f172a" stroke="#334155"/>
          <text x="55" y="282" fill="#f472b6" font-size="11" font-weight="bold">Bitmaps (Bit Operations)</text>
          <text x="55" y="293" fill="#94a3b8" font-size="9">1 bit per user | Ultra-compact DAU tracking</text>

          <rect x="45" y="305" width="250" height="42" rx="5" fill="#0f172a" stroke="#38bdf8"/>
          <text x="55" y="322" fill="#38bdf8" font-size="11" font-weight="bold">HyperLogLog (16,384 Registers)</text>
          <text x="55" y="337" fill="#cbd5e1" font-size="9">Fixed 12 KB RAM | 0.81% error unique visitors</text>

          <!-- 2. Memory Eviction Engine Column -->
          <rect x="330" y="60" width="300" height="300" rx="10" fill="url(#d18GradEvict)" stroke="#ef4444" stroke-width="1.5"/>
          <text x="350" y="88" fill="#f87171" font-size="14" font-weight="bold">Eviction Under Memory Pressure</text>

          <rect x="345" y="105" width="270" height="45" rx="5" fill="#0f172a" stroke="#475569"/>
          <text x="355" y="123" fill="#fbbf24" font-size="11" font-weight="bold">Memory Limit Trigger</text>
          <text x="355" y="138" fill="#94a3b8" font-size="9">used_memory &gt;= maxmemory &#8594; Run Eviction Policy</text>

          <rect x="345" y="157" width="270" height="50" rx="5" fill="#0f172a" stroke="#ef4444"/>
          <text x="355" y="175" fill="#f87171" font-size="11" font-weight="bold">Approximate LRU (16-Key Pool)</text>
          <text x="355" y="189" fill="#cbd5e1" font-size="9">Sample K keys (default 5-10) &#8594; Order by idle time</text>
          <text x="355" y="200" fill="#94a3b8" font-size="8">Saves 1.2 GB pointer RAM vs true doubly linked list</text>

          <rect x="345" y="214" width="270" height="50" rx="5" fill="#0f172a" stroke="#c084fc"/>
          <text x="355" y="232" fill="#c084fc" font-size="11" font-weight="bold">LFU: Morris Counter + Time Decay</text>
          <text x="355" y="246" fill="#cbd5e1" font-size="9">16-bit time decay + 8-bit log frequency (0-255)</text>
          <text x="355" y="257" fill="#94a3b8" font-size="8">Prevents cache pollution from burst scans</text>

          <rect x="345" y="271" width="270" height="40" rx="5" fill="#0f172a" stroke="#34d399"/>
          <text x="355" y="289" fill="#34d399" font-size="11" font-weight="bold">Volatile-TTL &amp; NoEviction</text>
          <text x="355" y="302" fill="#94a3b8" font-size="9">Volatile: Evict shortest TTL | NoEvict: Return OOM error</text>

          <text x="350" y="345" fill="#f87171" font-size="11" font-weight="bold">&#9888; Volatile hazard: OOM if persistent keys fill RAM</text>

          <!-- 3. DSA Tree Recursion Column -->
          <rect x="650" y="60" width="240" height="300" rx="10" fill="url(#d18GradTree)" stroke="#10b981" stroke-width="1.5"/>
          <text x="670" y="88" fill="#34d399" font-size="14" font-weight="bold">DSA Tree Metrics O(N)</text>

          <rect x="665" y="105" width="210" height="110" rx="5" fill="#0f172a" stroke="#334155"/>
          <text x="675" y="125" fill="#38bdf8" font-size="11" font-weight="bold">LC #543: Tree Diameter</text>
          <text x="675" y="142" fill="#94a3b8" font-size="9">Longest path between any nodes</text>
          <text x="675" y="157" fill="#cbd5e1" font-size="9">At each node in post-order ascent:</text>
          <text x="675" y="173" fill="#38bdf8" font-size="10" font-weight="bold">diam = max(diam, h_L + h_R)</text>
          <text x="675" y="188" fill="#94a3b8" font-size="9">return 1 + max(h_L, h_R)</text>
          <text x="675" y="202" fill="#34d399" font-size="9">O(N) Time | O(H) Stack</text>

          <rect x="665" y="225" width="210" height="110" rx="5" fill="#0f172a" stroke="#334155"/>
          <text x="675" y="245" fill="#fbbf24" font-size="11" font-weight="bold">LC #110: Balanced Tree</text>
          <text x="675" y="262" fill="#94a3b8" font-size="9">|h_L - h_R| &lt;= 1 for all nodes</text>
          <text x="675" y="278" fill="#cbd5e1" font-size="9">Bottom-up short circuit:</text>
          <text x="675" y="294" fill="#ef4444" font-size="10" font-weight="bold">if |h_L - h_R| &gt; 1 return -1;</text>
          <text x="675" y="309" fill="#94a3b8" font-size="9">Aborts redundant tree traversal</text>
          <text x="675" y="323" fill="#34d399" font-size="9">O(N) vs Top-down O(N^2)</text>
        </svg>
        """
    elif day_num == 19:
        return """
        <svg viewBox="0 0 920 380" width="100%" height="auto" xmlns="http://www.w3.org/2000/svg">
          <defs>
            <linearGradient id="d19GradBg" x1="0%" y1="0%" x2="100%" y2="100%">
              <stop offset="0%" stop-color="#0b0f19"/><stop offset="100%" stop-color="#111827"/>
            </linearGradient>
            <linearGradient id="d19GradQuorum" x1="0%" y1="0%" x2="100%" y2="100%">
              <stop offset="0%" stop-color="#1e293b"/><stop offset="100%" stop-color="#0f172a"/>
            </linearGradient>
            <linearGradient id="d19GradFence" x1="0%" y1="0%" x2="100%" y2="100%">
              <stop offset="0%" stop-color="#2d1537"/><stop offset="100%" stop-color="#150a1c"/>
            </linearGradient>
            <linearGradient id="d19GradTree" x1="0%" y1="0%" x2="100%" y2="100%">
              <stop offset="0%" stop-color="#064e3b"/><stop offset="100%" stop-color="#0b1e19"/>
            </linearGradient>
          </defs>
          <rect width="920" height="380" rx="12" fill="url(#d19GradBg)" stroke="#1e293b" stroke-width="2"/>
          <text x="460" y="32" fill="#f8fafc" font-size="16" font-weight="bold" text-anchor="middle">RedLock Multi-Instance Consensus Architecture &amp; Tree Isomorphism</text>

          <!-- 1. RedLock Quorum Consensus Column -->
          <rect x="30" y="60" width="300" height="300" rx="10" fill="url(#d19GradQuorum)" stroke="#38bdf8" stroke-width="1.5"/>
          <text x="50" y="88" fill="#38bdf8" font-size="14" font-weight="bold">5-Node RedLock Consensus</text>

          <!-- Client Coordinator -->
          <rect x="45" y="105" width="270" height="38" rx="6" fill="#0284c7" stroke="#38bdf8"/>
          <text x="180" y="128" fill="#ffffff" font-size="12" font-weight="bold" text-anchor="middle">Client Lock Coordinator</text>

          <!-- 5 Node Pills -->
          <g transform="translate(45, 155)">
            <rect x="0" y="0" width="48" height="40" rx="4" fill="#0f172a" stroke="#10b981"/><text x="24" y="24" fill="#34d399" font-size="10" font-weight="bold" text-anchor="middle">N1: OK</text>
            <rect x="55" y="0" width="48" height="40" rx="4" fill="#0f172a" stroke="#10b981"/><text x="79" y="24" fill="#34d399" font-size="10" font-weight="bold">N2: OK</text>
            <rect x="110" y="0" width="48" height="40" rx="4" fill="#0f172a" stroke="#10b981"/><text x="134" y="24" fill="#34d399" font-size="10" font-weight="bold">N3: OK</text>
            <rect x="165" y="0" width="48" height="40" rx="4" fill="#0f172a" stroke="#ef4444"/><text x="189" y="24" fill="#f87171" font-size="9" text-anchor="middle">N4: FAIL</text>
            <rect x="220" y="0" width="50" height="40" rx="4" fill="#0f172a" stroke="#64748b"/><text x="245" y="24" fill="#94a3b8" font-size="9" text-anchor="middle">N5: TIMEOUT</text>
          </g>

          <rect x="45" y="208" width="270" height="45" rx="5" fill="#0f172a" stroke="#334155"/>
          <text x="55" y="226" fill="#34d399" font-size="11" font-weight="bold">Quorum Achieved: 3 / 5 Nodes</text>
          <text x="55" y="241" fill="#94a3b8" font-size="9">Required: floor(5/2) + 1 = 3 instances</text>

          <rect x="45" y="260" width="270" height="48" rx="5" fill="#0f172a" stroke="#334155"/>
          <text x="55" y="278" fill="#fbbf24" font-size="11" font-weight="bold">Validity Time Window</text>
          <text x="55" y="293" fill="#cbd5e1" font-size="9">ValTime = TTL - Delta_T - ClockDrift</text>
          <text x="55" y="303" fill="#94a3b8" font-size="8">Must be &gt; 0, otherwise compensation rollback</text>

          <text x="50" y="335" fill="#38bdf8" font-size="10">&#10003; No single point of failure (Zero Master-Replica risk)</text>

          <!-- 2. Safety Guarantees & Fencing Tokens Column -->
          <rect x="350" y="60" width="280" height="300" rx="10" fill="url(#d19GradFence)" stroke="#c084fc" stroke-width="1.5"/>
          <text x="370" y="88" fill="#c084fc" font-size="14" font-weight="bold">Safety &amp; Zombie Mitigation</text>

          <rect x="365" y="105" width="250" height="65" rx="5" fill="#0f172a" stroke="#475569"/>
          <text x="375" y="125" fill="#60a5fa" font-size="11" font-weight="bold">1. Atomic Lua Script Release</text>
          <text x="375" y="142" fill="#cbd5e1" font-size="9">if get(key) == token then del(key)</text>
          <text x="375" y="157" fill="#94a3b8" font-size="9">Guarantees client never deletes foreign lock</text>

          <rect x="365" y="180" width="250" height="65" rx="5" fill="#0f172a" stroke="#475569"/>
          <text x="375" y="200" fill="#34d399" font-size="11" font-weight="bold">2. Lease Watchdog Heartbeat</text>
          <text x="375" y="217" fill="#cbd5e1" font-size="9">Auto-extends lease via PEXPIRE at TTL/3</text>
          <text x="375" y="232" fill="#94a3b8" font-size="9">Cancels immediately on IAsyncDisposable</text>

          <rect x="365" y="255" width="250" height="85" rx="5" fill="#0f172a" stroke="#ef4444"/>
          <text x="375" y="275" fill="#f87171" font-size="11" font-weight="bold">3. Monotonic Fencing Tokens</text>
          <text x="375" y="292" fill="#fbbf24" font-size="10">Kleppmann GC Pause Defense</text>
          <text x="375" y="309" fill="#cbd5e1" font-size="9">Tokens strictly increment: 33 &#8594; 34 &#8594; 35</text>
          <text x="375" y="324" fill="#94a3b8" font-size="9">Storage rejects write if token &lt;= last seen</text>

          <!-- 3. DSA Tree Isomorphism Column -->
          <rect x="650" y="60" width="240" height="300" rx="10" fill="url(#d19GradTree)" stroke="#10b981" stroke-width="1.5"/>
          <text x="670" y="88" fill="#34d399" font-size="14" font-weight="bold">Tree Isomorphism</text>

          <rect x="665" y="105" width="210" height="110" rx="5" fill="#0f172a" stroke="#334155"/>
          <text x="675" y="125" fill="#38bdf8" font-size="11" font-weight="bold">LC #100: Same Tree</text>
          <text x="675" y="142" fill="#94a3b8" font-size="9">Structural &amp; value equality</text>
          <text x="675" y="157" fill="#cbd5e1" font-size="9">Recursive DFS check:</text>
          <text x="675" y="173" fill="#38bdf8" font-size="10" font-weight="bold">p.val == q.val &amp;&amp; L &amp;&amp; R</text>
          <text x="675" y="188" fill="#94a3b8" font-size="9">Base: (null, null) &#8594; true</text>
          <text x="675" y="202" fill="#34d399" font-size="9">O(min(N, M)) Time | O(H) Stack</text>

          <rect x="665" y="225" width="210" height="120" rx="5" fill="#0f172a" stroke="#334155"/>
          <text x="675" y="245" fill="#fbbf24" font-size="11" font-weight="bold">LC #572: Subtree Match</text>
          <text x="675" y="262" fill="#94a3b8" font-size="9">Classic DFS: O(N * M)</text>
          <text x="675" y="278" fill="#34d399" font-size="10" font-weight="bold">Merkle Hashing: O(N + M)</text>
          <text x="675" y="295" fill="#cbd5e1" font-size="9">H(u) = Mix(val, H_L, H_R)</text>
          <text x="675" y="310" fill="#94a3b8" font-size="9">64-bit polynomial hash set</text>
          <text x="675" y="325" fill="#34d399" font-size="9">Linear time subtree fingerprint</text>
        </svg>
        """
    elif day_num == 20:
        return """
        <svg viewBox="0 0 920 380" width="100%" height="auto" xmlns="http://www.w3.org/2000/svg">
          <defs>
            <linearGradient id="d20GradBg" x1="0%" y1="0%" x2="100%" y2="100%">
              <stop offset="0%" stop-color="#0b0f19"/><stop offset="100%" stop-color="#111827"/>
            </linearGradient>
            <linearGradient id="d20GradStreams" x1="0%" y1="0%" x2="100%" y2="100%">
              <stop offset="0%" stop-color="#1e293b"/><stop offset="100%" stop-color="#0f172a"/>
            </linearGradient>
            <linearGradient id="d20GradPel" x1="0%" y1="0%" x2="100%" y2="100%">
              <stop offset="0%" stop-color="#1e1b4b"/><stop offset="100%" stop-color="#0f172a"/>
            </linearGradient>
            <linearGradient id="d20GradTree" x1="0%" y1="0%" x2="100%" y2="100%">
              <stop offset="0%" stop-color="#064e3b"/><stop offset="100%" stop-color="#0b1e19"/>
            </linearGradient>
          </defs>
          <rect width="920" height="380" rx="12" fill="url(#d20GradBg)" stroke="#1e293b" stroke-width="2"/>
          <text x="460" y="32" fill="#f8fafc" font-size="16" font-weight="bold" text-anchor="middle">Redis Streams Architecture: Radix Tree + ListPacks, Consumer Groups, PEL &amp; XCLAIM</text>

          <!-- 1. Streams Radix Tree Log Column -->
          <rect x="30" y="60" width="300" height="300" rx="10" fill="url(#d20GradStreams)" stroke="#38bdf8" stroke-width="1.5"/>
          <text x="50" y="88" fill="#38bdf8" font-size="14" font-weight="bold">Stream Log: Radix + ListPacks</text>

          <!-- Append-only Radix Tree representation -->
          <rect x="45" y="105" width="270" height="50" rx="6" fill="#0369a1" stroke="#38bdf8"/>
          <text x="55" y="125" fill="#ffffff" font-size="11" font-weight="bold">Radix Tree (raxNode)</text>
          <text x="55" y="142" fill="#bae6fd" font-size="9">Radix indexing on Entry ID: &lt;timestamp_ms&gt;-&lt;seq&gt;</text>

          <!-- ListPacks block -->
          <rect x="45" y="165" width="270" height="75" rx="6" fill="#0f172a" stroke="#334155"/>
          <text x="55" y="185" fill="#38bdf8" font-size="11" font-weight="bold">Contiguous ListPack Chunk</text>
          <text x="55" y="202" fill="#cbd5e1" font-size="9">1728475200000-0: {sensor: &quot;temp&quot;, val: 24.5}</text>
          <text x="55" y="217" fill="#cbd5e1" font-size="9">1728475200001-0: {sensor: &quot;hum&quot;, val: 60.1}</text>
          <text x="55" y="232" fill="#94a3b8" font-size="8">Delta-compressed IDs • Zero per-entry pointer bloat</text>

          <!-- PubSub vs Stream Contrast -->
          <rect x="45" y="250" width="270" height="95" rx="6" fill="#0f172a" stroke="#334155"/>
          <text x="55" y="270" fill="#fbbf24" font-size="11" font-weight="bold">Pub/Sub vs Streams</text>
          <text x="55" y="288" fill="#ef4444" font-size="9">Pub/Sub: Ephemeral socket push • No history</text>
          <text x="55" y="303" fill="#34d399" font-size="9">Streams: Append-only disk log • Range replay</text>
          <text x="55" y="318" fill="#94a3b8" font-size="9">XTRIM MINID / MAXLEN prevents unbounded RAM</text>

          <!-- 2. Consumer Groups & PEL Column -->
          <rect x="350" y="60" width="280" height="300" rx="10" fill="url(#d20GradPel)" stroke="#a855f7" stroke-width="1.5"/>
          <text x="370" y="88" fill="#c084fc" font-size="14" font-weight="bold">Consumer Groups &amp; PEL</text>

          <rect x="365" y="105" width="250" height="60" rx="5" fill="#0f172a" stroke="#475569"/>
          <text x="375" y="125" fill="#60a5fa" font-size="11" font-weight="bold">Group Cursor &amp; Competing Consumers</text>
          <text x="375" y="142" fill="#cbd5e1" font-size="9">Last-Delivered-ID tracks stream frontier</text>
          <text x="375" y="157" fill="#94a3b8" font-size="9">XREADGROUP GROUP g1 w1 STREAMS s1 &gt;</text>

          <rect x="365" y="175" width="250" height="80" rx="5" fill="#0f172a" stroke="#475569"/>
          <text x="375" y="195" fill="#fbbf24" font-size="11" font-weight="bold">Pending Entries List (PEL)</text>
          <text x="375" y="212" fill="#cbd5e1" font-size="9">Worker 1: msg-101 (idle: 400ms) &#8594; XACK &#10003;</text>
          <text x="375" y="227" fill="#f87171" font-size="9">Worker 2 (CRASHED): msg-102 (idle: 95s) &#9888;</text>
          <text x="375" y="242" fill="#94a3b8" font-size="8">PEL tracks ID, consumer, idle time &amp; delivery count</text>

          <rect x="365" y="265" width="250" height="78" rx="5" fill="#0f172a" stroke="#10b981"/>
          <text x="375" y="285" fill="#34d399" font-size="11" font-weight="bold">Fault Recovery: XCLAIM / XAUTOCLAIM</text>
          <text x="375" y="302" fill="#cbd5e1" font-size="9">Worker 3 claims msg-102 after min-idle-time</text>
          <text x="375" y="317" fill="#cbd5e1" font-size="9">Ownership transfers safely without re-reading</text>
          <text x="375" y="332" fill="#94a3b8" font-size="8">At-least-once delivery + Dead Letter Queue defense</text>

          <!-- 3. DSA Tree Algorithms Column -->
          <rect x="650" y="60" width="240" height="300" rx="10" fill="url(#d20GradTree)" stroke="#10b981" stroke-width="1.5"/>
          <text x="670" y="88" fill="#34d399" font-size="14" font-weight="bold">Tree Traversal &amp; LCA</text>

          <rect x="665" y="105" width="210" height="110" rx="5" fill="#0f172a" stroke="#334155"/>
          <text x="675" y="125" fill="#38bdf8" font-size="11" font-weight="bold">LC #235: LCA of BST</text>
          <text x="675" y="142" fill="#94a3b8" font-size="9">Exploits BST Ordering Property:</text>
          <text x="675" y="157" fill="#cbd5e1" font-size="9">p, q &lt; curr.val &#8594; curr = curr.left</text>
          <text x="675" y="172" fill="#cbd5e1" font-size="9">p, q &gt; curr.val &#8594; curr = curr.right</text>
          <text x="675" y="187" fill="#fbbf24" font-size="9">Split point &#8594; curr is LCA!</text>
          <text x="675" y="202" fill="#34d399" font-size="9">O(H) Time | O(1) Iterative Space</text>

          <rect x="665" y="225" width="210" height="120" rx="5" fill="#0f172a" stroke="#334155"/>
          <text x="675" y="245" fill="#fbbf24" font-size="11" font-weight="bold">LC #102: Level Order BFS</text>
          <text x="675" y="262" fill="#94a3b8" font-size="9">FIFO Queue level-by-level:</text>
          <text x="675" y="278" fill="#cbd5e1" font-size="9">levelSize = queue.Count</text>
          <text x="675" y="293" fill="#38bdf8" font-size="9">Batch-process current tier</text>
          <text x="675" y="308" fill="#94a3b8" font-size="9">Enqueue left &amp; right children</text>
          <text x="675" y="325" fill="#34d399" font-size="9">O(N) Time | O(W) Frontier Width</text>
        </svg>
        """
    elif day_num == 21:
        return """
        <svg viewBox="0 0 920 380" width="100%" height="auto" xmlns="http://www.w3.org/2000/svg">
          <defs>
            <linearGradient id="d21GradBg" x1="0%" y1="0%" x2="100%" y2="100%">
              <stop offset="0%" stop-color="#0b0f19"/><stop offset="100%" stop-color="#111827"/>
            </linearGradient>
            <linearGradient id="d21GradRing" x1="0%" y1="0%" x2="100%" y2="100%">
              <stop offset="0%" stop-color="#1e293b"/><stop offset="100%" stop-color="#0f172a"/>
            </linearGradient>
            <linearGradient id="d21GradCache" x1="0%" y1="0%" x2="100%" y2="100%">
              <stop offset="0%" stop-color="#2e1065"/><stop offset="100%" stop-color="#0f172a"/>
            </linearGradient>
            <linearGradient id="d21GradTree" x1="0%" y1="0%" x2="100%" y2="100%">
              <stop offset="0%" stop-color="#064e3b"/><stop offset="100%" stop-color="#0b1e19"/>
            </linearGradient>
          </defs>
          <rect width="920" height="380" rx="12" fill="url(#d21GradBg)" stroke="#1e293b" stroke-width="2"/>
          <text x="460" y="32" fill="#f8fafc" font-size="16" font-weight="bold" text-anchor="middle">Distributed Cache Architecture: Ketama Consistent Hashing, Stampede Shield &amp; Tree Views</text>

          <!-- 1. Ketama Consistent Hash Ring Column -->
          <rect x="30" y="60" width="300" height="300" rx="10" fill="url(#d21GradRing)" stroke="#38bdf8" stroke-width="1.5"/>
          <text x="50" y="88" fill="#38bdf8" font-size="14" font-weight="bold">Ketama Consistent Hash Ring</text>

          <rect x="45" y="105" width="270" height="60" rx="6" fill="#0284c7" stroke="#38bdf8"/>
          <text x="55" y="125" fill="#ffffff" font-size="11" font-weight="bold">Ring Space: [0, 2^32 - 1]</text>
          <text x="55" y="142" fill="#e0f2fe" font-size="9">Virtual Nodes (150/server) prevent hot spots</text>
          <text x="55" y="155" fill="#bae6fd" font-size="9">BinarySearch O(log(N * V)) nearest clockwise node</text>

          <rect x="45" y="175" width="270" height="75" rx="6" fill="#0f172a" stroke="#334155"/>
          <text x="55" y="195" fill="#fbbf24" font-size="11" font-weight="bold">Minimal Cluster Disruption</text>
          <text x="55" y="212" fill="#cbd5e1" font-size="9">Hash(key) &#8594; Node A#42 &#8594; Physical Server A</text>
          <text x="55" y="227" fill="#34d399" font-size="9">Node scale up/down: Only 1/N keys remapped</text>
          <text x="55" y="242" fill="#ef4444" font-size="8">vs Traditional Hash(k) % N: Remaps 100% of keys!</text>

          <rect x="45" y="260" width="270" height="85" rx="6" fill="#0f172a" stroke="#334155"/>
          <text x="55" y="280" fill="#38bdf8" font-size="11" font-weight="bold">Hot-Key Suffix Scattering</text>
          <text x="55" y="297" fill="#cbd5e1" font-size="9">Scatter key: &quot;celebrity_feed_#&quot; + rand(0..10)</text>
          <text x="55" y="312" fill="#cbd5e1" font-size="9">Spreads 100k QPS across 10 independent shards</text>
          <text x="55" y="327" fill="#94a3b8" font-size="8">Writes fan-out or write to primary with background sync</text>

          <!-- 2. Two-Tier Cache & Stampede Shield Column -->
          <rect x="350" y="60" width="280" height="300" rx="10" fill="url(#d21GradCache)" stroke="#c084fc" stroke-width="1.5"/>
          <text x="370" y="88" fill="#c084fc" font-size="14" font-weight="bold">Cache Stampede &amp; 2-Tier Shield</text>

          <rect x="365" y="105" width="250" height="70" rx="5" fill="#0f172a" stroke="#475569"/>
          <text x="375" y="125" fill="#60a5fa" font-size="11" font-weight="bold">Two-Tier Architecture</text>
          <text x="375" y="142" fill="#cbd5e1" font-size="9">L1: MemoryCache (in-proc, &lt; 100ns)</text>
          <text x="375" y="157" fill="#cbd5e1" font-size="9">L2: Redis Sharded Cluster (remote, ~1ms)</text>
          <text x="375" y="170" fill="#94a3b8" font-size="8">L1 shields network roundtrips for hottest 5% keys</text>

          <rect x="365" y="185" width="250" height="75" rx="5" fill="#0f172a" stroke="#475569"/>
          <text x="375" y="205" fill="#fbbf24" font-size="11" font-weight="bold">Single-Flight Mutex</text>
          <text x="375" y="222" fill="#cbd5e1" font-size="9">Concurrent requests for expired key</text>
          <text x="375" y="237" fill="#34d399" font-size="9">1 thread acquires semaphore &#8594; fetches DB</text>
          <text x="375" y="252" fill="#94a3b8" font-size="8">Remaining N-1 threads await Task without DB hit</text>

          <rect x="365" y="270" width="250" height="75" rx="5" fill="#0f172a" stroke="#10b981"/>
          <text x="375" y="290" fill="#34d399" font-size="11" font-weight="bold">XFetch Probabilistic Refresh</text>
          <text x="375" y="307" fill="#cbd5e1" font-size="9">-delta * beta * ln(rand()) &gt; rem_ttl</text>
          <text x="375" y="322" fill="#cbd5e1" font-size="9">Background asynchronous DB pre-computation</text>
          <text x="375" y="337" fill="#94a3b8" font-size="8">Guarantees zero-downtime cache misses under load</text>

          <!-- 3. DSA Tree Inspection Column -->
          <rect x="650" y="60" width="240" height="300" rx="10" fill="url(#d21GradTree)" stroke="#10b981" stroke-width="1.5"/>
          <text x="670" y="88" fill="#34d399" font-size="14" font-weight="bold">Tree Inspection</text>

          <rect x="665" y="105" width="210" height="110" rx="5" fill="#0f172a" stroke="#334155"/>
          <text x="675" y="125" fill="#38bdf8" font-size="11" font-weight="bold">LC #199: Right Side View</text>
          <text x="675" y="142" fill="#94a3b8" font-size="9">Right-First Preorder DFS:</text>
          <text x="675" y="157" fill="#cbd5e1" font-size="9">if (depth == result.Count)</text>
          <text x="675" y="172" fill="#fbbf24" font-size="9">  result.Add(node.val);</text>
          <text x="675" y="187" fill="#cbd5e1" font-size="9">DFS(node.right); DFS(node.left);</text>
          <text x="675" y="202" fill="#34d399" font-size="9">O(N) Time | O(H) Call Stack</text>

          <rect x="665" y="225" width="210" height="120" rx="5" fill="#0f172a" stroke="#334155"/>
          <text x="675" y="245" fill="#fbbf24" font-size="11" font-weight="bold">LC #1448: Good Nodes</text>
          <text x="675" y="262" fill="#94a3b8" font-size="9">Path Maximum Invariant:</text>
          <text x="675" y="278" fill="#cbd5e1" font-size="9">if (node.val &gt;= maxSoFar)</text>
          <text x="675" y="293" fill="#34d399" font-size="9">  count++;</text>
          <text x="675" y="308" fill="#cbd5e1" font-size="9">newMax = max(maxSoFar, val)</text>
          <text x="675" y="325" fill="#34d399" font-size="9">O(N) Time | O(H) Depth-First Stack</text>
        </svg>
        """
    elif day_num == 22:
        return """
        <svg viewBox="0 0 920 380" width="100%" height="auto" xmlns="http://www.w3.org/2000/svg">
          <defs>
            <linearGradient id="d22GradBg" x1="0%" y1="0%" x2="100%" y2="100%">
              <stop offset="0%" stop-color="#0b0f19"/><stop offset="100%" stop-color="#111827"/>
            </linearGradient>
            <linearGradient id="d22GradToken" x1="0%" y1="0%" x2="100%" y2="100%">
              <stop offset="0%" stop-color="#1e293b"/><stop offset="100%" stop-color="#0f172a"/>
            </linearGradient>
            <linearGradient id="d22GradSlide" x1="0%" y1="0%" x2="100%" y2="100%">
              <stop offset="0%" stop-color="#1e1b4b"/><stop offset="100%" stop-color="#0f172a"/>
            </linearGradient>
            <linearGradient id="d22GradTree" x1="0%" y1="0%" x2="100%" y2="100%">
              <stop offset="0%" stop-color="#064e3b"/><stop offset="100%" stop-color="#0b1e19"/>
            </linearGradient>
          </defs>
          <rect width="920" height="380" rx="12" fill="url(#d22GradBg)" stroke="#1e293b" stroke-width="2"/>
          <text x="460" y="32" fill="#f8fafc" font-size="16" font-weight="bold" text-anchor="middle">Distributed Rate Limiter Architecture: Token Bucket, Sliding Window &amp; BST In-Order</text>

          <!-- 1. Token & Leaky Bucket Column -->
          <rect x="30" y="60" width="300" height="300" rx="10" fill="url(#d22GradToken)" stroke="#38bdf8" stroke-width="1.5"/>
          <text x="50" y="88" fill="#38bdf8" font-size="14" font-weight="bold">Token Bucket &amp; Traffic Shaping</text>

          <rect x="45" y="105" width="270" height="65" rx="6" fill="#0284c7" stroke="#38bdf8"/>
          <text x="55" y="125" fill="#ffffff" font-size="11" font-weight="bold">Token Bucket (AWS, Stripe Standard)</text>
          <text x="55" y="142" fill="#e0f2fe" font-size="9">Burst capacity C=10 • Refill rate R=2 tokens/s</text>
          <text x="55" y="157" fill="#bae6fd" font-size="9">Tokens = min(C, old + delta_t * R)</text>

          <rect x="45" y="180" width="270" height="75" rx="6" fill="#0f172a" stroke="#334155"/>
          <text x="55" y="200" fill="#fbbf24" font-size="11" font-weight="bold">Leaky Bucket (Constant Outflow)</text>
          <text x="55" y="217" fill="#cbd5e1" font-size="9">FIFO queue buffer drops excess packets</text>
          <text x="55" y="232" fill="#34d399" font-size="9">Outputs strict steady stream at rate R</text>
          <text x="55" y="247" fill="#94a3b8" font-size="8">Ideal for webhook dispatchers &amp; third-party APIs</text>

          <rect x="45" y="265" width="270" height="80" rx="6" fill="#0f172a" stroke="#ef4444"/>
          <text x="55" y="285" fill="#f87171" font-size="11" font-weight="bold">Fixed Window 2x Boundary Spike</text>
          <text x="55" y="302" fill="#cbd5e1" font-size="9">100 req at 12:00:59 + 100 req at 12:01:01</text>
          <text x="55" y="317" fill="#ef4444" font-size="9">&#9888; 200 requests pass in a 2-second interval!</text>
          <text x="55" y="332" fill="#94a3b8" font-size="8">Fixed window counters are vulnerable to DDoS bursts</text>

          <!-- 2. Sliding Window Counter & Middleware Column -->
          <rect x="350" y="60" width="280" height="300" rx="10" fill="url(#d22GradSlide)" stroke="#a855f7" stroke-width="1.5"/>
          <text x="370" y="88" fill="#c084fc" font-size="14" font-weight="bold">Sliding Window &amp; Middleware</text>

          <rect x="365" y="105" width="250" height="80" rx="5" fill="#0f172a" stroke="#475569"/>
          <text x="375" y="125" fill="#60a5fa" font-size="11" font-weight="bold">Cloudflare Hybrid Sliding Counter</text>
          <text x="375" y="142" fill="#cbd5e1" font-size="9">Count = Curr + Prev * (1 - elapsed / window)</text>
          <text x="375" y="157" fill="#34d399" font-size="9">&#10003; Memory: Strictly O(1) (only 2 counters)</text>
          <text x="375" y="172" fill="#94a3b8" font-size="8">Eliminates 2x spike with &lt; 0.05% error rate</text>

          <rect x="365" y="195" width="250" height="75" rx="5" fill="#0f172a" stroke="#475569"/>
          <text x="375" y="215" fill="#fbbf24" font-size="11" font-weight="bold">ASP.NET Core Middleware Headers</text>
          <text x="375" y="232" fill="#cbd5e1" font-size="9">X-RateLimit-Limit: 100</text>
          <text x="375" y="247" fill="#cbd5e1" font-size="9">X-RateLimit-Remaining: 42</text>
          <text x="375" y="260" fill="#cbd5e1" font-size="9">X-RateLimit-Reset: 1728561600</text>

          <rect x="365" y="280" width="250" height="65" rx="5" fill="#0f172a" stroke="#10b981"/>
          <text x="375" y="300" fill="#34d399" font-size="11" font-weight="bold">HTTP 429 &amp; RFC 7807 Details</text>
          <text x="375" y="317" fill="#cbd5e1" font-size="9">Retry-After: 18 seconds</text>
          <text x="375" y="332" fill="#94a3b8" font-size="8">Standardized problem+json automated backoff</text>

          <!-- 3. DSA BST Traversal Column -->
          <rect x="650" y="60" width="240" height="300" rx="10" fill="url(#d22GradTree)" stroke="#10b981" stroke-width="1.5"/>
          <text x="670" y="88" fill="#34d399" font-size="14" font-weight="bold">BST In-Order Invariants</text>

          <rect x="665" y="105" width="210" height="110" rx="5" fill="#0f172a" stroke="#334155"/>
          <text x="675" y="125" fill="#38bdf8" font-size="11" font-weight="bold">LC #98: Validate BST</text>
          <text x="675" y="142" fill="#94a3b8" font-size="9">Global Interval Range Check:</text>
          <text x="675" y="157" fill="#cbd5e1" font-size="9">min &lt; node.val &lt; max (64-bit bounds)</text>
          <text x="675" y="172" fill="#fbbf24" font-size="9">In-order: prev &lt; curr strictly</text>
          <text x="675" y="187" fill="#94a3b8" font-size="9">Prevents deep ancestor leaks</text>
          <text x="675" y="202" fill="#34d399" font-size="9">O(N) Time | O(H) Call Stack</text>

          <rect x="665" y="225" width="210" height="120" rx="5" fill="#0f172a" stroke="#334155"/>
          <text x="675" y="245" fill="#fbbf24" font-size="11" font-weight="bold">LC #230: Kth Smallest BST</text>
          <text x="675" y="262" fill="#94a3b8" font-size="9">Iterative Stack In-Order:</text>
          <text x="675" y="278" fill="#cbd5e1" font-size="9">while (curr != null) Push(curr)</text>
          <text x="675" y="293" fill="#38bdf8" font-size="9">curr = Pop(); if (--k == 0) return</text>
          <text x="675" y="308" fill="#94a3b8" font-size="9">Early exit without full traversal</text>
          <text x="675" y="325" fill="#34d399" font-size="9">O(H + k) Time | O(H) Memory</text>
        </svg>
        """
    elif day_num == 23:
        return """
        <svg viewBox="0 0 920 380" width="100%" height="auto" xmlns="http://www.w3.org/2000/svg">
          <defs>
            <linearGradient id="d23GradBg" x1="0%" y1="0%" x2="100%" y2="100%">
              <stop offset="0%" stop-color="#0b0f19"/><stop offset="100%" stop-color="#111827"/>
            </linearGradient>
            <linearGradient id="d23GradSnowflake" x1="0%" y1="0%" x2="100%" y2="100%">
              <stop offset="0%" stop-color="#1e293b"/><stop offset="100%" stop-color="#0f172a"/>
            </linearGradient>
            <linearGradient id="d23GradCluster" x1="0%" y1="0%" x2="100%" y2="100%">
              <stop offset="0%" stop-color="#2e1065"/><stop offset="100%" stop-color="#0f172a"/>
            </linearGradient>
            <linearGradient id="d23GradTree" x1="0%" y1="0%" x2="100%" y2="100%">
              <stop offset="0%" stop-color="#064e3b"/><stop offset="100%" stop-color="#0b1e19"/>
            </linearGradient>
          </defs>
          <rect width="920" height="380" rx="12" fill="url(#d23GradBg)" stroke="#1e293b" stroke-width="2"/>
          <text x="460" y="32" fill="#f8fafc" font-size="16" font-weight="bold" text-anchor="middle">TinyURL System Architecture: Snowflake 64-bit ID, Base62, Sharding &amp; Tree Reconstruction</text>

          <!-- 1. Snowflake & Base62 Column -->
          <rect x="30" y="60" width="300" height="300" rx="10" fill="url(#d23GradSnowflake)" stroke="#38bdf8" stroke-width="1.5"/>
          <text x="50" y="88" fill="#38bdf8" font-size="14" font-weight="bold">Snowflake ID &amp; Base62 Codec</text>

          <rect x="45" y="105" width="270" height="65" rx="6" fill="#0284c7" stroke="#38bdf8"/>
          <text x="55" y="125" fill="#ffffff" font-size="11" font-weight="bold">64-bit Twitter Snowflake</text>
          <text x="55" y="142" fill="#e0f2fe" font-size="9">1-bit Sign | 41-bit ms Epoch (69 yrs)</text>
          <text x="55" y="157" fill="#bae6fd" font-size="9">5-bit DC | 5-bit Worker | 12-bit Seq (4096/ms)</text>

          <rect x="45" y="180" width="270" height="75" rx="6" fill="#0f172a" stroke="#334155"/>
          <text x="55" y="200" fill="#fbbf24" font-size="11" font-weight="bold">Base62 Bijective Codec</text>
          <text x="55" y="217" fill="#cbd5e1" font-size="9">Alphabet: [0-9a-zA-Z] (62 URL-safe chars)</text>
          <text x="55" y="232" fill="#34d399" font-size="9">Zero hash collision risk • 7-8 char links</text>
          <text x="55" y="247" fill="#94a3b8" font-size="8">123456789 &#8594; &quot;8m0Kx&quot; (Bi-directional)</text>

          <rect x="45" y="265" width="270" height="80" rx="6" fill="#0f172a" stroke="#334155"/>
          <text x="55" y="285" fill="#38bdf8" font-size="11" font-weight="bold">Monotonic B-Tree Friendly</text>
          <text x="55" y="302" fill="#cbd5e1" font-size="9">k-sorted IDs prevent random B-Tree page splits</text>
          <text x="55" y="317" fill="#34d399" font-size="9">Handles 4.096M IDs/sec per machine node</text>
          <text x="55" y="332" fill="#94a3b8" font-size="8">Spin-wait protection against NTP clock backwards drift</text>

          <!-- 2. Sharding & Capacity Column -->
          <rect x="350" y="60" width="280" height="300" rx="10" fill="url(#d23GradCluster)" stroke="#c084fc" stroke-width="1.5"/>
          <text x="370" y="88" fill="#c084fc" font-size="14" font-weight="bold">Capacity Planning &amp; Sharding</text>

          <rect x="365" y="105" width="250" height="75" rx="5" fill="#0f172a" stroke="#475569"/>
          <text x="375" y="125" fill="#60a5fa" font-size="11" font-weight="bold">100M Writes &amp; 1B Reads Workload</text>
          <text x="375" y="142" fill="#cbd5e1" font-size="9">Write QPS: ~1,157 (Peak 3x: ~3,500)</text>
          <text x="375" y="157" fill="#cbd5e1" font-size="9">Read QPS: ~11,574 (Peak 3x: ~35,000)</text>
          <text x="375" y="170" fill="#94a3b8" font-size="8">10:1 Read-to-Write Ratio • Latency SLA &lt; 10ms</text>

          <rect x="365" y="190" width="250" height="75" rx="5" fill="#0f172a" stroke="#475569"/>
          <text x="375" y="210" fill="#fbbf24" font-size="11" font-weight="bold">Storage &amp; Cache Requirements</text>
          <text x="375" y="227" fill="#cbd5e1" font-size="9">500 bytes/rec &#8594; 18.25 TB/yr (91.25 TB/5yr)</text>
          <text x="375" y="242" fill="#34d399" font-size="9">Pareto 80/20 Rule: 100 GB RAM Redis Cache</text>
          <text x="375" y="255" fill="#94a3b8" font-size="8">Caches 20% of daily read URLs (200M links)</text>

          <rect x="365" y="275" width="250" height="70" rx="5" fill="#0f172a" stroke="#10b981"/>
          <text x="375" y="295" fill="#34d399" font-size="11" font-weight="bold">Hash(ID) % N Sharding &amp; HTTP 302</text>
          <text x="375" y="312" fill="#cbd5e1" font-size="9">Decode(shortCode) % N finds target shard directly</text>
          <text x="375" y="327" fill="#94a3b8" font-size="8">HTTP 302 preserves 100% telemetry &amp; analytics tracking</text>

          <!-- 3. DSA Tree Reconstruction Column -->
          <rect x="650" y="60" width="240" height="300" rx="10" fill="url(#d23GradTree)" stroke="#10b981" stroke-width="1.5"/>
          <text x="670" y="88" fill="#34d399" font-size="14" font-weight="bold">Tree Reconstruction</text>

          <rect x="665" y="105" width="210" height="115" rx="5" fill="#0f172a" stroke="#334155"/>
          <text x="675" y="125" fill="#38bdf8" font-size="11" font-weight="bold">LC #105: Preorder + Inorder</text>
          <text x="675" y="142" fill="#94a3b8" font-size="9">Preorder: [Root, Left..., Right...]</text>
          <text x="675" y="157" fill="#cbd5e1" font-size="9">rootVal = preorder[preIndex++]</text>
          <text x="675" y="172" fill="#fbbf24" font-size="9">Inorder: [Left..., Root, Right...]</text>
          <text x="675" y="187" fill="#cbd5e1" font-size="9">inRoot divides left &amp; right</text>
          <text x="675" y="202" fill="#34d399" font-size="9">Recursive divide-and-conquer</text>

          <rect x="665" y="230" width="210" height="115" rx="5" fill="#0f172a" stroke="#334155"/>
          <text x="675" y="250" fill="#fbbf24" font-size="11" font-weight="bold">Hash Map O(1) Index Lookup</text>
          <text x="675" y="267" fill="#94a3b8" font-size="9">Without map: O(N) scan &#8594; O(N^2)</text>
          <text x="675" y="283" fill="#34d399" font-size="10" font-weight="bold">inorderMap[val] = index</text>
          <text x="675" y="300" fill="#cbd5e1" font-size="9">Reduces split lookup to O(1)</text>
          <text x="675" y="315" fill="#34d399" font-size="9">Optimal O(N) Time | O(N) Space</text>
          <text x="675" y="330" fill="#94a3b8" font-size="8">Strictly builds left child before right</text>
        </svg>
        """

    if day_num == 24:
        return """
        <svg viewBox="0 0 900 360" width="100%" height="auto" xmlns="http://www.w3.org/2000/svg">
          <defs>
            <linearGradient id="d24GradWin" x1="0%" y1="0%" x2="100%" y2="100%"><stop offset="0%" stop-color="#1e1b4b"/><stop offset="100%" stop-color="#0f172a"/></linearGradient>
            <linearGradient id="d24GradPart" x1="0%" y1="0%" x2="100%" y2="100%"><stop offset="0%" stop-color="#042f2e"/><stop offset="100%" stop-color="#0f172a"/></linearGradient>
            <linearGradient id="d24GradTree" x1="0%" y1="0%" x2="100%" y2="100%"><stop offset="0%" stop-color="#451a03"/><stop offset="100%" stop-color="#0f172a"/></linearGradient>
          </defs>
          <rect width="900" height="360" rx="12" fill="#0b0f19" stroke="#1e293b" stroke-width="2"/>
          <text x="450" y="32" fill="#f8fafc" font-size="16" font-weight="bold" text-anchor="middle">Day 24: Advanced SQL (CTEs, Window Functions, Table Partitions) &amp; LC #124 Max Path Sum</text>

          <!-- 1. Window Functions Column -->
          <rect x="25" y="60" width="275" height="280" rx="10" fill="url(#d24GradWin)" stroke="#818cf8" stroke-width="1.5"/>
          <text x="45" y="88" fill="#a5b4fc" font-size="14" font-weight="bold">Window Functions &amp; CTEs</text>

          <rect x="40" y="105" width="245" height="105" rx="5" fill="#0f172a" stroke="#334155"/>
          <text x="50" y="125" fill="#38bdf8" font-size="11" font-weight="bold">Ranking Operators</text>
          <text x="50" y="145" fill="#cbd5e1" font-size="9">ROW_NUMBER(): Sequential (1, 2, 3, 4)</text>
          <text x="50" y="162" fill="#cbd5e1" font-size="9">RANK(): Ties skip (1, 2, 2, 4)</text>
          <text x="50" y="179" fill="#cbd5e1" font-size="9">DENSE_RANK(): Ties compact (1, 2, 2, 3)</text>
          <text x="50" y="196" fill="#34d399" font-size="9">OVER (PARTITION BY cat ORDER BY val DESC)</text>

          <rect x="40" y="220" width="245" height="105" rx="5" fill="#0f172a" stroke="#334155"/>
          <text x="50" y="240" fill="#fbbf24" font-size="11" font-weight="bold">Time-Series Offset &amp; Frames</text>
          <text x="50" y="258" fill="#cbd5e1" font-size="9">LAG(val, 1): Prior row in window</text>
          <text x="50" y="274" fill="#cbd5e1" font-size="9">LEAD(val, 1): Next row in window</text>
          <text x="50" y="290" fill="#cbd5e1" font-size="9">ROWS BETWEEN 2 PRECEDING AND CURRENT</text>
          <text x="50" y="306" fill="#a5b4fc" font-size="9">Zero self-join overhead | WindowAgg engine</text>

          <!-- 2. Table Partitioning Column -->
          <rect x="315" y="60" width="275" height="280" rx="10" fill="url(#d24GradPart)" stroke="#2dd4bf" stroke-width="1.5"/>
          <text x="335" y="88" fill="#5eead4" font-size="14" font-weight="bold">Declarative Table Partitioning</text>

          <rect x="330" y="105" width="245" height="105" rx="5" fill="#0f172a" stroke="#334155"/>
          <text x="340" y="125" fill="#2dd4bf" font-size="11" font-weight="bold">Range-Based Partitions</text>
          <text x="340" y="145" fill="#cbd5e1" font-size="9">Master: orders (order_date, total)</text>
          <text x="340" y="162" fill="#cbd5e1" font-size="9">&#8226; orders_2026_q1: ['2026-01-01', '2026-04-01')</text>
          <text x="340" y="179" fill="#cbd5e1" font-size="9">&#8226; orders_2026_q2: ['2026-04-01', '2026-07-01')</text>
          <text x="340" y="196" fill="#34d399" font-size="9">O(1) Data Lifecycle: DROP TABLE vs DELETE</text>

          <rect x="330" y="220" width="245" height="105" rx="5" fill="#0f172a" stroke="#334155"/>
          <text x="340" y="240" fill="#f43f5e" font-size="11" font-weight="bold">Query Plan Partition Pruning</text>
          <text x="340" y="258" fill="#cbd5e1" font-size="9">WHERE order_date BETWEEN '2026-05-01'...</text>
          <text x="340" y="274" fill="#fbbf24" font-size="9">Planner evaluates range constraints</text>
          <text x="340" y="290" fill="#34d399" font-size="9">PRUNED: orders_2026_q1, q3, q4 bypassed!</text>
          <text x="340" y="306" fill="#94a3b8" font-size="9">Scans 1 partition only &#8594; 90% IO reduction</text>

          <!-- 3. LeetCode #124 Max Path Sum Column -->
          <rect x="605" y="60" width="270" height="280" rx="10" fill="url(#d24GradTree)" stroke="#f97316" stroke-width="1.5"/>
          <text x="625" y="88" fill="#fdba74" font-size="14" font-weight="bold">LC #124: Tree Max Path Sum</text>

          <rect x="620" y="105" width="240" height="105" rx="5" fill="#0f172a" stroke="#334155"/>
          <text x="630" y="125" fill="#fbbf24" font-size="11" font-weight="bold">Post-Order Bottom-Up DFS</text>
          <text x="630" y="145" fill="#cbd5e1" font-size="9">LeftGain = max(0, Dfs(node.left))</text>
          <text x="630" y="162" fill="#cbd5e1" font-size="9">RightGain = max(0, Dfs(node.right))</text>
          <text x="630" y="179" fill="#f43f5e" font-size="9">Truncate negatives: max(0, gain)</text>
          <text x="630" y="196" fill="#34d399" font-size="9">Local Arch = node.val + Left + Right</text>

          <rect x="620" y="220" width="240" height="105" rx="5" fill="#0f172a" stroke="#334155"/>
          <text x="630" y="240" fill="#38bdf8" font-size="11" font-weight="bold">Complexity &amp; Invariants</text>
          <text x="630" y="258" fill="#34d399" font-size="9">GlobalMax = max(GlobalMax, LocalArch)</text>
          <text x="630" y="274" fill="#cbd5e1" font-size="9">Return node.val + max(Left, Right)</text>
          <text x="630" y="290" fill="#fbbf24" font-size="9">Time: O(N) single traversal</text>
          <text x="630" y="306" fill="#94a3b8" font-size="9">Space: O(H) recursion call stack</text>
        </svg>
        """

    if day_num == 25:
        return """
        <svg viewBox="0 0 900 360" width="100%" height="auto" xmlns="http://www.w3.org/2000/svg">
          <defs>
            <linearGradient id="d25GradMem" x1="0%" y1="0%" x2="100%" y2="100%"><stop offset="0%" stop-color="#1e3a8a"/><stop offset="100%" stop-color="#0f172a"/></linearGradient>
            <linearGradient id="d25GradAsync" x1="0%" y1="0%" x2="100%" y2="100%"><stop offset="0%" stop-color="#064e3b"/><stop offset="100%" stop-color="#0f172a"/></linearGradient>
            <linearGradient id="d25GradCodec" x1="0%" y1="0%" x2="100%" y2="100%"><stop offset="0%" stop-color="#581c87"/><stop offset="100%" stop-color="#0f172a"/></linearGradient>
          </defs>
          <rect width="900" height="360" rx="12" fill="#0b0f19" stroke="#1e293b" stroke-width="2"/>
          <text x="450" y="32" fill="#f8fafc" font-size="16" font-weight="bold" text-anchor="middle">Day 25: Phase 1 Milestone Assessment &amp; LC #297 Tree Codec (Hard)</text>

          <!-- 1. Zero-Alloc Memory Synthesis -->
          <rect x="25" y="60" width="275" height="280" rx="10" fill="url(#d25GradMem)" stroke="#3b82f6" stroke-width="1.5"/>
          <text x="45" y="88" fill="#60a5fa" font-size="14" font-weight="bold">Zero-Allocation Memory</text>

          <rect x="40" y="105" width="245" height="105" rx="5" fill="#0f172a" stroke="#334155"/>
          <text x="50" y="125" fill="#f87171" font-size="11" font-weight="bold">Heap Slicing: Substring()</text>
          <text x="50" y="145" fill="#cbd5e1" font-size="9">Allocates new string on Managed Heap</text>
          <text x="50" y="162" fill="#f87171" font-size="9">Cost: 48 bytes per slice &#8594; GC pressure</text>
          <text x="50" y="179" fill="#94a3b8" font-size="9">Gen 0 / Ephemeral collections</text>
          <text x="50" y="196" fill="#f87171" font-size="9">Heap fragmentation on high throughput</text>

          <rect x="40" y="220" width="245" height="105" rx="5" fill="#0f172a" stroke="#334155"/>
          <text x="50" y="240" fill="#34d399" font-size="11" font-weight="bold">Stack Slicing: ReadOnlySpan&lt;T&gt;</text>
          <text x="50" y="258" fill="#38bdf8" font-size="9">Pointer + Length on Thread Stack</text>
          <text x="50" y="274" fill="#34d399" font-size="9">Allocates EXACTLY 0 BYTES</text>
          <text x="50" y="290" fill="#cbd5e1" font-size="9">ref struct guarantees zero heap leakage</text>
          <text x="50" y="306" fill="#34d399" font-size="9">Instant O(1) slice | Zero GC pauses</text>

          <!-- 2. Async ValueTask & Concurrency -->
          <rect x="315" y="60" width="275" height="280" rx="10" fill="url(#d25GradAsync)" stroke="#10b981" stroke-width="1.5"/>
          <text x="335" y="88" fill="#34d399" font-size="14" font-weight="bold">Async State Machine &amp; Queues</text>

          <rect x="330" y="105" width="245" height="105" rx="5" fill="#0f172a" stroke="#334155"/>
          <text x="340" y="125" fill="#fbbf24" font-size="11" font-weight="bold">ValueTask&lt;T&gt; vs Task&lt;T&gt;</text>
          <text x="340" y="145" fill="#cbd5e1" font-size="9">Cache Hit: ValueTask completes synchronously</text>
          <text x="340" y="162" fill="#34d399" font-size="9">0 Heap Bytes: Discriminated struct union</text>
          <text x="340" y="179" fill="#94a3b8" font-size="9">Task&lt;T&gt; allocates object + state machine</text>
          <text x="340" y="196" fill="#38bdf8" font-size="9">Ideal for high cache hit ratios (&gt;80%)</text>

          <rect x="330" y="220" width="245" height="105" rx="5" fill="#0f172a" stroke="#334155"/>
          <text x="340" y="240" fill="#38bdf8" font-size="11" font-weight="bold">Lock-Free Channels &amp; Cache</text>
          <text x="340" y="258" fill="#cbd5e1" font-size="9">System.Threading.Channels.Channel&lt;T&gt;</text>
          <text x="340" y="274" fill="#34d399" font-size="9">CAS Ring Buffer: Millions msgs/sec</text>
          <text x="340" y="290" fill="#cbd5e1" font-size="9">Multi-Tier Redis Caching with Pub/Sub</text>
          <text x="340" y="306" fill="#a5b4fc" font-size="9">Eliminates DB stampedes via RedLock</text>

          <!-- 3. LeetCode #297 Hard Codec -->
          <rect x="605" y="60" width="270" height="280" rx="10" fill="url(#d25GradCodec)" stroke="#c084fc" stroke-width="1.5"/>
          <text x="625" y="88" fill="#e9d5ff" font-size="14" font-weight="bold">LC #297: Binary Tree Codec</text>

          <rect x="620" y="105" width="240" height="105" rx="5" fill="#0f172a" stroke="#334155"/>
          <text x="630" y="125" fill="#c084fc" font-size="11" font-weight="bold">Preorder DFS Codec</text>
          <text x="630" y="145" fill="#cbd5e1" font-size="9">Serialization: Root &#8594; Left &#8594; Right</text>
          <text x="630" y="162" fill="#cbd5e1" font-size="9">Null sentinel: '#' token</text>
          <text x="630" y="179" fill="#34d399" font-size="9">Stream: "1,2,#,#,3,4,#,#,5,#,#"</text>
          <text x="630" y="196" fill="#fbbf24" font-size="9">Queue iterator deserializes in O(N)</text>

          <rect x="620" y="220" width="240" height="105" rx="5" fill="#0f172a" stroke="#334155"/>
          <text x="630" y="240" fill="#38bdf8" font-size="11" font-weight="bold">Level-Order BFS Codec</text>
          <text x="630" y="258" fill="#cbd5e1" font-size="9">FIFO Queue captures tree levels</text>
          <text x="630" y="274" fill="#cbd5e1" font-size="9">Deserializer connects children via Queue</text>
          <text x="630" y="290" fill="#34d399" font-size="9">Zero recursion call stack overflow risk</text>
          <text x="630" y="306" fill="#94a3b8" font-size="9">Time: O(N) | Space: O(W) where W &#8804; N/2</text>
        </svg>
        """

    if day_num == 26:
        return """
        <svg viewBox="0 0 900 360" width="100%" height="auto" xmlns="http://www.w3.org/2000/svg">
          <defs>
            <linearGradient id="d26GradDdd" x1="0%" y1="0%" x2="100%" y2="100%"><stop offset="0%" stop-color="#1e1b4b"/><stop offset="100%" stop-color="#0f172a"/></linearGradient>
            <linearGradient id="d26GradAcl" x1="0%" y1="0%" x2="100%" y2="100%"><stop offset="0%" stop-color="#064e3b"/><stop offset="100%" stop-color="#0f172a"/></linearGradient>
            <linearGradient id="d26GradGrid" x1="0%" y1="0%" x2="100%" y2="100%"><stop offset="0%" stop-color="#701a75"/><stop offset="100%" stop-color="#0f172a"/></linearGradient>
          </defs>
          <rect width="900" height="360" rx="12" fill="#0b0f19" stroke="#1e293b" stroke-width="2"/>
          <text x="450" y="32" fill="#f8fafc" font-size="16" font-weight="bold" text-anchor="middle">Day 26: Microservices Principles, Bounded Contexts &amp; LC #200 Number of Islands</text>

          <!-- 1. Strategic DDD & Bounded Contexts -->
          <rect x="25" y="60" width="275" height="280" rx="10" fill="url(#d26GradDdd)" stroke="#818cf8" stroke-width="1.5"/>
          <text x="45" y="88" fill="#a5b4fc" font-size="14" font-weight="bold">Strategic DDD Subdomains</text>

          <rect x="40" y="105" width="245" height="105" rx="5" fill="#0f172a" stroke="#334155"/>
          <text x="50" y="125" fill="#fbbf24" font-size="11" font-weight="bold">Subdomain Classification</text>
          <text x="50" y="145" fill="#cbd5e1" font-size="9">&#8226; Core: Ordering (Competitive Edge)</text>
          <text x="50" y="162" fill="#cbd5e1" font-size="9">&#8226; Supporting: Inventory Allocation</text>
          <text x="50" y="179" fill="#cbd5e1" font-size="9">&#8226; Generic: Payment Gateway, Auth</text>
          <text x="50" y="196" fill="#38bdf8" font-size="9">Ubiquitous Language within context only</text>

          <rect x="40" y="220" width="245" height="105" rx="5" fill="#0f172a" stroke="#334155"/>
          <text x="50" y="240" fill="#34d399" font-size="11" font-weight="bold">Aggregate Root Rules</text>
          <text x="50" y="258" fill="#cbd5e1" font-size="9">Order Aggregate enforces total invariants</text>
          <text x="50" y="274" fill="#cbd5e1" font-size="9">Reference other aggregates by ID ONLY</text>
          <text x="50" y="290" fill="#34d399" font-size="9">One aggregate modified per transaction</text>
          <text x="50" y="306" fill="#94a3b8" font-size="9">Value Objects: Immutable Money, Sku</text>

          <!-- 2. Context Mapping & Anti-Corruption Layer -->
          <rect x="315" y="60" width="275" height="280" rx="10" fill="url(#d26GradAcl)" stroke="#10b981" stroke-width="1.5"/>
          <text x="335" y="88" fill="#34d399" font-size="14" font-weight="bold">Context Mapping &amp; ACL</text>

          <rect x="330" y="105" width="245" height="105" rx="5" fill="#0f172a" stroke="#334155"/>
          <text x="340" y="125" fill="#f43f5e" font-size="11" font-weight="bold">Anti-Corruption Layer (ACL)</text>
          <text x="340" y="145" fill="#cbd5e1" font-size="9">Legacy Payload: { tx_id, amt_cents: "245000" }</text>
          <text x="340" y="162" fill="#38bdf8" font-size="9">ACL adapts untyped vendor schema</text>
          <text x="340" y="179" fill="#34d399" font-size="9">Produces clean: PaymentAuthorizedEvent</text>
          <text x="340" y="196" fill="#cbd5e1" font-size="9">Zero pollution in Core Domain Model</text>

          <rect x="330" y="220" width="245" height="105" rx="5" fill="#0f172a" stroke="#334155"/>
          <text x="340" y="240" fill="#fbbf24" font-size="11" font-weight="bold">In-Process Domain Events</text>
          <text x="340" y="258" fill="#cbd5e1" font-size="9">Order.Place() &#8594; OrderPlacedEvent</text>
          <text x="340" y="274" fill="#cbd5e1" font-size="9">Dispatcher routes event asynchronously</text>
          <text x="340" y="290" fill="#34d399" font-size="9">Inventory consumes &#8594; StockReserved</text>
          <text x="340" y="306" fill="#a5b4fc" font-size="9">Outbox Pattern prepares broker publish</text>

          <!-- 3. LeetCode #200 Number of Islands -->
          <rect x="605" y="60" width="270" height="280" rx="10" fill="url(#d26GradGrid)" stroke="#f472b6" stroke-width="1.5"/>
          <text x="625" y="88" fill="#fbcfe8" font-size="14" font-weight="bold">LC #200: Number of Islands</text>

          <rect x="620" y="105" width="240" height="105" rx="5" fill="#0f172a" stroke="#334155"/>
          <text x="630" y="125" fill="#f472b6" font-size="11" font-weight="bold">DFS In-Place Sinking</text>
          <text x="630" y="145" fill="#cbd5e1" font-size="9">Encounter '1' &#8594; islandCount++</text>
          <text x="630" y="162" fill="#cbd5e1" font-size="9">Sink '1' &#8594; '0' on all 4 neighbors</text>
          <text x="630" y="179" fill="#34d399" font-size="9">Linear scan M x N cells</text>
          <text x="630" y="196" fill="#fbbf24" font-size="9">Time: O(M * N) | Space: O(M * N)</text>

          <rect x="620" y="220" width="240" height="105" rx="5" fill="#0f172a" stroke="#334155"/>
          <text x="630" y="240" fill="#38bdf8" font-size="11" font-weight="bold">BFS Queue Exploration</text>
          <text x="630" y="258" fill="#cbd5e1" font-size="9">Queue stores frontier cells (r, c)</text>
          <text x="630" y="274" fill="#34d399" font-size="9">Mark '0' on enqueue prevents duplicates</text>
          <text x="630" y="290" fill="#cbd5e1" font-size="9">Memory bounded by perimeter O(min(M, N))</text>
          <text x="630" y="306" fill="#94a3b8" font-size="9">Diagonals unconnected &#8594; distinct islands</text>
        </svg>
        """

    if day_num == 27:
        return """
        <svg viewBox="0 0 900 360" width="100%" height="auto" xmlns="http://www.w3.org/2000/svg">
          <defs>
            <linearGradient id="d27GradYarp" x1="0%" y1="0%" x2="100%" y2="100%"><stop offset="0%" stop-color="#1e1b4b"/><stop offset="100%" stop-color="#0f172a"/></linearGradient>
            <linearGradient id="d27GradHealth" x1="0%" y1="0%" x2="100%" y2="100%"><stop offset="0%" stop-color="#064e3b"/><stop offset="100%" stop-color="#0f172a"/></linearGradient>
            <linearGradient id="d27GradGraph" x1="0%" y1="0%" x2="100%" y2="100%"><stop offset="0%" stop-color="#581c87"/><stop offset="100%" stop-color="#0f172a"/></linearGradient>
          </defs>
          <rect width="900" height="360" rx="12" fill="#0b0f19" stroke="#1e293b" stroke-width="2"/>
          <text x="450" y="32" fill="#f8fafc" font-size="16" font-weight="bold" text-anchor="middle">Day 27: API Gateway Architecture with YARP &amp; LC #133 Clone Graph / #695 Max Area</text>

          <!-- 1. YARP Routing & Transforms -->
          <rect x="25" y="60" width="275" height="280" rx="10" fill="url(#d27GradYarp)" stroke="#818cf8" stroke-width="1.5"/>
          <text x="45" y="88" fill="#a5b4fc" font-size="14" font-weight="bold">YARP Routing &amp; Transforms</text>

          <rect x="40" y="105" width="245" height="105" rx="5" fill="#0f172a" stroke="#334155"/>
          <text x="50" y="125" fill="#38bdf8" font-size="11" font-weight="bold">Route &amp; Cluster Config</text>
          <text x="50" y="145" fill="#cbd5e1" font-size="9">Match: /api/orders/{**catch-all}</text>
          <text x="50" y="162" fill="#cbd5e1" font-size="9">Clusters &#8594; Destinations [Node1, Node2]</text>
          <text x="50" y="179" fill="#34d399" font-size="9">IProxyConfigProvider dynamic reload</text>
          <text x="50" y="196" fill="#a5b4fc" font-size="9">Zero-copy ASP.NET Core Kestrel sockets</text>

          <rect x="40" y="220" width="245" height="105" rx="5" fill="#0f172a" stroke="#334155"/>
          <text x="50" y="240" fill="#fbbf24" font-size="11" font-weight="bold">Pipeline Transforms</text>
          <text x="50" y="258" fill="#cbd5e1" font-size="9">Add: X-Forwarded-*, X-Correlation-ID</text>
          <text x="50" y="274" fill="#f87171" font-size="9">Strip: X-Client-Secret, Internal-Token</text>
          <text x="50" y="290" fill="#38bdf8" font-size="9">PathRemovePrefix: /api/v1 &#8594; /orders</text>
          <text x="50" y="306" fill="#94a3b8" font-size="9">Prevents header spoofing &amp; smuggling</text>

          <!-- 2. Health Checks & Rate Limiting -->
          <rect x="315" y="60" width="275" height="280" rx="10" fill="url(#d27GradHealth)" stroke="#10b981" stroke-width="1.5"/>
          <text x="335" y="88" fill="#34d399" font-size="14" font-weight="bold">Health Checks &amp; Rate Limiting</text>

          <rect x="330" y="105" width="245" height="105" rx="5" fill="#0f172a" stroke="#334155"/>
          <text x="340" y="125" fill="#34d399" font-size="11" font-weight="bold">Active &amp; Passive Health Checks</text>
          <text x="340" y="145" fill="#cbd5e1" font-size="9">Active: Probes GET /health every 10s</text>
          <text x="340" y="162" fill="#cbd5e1" font-size="9">Passive: Real-time 5xx &amp; timeout detection</text>
          <text x="340" y="179" fill="#f43f5e" font-size="9">Unhealthy destinations pruned dynamically</text>
          <text x="340" y="196" fill="#fbbf24" font-size="9">Load balancing: RoundRobin / P2C</text>

          <rect x="330" y="220" width="245" height="105" rx="5" fill="#0f172a" stroke="#334155"/>
          <text x="340" y="240" fill="#fbbf24" font-size="11" font-weight="bold">Gateway Rate Limiter</text>
          <text x="340" y="258" fill="#cbd5e1" font-size="9">Token Bucket partitioned by Client IP</text>
          <text x="340" y="274" fill="#f87171" font-size="9">HTTP 429 Too Many Requests</text>
          <text x="340" y="290" fill="#38bdf8" font-size="9">Headers: Retry-After, X-RateLimit-Left</text>
          <text x="340" y="306" fill="#a5b4fc" font-size="9">Protects edge before backend saturation</text>

          <!-- 3. LeetCode #133 & #695 -->
          <rect x="605" y="60" width="270" height="280" rx="10" fill="url(#d27GradGraph)" stroke="#c084fc" stroke-width="1.5"/>
          <text x="625" y="88" fill="#e9d5ff" font-size="14" font-weight="bold">LC #133 &amp; #695 Graph Algorithms</text>

          <rect x="620" y="105" width="240" height="105" rx="5" fill="#0f172a" stroke="#334155"/>
          <text x="630" y="125" fill="#c084fc" font-size="11" font-weight="bold">LC #133: Clone Graph</text>
          <text x="630" y="145" fill="#cbd5e1" font-size="9">Dictionary&lt;Node, Node&gt; visited clone map</text>
          <text x="630" y="162" fill="#34d399" font-size="9">Handles cyclic references seamlessly</text>
          <text x="630" y="179" fill="#cbd5e1" font-size="9">DFS / BFS traversal order</text>
          <text x="630" y="196" fill="#fbbf24" font-size="9">Time: O(V + E) | Space: O(V)</text>

          <rect x="620" y="220" width="240" height="105" rx="5" fill="#0f172a" stroke="#334155"/>
          <text x="630" y="240" fill="#38bdf8" font-size="11" font-weight="bold">LC #695: Max Area of Island</text>
          <text x="630" y="258" fill="#cbd5e1" font-size="9">Grid cell (r, c) = 1 &#8594; sink to 0</text>
          <text x="630" y="274" fill="#34d399" font-size="9">Accumulate area across 4 directions</text>
          <text x="630" y="290" fill="#cbd5e1" font-size="9">Global maxArea = max(maxArea, area)</text>
          <text x="630" y="306" fill="#94a3b8" font-size="9">Time: O(M * N) | Space: O(M * N)</text>
        </svg>
        """

    if day_num == 28:
        return """
        <svg viewBox="0 0 900 360" width="100%" height="auto" xmlns="http://www.w3.org/2000/svg">
          <defs>
            <linearGradient id="d28GradRefit" x1="0%" y1="0%" x2="100%" y2="100%"><stop offset="0%" stop-color="#0284c7"/><stop offset="100%" stop-color="#0f172a"/></linearGradient>
            <linearGradient id="d28GradPolly" x1="0%" y1="0%" x2="100%" y2="100%"><stop offset="0%" stop-color="#b45309"/><stop offset="100%" stop-color="#0f172a"/></linearGradient>
            <linearGradient id="d28GradFlow" x1="0%" y1="0%" x2="100%" y2="100%"><stop offset="0%" stop-color="#0f766e"/><stop offset="100%" stop-color="#0f172a"/></linearGradient>
          </defs>
          <rect width="900" height="360" rx="12" fill="#0b0f19" stroke="#1e293b" stroke-width="2"/>
          <text x="450" y="32" fill="#f8fafc" font-size="16" font-weight="bold" text-anchor="middle">Day 28: Resilient Service-to-Service HTTP with Refit &amp; Polly v8 &amp; LC #417 Pacific Atlantic</text>

          <!-- 1. Refit Type-Safe REST -->
          <rect x="25" y="60" width="275" height="280" rx="10" fill="url(#d28GradRefit)" stroke="#38bdf8" stroke-width="1.5"/>
          <text x="45" y="88" fill="#7dd3fc" font-size="14" font-weight="bold">Refit Declarative REST</text>

          <rect x="40" y="105" width="245" height="105" rx="5" fill="#0f172a" stroke="#334155"/>
          <text x="50" y="125" fill="#38bdf8" font-size="11" font-weight="bold">C# Interface Contract</text>
          <text x="50" y="145" fill="#cbd5e1" font-size="9">[Get("/api/inventory/{sku}")]</text>
          <text x="50" y="162" fill="#cbd5e1" font-size="9">[Post("/api/inventory/reserve")]</text>
          <text x="50" y="179" fill="#34d399" font-size="9">Roslyn compile-time source generator</text>
          <text x="50" y="196" fill="#a5b4fc" font-size="9">Zero raw URL strings | Strong typing</text>

          <rect x="40" y="220" width="245" height="105" rx="5" fill="#0f172a" stroke="#334155"/>
          <text x="50" y="240" fill="#fbbf24" font-size="11" font-weight="bold">Header &amp; Cancellation Binding</text>
          <text x="50" y="258" fill="#cbd5e1" font-size="9">[Header("X-Correlation-ID")] string? id</text>
          <text x="50" y="274" fill="#cbd5e1" font-size="9">CancellationToken cancellationToken</text>
          <text x="50" y="290" fill="#34d399" font-size="9">ApiResponse&lt;T&gt; with HTTP status codes</text>
          <text x="50" y="306" fill="#94a3b8" font-size="9">Automatic JSON streaming serialization</text>

          <!-- 2. Polly v8 Resilience Pipeline -->
          <rect x="315" y="60" width="275" height="280" rx="10" fill="url(#d28GradPolly)" stroke="#f59e0b" stroke-width="1.5"/>
          <text x="335" y="88" fill="#fcd34d" font-size="14" font-weight="bold">Polly v8 Resilience Pipeline</text>

          <rect x="330" y="105" width="245" height="105" rx="5" fill="#0f172a" stroke="#334155"/>
          <text x="340" y="125" fill="#fbbf24" font-size="11" font-weight="bold">Retry with Decorrelated Jitter</text>
          <text x="340" y="145" fill="#cbd5e1" font-size="9">Exponential backoff: 2^attempt * base</text>
          <text x="340" y="162" fill="#34d399" font-size="9">Full Jitter prevents retry storms</text>
          <text x="340" y="179" fill="#cbd5e1" font-size="9">Zero-allocation ValueTask execution</text>
          <text x="340" y="196" fill="#38bdf8" font-size="9">PredicateBuilder filters transient 5xx/429</text>

          <rect x="330" y="220" width="245" height="105" rx="5" fill="#0f172a" stroke="#334155"/>
          <text x="340" y="240" fill="#f87171" font-size="11" font-weight="bold">Circuit Breaker &amp; Hedging</text>
          <text x="340" y="258" fill="#cbd5e1" font-size="9">Closed &#8594; Open (Fast fail BrokenCircuit)</text>
          <text x="340" y="274" fill="#34d399" font-size="9">Half-Open: Canary probes for recovery</text>
          <text x="340" y="290" fill="#a5b4fc" font-size="9">Hedging: Parallel backup call after 40ms</text>
          <text x="340" y="306" fill="#94a3b8" font-size="9">Cuts p99 latency spikes on read replicas</text>

          <!-- 3. LeetCode #417 Pacific Atlantic -->
          <rect x="605" y="60" width="270" height="280" rx="10" fill="url(#d28GradFlow)" stroke="#14b8a6" stroke-width="1.5"/>
          <text x="625" y="88" fill="#5eead4" font-size="14" font-weight="bold">LC #417: Pacific Atlantic Flow</text>

          <rect x="620" y="105" width="240" height="105" rx="5" fill="#0f172a" stroke="#334155"/>
          <text x="630" y="125" fill="#14b8a6" font-size="11" font-weight="bold">Reverse Multi-Source Search</text>
          <text x="630" y="145" fill="#cbd5e1" font-size="9">Flow UPHILL from ocean borders</text>
          <text x="630" y="162" fill="#cbd5e1" font-size="9">Pacific: Top (r=0) &amp; Left (c=0)</text>
          <text x="630" y="179" fill="#cbd5e1" font-size="9">Atlantic: Bottom (r=m-1) &amp; Right (c=n-1)</text>
          <text x="630" y="196" fill="#fbbf24" font-size="9">Neighbor height &gt;= Current height</text>

          <rect x="620" y="220" width="240" height="105" rx="5" fill="#0f172a" stroke="#334155"/>
          <text x="630" y="240" fill="#38bdf8" font-size="11" font-weight="bold">Intersection &amp; Complexity</text>
          <text x="630" y="258" fill="#34d399" font-size="9">pacific[r,c] &amp;&amp; atlantic[r,c] == true</text>
          <text x="630" y="274" fill="#cbd5e1" font-size="9">DFS recursion &amp; BFS Queue supported</text>
          <text x="630" y="290" fill="#fbbf24" font-size="9">Optimal O(M * N) Linear Time</text>
          <text x="630" y="306" fill="#94a3b8" font-size="9">Avoids naive O((M*N)^2) search explosion</text>
        </svg>
        """

    if day_num == 29:
        return """
        <svg viewBox="0 0 900 360" width="100%" height="auto" xmlns="http://www.w3.org/2000/svg">
          <defs>
            <linearGradient id="d29GradRmq" x1="0%" y1="0%" x2="100%" y2="100%"><stop offset="0%" stop-color="#ea580c"/><stop offset="100%" stop-color="#0f172a"/></linearGradient>
            <linearGradient id="d29GradRel" x1="0%" y1="0%" x2="100%" y2="100%"><stop offset="0%" stop-color="#1e1b4b"/><stop offset="100%" stop-color="#0f172a"/></linearGradient>
            <linearGradient id="d29GradBfs" x1="0%" y1="0%" x2="100%" y2="100%"><stop offset="0%" stop-color="#042f2e"/><stop offset="100%" stop-color="#0f172a"/></linearGradient>
          </defs>
          <rect width="900" height="360" rx="12" fill="#0b0f19" stroke="#1e293b" stroke-width="2"/>
          <text x="450" y="32" fill="#f8fafc" font-size="16" font-weight="bold" text-anchor="middle">Day 29: RabbitMQ Core Concepts: Exchanges, Queues &amp; LC #130 / #994</text>

          <!-- 1. The 4 AMQP Exchanges -->
          <rect x="25" y="60" width="275" height="280" rx="10" fill="url(#d29GradRmq)" stroke="#f97316" stroke-width="1.5"/>
          <text x="45" y="88" fill="#fdba74" font-size="14" font-weight="bold">The 4 AMQP Exchange Types</text>

          <rect x="40" y="105" width="245" height="105" rx="5" fill="#0f172a" stroke="#334155"/>
          <text x="50" y="125" fill="#fbbf24" font-size="11" font-weight="bold">Direct &amp; Fanout Exchanges</text>
          <text x="50" y="145" fill="#cbd5e1" font-size="9">Direct: Exact match (order.created)</text>
          <text x="50" y="162" fill="#34d399" font-size="9">Fanout: Broadcast to ALL bound queues</text>
          <text x="50" y="179" fill="#cbd5e1" font-size="9">Bypasses routing key &#8594; High throughput</text>
          <text x="50" y="196" fill="#a5b4fc" font-size="9">Multiplexed channels over single TCP</text>

          <rect x="40" y="220" width="245" height="105" rx="5" fill="#0f172a" stroke="#334155"/>
          <text x="50" y="240" fill="#38bdf8" font-size="11" font-weight="bold">Topic &amp; Headers Exchanges</text>
          <text x="50" y="258" fill="#cbd5e1" font-size="9">Topic: * = 1 word | # = 0+ words</text>
          <text x="50" y="274" fill="#cbd5e1" font-size="9">Pattern: orders.eu.* matches orders.eu.paid</text>
          <text x="50" y="290" fill="#34d399" font-size="9">Headers: x-match all/any attribute match</text>
          <text x="50" y="306" fill="#94a3b8" font-size="9">Trie prefix tree internal routing</text>

          <!-- 2. Reliability, DLX & QoS -->
          <rect x="315" y="60" width="275" height="280" rx="10" fill="url(#d29GradRel)" stroke="#818cf8" stroke-width="1.5"/>
          <text x="335" y="88" fill="#a5b4fc" font-size="14" font-weight="bold">Reliability, DLX &amp; QoS</text>

          <rect x="330" y="105" width="245" height="105" rx="5" fill="#0f172a" stroke="#334155"/>
          <text x="340" y="125" fill="#38bdf8" font-size="11" font-weight="bold">Publisher Confirms &amp; Acks</text>
          <text x="340" y="145" fill="#cbd5e1" font-size="9">Publisher Confirms: Broker ACK on disk sync</text>
          <text x="340" y="162" fill="#34d399" font-size="9">Consumer BasicAck clears unacked state</text>
          <text x="340" y="179" fill="#fbbf24" font-size="9">BasicNack(requeue:true) &#8594; Head of queue</text>
          <text x="340" y="196" fill="#a5b4fc" font-size="9">Eliminates message loss on node crash</text>

          <rect x="330" y="220" width="245" height="105" rx="5" fill="#0f172a" stroke="#334155"/>
          <text x="340" y="240" fill="#f43f5e" font-size="11" font-weight="bold">Dead Letter Exchange &amp; QoS</text>
          <text x="340" y="258" fill="#cbd5e1" font-size="9">BasicNack(requeue:false) &#8594; Routes to DLX</text>
          <text x="340" y="274" fill="#cbd5e1" font-size="9">x-dead-letter-exchange for poison messages</text>
          <text x="340" y="290" fill="#34d399" font-size="9">basic.qos(prefetch=1) fair dispatching</text>
          <text x="340" y="306" fill="#94a3b8" font-size="9">Prevents worker buffer memory bloat</text>

          <!-- 3. LeetCode #130 & #994 -->
          <rect x="605" y="60" width="270" height="280" rx="10" fill="url(#d29GradBfs)" stroke="#2dd4bf" stroke-width="1.5"/>
          <text x="625" y="88" fill="#5eead4" font-size="14" font-weight="bold">LC #130 &amp; #994 Grid Algorithms</text>

          <rect x="620" y="105" width="240" height="105" rx="5" fill="#0f172a" stroke="#334155"/>
          <text x="630" y="125" fill="#2dd4bf" font-size="11" font-weight="bold">LC #130: Surrounded Regions</text>
          <text x="630" y="145" fill="#cbd5e1" font-size="9">Boundary 'O' cells can never be captured</text>
          <text x="630" y="162" fill="#34d399" font-size="9">Reverse DFS marks boundary 'O' &#8594; '#'</text>
          <text x="630" y="179" fill="#cbd5e1" font-size="9">Flip remaining 'O' &#8594; 'X' (captured)</text>
          <text x="630" y="196" fill="#fbbf24" font-size="9">Restore '#' &#8594; 'O' in linear O(M*N) time</text>

          <rect x="620" y="220" width="240" height="105" rx="5" fill="#0f172a" stroke="#334155"/>
          <text x="630" y="240" fill="#fbbf24" font-size="11" font-weight="bold">LC #994: Rotting Oranges</text>
          <text x="630" y="258" fill="#cbd5e1" font-size="9">Multi-Source BFS from rotten oranges (2)</text>
          <text x="630" y="274" fill="#34d399" font-size="9">Level-by-level = Minute-by-minute wave</text>
          <text x="630" y="290" fill="#cbd5e1" font-size="9">Fresh count == 0 &#8594; Return minutes</text>
          <text x="630" y="306" fill="#f87171" font-size="9">Unreachable fresh &#8594; Return -1</text>
        </svg>
        """

    if day_num == 30:
        return """
        <svg viewBox="0 0 900 360" width="100%" height="auto" xmlns="http://www.w3.org/2000/svg">
          <defs>
            <linearGradient id="d30GradBg" x1="0%" y1="0%" x2="100%" y2="100%"><stop offset="0%" stop-color="#090d16"/><stop offset="100%" stop-color="#111827"/></linearGradient>
            <linearGradient id="d30GradDlx" x1="0%" y1="0%" x2="100%" y2="100%"><stop offset="0%" stop-color="#1e1b4b"/><stop offset="100%" stop-color="#0f172a"/></linearGradient>
            <linearGradient id="d30GradSaga" x1="0%" y1="0%" x2="100%" y2="100%"><stop offset="0%" stop-color="#14532d"/><stop offset="100%" stop-color="#0f172a"/></linearGradient>
            <linearGradient id="d30GradDsa" x1="0%" y1="0%" x2="100%" y2="100%"><stop offset="0%" stop-color="#312e81"/><stop offset="100%" stop-color="#0f172a"/></linearGradient>
          </defs>
          <rect width="900" height="360" rx="12" fill="url(#d30GradBg)"/>
          
          <text x="30" y="38" fill="#f8fafc" font-size="18" font-weight="bold" font-family="sans-serif">Day 30: RabbitMQ DLQ, TTL Retry Pipeline &amp; MassTransit Sagas</text>
          
          <!-- 1. DLX & TTL Retry Loop -->
          <rect x="25" y="60" width="275" height="280" rx="10" fill="url(#d30GradDlx)" stroke="#ec4899" stroke-width="1.5"/>
          <text x="45" y="88" fill="#f472b6" font-size="14" font-weight="bold">DLX &amp; Progressive TTL Retry</text>
          
          <rect x="40" y="105" width="245" height="105" rx="5" fill="#0f172a" stroke="#334155"/>
          <text x="50" y="125" fill="#fbbf24" font-size="11" font-weight="bold">Zero-Consumer Retry Loop</text>
          <text x="50" y="145" fill="#cbd5e1" font-size="9">Worker Queue: orders.work</text>
          <text x="50" y="162" fill="#f43f5e" font-size="9">Failure &#8594; BasicReject(requeue:false)</text>
          <text x="50" y="179" fill="#38bdf8" font-size="9">orders.retry.5s: x-message-ttl = 5000ms</text>
          <text x="50" y="196" fill="#34d399" font-size="9">TTL Expiration &#8594; DLX back to orders.work</text>

          <rect x="40" y="220" width="245" height="105" rx="5" fill="#0f172a" stroke="#334155"/>
          <text x="50" y="240" fill="#f43f5e" font-size="11" font-weight="bold">Poison Queue &amp; x-death</text>
          <text x="50" y="258" fill="#cbd5e1" font-size="9">RetryCount &gt;= 3 &#8594; orders.dlq</text>
          <text x="50" y="274" fill="#a5b4fc" font-size="9">x-death: original queue, count, time</text>
          <text x="50" y="290" fill="#34d399" font-size="9">No Head-of-Line blocking (discrete TTL)</text>
          <text x="50" y="306" fill="#94a3b8" font-size="9">Quorum queue dead-letter isolation</text>

          <!-- 2. MassTransit State Machine Saga -->
          <rect x="315" y="60" width="275" height="280" rx="10" fill="url(#d30GradSaga)" stroke="#10b981" stroke-width="1.5"/>
          <text x="335" y="88" fill="#34d399" font-size="14" font-weight="bold">MassTransit State Machine Saga</text>

          <rect x="330" y="105" width="245" height="105" rx="5" fill="#0f172a" stroke="#334155"/>
          <text x="340" y="125" fill="#38bdf8" font-size="11" font-weight="bold">OrderStateInstance Workflow</text>
          <text x="340" y="145" fill="#cbd5e1" font-size="9">CorrelateById: CorrelationId Guid</text>
          <text x="340" y="162" fill="#34d399" font-size="9">Initial &#8594; Submitted &#8594; Reserved</text>
          <text x="340" y="179" fill="#38bdf8" font-size="9">PaymentCompleted &#8594; Completed</text>
          <text x="340" y="196" fill="#f43f5e" font-size="9">OrderFaulted &#8594; Faulted State</text>

          <rect x="330" y="220" width="245" height="105" rx="5" fill="#0f172a" stroke="#334155"/>
          <text x="340" y="240" fill="#fbbf24" font-size="11" font-weight="bold">Saga Concurrency &amp; Retries</text>
          <text x="340" y="258" fill="#cbd5e1" font-size="9">Optimistic Concurrency with Version</text>
          <text x="340" y="274" fill="#cbd5e1" font-size="9">GreenPipes pipeline filter chain</text>
          <text x="340" y="290" fill="#a5b4fc" font-size="9">UseMessageRetry (in-memory backoff)</text>
          <text x="340" y="306" fill="#94a3b8" font-size="9">UseDelayedRedelivery (broker backoff)</text>

          <!-- 3. LeetCode #207 & #210: Course Schedule -->
          <rect x="605" y="60" width="270" height="280" rx="10" fill="url(#d30GradDsa)" stroke="#818cf8" stroke-width="1.5"/>
          <text x="625" y="88" fill="#a5b4fc" font-size="14" font-weight="bold">LC #207 &amp; #210 Topological Sort</text>

          <rect x="620" y="105" width="240" height="105" rx="5" fill="#0f172a" stroke="#334155"/>
          <text x="630" y="125" fill="#38bdf8" font-size="11" font-weight="bold">Kahn's Algorithm (In-Degree BFS)</text>
          <text x="630" y="145" fill="#cbd5e1" font-size="9">Calculate in-degrees for all V nodes</text>
          <text x="630" y="162" fill="#34d399" font-size="9">Queue all in-degree == 0 nodes</text>
          <text x="630" y="179" fill="#cbd5e1" font-size="9">Dequeue &amp; decrement neighbor in-degree</text>
          <text x="630" y="196" fill="#fbbf24" font-size="9">Visited == V &#8594; Valid DAG (No cycle)</text>

          <rect x="620" y="220" width="240" height="105" rx="5" fill="#0f172a" stroke="#334155"/>
          <text x="630" y="240" fill="#ec4899" font-size="11" font-weight="bold">DFS 3-Coloring Cycle Detection</text>
          <text x="630" y="258" fill="#cbd5e1" font-size="9">0 = Unvisited | 1 = Visiting | 2 = Done</text>
          <text x="630" y="274" fill="#f43f5e" font-size="9">Neighbor in state 1 &#8594; Cycle detected!</text>
          <text x="630" y="290" fill="#34d399" font-size="9">Post-order reversal gives ordering</text>
          <text x="630" y="306" fill="#94a3b8" font-size="9">Linear O(V + E) time &amp; space</text>
        </svg>
        """

    if day_num == 31:
        return """
        <svg viewBox="0 0 900 360" width="100%" height="auto" xmlns="http://www.w3.org/2000/svg">
          <defs>
            <linearGradient id="d31GradBg" x1="0%" y1="0%" x2="100%" y2="100%"><stop offset="0%" stop-color="#090d16"/><stop offset="100%" stop-color="#111827"/></linearGradient>
            <linearGradient id="d31GradOutbox" x1="0%" y1="0%" x2="100%" y2="100%"><stop offset="0%" stop-color="#1e3a8a"/><stop offset="100%" stop-color="#0f172a"/></linearGradient>
            <linearGradient id="d31GradInbox" x1="0%" y1="0%" x2="100%" y2="100%"><stop offset="0%" stop-color="#14532d"/><stop offset="100%" stop-color="#0f172a"/></linearGradient>
            <linearGradient id="d31GradDsu" x1="0%" y1="0%" x2="100%" y2="100%"><stop offset="0%" stop-color="#701a75"/><stop offset="100%" stop-color="#0f172a"/></linearGradient>
          </defs>
          <rect width="900" height="360" rx="12" fill="url(#d31GradBg)"/>
          
          <text x="30" y="38" fill="#f8fafc" font-size="18" font-weight="bold" font-family="sans-serif">Day 31: The Transactional Outbox Pattern &amp; Disjoint Set Union</text>
          
          <!-- 1. The Dual-Write Problem & Atomic Outbox -->
          <rect x="25" y="60" width="275" height="280" rx="10" fill="url(#d31GradOutbox)" stroke="#38bdf8" stroke-width="1.5"/>
          <text x="45" y="88" fill="#38bdf8" font-size="14" font-weight="bold">Atomic Transactional Outbox</text>
          
          <rect x="40" y="105" width="245" height="105" rx="5" fill="#0f172a" stroke="#334155"/>
          <text x="50" y="125" fill="#f43f5e" font-size="11" font-weight="bold">Dual-Write Hazard</text>
          <text x="50" y="145" fill="#cbd5e1" font-size="9">DB Commit + Direct Broker Publish</text>
          <text x="50" y="162" fill="#f43f5e" font-size="9">Network drop after commit &#8594; Lost event</text>
          <text x="50" y="179" fill="#f43f5e" font-size="9">Broker publish + DB rollback &#8594; Ghost event</text>
          <text x="50" y="196" fill="#a5b4fc" font-size="9">2PC / XA non-viable in microservices</text>

          <rect x="40" y="220" width="245" height="105" rx="5" fill="#0f172a" stroke="#334155"/>
          <text x="50" y="240" fill="#34d399" font-size="11" font-weight="bold">Local ACID Atomicity</text>
          <text x="50" y="258" fill="#cbd5e1" font-size="9">INSERT INTO Orders (...)</text>
          <text x="50" y="274" fill="#38bdf8" font-size="9">INSERT INTO OutboxMessages (...)</text>
          <text x="50" y="290" fill="#34d399" font-size="9">Single local database transaction</text>
          <text x="50" y="306" fill="#94a3b8" font-size="9">Both commit or both roll back together</text>

          <!-- 2. Polling Publisher & Inbox Pattern -->
          <rect x="315" y="60" width="275" height="280" rx="10" fill="url(#d31GradInbox)" stroke="#10b981" stroke-width="1.5"/>
          <text x="335" y="88" fill="#34d399" font-size="14" font-weight="bold">Polling Publisher &amp; Inbox Pattern</text>

          <rect x="330" y="105" width="245" height="105" rx="5" fill="#0f172a" stroke="#334155"/>
          <text x="340" y="125" fill="#38bdf8" font-size="11" font-weight="bold">Worker Leasing &amp; SKIP LOCKED</text>
          <text x="340" y="145" fill="#cbd5e1" font-size="9">SELECT ... FOR UPDATE SKIP LOCKED</text>
          <text x="340" y="162" fill="#34d399" font-size="9">Lease timeout: LockedUntilUtc + LockId</text>
          <text x="340" y="179" fill="#cbd5e1" font-size="9">On publish confirm &#8594; ProcessedOnUtc = now</text>
          <text x="340" y="196" fill="#fbbf24" font-size="9">CDC (Debezium WAL) for sub-10ms scale</text>

          <rect x="330" y="220" width="245" height="105" rx="5" fill="#0f172a" stroke="#334155"/>
          <text x="340" y="240" fill="#a855f7" font-size="11" font-weight="bold">Idempotent Consumer (Inbox)</text>
          <text x="340" y="258" fill="#cbd5e1" font-size="9">At-Least-Once delivery produces duplicates</text>
          <text x="340" y="274" fill="#34d399" font-size="9">InboxMessages table deduplication</text>
          <text x="340" y="290" fill="#cbd5e1" font-size="9">Duplicate detected &#8594; ACK without side effect</text>
          <text x="340" y="306" fill="#94a3b8" font-size="9">Guarantees exactly-once business side effects</text>

          <!-- 3. LeetCode #684: Redundant Connection -->
          <rect x="605" y="60" width="270" height="280" rx="10" fill="url(#d31GradDsu)" stroke="#e879f9" stroke-width="1.5"/>
          <text x="625" y="88" fill="#f0abfc" font-size="14" font-weight="bold">LC #684: Disjoint Set Union</text>

          <rect x="620" y="105" width="240" height="105" rx="5" fill="#0f172a" stroke="#334155"/>
          <text x="630" y="125" fill="#38bdf8" font-size="11" font-weight="bold">Path Compression &amp; Rank</text>
          <text x="630" y="145" fill="#cbd5e1" font-size="9">parent[x] = Find(parent[x]) flattens tree</text>
          <text x="630" y="162" fill="#34d399" font-size="9">Union by rank attaches small tree to root</text>
          <text x="630" y="179" fill="#cbd5e1" font-size="9">Inverse Ackermann alpha(N) &lt; 5</text>
          <text x="630" y="196" fill="#fbbf24" font-size="9">Amortized near-constant time O(1)</text>

          <rect x="620" y="220" width="240" height="105" rx="5" fill="#0f172a" stroke="#334155"/>
          <text x="630" y="240" fill="#ec4899" font-size="11" font-weight="bold">Cycle-Forming Edge Detection</text>
          <text x="630" y="258" fill="#cbd5e1" font-size="9">For each edge (u, v):</text>
          <text x="630" y="274" fill="#f43f5e" font-size="9">Find(u) == Find(v) &#8594; Cycle detected!</text>
          <text x="630" y="290" fill="#34d399" font-size="9">Return (u, v) as redundant edge</text>
          <text x="630" y="306" fill="#94a3b8" font-size="9">Linear O(N * alpha(N)) time, O(N) space</text>
        </svg>
        """
    if day_num == 32:
        return """
        <svg viewBox="0 0 900 360" width="100%" height="auto" xmlns="http://www.w3.org/2000/svg">
          <defs>
            <linearGradient id="d32Bg" x1="0%" y1="0%" x2="100%" y2="100%"><stop offset="0%" stop-color="#090d16"/><stop offset="100%" stop-color="#111827"/></linearGradient>
            <linearGradient id="d32P1" x1="0%" y1="0%" x2="100%" y2="100%"><stop offset="0%" stop-color="#1e3a8a"/><stop offset="100%" stop-color="#0f172a"/></linearGradient>
            <linearGradient id="d32P2" x1="0%" y1="0%" x2="100%" y2="100%"><stop offset="0%" stop-color="#701a75"/><stop offset="100%" stop-color="#0f172a"/></linearGradient>
            <linearGradient id="d32P3" x1="0%" y1="0%" x2="100%" y2="100%"><stop offset="0%" stop-color="#14532d"/><stop offset="100%" stop-color="#0f172a"/></linearGradient>
          </defs>
          <rect width="900" height="360" rx="12" fill="url(#d32Bg)"/>
          <text x="30" y="38" fill="#f8fafc" font-size="18" font-weight="bold" font-family="sans-serif">Day 32: Saga Pattern: Orchestration vs Choreography &amp; Bidirectional BFS</text>
          
          <rect x="25" y="60" width="275" height="280" rx="10" fill="url(#d32P1)" stroke="#38bdf8" stroke-width="1.5"/>
          <text x="45" y="88" fill="#38bdf8" font-size="14" font-weight="bold">Saga Orchestrator (MassTransit)</text>
          <rect x="40" y="105" width="245" height="105" rx="5" fill="#0f172a" stroke="#334155"/>
          <text x="50" y="125" fill="#38bdf8" font-size="11" font-weight="bold">State Machine Definition</text>
          <text x="50" y="145" fill="#cbd5e1" font-size="9">MassTransitStateMachine&lt;OrderState&gt;</text>
          <text x="50" y="162" fill="#34d399" font-size="9">Initial &#8594; Submitted &#8594; InventoryReserved</text>
          <text x="50" y="179" fill="#38bdf8" font-size="9">PaymentAuthorized &#8594; Completed</text>
          <text x="50" y="196" fill="#f43f5e" font-size="9">CorrelateById: CorrelationId Guid</text>
          
          <rect x="40" y="220" width="245" height="105" rx="5" fill="#0f172a" stroke="#334155"/>
          <text x="50" y="240" fill="#fbbf24" font-size="11" font-weight="bold">Compensating Transactions</text>
          <text x="50" y="258" fill="#cbd5e1" font-size="9">PaymentFailedEvent triggers rollback</text>
          <text x="50" y="274" fill="#f43f5e" font-size="9">ReleaseInventoryCommand sent</text>
          <text x="50" y="290" fill="#cbd5e1" font-size="9">Semantic reversal of committed actions</text>
          <text x="50" y="306" fill="#94a3b8" font-size="9">Pivot Step: Point of no return</text>

          <rect x="315" y="60" width="275" height="280" rx="10" fill="url(#d32P2)" stroke="#c084fc" stroke-width="1.5"/>
          <text x="335" y="88" fill="#c084fc" font-size="14" font-weight="bold">Orchestration vs Choreography</text>
          <rect x="330" y="105" width="245" height="105" rx="5" fill="#0f172a" stroke="#334155"/>
          <text x="340" y="125" fill="#38bdf8" font-size="11" font-weight="bold">Choreography (Event-Driven)</text>
          <text x="340" y="145" fill="#cbd5e1" font-size="9">Decentralized: services emit domain events</text>
          <text x="340" y="162" fill="#34d399" font-size="9">High decoupling, simple for 2-3 services</text>
          <text x="340" y="179" fill="#f43f5e" font-size="9">Risk: Cyclic dependencies, tracing fog</text>
          <text x="340" y="196" fill="#fbbf24" font-size="9">Hard to track global process state</text>

          <rect x="330" y="220" width="245" height="105" rx="5" fill="#0f172a" stroke="#334155"/>
          <text x="340" y="240" fill="#a855f7" font-size="11" font-weight="bold">Orchestration (Central Coordinator)</text>
          <text x="340" y="258" fill="#cbd5e1" font-size="9">Single coordinator owns workflow state</text>
          <text x="340" y="274" fill="#34d399" font-size="9">Explicit timeouts, retries, and errors</text>
          <text x="340" y="290" fill="#cbd5e1" font-size="9">MassTransit Saga Repository (Postgres)</text>
          <text x="340" y="306" fill="#94a3b8" font-size="9">Standard for enterprise order checkout</text>

          <rect x="605" y="60" width="270" height="280" rx="10" fill="url(#d32P3)" stroke="#4ade80" stroke-width="1.5"/>
          <text x="625" y="88" fill="#4ade80" font-size="14" font-weight="bold">LC #127: Word Ladder (Hard)</text>
          <rect x="620" y="105" width="240" height="105" rx="5" fill="#0f172a" stroke="#334155"/>
          <text x="630" y="125" fill="#38bdf8" font-size="11" font-weight="bold">Bidirectional BFS Strategy</text>
          <text x="630" y="145" fill="#cbd5e1" font-size="9">Search forward from beginWord</text>
          <text x="630" y="162" fill="#34d399" font-size="9">Search backward from endWord</text>
          <text x="630" y="179" fill="#cbd5e1" font-size="9">Always expand the smaller frontier</text>
          <text x="630" y="196" fill="#fbbf24" font-size="9">Frontier intersection &#8594; Shortest path</text>

          <rect x="620" y="220" width="240" height="105" rx="5" fill="#0f172a" stroke="#334155"/>
          <text x="630" y="240" fill="#34d399" font-size="11" font-weight="bold">Complexity Reduction</text>
          <text x="630" y="258" fill="#cbd5e1" font-size="9">Standard BFS: O(B^d) branching factor</text>
          <text x="630" y="274" fill="#4ade80" font-size="9">Bidirectional: O(2 * B^(d/2))</text>
          <text x="630" y="290" fill="#cbd5e1" font-size="9">Exponentially smaller search tree</text>
          <text x="630" y="306" fill="#94a3b8" font-size="9">Runtime drops from 800ms to 25ms</text>
        </svg>
        """

    if day_num == 33:
        return """
        <svg viewBox="0 0 900 360" width="100%" height="auto" xmlns="http://www.w3.org/2000/svg">
          <defs>
            <linearGradient id="d33Bg" x1="0%" y1="0%" x2="100%" y2="100%"><stop offset="0%" stop-color="#090d16"/><stop offset="100%" stop-color="#111827"/></linearGradient>
            <linearGradient id="d33P1" x1="0%" y1="0%" x2="100%" y2="100%"><stop offset="0%" stop-color="#1e3a8a"/><stop offset="100%" stop-color="#0f172a"/></linearGradient>
            <linearGradient id="d33P2" x1="0%" y1="0%" x2="100%" y2="100%"><stop offset="0%" stop-color="#155e75"/><stop offset="100%" stop-color="#0f172a"/></linearGradient>
            <linearGradient id="d33P3" x1="0%" y1="0%" x2="100%" y2="100%"><stop offset="0%" stop-color="#701a75"/><stop offset="100%" stop-color="#0f172a"/></linearGradient>
          </defs>
          <rect width="900" height="360" rx="12" fill="url(#d33Bg)"/>
          <text x="30" y="38" fill="#f8fafc" font-size="18" font-weight="bold" font-family="sans-serif">Day 33: Distributed Idempotency &amp; Quickselect Selection</text>
          
          <rect x="25" y="60" width="275" height="280" rx="10" fill="url(#d33P1)" stroke="#38bdf8" stroke-width="1.5"/>
          <text x="45" y="88" fill="#38bdf8" font-size="14" font-weight="bold">Idempotency Protocol</text>
          <rect x="40" y="105" width="245" height="105" rx="5" fill="#0f172a" stroke="#334155"/>
          <text x="50" y="125" fill="#38bdf8" font-size="11" font-weight="bold">Key &amp; Payload Hashing</text>
          <text x="50" y="145" fill="#cbd5e1" font-size="9">Idempotency-Key header (UUID)</text>
          <text x="50" y="162" fill="#34d399" font-size="9">SHA-256 fingerprint over request body</text>
          <text x="50" y="179" fill="#f43f5e" font-size="9">Payload mismatch &#8594; 422 Unprocessable</text>
          <text x="50" y="196" fill="#cbd5e1" font-size="9">Guarantees cryptographic integrity</text>

          <rect x="40" y="220" width="245" height="105" rx="5" fill="#0f172a" stroke="#334155"/>
          <text x="50" y="240" fill="#fbbf24" font-size="11" font-weight="bold">State &amp; Response Replay</text>
          <text x="50" y="258" fill="#cbd5e1" font-size="9">Status: Processing | Completed | Failed</text>
          <text x="50" y="274" fill="#34d399" font-size="9">Completed: Replay cached 200/201 response</text>
          <text x="50" y="290" fill="#cbd5e1" font-size="9">FromCache = true header flag</text>
          <text x="50" y="306" fill="#94a3b8" font-size="9">Zero duplicate business mutations</text>

          <rect x="315" y="60" width="275" height="280" rx="10" fill="url(#d33P2)" stroke="#22d3ee" stroke-width="1.5"/>
          <text x="335" y="88" fill="#22d3ee" font-size="14" font-weight="bold">Lock Leases &amp; Fault Recovery</text>
          <rect x="330" y="105" width="245" height="105" rx="5" fill="#0f172a" stroke="#334155"/>
          <text x="340" y="125" fill="#38bdf8" font-size="11" font-weight="bold">In-Flight Concurrency</text>
          <text x="340" y="145" fill="#cbd5e1" font-size="9">Concurrent duplicate arrives</text>
          <text x="340" y="162" fill="#f43f5e" font-size="9">Status == Processing &amp;&amp; Lease active</text>
          <text x="340" y="179" fill="#fbbf24" font-size="9">Returns HTTP 409 Conflict</text>
          <text x="340" y="196" fill="#cbd5e1" font-size="9">Prevents concurrent double execution</text>

          <rect x="330" y="220" width="245" height="105" rx="5" fill="#0f172a" stroke="#334155"/>
          <text x="340" y="240" fill="#22d3ee" font-size="11" font-weight="bold">Crash Recovery via Lease TTL</text>
          <text x="340" y="258" fill="#cbd5e1" font-size="9">Worker crashes while processing</text>
          <text x="340" y="274" fill="#34d399" font-size="9">LockedUntilUtc &lt; now (lease expired)</text>
          <text x="340" y="290" fill="#cbd5e1" font-size="9">New worker safely re-acquires lock</text>
          <text x="340" y="306" fill="#94a3b8" font-size="9">No permanent deadlocks on server crashes</text>

          <rect x="605" y="60" width="270" height="280" rx="10" fill="url(#d33P3)" stroke="#e879f9" stroke-width="1.5"/>
          <text x="625" y="88" fill="#e879f9" font-size="14" font-weight="bold">LC #215: Kth Largest Element</text>
          <rect x="620" y="105" width="240" height="105" rx="5" fill="#0f172a" stroke="#334155"/>
          <text x="630" y="125" fill="#38bdf8" font-size="11" font-weight="bold">Quickselect (Randomized Pivot)</text>
          <text x="630" y="145" fill="#cbd5e1" font-size="9">Hoare's Selection: target = N - k</text>
          <text x="630" y="162" fill="#34d399" font-size="9">Average Time: O(N) linear!</text>
          <text x="630" y="179" fill="#cbd5e1" font-size="9">Space: O(1) iterative in-place</text>
          <text x="630" y="196" fill="#fbbf24" font-size="9">Random pivot avoids sorted worst case</text>

          <rect x="620" y="220" width="240" height="105" rx="5" fill="#0f172a" stroke="#334155"/>
          <text x="630" y="240" fill="#e879f9" font-size="11" font-weight="bold">Min-Heap for Streaming Data</text>
          <text x="630" y="258" fill="#cbd5e1" font-size="9">PriorityQueue&lt;int, int&gt; bounded to K</text>
          <text x="630" y="274" fill="#34d399" font-size="9">Dequeue when count &gt; K</text>
          <text x="630" y="290" fill="#cbd5e1" font-size="9">Time: O(N log K), Space: O(K)</text>
          <text x="630" y="306" fill="#94a3b8" font-size="9">Ideal for unbounded incoming streams</text>
        </svg>
        """

    if day_num == 34:
        return """
        <svg viewBox="0 0 900 360" width="100%" height="auto" xmlns="http://www.w3.org/2000/svg">
          <defs>
            <linearGradient id="d34Bg" x1="0%" y1="0%" x2="100%" y2="100%"><stop offset="0%" stop-color="#090d16"/><stop offset="100%" stop-color="#111827"/></linearGradient>
            <linearGradient id="d34P1" x1="0%" y1="0%" x2="100%" y2="100%"><stop offset="0%" stop-color="#1e3a8a"/><stop offset="100%" stop-color="#0f172a"/></linearGradient>
            <linearGradient id="d34P2" x1="0%" y1="0%" x2="100%" y2="100%"><stop offset="0%" stop-color="#047857"/><stop offset="100%" stop-color="#0f172a"/></linearGradient>
            <linearGradient id="d34P3" x1="0%" y1="0%" x2="100%" y2="100%"><stop offset="0%" stop-color="#b45309"/><stop offset="100%" stop-color="#0f172a"/></linearGradient>
          </defs>
          <rect width="900" height="360" rx="12" fill="url(#d34Bg)"/>
          <text x="30" y="38" fill="#f8fafc" font-size="18" font-weight="bold" font-family="sans-serif">Day 34: OpenTelemetry in .NET: Distributed Tracing &amp; Task Scheduling</text>
          
          <rect x="25" y="60" width="275" height="280" rx="10" fill="url(#d34P1)" stroke="#38bdf8" stroke-width="1.5"/>
          <text x="45" y="88" fill="#38bdf8" font-size="14" font-weight="bold">W3C TraceContext Protocol</text>
          <rect x="40" y="105" width="245" height="105" rx="5" fill="#0f172a" stroke="#334155"/>
          <text x="50" y="125" fill="#38bdf8" font-size="11" font-weight="bold">traceparent Header</text>
          <text x="50" y="145" fill="#cbd5e1" font-size="9">00-{trace_id}-{span_id}-{flags}</text>
          <text x="50" y="162" fill="#34d399" font-size="9">Trace ID: 32 hex chars (end-to-end)</text>
          <text x="50" y="179" fill="#38bdf8" font-size="9">Span ID: 16 hex chars (current op)</text>
          <text x="50" y="196" fill="#fbbf24" font-size="9">Flags: 01 = Sampled/Recorded</text>

          <rect x="40" y="220" width="245" height="105" rx="5" fill="#0f172a" stroke="#334155"/>
          <text x="50" y="240" fill="#a855f7" font-size="11" font-weight="bold">Baggage Propagation</text>
          <text x="50" y="258" fill="#cbd5e1" font-size="9">Contextual metadata: tenant.id=corp-1</text>
          <text x="50" y="274" fill="#34d399" font-size="9">Propagates across all downstream hops</text>
          <text x="50" y="290" fill="#cbd5e1" font-size="9">Injected in HTTP headers &amp; RabbitMQ</text>
          <text x="50" y="306" fill="#94a3b8" font-size="9">Ambient context for logs and routing</text>

          <rect x="315" y="60" width="275" height="280" rx="10" fill="url(#d34P2)" stroke="#34d399" stroke-width="1.5"/>
          <text x="335" y="88" fill="#34d399" font-size="14" font-weight="bold">Multi-Hop Span Hierarchy</text>
          <rect x="330" y="105" width="245" height="105" rx="5" fill="#0f172a" stroke="#334155"/>
          <text x="340" y="125" fill="#38bdf8" font-size="11" font-weight="bold">ActivitySource &amp; Kinds</text>
          <text x="340" y="145" fill="#cbd5e1" font-size="9">Gateway: Server Span</text>
          <text x="340" y="162" fill="#34d399" font-size="9">OrderService: Server Span (Child 1)</text>
          <text x="340" y="179" fill="#38bdf8" font-size="9">RabbitMQ: Producer Span (Child 2)</text>
          <text x="340" y="196" fill="#fbbf24" font-size="9">Inventory: Consumer Span (Child 3)</text>

          <rect x="330" y="220" width="245" height="105" rx="5" fill="#0f172a" stroke="#334155"/>
          <text x="340" y="240" fill="#34d399" font-size="11" font-weight="bold">Zero-Allocation Diagnostics</text>
          <text x="340" y="258" fill="#cbd5e1" font-size="9">Returns null if no listeners registered</text>
          <text x="340" y="274" fill="#34d399" font-size="9">Zero heap overhead when tracing off</text>
          <text x="340" y="290" fill="#cbd5e1" font-size="9">ActivityEvent milestones &amp; tags</text>
          <text x="340" y="306" fill="#94a3b8" font-size="9">Correlates distributed transactions</text>

          <rect x="605" y="60" width="270" height="280" rx="10" fill="url(#d34P3)" stroke="#f59e0b" stroke-width="1.5"/>
          <text x="625" y="88" fill="#f59e0b" font-size="14" font-weight="bold">LC #621: Task Scheduler</text>
          <rect x="620" y="105" width="240" height="105" rx="5" fill="#0f172a" stroke="#334155"/>
          <text x="630" y="125" fill="#38bdf8" font-size="11" font-weight="bold">Greedy Math Formula</text>
          <text x="630" y="145" fill="#cbd5e1" font-size="9">(maxFreq - 1) * (n + 1) + maxCount</text>
          <text x="630" y="162" fill="#34d399" font-size="9">Ans = Max(tasks.Length, slots)</text>
          <text x="630" y="179" fill="#cbd5e1" font-size="9">Time: O(N) linear, Space: O(1)</text>
          <text x="630" y="196" fill="#fbbf24" font-size="9">Optimal execution time calculation</text>

          <rect x="620" y="220" width="240" height="105" rx="5" fill="#0f172a" stroke="#334155"/>
          <text x="630" y="240" fill="#f59e0b" font-size="11" font-weight="bold">Max-Heap + Cooldown Queue</text>
          <text x="630" y="258" fill="#cbd5e1" font-size="9">PriorityQueue simulates CPU ticks</text>
          <text x="630" y="274" fill="#34d399" font-size="9">Cooldown queue enforces n cooldown</text>
          <text x="630" y="290" fill="#cbd5e1" font-size="9">Re-enqueues tasks as cooldown expires</text>
          <text x="630" y="306" fill="#94a3b8" font-size="9">Models real OS job scheduler</text>
        </svg>
        """

    if day_num == 35:
        return """
        <svg viewBox="0 0 900 360" width="100%" height="auto" xmlns="http://www.w3.org/2000/svg">
          <defs>
            <linearGradient id="d35Bg" x1="0%" y1="0%" x2="100%" y2="100%"><stop offset="0%" stop-color="#090d16"/><stop offset="100%" stop-color="#111827"/></linearGradient>
            <linearGradient id="d35P1" x1="0%" y1="0%" x2="100%" y2="100%"><stop offset="0%" stop-color="#1e3a8a"/><stop offset="100%" stop-color="#0f172a"/></linearGradient>
            <linearGradient id="d35P2" x1="0%" y1="0%" x2="100%" y2="100%"><stop offset="0%" stop-color="#701a75"/><stop offset="100%" stop-color="#0f172a"/></linearGradient>
            <linearGradient id="d35P3" x1="0%" y1="0%" x2="100%" y2="100%"><stop offset="0%" stop-color="#0284c7"/><stop offset="100%" stop-color="#0f172a"/></linearGradient>
          </defs>
          <rect width="900" height="360" rx="12" fill="url(#d35Bg)"/>
          <text x="30" y="38" fill="#f8fafc" font-size="18" font-weight="bold" font-family="sans-serif">Day 35: Observability Stack: Prometheus, Grafana, Jaeger &amp; Design Twitter</text>
          
          <rect x="25" y="60" width="275" height="280" rx="10" fill="url(#d35P1)" stroke="#38bdf8" stroke-width="1.5"/>
          <text x="45" y="88" fill="#38bdf8" font-size="14" font-weight="bold">RED &amp; USE Metric Models</text>
          <rect x="40" y="105" width="245" height="105" rx="5" fill="#0f172a" stroke="#334155"/>
          <text x="50" y="125" fill="#38bdf8" font-size="11" font-weight="bold">RED (Service Level)</text>
          <text x="50" y="145" fill="#cbd5e1" font-size="9">Rate: Requests / second counter</text>
          <text x="50" y="162" fill="#f43f5e" font-size="9">Errors: 5xx responses counter</text>
          <text x="50" y="179" fill="#34d399" font-size="9">Duration: Latency histogram (ms)</text>
          <text x="50" y="196" fill="#cbd5e1" font-size="9">Measures consumer experience</text>

          <rect x="40" y="220" width="245" height="105" rx="5" fill="#0f172a" stroke="#334155"/>
          <text x="50" y="240" fill="#fbbf24" font-size="11" font-weight="bold">USE (Hardware/Resource Level)</text>
          <text x="50" y="258" fill="#cbd5e1" font-size="9">Utilization: CPU % busy gauge</text>
          <text x="50" y="274" fill="#fbbf24" font-size="9">Saturation: ThreadPool backlog queue</text>
          <text x="50" y="290" fill="#f43f5e" font-size="9">Errors: Network drops &amp; device faults</text>
          <text x="50" y="306" fill="#94a3b8" font-size="9">Identifies underlying bottlenecks</text>

          <rect x="315" y="60" width="275" height="280" rx="10" fill="url(#d35P2)" stroke="#e879f9" stroke-width="1.5"/>
          <text x="335" y="88" fill="#e879f9" font-size="14" font-weight="bold">Percentiles &amp; Correlation</text>
          <rect x="330" y="105" width="245" height="105" rx="5" fill="#0f172a" stroke="#334155"/>
          <text x="340" y="125" fill="#38bdf8" font-size="11" font-weight="bold">P50 / P95 / P99 Tail Latency</text>
          <text x="340" y="145" fill="#cbd5e1" font-size="9">Averages hide critical tail outliers</text>
          <text x="340" y="162" fill="#34d399" font-size="9">Nearest-rank percentile formula</text>
          <text x="340" y="179" fill="#cbd5e1" font-size="9">Prometheus histogram_quantile()</text>
          <text x="340" y="196" fill="#fbbf24" font-size="9">Explicit bucket boundaries</text>

          <rect x="330" y="220" width="245" height="105" rx="5" fill="#0f172a" stroke="#334155"/>
          <text x="340" y="240" fill="#e879f9" font-size="11" font-weight="bold">Metrics + Traces + Logs</text>
          <text x="340" y="258" fill="#cbd5e1" font-size="9">Exemplars link metric spikes to traces</text>
          <text x="340" y="274" fill="#34d399" font-size="9">Structured logs embed trace_id &amp; span_id</text>
          <text x="340" y="290" fill="#cbd5e1" font-size="9">One-click drilldown in Grafana</text>
          <text x="340" y="306" fill="#94a3b8" font-size="9">Prometheus &#8594; Jaeger &#8594; Loki</text>

          <rect x="605" y="60" width="270" height="280" rx="10" fill="url(#d35P3)" stroke="#38bdf8" stroke-width="1.5"/>
          <text x="625" y="88" fill="#38bdf8" font-size="14" font-weight="bold">LC #355: Design Twitter</text>
          <rect x="620" y="105" width="240" height="105" rx="5" fill="#0f172a" stroke="#334155"/>
          <text x="630" y="125" fill="#38bdf8" font-size="11" font-weight="bold">K-Way Merge via Max-Heap</text>
          <text x="630" y="145" fill="#cbd5e1" font-size="9">Users have chronologically sorted tweets</text>
          <text x="630" y="162" fill="#34d399" font-size="9">PriorityQueue merges top 10 tweets</text>
          <text x="630" y="179" fill="#cbd5e1" font-size="9">Time: O(F log F + 10 log F)</text>
          <text x="630" y="196" fill="#fbbf24" font-size="9">Space: O(F) where F = followees</text>

          <rect x="620" y="220" width="240" height="105" rx="5" fill="#0f172a" stroke="#334155"/>
          <text x="630" y="240" fill="#38bdf8" font-size="11" font-weight="bold">Fan-out: Push vs Pull</text>
          <text x="630" y="258" fill="#cbd5e1" font-size="9">Standard users: Fan-out on write (push)</text>
          <text x="630" y="274" fill="#34d399" font-size="9">Celebrities: Fan-out on read (pull)</text>
          <text x="630" y="290" fill="#cbd5e1" font-size="9">Solves write amplification at scale</text>
          <text x="630" y="306" fill="#94a3b8" font-size="9">Enterprise news feed architecture</text>
        </svg>
        """

    if day_num == 36:
        return """
        <svg viewBox="0 0 900 360" width="100%" height="auto" xmlns="http://www.w3.org/2000/svg">
          <defs>
            <linearGradient id="d36Bg" x1="0%" y1="0%" x2="100%" y2="100%"><stop offset="0%" stop-color="#090d16"/><stop offset="100%" stop-color="#111827"/></linearGradient>
            <linearGradient id="d36P1" x1="0%" y1="0%" x2="100%" y2="100%"><stop offset="0%" stop-color="#1e3a8a"/><stop offset="100%" stop-color="#0f172a"/></linearGradient>
            <linearGradient id="d36P2" x1="0%" y1="0%" x2="100%" y2="100%"><stop offset="0%" stop-color="#065f46"/><stop offset="100%" stop-color="#0f172a"/></linearGradient>
            <linearGradient id="d36P3" x1="0%" y1="0%" x2="100%" y2="100%"><stop offset="0%" stop-color="#831843"/><stop offset="100%" stop-color="#0f172a"/></linearGradient>
          </defs>
          <rect width="900" height="360" rx="12" fill="url(#d36Bg)"/>
          <text x="30" y="38" fill="#f8fafc" font-size="18" font-weight="bold" font-family="sans-serif">Day 36: System Design: Notification Service &amp; Subsets I &amp; II</text>
          
          <rect x="25" y="60" width="275" height="280" rx="10" fill="url(#d36P1)" stroke="#38bdf8" stroke-width="1.5"/>
          <text x="45" y="88" fill="#38bdf8" font-size="14" font-weight="bold">Multi-Channel Notification Engine</text>
          <rect x="40" y="105" width="245" height="105" rx="5" fill="#0f172a" stroke="#334155"/>
          <text x="50" y="125" fill="#38bdf8" font-size="11" font-weight="bold">Channel Routing</text>
          <text x="50" y="145" fill="#cbd5e1" font-size="9">Email (SendGrid), SMS (Twilio)</text>
          <text x="50" y="162" fill="#34d399" font-size="9">Push (FCM / APNs), In-App WebSockets</text>
          <text x="50" y="179" fill="#38bdf8" font-size="9">Priority: High (OTP) &gt; Normal &gt; Low</text>
          <text x="50" y="196" fill="#fbbf24" font-size="9">Priority queue preempts bulk queues</text>

          <rect x="40" y="220" width="245" height="105" rx="5" fill="#0f172a" stroke="#334155"/>
          <text x="50" y="240" fill="#fbbf24" font-size="11" font-weight="bold">Failover &amp; Circuit Breakers</text>
          <text x="50" y="258" fill="#cbd5e1" font-size="9">Primary provider degradation (Twilio)</text>
          <text x="50" y="274" fill="#34d399" font-size="9">Auto-switch to AWS SNS fallback</text>
          <text x="50" y="290" fill="#cbd5e1" font-size="9">Idempotency deduplication cache</text>
          <text x="50" y="306" fill="#94a3b8" font-size="9">Prevents double-charging / spam</text>

          <rect x="315" y="60" width="275" height="280" rx="10" fill="url(#d36P2)" stroke="#34d399" stroke-width="1.5"/>
          <text x="335" y="88" fill="#34d399" font-size="14" font-weight="bold">Throttling &amp; User Opt-Outs</text>
          <rect x="330" y="105" width="245" height="105" rx="5" fill="#0f172a" stroke="#334155"/>
          <text x="340" y="125" fill="#38bdf8" font-size="11" font-weight="bold">Sliding-Window Rate Limiting</text>
          <text x="340" y="145" fill="#cbd5e1" font-size="9">Max 5 SMS/min per user (token bucket)</text>
          <text x="340" y="162" fill="#34d399" font-size="9">Purges timestamps older than window</text>
          <text x="340" y="179" fill="#f43f5e" font-size="9">Prevents notification fatigue &amp; spam</text>
          <text x="340" y="196" fill="#cbd5e1" font-size="9">Protects provider billing quotas</text>

          <rect x="330" y="220" width="245" height="105" rx="5" fill="#0f172a" stroke="#334155"/>
          <text x="340" y="240" fill="#34d399" font-size="11" font-weight="bold">Preference Enforcement</text>
          <text x="340" y="258" fill="#cbd5e1" font-size="9">User opt-out channel bitmask/matrix</text>
          <text x="340" y="274" fill="#34d399" font-size="9">Quiet hours: Do-not-disturb timezone</text>
          <text x="340" y="290" fill="#cbd5e1" font-size="9">Non-urgent alerts deferred to 8am</text>
          <text x="340" y="306" fill="#94a3b8" font-size="9">Regulatory compliance (GDPR/TCPA)</text>

          <rect x="605" y="60" width="270" height="280" rx="10" fill="url(#d36P3)" stroke="#f43f5e" stroke-width="1.5"/>
          <text x="625" y="88" fill="#f43f5e" font-size="14" font-weight="bold">LC #78 &amp; #90: Subsets I &amp; II</text>
          <rect x="620" y="105" width="240" height="105" rx="5" fill="#0f172a" stroke="#334155"/>
          <text x="630" y="125" fill="#38bdf8" font-size="11" font-weight="bold">Power Set Backtracking (LC #78)</text>
          <text x="630" y="145" fill="#cbd5e1" font-size="9">DFS state space exploration</text>
          <text x="630" y="162" fill="#34d399" font-size="9">Total subsets: 2^N</text>
          <text x="630" y="179" fill="#cbd5e1" font-size="9">Bitmask: 0 to (1 &lt;&lt; N) - 1</text>
          <text x="630" y="196" fill="#fbbf24" font-size="9">Time: O(N * 2^N), Space: O(N)</text>

          <rect x="620" y="220" width="240" height="105" rx="5" fill="#0f172a" stroke="#334155"/>
          <text x="630" y="240" fill="#f43f5e" font-size="11" font-weight="bold">Duplicate Pruning (LC #90)</text>
          <text x="630" y="258" fill="#cbd5e1" font-size="9">Sort array first</text>
          <text x="630" y="274" fill="#34d399" font-size="9">if (i &gt; start &amp;&amp; nums[i] == nums[i-1])</text>
          <text x="630" y="290" fill="#f43f5e" font-size="9">continue; // skip duplicate siblings</text>
          <text x="630" y="306" fill="#94a3b8" font-size="9">100% unique subsets without set allocs</text>
        </svg>
        """

    if day_num == 37:
        return """
        <svg viewBox="0 0 900 360" width="100%" height="auto" xmlns="http://www.w3.org/2000/svg">
          <defs>
            <linearGradient id="d37Bg" x1="0%" y1="0%" x2="100%" y2="100%"><stop offset="0%" stop-color="#090d16"/><stop offset="100%" stop-color="#111827"/></linearGradient>
            <linearGradient id="d37P1" x1="0%" y1="0%" x2="100%" y2="100%"><stop offset="0%" stop-color="#1e3a8a"/><stop offset="100%" stop-color="#0f172a"/></linearGradient>
            <linearGradient id="d37P2" x1="0%" y1="0%" x2="100%" y2="100%"><stop offset="0%" stop-color="#4c1d95"/><stop offset="100%" stop-color="#0f172a"/></linearGradient>
            <linearGradient id="d37P3" x1="0%" y1="0%" x2="100%" y2="100%"><stop offset="0%" stop-color="#155e75"/><stop offset="100%" stop-color="#0f172a"/></linearGradient>
          </defs>
          <rect width="900" height="360" rx="12" fill="url(#d37Bg)"/>
          <text x="30" y="38" fill="#f8fafc" font-size="18" font-weight="bold" font-family="sans-serif">Day 37: High-Throughput Chat System &amp; Combination Sum I &amp; II</text>
          
          <rect x="25" y="60" width="275" height="280" rx="10" fill="url(#d37P1)" stroke="#38bdf8" stroke-width="1.5"/>
          <text x="45" y="88" fill="#38bdf8" font-size="14" font-weight="bold">WebSocket Gateway &amp; Scale</text>
          <rect x="40" y="105" width="245" height="105" rx="5" fill="#0f172a" stroke="#334155"/>
          <text x="50" y="125" fill="#38bdf8" font-size="11" font-weight="bold">C10M Sockets Architecture</text>
          <text x="50" y="145" fill="#cbd5e1" font-size="9">Non-blocking epoll / Kestrel sockets</text>
          <text x="50" y="162" fill="#34d399" font-size="9">Reduced TCP buffer allocation (4KB)</text>
          <text x="50" y="179" fill="#38bdf8" font-size="9">1M concurrent idle sockets per 64GB node</text>
          <text x="50" y="196" fill="#fbbf24" font-size="9">NLB Layer 4 TLS termination</text>

          <rect x="40" y="220" width="245" height="105" rx="5" fill="#0f172a" stroke="#334155"/>
          <text x="50" y="240" fill="#a855f7" font-size="11" font-weight="bold">Presence &amp; Ephemeral TTL</text>
          <text x="50" y="258" fill="#cbd5e1" font-size="9">Client heartbeats every 30s</text>
          <text x="50" y="274" fill="#34d399" font-size="9">Redis SET presence ONLINE EX 60</text>
          <text x="50" y="290" fill="#f43f5e" font-size="9">Auto-offline on abrupt disconnect</text>
          <text x="50" y="306" fill="#94a3b8" font-size="9">Typing indicators bypass DB entirely</text>

          <rect x="315" y="60" width="275" height="280" rx="10" fill="url(#d37P2)" stroke="#a855f7" stroke-width="1.5"/>
          <text x="335" y="88" fill="#a855f7" font-size="14" font-weight="bold">Sequencing &amp; Redis Pub/Sub</text>
          <rect x="330" y="105" width="245" height="105" rx="5" fill="#0f172a" stroke="#334155"/>
          <text x="340" y="125" fill="#38bdf8" font-size="11" font-weight="bold">Monotonic Channel Sequencer</text>
          <text x="340" y="145" fill="#cbd5e1" font-size="9">Redis INCR channel:{id}:seq</text>
          <text x="340" y="162" fill="#34d399" font-size="9">Strict monotonic ordering: 1, 2, 3...</text>
          <text x="340" y="179" fill="#cbd5e1" font-size="9">Eliminates clock drift &amp; race conditions</text>
          <text x="340" y="196" fill="#fbbf24" font-size="9">Clients detect missing sequence gaps</text>

          <rect x="330" y="220" width="245" height="105" rx="5" fill="#0f172a" stroke="#334155"/>
          <text x="340" y="240" fill="#a855f7" font-size="11" font-weight="bold">Cross-Server Pub/Sub Backplane</text>
          <text x="340" y="258" fill="#cbd5e1" font-size="9">Server 1 publishes to chat:channel</text>
          <text x="340" y="274" fill="#34d399" font-size="9">Server 2 receives and pushes to Bob</text>
          <text x="340" y="290" fill="#cbd5e1" font-size="9">ScyllaDB append: ((channel_id), seq)</text>
          <text x="340" y="306" fill="#94a3b8" font-size="9">Fast contiguous SSTable range queries</text>

          <rect x="605" y="60" width="270" height="280" rx="10" fill="url(#d37P3)" stroke="#22d3ee" stroke-width="1.5"/>
          <text x="625" y="88" fill="#22d3ee" font-size="14" font-weight="bold">LC #39 &amp; #40: Combination Sum</text>
          <rect x="620" y="105" width="240" height="105" rx="5" fill="#0f172a" stroke="#334155"/>
          <text x="630" y="125" fill="#38bdf8" font-size="11" font-weight="bold">Combination Sum I (Reuse)</text>
          <text x="630" y="145" fill="#cbd5e1" font-size="9">Unlimited element reuse allowed</text>
          <text x="630" y="162" fill="#34d399" font-size="9">Recurse on same index i</text>
          <text x="630" y="179" fill="#cbd5e1" font-size="9">Sort candidates for early pruning</text>
          <text x="630" y="196" fill="#fbbf24" font-size="9">if (candidates[i] &gt; remain) break;</text>

          <rect x="620" y="220" width="240" height="105" rx="5" fill="#0f172a" stroke="#334155"/>
          <text x="630" y="240" fill="#22d3ee" font-size="11" font-weight="bold">Combination Sum II (Single Use)</text>
          <text x="630" y="258" fill="#cbd5e1" font-size="9">Each element used at most once</text>
          <text x="630" y="274" fill="#34d399" font-size="9">Recurse on index i + 1</text>
          <text x="630" y="290" fill="#f43f5e" font-size="9">if (i &gt; start &amp;&amp; c[i] == c[i-1]) continue;</text>
          <text x="630" y="306" fill="#94a3b8" font-size="9">Zero duplicate combination results</text>
        </svg>
        """

    if day_num == 38:
        return """
        <svg viewBox="0 0 900 360" width="100%" height="auto" xmlns="http://www.w3.org/2000/svg">
          <defs>
            <linearGradient id="d38Bg" x1="0%" y1="0%" x2="100%" y2="100%"><stop offset="0%" stop-color="#090d16"/><stop offset="100%" stop-color="#111827"/></linearGradient>
            <linearGradient id="d38P1" x1="0%" y1="0%" x2="100%" y2="100%"><stop offset="0%" stop-color="#1e3a8a"/><stop offset="100%" stop-color="#0f172a"/></linearGradient>
            <linearGradient id="d38P2" x1="0%" y1="0%" x2="100%" y2="100%"><stop offset="0%" stop-color="#065f46"/><stop offset="100%" stop-color="#0f172a"/></linearGradient>
            <linearGradient id="d38P3" x1="0%" y1="0%" x2="100%" y2="100%"><stop offset="0%" stop-color="#701a75"/><stop offset="100%" stop-color="#0f172a"/></linearGradient>
          </defs>
          <rect width="900" height="360" rx="12" fill="url(#d38Bg)"/>
          <text x="30" y="38" fill="#f8fafc" font-size="18" font-weight="bold" font-family="sans-serif">Day 38: Video Streaming System (YouTube/Netflix) &amp; Permutations</text>
          
          <rect x="25" y="60" width="275" height="280" rx="10" fill="url(#d38P1)" stroke="#38bdf8" stroke-width="1.5"/>
          <text x="45" y="88" fill="#38bdf8" font-size="14" font-weight="bold">ABR &amp; HLS Playlists (.m3u8)</text>
          <rect x="40" y="105" width="245" height="105" rx="5" fill="#0f172a" stroke="#334155"/>
          <text x="50" y="125" fill="#38bdf8" font-size="11" font-weight="bold">Master Playlist Index</text>
          <text x="50" y="145" fill="#cbd5e1" font-size="9">#EXT-X-STREAM-INF:BANDWIDTH=5M</text>
          <text x="50" y="162" fill="#34d399" font-size="9">RESOLUTION=1920x1080 (1080p)</text>
          <text x="50" y="179" fill="#38bdf8" font-size="9">Renditions: 1080p, 720p, 480p, 360p</text>
          <text x="50" y="196" fill="#fbbf24" font-size="9">Player switches bitrates dynamically</text>

          <rect x="40" y="220" width="245" height="105" rx="5" fill="#0f172a" stroke="#334155"/>
          <text x="50" y="240" fill="#fbbf24" font-size="11" font-weight="bold">Media Variant Chunks</text>
          <text x="50" y="258" fill="#cbd5e1" font-size="9">#EXT-X-TARGETDURATION:4</text>
          <text x="50" y="274" fill="#34d399" font-size="9">#EXTINF:4.000, segment_0.ts</text>
          <text x="50" y="290" fill="#cbd5e1" font-size="9">MPEG-2 TS or fragmented MP4 (.m4s)</text>
          <text x="50" y="306" fill="#94a3b8" font-size="9">Short chunks prevent playback stalls</text>

          <rect x="315" y="60" width="275" height="280" rx="10" fill="url(#d38P2)" stroke="#34d399" stroke-width="1.5"/>
          <text x="335" y="88" fill="#34d399" font-size="14" font-weight="bold">S3 Direct Upload &amp; CDN Cache</text>
          <rect x="330" y="105" width="245" height="105" rx="5" fill="#0f172a" stroke="#334155"/>
          <text x="340" y="125" fill="#38bdf8" font-size="11" font-weight="bold">Presigned Multipart S3</text>
          <text x="340" y="145" fill="#cbd5e1" font-size="9">Upload direct to S3 via HMAC URLs</text>
          <text x="340" y="162" fill="#34d399" font-size="9">Zero web server bandwidth saturation</text>
          <text x="340" y="179" fill="#cbd5e1" font-size="9">5MB-10MB chunks with parallel retries</text>
          <text x="340" y="196" fill="#fbbf24" font-size="9">ETag completion verification</text>

          <rect x="330" y="220" width="245" height="105" rx="5" fill="#0f172a" stroke="#334155"/>
          <text x="340" y="240" fill="#34d399" font-size="11" font-weight="bold">CDN Edge Caching Strategy</text>
          <text x="340" y="258" fill="#cbd5e1" font-size="9">Chunks (.ts): max-age=31536000, immutable</text>
          <text x="340" y="274" fill="#34d399" font-size="9">&gt;98% edge cache hit ratio</text>
          <text x="340" y="290" fill="#f43f5e" font-size="9">Playlists (.m3u8): max-age=10s</text>
          <text x="340" y="306" fill="#94a3b8" font-size="9">Signed tokens protect premium VOD</text>

          <rect x="605" y="60" width="270" height="280" rx="10" fill="url(#d38P3)" stroke="#e879f9" stroke-width="1.5"/>
          <text x="625" y="88" fill="#e879f9" font-size="14" font-weight="bold">LC #46 &amp; #47: Permutations</text>
          <rect x="620" y="105" width="240" height="105" rx="5" fill="#0f172a" stroke="#334155"/>
          <text x="630" y="125" fill="#38bdf8" font-size="11" font-weight="bold">Permutations I (Unique)</text>
          <text x="630" y="145" fill="#cbd5e1" font-size="9">Backtracking with used[i] array</text>
          <text x="630" y="162" fill="#34d399" font-size="9">Total permutations: N!</text>
          <text x="630" y="179" fill="#cbd5e1" font-size="9">Time: O(N * N!), Space: O(N)</text>
          <text x="630" y="196" fill="#fbbf24" font-size="9">Exhaustive state-space search</text>

          <rect x="620" y="220" width="240" height="105" rx="5" fill="#0f172a" stroke="#334155"/>
          <text x="630" y="240" fill="#e879f9" font-size="11" font-weight="bold">Permutations II (Duplicates)</text>
          <text x="630" y="258" fill="#cbd5e1" font-size="9">Sort array first</text>
          <text x="630" y="274" fill="#34d399" font-size="9">if (nums[i] == nums[i-1] &amp;&amp; !used[i-1])</text>
          <text x="630" y="290" fill="#f43f5e" font-size="9">continue; // skip duplicate branch</text>
          <text x="630" y="306" fill="#94a3b8" font-size="9">Prunes identical permutation paths</text>
        </svg>
        """

    if day_num == 39:
        return """
        <svg viewBox="0 0 900 360" width="100%" height="auto" xmlns="http://www.w3.org/2000/svg">
          <defs>
            <linearGradient id="d39Bg" x1="0%" y1="0%" x2="100%" y2="100%"><stop offset="0%" stop-color="#090d16"/><stop offset="100%" stop-color="#111827"/></linearGradient>
            <linearGradient id="d39P1" x1="0%" y1="0%" x2="100%" y2="100%"><stop offset="0%" stop-color="#1e3a8a"/><stop offset="100%" stop-color="#0f172a"/></linearGradient>
            <linearGradient id="d39P2" x1="0%" y1="0%" x2="100%" y2="100%"><stop offset="0%" stop-color="#14532d"/><stop offset="100%" stop-color="#0f172a"/></linearGradient>
            <linearGradient id="d39P3" x1="0%" y1="0%" x2="100%" y2="100%"><stop offset="0%" stop-color="#701a75"/><stop offset="100%" stop-color="#0f172a"/></linearGradient>
          </defs>
          <rect width="900" height="360" rx="12" fill="url(#d39Bg)"/>
          <text x="30" y="38" fill="#f8fafc" font-size="18" font-weight="bold" font-family="sans-serif">Day 39: CAP &amp; PACELC Theorems &amp; 2D Word Search / Phone Backtracking</text>
          
          <rect x="25" y="60" width="275" height="280" rx="10" fill="url(#d39P1)" stroke="#38bdf8" stroke-width="1.5"/>
          <text x="45" y="88" fill="#38bdf8" font-size="14" font-weight="bold">CAP &amp; PACELC Theorems</text>
          <rect x="40" y="105" width="245" height="105" rx="5" fill="#0f172a" stroke="#334155"/>
          <text x="50" y="125" fill="#38bdf8" font-size="11" font-weight="bold">CAP Under Partition (P)</text>
          <text x="50" y="145" fill="#cbd5e1" font-size="9">Partitions are inevitable physical reality</text>
          <text x="50" y="162" fill="#34d399" font-size="9">CP: Preserves consistency, drops writes</text>
          <text x="50" y="179" fill="#fbbf24" font-size="9">AP: Preserves availability, accepts stale</text>
          <text x="50" y="196" fill="#f43f5e" font-size="9">No real distributed system can choose CA</text>

          <rect x="40" y="220" width="245" height="105" rx="5" fill="#0f172a" stroke="#334155"/>
          <text x="50" y="240" fill="#a855f7" font-size="11" font-weight="bold">PACELC Normal State (E)</text>
          <text x="50" y="258" fill="#cbd5e1" font-size="9">Else: Latency (L) vs Consistency (C)</text>
          <text x="50" y="274" fill="#34d399" font-size="9">PC/EC: Spanner, CockroachDB</text>
          <text x="50" y="290" fill="#cbd5e1" font-size="9">PA/EL: Cassandra, DynamoDB, Couchbase</text>
          <text x="50" y="306" fill="#94a3b8" font-size="9">PC/EL: MongoDB primary reads</text>

          <rect x="315" y="60" width="275" height="280" rx="10" fill="url(#d39P2)" stroke="#4ade80" stroke-width="1.5"/>
          <text x="335" y="88" fill="#4ade80" font-size="14" font-weight="bold">Tunable Quorums (N, W, R)</text>
          <rect x="330" y="105" width="245" height="105" rx="5" fill="#0f172a" stroke="#334155"/>
          <text x="340" y="125" fill="#38bdf8" font-size="11" font-weight="bold">Strong Consistency (R + W &gt; N)</text>
          <text x="340" y="145" fill="#cbd5e1" font-size="9">Pigeonhole: Read &amp; write sets overlap</text>
          <text x="340" y="162" fill="#34d399" font-size="9">N=3, W=2, R=2 &#8594; 2+2=4 &gt; 3 (Strong)</text>
          <text x="340" y="179" fill="#cbd5e1" font-size="9">N=3, W=1, R=1 &#8594; Eventual consistency</text>
          <text x="340" y="196" fill="#fbbf24" font-size="9">Guarantees linearizable client reads</text>

          <rect x="330" y="220" width="245" height="105" rx="5" fill="#0f172a" stroke="#334155"/>
          <text x="340" y="240" fill="#4ade80" font-size="11" font-weight="bold">Read Repair &amp; LWW</text>
          <text x="340" y="258" fill="#cbd5e1" font-size="9">Coordinator resolves version conflicts</text>
          <text x="340" y="274" fill="#34d399" font-size="9">Latest version wins (LWW / Vector clock)</text>
          <text x="340" y="290" fill="#cbd5e1" font-size="9">Asynchronously heals stale replica nodes</text>
          <text x="340" y="306" fill="#94a3b8" font-size="9">Self-healing cluster data consistency</text>

          <rect x="605" y="60" width="270" height="280" rx="10" fill="url(#d39P3)" stroke="#e879f9" stroke-width="1.5"/>
          <text x="625" y="88" fill="#e879f9" font-size="14" font-weight="bold">LC #79 &amp; #17: DFS Backtracking</text>
          <rect x="620" y="105" width="240" height="105" rx="5" fill="#0f172a" stroke="#334155"/>
          <text x="630" y="125" fill="#38bdf8" font-size="11" font-weight="bold">Word Search (2D Grid DFS)</text>
          <text x="630" y="145" fill="#cbd5e1" font-size="9">In-place cell marking: board[r][c] = '#'</text>
          <text x="630" y="162" fill="#34d399" font-size="9">O(1) auxiliary space (no hash set alloc)</text>
          <text x="630" y="179" fill="#cbd5e1" font-size="9">Time: O(M * N * 3^L)</text>
          <text x="630" y="196" fill="#fbbf24" font-size="9">Restores original char on backtrack</text>

          <rect x="620" y="220" width="240" height="105" rx="5" fill="#0f172a" stroke="#334155"/>
          <text x="630" y="240" fill="#e879f9" font-size="11" font-weight="bold">Letter Combinations (Phone)</text>
          <text x="630" y="258" fill="#cbd5e1" font-size="9">Keypad digit mapping: '2' &#8594; "abc"</text>
          <text x="630" y="274" fill="#34d399" font-size="9">Cartesian product generation</text>
          <text x="630" y="290" fill="#cbd5e1" font-size="9">Time: O(4^N * N), Space: O(N)</text>
          <text x="630" y="306" fill="#94a3b8" font-size="9">Recursive backtracking formulation</text>
        </svg>
        """

    if day_num == 40:
        return """
        <svg viewBox="0 0 900 360" width="100%" height="auto" xmlns="http://www.w3.org/2000/svg">
          <defs>
            <linearGradient id="d40Bg" x1="0%" y1="0%" x2="100%" y2="100%"><stop offset="0%" stop-color="#090d16"/><stop offset="100%" stop-color="#111827"/></linearGradient>
            <linearGradient id="d40P1" x1="0%" y1="0%" x2="100%" y2="100%"><stop offset="0%" stop-color="#1e3a8a"/><stop offset="100%" stop-color="#0f172a"/></linearGradient>
            <linearGradient id="d40P2" x1="0%" y1="0%" x2="100%" y2="100%"><stop offset="0%" stop-color="#701a75"/><stop offset="100%" stop-color="#0f172a"/></linearGradient>
            <linearGradient id="d40P3" x1="0%" y1="0%" x2="100%" y2="100%"><stop offset="0%" stop-color="#065f46"/><stop offset="100%" stop-color="#0f172a"/></linearGradient>
          </defs>
          <rect width="900" height="360" rx="12" fill="url(#d40Bg)"/>
          <text x="30" y="38" fill="#f8fafc" font-size="18" font-weight="bold" font-family="sans-serif">Day 40: Event Sourcing, CQRS &amp; Palindrome Partitioning</text>
          
          <rect x="25" y="60" width="275" height="280" rx="10" fill="url(#d40P1)" stroke="#38bdf8" stroke-width="1.5"/>
          <text x="45" y="88" fill="#38bdf8" font-size="14" font-weight="bold">Event Sourcing Core</text>
          <rect x="40" y="105" width="245" height="105" rx="5" fill="#0f172a" stroke="#334155"/>
          <text x="50" y="125" fill="#38bdf8" font-size="11" font-weight="bold">Append-Only Event Store</text>
          <text x="50" y="145" fill="#cbd5e1" font-size="9">Immutable domain events</text>
          <text x="50" y="162" fill="#34d399" font-size="9">State = FoldLeft(Initial, Events)</text>
          <text x="50" y="179" fill="#38bdf8" font-size="9">100% auditability &amp; time travel</text>
          <text x="50" y="196" fill="#fbbf24" font-size="9">Zero destructive UPDATEs</text>

          <rect x="40" y="220" width="245" height="105" rx="5" fill="#0f172a" stroke="#334155"/>
          <text x="50" y="240" fill="#fbbf24" font-size="11" font-weight="bold">Optimistic Concurrency &amp; OCC</text>
          <text x="50" y="258" fill="#cbd5e1" font-size="9">ExpectedVersion check on stream</text>
          <text x="50" y="274" fill="#f43f5e" font-size="9">Unique constraint: (stream_id, version)</text>
          <text x="50" y="290" fill="#cbd5e1" font-size="9">Rejects concurrent write collisions</text>
          <text x="50" y="306" fill="#94a3b8" font-size="9">Snapshots bound replay time to O(k)</text>

          <rect x="315" y="60" width="275" height="280" rx="10" fill="url(#d40P2)" stroke="#c084fc" stroke-width="1.5"/>
          <text x="335" y="88" fill="#c084fc" font-size="14" font-weight="bold">CQRS Read Projections</text>
          <rect x="330" y="105" width="245" height="105" rx="5" fill="#0f172a" stroke="#334155"/>
          <text x="340" y="125" fill="#38bdf8" font-size="11" font-weight="bold">Command / Query Separation</text>
          <text x="340" y="145" fill="#cbd5e1" font-size="9">Commands: Hit Aggregate Root (Write)</text>
          <text x="340" y="162" fill="#34d399" font-size="9">Queries: Hit Denormalized Views (Read)</text>
          <text x="340" y="179" fill="#cbd5e1" font-size="9">Asymmetric scaling: 99% reads</text>
          <text x="340" y="196" fill="#fbbf24" font-size="9">Postgres, Redis, or Elasticsearch views</text>

          <rect x="330" y="220" width="245" height="105" rx="5" fill="#0f172a" stroke="#334155"/>
          <text x="340" y="240" fill="#c084fc" font-size="11" font-weight="bold">Eventual Consistency in UI</text>
          <text x="340" y="258" fill="#cbd5e1" font-size="9">Projections update asynchronously</text>
          <text x="340" y="274" fill="#34d399" font-size="9">Optimistic UI updates</text>
          <text x="340" y="290" fill="#cbd5e1" font-size="9">Read-your-own-writes version tokens</text>
          <text x="340" y="306" fill="#94a3b8" font-size="9">GDPR crypto-shredding keys</text>

          <rect x="605" y="60" width="270" height="280" rx="10" fill="url(#d40P3)" stroke="#34d399" stroke-width="1.5"/>
          <text x="625" y="88" fill="#34d399" font-size="14" font-weight="bold">LC #131: Palindrome Partition</text>
          <rect x="620" y="105" width="240" height="105" rx="5" fill="#0f172a" stroke="#334155"/>
          <text x="630" y="125" fill="#38bdf8" font-size="11" font-weight="bold">DP Table Precomputation</text>
          <text x="630" y="145" fill="#cbd5e1" font-size="9">dp[i, j] = true if s[i..j] is palindrome</text>
          <text x="630" y="162" fill="#34d399" font-size="9">dp[i, j] = (s[i] == s[j]) &amp;&amp; dp[i+1, j-1]</text>
          <text x="630" y="179" fill="#cbd5e1" font-size="9">O(N^2) table initialization</text>
          <text x="630" y="196" fill="#fbbf24" font-size="9">O(1) lookup during backtracking</text>

          <rect x="620" y="220" width="240" height="105" rx="5" fill="#0f172a" stroke="#334155"/>
          <text x="630" y="240" fill="#34d399" font-size="11" font-weight="bold">Backtracking DFS</text>
          <text x="630" y="258" fill="#cbd5e1" font-size="9">Traverse all partitioning points</text>
          <text x="630" y="274" fill="#34d399" font-size="9">Eliminates repeated string reversals</text>
          <text x="630" y="290" fill="#cbd5e1" font-size="9">Time: O(N * 2^N), Space: O(N^2)</text>
          <text x="630" y="306" fill="#94a3b8" font-size="9">High-performance partition generation</text>
        </svg>
        """

    if day_num == 41:
        return """
        <svg viewBox="0 0 900 360" width="100%" height="auto" xmlns="http://www.w3.org/2000/svg">
          <defs>
            <linearGradient id="d41Bg" x1="0%" y1="0%" x2="100%" y2="100%"><stop offset="0%" stop-color="#090d16"/><stop offset="100%" stop-color="#111827"/></linearGradient>
            <linearGradient id="d41P1" x1="0%" y1="0%" x2="100%" y2="100%"><stop offset="0%" stop-color="#1e3a8a"/><stop offset="100%" stop-color="#0f172a"/></linearGradient>
            <linearGradient id="d41P2" x1="0%" y1="0%" x2="100%" y2="100%"><stop offset="0%" stop-color="#065f46"/><stop offset="100%" stop-color="#0f172a"/></linearGradient>
            <linearGradient id="d41P3" x1="0%" y1="0%" x2="100%" y2="100%"><stop offset="0%" stop-color="#7c2d12"/><stop offset="100%" stop-color="#0f172a"/></linearGradient>
          </defs>
          <rect width="900" height="360" rx="12" fill="url(#d41Bg)"/>
          <text x="30" y="38" fill="#f8fafc" font-size="18" font-weight="bold" font-family="sans-serif">Day 41: Docker Deep Dive for .NET Services &amp; Dynamic Programming</text>
          
          <rect x="25" y="60" width="275" height="280" rx="10" fill="url(#d41P1)" stroke="#38bdf8" stroke-width="1.5"/>
          <text x="45" y="88" fill="#38bdf8" font-size="14" font-weight="bold">Multi-Stage &amp; Chiseled Runtime</text>
          <rect x="40" y="105" width="245" height="105" rx="5" fill="#0f172a" stroke="#334155"/>
          <text x="50" y="125" fill="#38bdf8" font-size="11" font-weight="bold">Layer Caching Optimization</text>
          <text x="50" y="145" fill="#cbd5e1" font-size="9">COPY *.csproj before source</text>
          <text x="50" y="162" fill="#34d399" font-size="9">RUN dotnet restore cached in layer</text>
          <text x="50" y="179" fill="#cbd5e1" font-size="9">Zero package re-downloads on edit</text>
          <text x="50" y="196" fill="#fbbf24" font-size="9">Sub-second incremental build times</text>

          <rect x="40" y="220" width="245" height="105" rx="5" fill="#0f172a" stroke="#334155"/>
          <text x="50" y="240" fill="#38bdf8" font-size="11" font-weight="bold">Ubuntu Chiseled Security</text>
          <text x="50" y="258" fill="#cbd5e1" font-size="9">aspnet:8.0-jammy-chiseled base</text>
          <text x="50" y="274" fill="#34d399" font-size="9">USER app (UID 1654, non-root)</text>
          <text x="50" y="290" fill="#f43f5e" font-size="9">Zero shells, zero package managers</text>
          <text x="50" y="306" fill="#94a3b8" font-size="9">Near-zero CVE attack surface</text>

          <rect x="315" y="60" width="275" height="280" rx="10" fill="url(#d41P2)" stroke="#34d399" stroke-width="1.5"/>
          <text x="335" y="88" fill="#34d399" font-size="14" font-weight="bold">Cgroups &amp; GC Auto-Tuning</text>
          <rect x="330" y="105" width="245" height="105" rx="5" fill="#0f172a" stroke="#334155"/>
          <text x="340" y="125" fill="#38bdf8" font-size="11" font-weight="bold">Preventing 137 OOMKilled</text>
          <text x="340" y="145" fill="#cbd5e1" font-size="9">DOTNET_GCHeapHardLimitPercent=75</text>
          <text x="340" y="162" fill="#34d399" font-size="9">Caps managed heap at 75% limit</text>
          <text x="340" y="179" fill="#cbd5e1" font-size="9">Reserves 25% for native buffers</text>
          <text x="340" y="196" fill="#fbbf24" font-size="9">Thread stacks &amp; JIT code cache safe</text>

          <rect x="330" y="220" width="245" height="105" rx="5" fill="#0f172a" stroke="#334155"/>
          <text x="340" y="240" fill="#34d399" font-size="11" font-weight="bold">Server vs Workstation GC</text>
          <text x="340" y="258" fill="#cbd5e1" font-size="9">DOTNET_gcServer=1 for multi-core</text>
          <text x="340" y="274" fill="#34d399" font-size="9">Dedicated GC thread per CPU quota</text>
          <text x="340" y="290" fill="#cbd5e1" font-size="9">Workstation GC for &lt;= 1 CPU / 512MB</text>
          <text x="340" y="306" fill="#94a3b8" font-size="9">Eliminates thread context switching</text>

          <rect x="605" y="60" width="270" height="280" rx="10" fill="url(#d41P3)" stroke="#ea580c" stroke-width="1.5"/>
          <text x="625" y="88" fill="#ea580c" font-size="14" font-weight="bold">LC #70 &amp; #746: Dynamic Programming</text>
          <rect x="620" y="105" width="240" height="105" rx="5" fill="#0f172a" stroke="#334155"/>
          <text x="630" y="125" fill="#38bdf8" font-size="11" font-weight="bold">Climbing Stairs (LC #70)</text>
          <text x="630" y="145" fill="#cbd5e1" font-size="9">Fibonacci recurrence: dp[i] = dp[i-1] + dp[i-2]</text>
          <text x="630" y="162" fill="#34d399" font-size="9">Base cases: dp[1]=1, dp[2]=2</text>
          <text x="630" y="179" fill="#cbd5e1" font-size="9">Rolling variables: O(1) space</text>
          <text x="630" y="196" fill="#fbbf24" font-size="9">Time: O(N) linear</text>

          <rect x="620" y="220" width="240" height="105" rx="5" fill="#0f172a" stroke="#334155"/>
          <text x="630" y="240" fill="#ea580c" font-size="11" font-weight="bold">Min Cost Climbing Stairs (LC #746)</text>
          <text x="630" y="258" fill="#cbd5e1" font-size="9">dp[i] = min(dp[i-1]+c[i-1], dp[i-2]+c[i-2])</text>
          <text x="630" y="274" fill="#34d399" font-size="9">Start at step 0 or 1</text>
          <text x="630" y="290" fill="#cbd5e1" font-size="9">Two rolling variables: O(1) space</text>
          <text x="630" y="306" fill="#94a3b8" font-size="9">Optimal substructure &amp; overlapping subproblems</text>
        </svg>
        """

    return ""

def format_interview_qa(interview_md):
    """Parses markdown Q&A into rich HTML interactive cards with collapsibles."""
    if not interview_md:
        return ""
    
    # Split by ### Q
    pattern = re.compile(r'###\s+Q(\d+):\s*(.*?)(?=(?:###\s+Q\d+:|$))', re.DOTALL)
    matches = pattern.findall(interview_md)
    
    if not matches:
        return f'<div class="markdown-content">{html.escape(interview_md)}</div>'
    
    qa_cards = []
    for q_num, q_body in matches:
        lines = q_body.strip().split("\n")
        q_title = lines[0].strip()
        body_content = "\n".join(lines[1:]).strip()
        
        # Escape body content for client-side marked rendering
        card_id = f"qa-card-{q_num}"
        qa_cards.append(f"""
        <div class="qa-card" id="{card_id}">
          <div class="qa-header" onclick="toggleQa('{card_id}')">
            <div style="display:flex; align-items:center; gap:10px;">
              <span class="qa-badge">Q{q_num}</span>
              <span class="qa-title">{html.escape(q_title)}</span>
            </div>
            <span class="qa-toggle-icon">▼</span>
          </div>
          <div class="qa-body markdown-content" style="display:block;">
            <div class="raw-qa-content">{html.escape(body_content)}</div>
          </div>
        </div>
        """)
    
    return "\n".join(qa_cards)

print("Helper functions ready.")
