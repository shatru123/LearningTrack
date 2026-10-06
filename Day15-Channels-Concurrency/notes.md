# Day 15 - Engineering Notes: System.Threading.Channels & High-Throughput Concurrency

## 1. System.Threading.Channels Internal Mechanics

### Ring Buffer & Lock-Free Implementation
Under the hood, `Channel.CreateBounded<T>(capacity)` allocates a fixed-size circular array (ring buffer).
- State transitions are managed using atomic CAS operations via `Interlocked`.
- When readers await items, `ReadAsync()` returns an uncompleted `ValueTask<T>`. The reader's continuation delegate is enqueued directly into an internal waiter list.
- When a writer pushes an item, if a reader is waiting, the item is transferred directly to the reader without passing through the ring buffer, achieving near-zero allocation and bypassing queue overhead.

### SingleWriter / SingleReader Optimizations
When creating a channel:
```csharp
var options = new BoundedChannelOptions(1000)
{
    SingleWriter = true,
    SingleReader = false
};
```
Setting `SingleWriter = true` eliminates inter-producer synchronization locks, replacing thread-safe CAS operations with direct array index increments, drastically reducing CPU cache-line bouncing.

---

## 2. Bounded Capacity & Backpressure Policies

| `BoundedChannelFullMode` | Behavior on Buffer Saturation | Typical Use Case |
|---|---|---|
| **`Wait`** | `WriteAsync` awaits asynchronously | Financial transactions, audit logging, orders |
| **`DropOldest`** | Drops queue head, writes new item | GPS tracking, live video frames, UI sensor metrics |
| **`DropNewest`** | Drops newly incoming item | Rate-limiting incoming requests |
| **`DropWrite`** | Returns `false` on `TryWrite` | Non-blocking best-effort notifications |

---

## 3. Linked List Pointer Mechanics (LC #206 & #21)

### The Sentinel (Dummy Head) Pattern
Without a dummy head, merging two lists requires initializing the head pointer separately:
```csharp
var dummy = new ListNode(-1);
var tail = dummy;

while (l1 != null && l2 != null)
{
    if (l1.val <= l2.val) { tail.next = l1; l1 = l1.next; }
    else { tail.next = l2; l2 = l2.next; }
    tail = tail.next;
}
tail.next = l1 ?? l2;
return dummy.next;
```
This reduces branching, eliminates null-pointer checks for the initial node, and guarantees $O(1)$ extra space.
