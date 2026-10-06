# Day 16 - Engineering Notes: Thread Synchronization & Distributed Locking

## 1. Concurrency Primitives Performance Comparison

| Primitive | Mechanism | Blocking? | Async Compatible? | Typical Cost |
|---|---|---|---|---|
| **`Interlocked`** | CPU hardware bus lock (`LOCK CMPXCHG`) | No (Spin/Retry) | Yes | ~5–10 ns |
| **`lock` (`Monitor`)** | Hybrid user-space spin + OS event | Yes (Blocks thread) | **NO** (Cannot await inside lock) | ~20–50 ns |
| **`ReaderWriterLockSlim`**| Read-shared, write-exclusive | Yes (Blocks thread) | **NO** | ~40–80 ns |
| **`SemaphoreSlim`** | Managed async waiter queue | Configurable | **YES** (`WaitAsync()`) | ~100–300 ns |
| **Distributed Lock (Redis)** | Network round-trip + Redis Lua script | Network latency | **YES** | ~1–5 ms |

---

## 2. Lock-Free Programming with `Interlocked.CompareExchange`

Optimistic concurrency in memory:
```csharp
public bool UpdateIfGreaterThan(long candidate)
{
    while (true)
    {
        long current = Interlocked.Read(ref _value);
        if (candidate <= current) return false;

        // Atomically set _value to candidate IF _value is still current
        if (Interlocked.CompareExchange(ref _value, candidate, current) == current)
        {
            return true;
        }
    }
}
```

---

## 3. Distributed Lock Leases & Fencing Tokens

```
Worker 1: Acquire Lock -> Granted (Token: 41) -> GC Pause (15s) ----[Resumes]---> Write (Token 41) [REJECTED!]
                                                    |
Redis:                                         Lock Expires (10s)
                                                    |
Worker 2:                                       Acquire Lock -> Granted (Token: 42) -> Write (Token 42) [COMMITTED]
```
The storage engine verifies `Write(token)`:
```sql
UPDATE Accounts SET Balance = @newBal, LastFencingToken = @token
WHERE Id = @id AND LastFencingToken < @token;
```
If `RowsAffected == 0`, the stale zombie write is safely rejected!
