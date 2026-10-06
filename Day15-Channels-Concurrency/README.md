# Day 15: Channels & Concurrency in C#

## Overview
High-throughput asynchronous producer-consumer architectures using `System.Threading.Channels`. This module explores zero-allocation bounded channels, backpressure enforcement (`BoundedChannelFullMode`), multi-producer/multi-consumer worker scheduling, cancellation propagation, and pointer manipulation DSA algorithms: Reverse Linked List (LeetCode #206) and Merge Two Sorted Lists (LeetCode #21).

---

## Key Architectural Concepts

### 1. `System.Threading.Channels` vs `BlockingCollection<T>`
- **`BlockingCollection<T>`**: Synchronous thread-blocking concurrency primitive (`Take()` blocks OS threads). In asynchronous web services, blocking threads causes thread pool starvation.
- **`Channel<T>`**: High-performance asynchronous construct (`ReadAsync()`, `WriteAsync()`) that cooperatively yields threads back to the .NET ThreadPool using `ValueTask<bool>` and lock-free ring buffers.

### 2. Backpressure Modes in Bounded Channels
When producer throughput outpaces consumer capacity, an unbounded channel leads to unbounded heap memory growth and fatal OutOfMemory exceptions. Bounded channels enforce backpressure:
- **`BoundedChannelFullMode.Wait`**: Asynchronously halts producers until buffer space is reclaimed. Guarantees zero message loss.
- **`BoundedChannelFullMode.DropOldest`**: Discards the oldest queued item to accept new writes. Ideal for real-time telemetry or latest market price feeds.
- **`BoundedChannelFullMode.DropNewest`**: Drops the newly incoming message if full.
- **`BoundedChannelFullMode.DropWrite`**: Rejects writes without queuing.

### 3. Graceful Shutdown & Completion Semantics
Producers signal completion via `channel.Writer.Complete()`.
Consumers iterating over `channel.Reader.ReadAllAsync()` automatically drain all buffered items and gracefully terminate once the queue is empty.

---

## LeetCode Problems Solved

### LeetCode #206: Reverse Linked List
- **Algorithm**: Three-pointer in-place iteration (`prev`, `curr`, `nextTemp`) and recursive post-order pointer inversion.
- **Complexity**: Time: $O(N)$, Space: $O(1)$ iterative, $O(N)$ recursive call stack.

### LeetCode #21: Merge Two Sorted Lists
- **Algorithm**: Two-pointer splice using a sentinel dummy head node to eliminate special cases for the head pointer.
- **Complexity**: Time: $O(N + M)$, Space: $O(1)$.
