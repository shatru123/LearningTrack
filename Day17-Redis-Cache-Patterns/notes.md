# Day 17 - Engineering Notes: Redis Architecture & Cache Topologies

## 1. StackExchange.Redis Connection Architecture

### Single ConnectionMultiplexer Pattern
`ConnectionMultiplexer` in `StackExchange.Redis` is designed to be shared as a **Singleton** across the entire application lifecycle.
- It manages multiplexed pipelining over a single underlying TCP socket. Multiple threads issue concurrent asynchronous commands without blocking or waiting for individual socket round-trips.
- **Anti-Pattern**: Creating a new `ConnectionMultiplexer` per HTTP request drains OS socket descriptors and causes massive connection handshakes and ThreadPool starvation.

### Timeout & ThreadPool Starvation Hazards
In high-throughput .NET services, Redis timeouts (`TimeoutException: Timeout performing GET ...`) are frequently caused not by Redis CPU saturation, but by **ThreadPool starvation** on the client machine:
- Synchronous blocking calls (`.Result`, `.Wait()`) on ThreadPool worker threads prevent IOCP callbacks from completing.
- Solution: Always use `await database.StringGetAsync()` and configure `ThreadPool.SetMinThreads(50, 50)`.

---

## 2. Cache Patterns Trade-Off Matrix

| Pattern | Read Latency | Write Latency | Consistency | Failure Risk |
|---|---|---|---|---|
| **Cache-Aside** | Low (on hit) | Normal | Eventual (TTL/Invalidation) | Stale reads |
| **Write-Through** | Low | High (2 writes) | Strong | Write latency overhead |
| **Write-Behind** | Ultra-Low | Ultra-Low | Weak / Eventual | Data loss if cache crashes |
| **Refresh-Ahead** | Predictable | Normal | High | Extra compute on unused keys |

---

## 3. The XFetch Optimal Probabilistic Refresh Algorithm

```csharp
public static bool ShouldRefreshEarly(DateTime expiresAtUtc, TimeSpan computeDuration, double beta = 1.0)
{
    double timeRemainingSec = (expiresAtUtc - DateTime.UtcNow).TotalSeconds;
    double deltaSec = computeDuration.TotalSeconds;
    double rand = Random.Shared.NextDouble();

    // Probability escalates as timeRemaining decreases and compute cost increases
    return (deltaSec * beta * -Math.Log(rand)) > timeRemainingSec;
}
```
Published by Vattani et al. in VLDB 2015, XFetch mathematically guarantees zero stampede with minimal redundant compute.
