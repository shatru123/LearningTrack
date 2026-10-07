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
