using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace Day18.RedisDataStructures;

public class RedisDataStructuresService
{
    // In-memory representations of Redis data structures
    private readonly ConcurrentDictionary<string, string> _strings = new();
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, string>> _hashes = new();
    private readonly ConcurrentDictionary<string, HashSet<string>> _sets = new();
    private readonly ConcurrentDictionary<string, SortedSet<(double Score, string Member)>> _zsets = new();
    private readonly ConcurrentDictionary<string, byte[]> _bitmaps = new();
    private readonly ConcurrentDictionary<string, byte[]> _hyperLogLogs = new();

    // 1. Strings: Atomic Counter & Value Cache
    public long StringIncrement(string key, long delta = 1)
    {
        while (true)
        {
            if (_strings.TryGetValue(key, out var currentStr))
            {
                long currentVal = long.Parse(currentStr);
                long newVal = currentVal + delta;
                if (_strings.TryUpdate(key, newVal.ToString(), currentStr))
                {
                    return newVal;
                }
            }
            else
            {
                if (_strings.TryAdd(key, delta.ToString()))
                {
                    return delta;
                }
            }
        }
    }

    public string? StringGet(string key) => _strings.TryGetValue(key, out var val) ? val : null;
    public void StringSet(string key, string value) => _strings[key] = value;

    // 2. Hashes: Field-Level Granular Storage
    public void HashSetField(string key, string field, string value)
    {
        var hash = _hashes.GetOrAdd(key, _ => new ConcurrentDictionary<string, string>());
        hash[field] = value;
    }

    public string? HashGetField(string key, string field)
    {
        if (_hashes.TryGetValue(key, out var hash) && hash.TryGetValue(field, out var val))
        {
            return val;
        }
        return null;
    }

    public long HashIncrementField(string key, string field, long delta = 1)
    {
        var hash = _hashes.GetOrAdd(key, _ => new ConcurrentDictionary<string, string>());
        while (true)
        {
            if (hash.TryGetValue(field, out var currentStr))
            {
                long currentVal = long.Parse(currentStr);
                long newVal = currentVal + delta;
                if (hash.TryUpdate(field, newVal.ToString(), currentStr))
                {
                    return newVal;
                }
            }
            else
            {
                if (hash.TryAdd(field, delta.ToString()))
                {
                    return delta;
                }
            }
        }
    }

    public IReadOnlyDictionary<string, string>? HashGetAll(string key)
    {
        return _hashes.TryGetValue(key, out var hash) ? hash : null;
    }

    // 3. Sets: Unordered Deduplicated Collections & Set Operations
    public bool SetAdd(string key, string member)
    {
        var set = _sets.GetOrAdd(key, _ => new HashSet<string>());
        lock (set)
        {
            return set.Add(member);
        }
    }

    public bool SetIsMember(string key, string member)
    {
        if (_sets.TryGetValue(key, out var set))
        {
            lock (set)
            {
                return set.Contains(member);
            }
        }
        return false;
    }

    public HashSet<string> SetIntersect(string key1, string key2)
    {
        var res = new HashSet<string>();
        if (_sets.TryGetValue(key1, out var s1) && _sets.TryGetValue(key2, out var s2))
        {
            lock (s1)
            lock (s2)
            {
                res = new HashSet<string>(s1);
                res.IntersectWith(s2);
            }
        }
        return res;
    }

    // 4. Sorted Sets (ZSET): Priority Queues, Leaderboards & Rate Limiting
    public bool ZSetAdd(string key, string member, double score)
    {
        var comparer = Comparer<(double Score, string Member)>.Create((a, b) =>
        {
            int cmp = a.Score.CompareTo(b.Score);
            return cmp != 0 ? cmp : string.Compare(a.Member, b.Member, StringComparison.Ordinal);
        });

        var zset = _zsets.GetOrAdd(key, _ => new SortedSet<(double Score, string Member)>(comparer));
        lock (zset)
        {
            // Remove existing member with different score
            var existing = zset.FirstOrDefault(x => x.Member == member);
            if (existing != default)
            {
                zset.Remove(existing);
            }
            return zset.Add((score, member));
        }
    }

    public IReadOnlyList<LeaderboardScore> ZSetGetTop(string key, int topN)
    {
        if (!_zsets.TryGetValue(key, out var zset)) return Array.Empty<LeaderboardScore>();

        lock (zset)
        {
            return zset.Reverse()
                .Take(topN)
                .Select((item, idx) => new LeaderboardScore(item.Member, item.Score, idx + 1))
                .ToList();
        }
    }

    // Sliding Window Rate Limiter using ZSET
    public bool IsRateLimited(string clientKey, int maxRequests, TimeSpan window, DateTime nowUtc)
    {
        string key = $"ratelimit:{clientKey}";
        double nowMs = nowUtc.Subtract(DateTime.UnixEpoch).TotalMilliseconds;
        double windowStartMs = nowMs - window.TotalMilliseconds;

        var comparer = Comparer<(double Score, string Member)>.Create((a, b) =>
        {
            int cmp = a.Score.CompareTo(b.Score);
            return cmp != 0 ? cmp : string.Compare(a.Member, b.Member, StringComparison.Ordinal);
        });

        var zset = _zsets.GetOrAdd(key, _ => new SortedSet<(double Score, string Member)>(comparer));
        lock (zset)
        {
            // 1. Remove entries older than windowStartMs
            zset.RemoveWhere(x => x.Score < windowStartMs);

            // 2. Count requests in current window
            if (zset.Count >= maxRequests)
            {
                return true; // Rate limited!
            }

            // 3. Add current timestamp
            zset.Add((nowMs, Guid.NewGuid().ToString("N")));
            return false;
        }
    }

    // 5. Bitmaps: Ultra-Compact User Presence / Daily Active Users
    public bool SetBit(string key, long offset, bool value)
    {
        int byteIndex = (int)(offset / 8);
        int bitOffset = (int)(offset % 8);

        var bytes = _bitmaps.GetOrAdd(key, _ => new byte[1024]);
        if (byteIndex >= bytes.Length)
        {
            Array.Resize(ref bytes, Math.Max(bytes.Length * 2, byteIndex + 128));
            _bitmaps[key] = bytes;
        }

        byte oldByte = bytes[byteIndex];
        bool oldBit = (oldByte & (1 << (7 - bitOffset))) != 0;

        if (value)
            bytes[byteIndex] |= (byte)(1 << (7 - bitOffset));
        else
            bytes[byteIndex] &= (byte)~(1 << (7 - bitOffset));

        return oldBit;
    }

    public bool GetBit(string key, long offset)
    {
        if (!_bitmaps.TryGetValue(key, out var bytes)) return false;
        int byteIndex = (int)(offset / 8);
        int bitOffset = (int)(offset % 8);

        if (byteIndex >= bytes.Length) return false;
        return (bytes[byteIndex] & (1 << (7 - bitOffset))) != 0;
    }

    public long BitCount(string key)
    {
        if (!_bitmaps.TryGetValue(key, out var bytes)) return 0;
        long count = 0;
        foreach (byte b in bytes)
        {
            count += System.Numerics.BitOperations.PopCount(b);
        }
        return count;
    }

    // 6. HyperLogLog: 12KB Fixed Memory Approximate Unique Cardinality
    private const int HllRegistersCount = 16384; // 2^14 registers (standard Redis HyperLogLog)
    private const double AlphaM = 0.7213 / (1 + 1.079 / HllRegistersCount);

    public bool HyperLogLogAdd(string key, string element)
    {
        var registers = _hyperLogLogs.GetOrAdd(key, _ => new byte[HllRegistersCount]);

        // Hash element using 64-bit Murmur/SHA256 simulation
        byte[] hash = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(element));
        ulong hashVal = BitConverter.ToUInt64(hash, 0);

        int registerIndex = (int)(hashVal & (HllRegistersCount - 1));
        ulong remainingBits = hashVal >> 14;

        // Count leading zeros + 1 (rho)
        byte rho = (byte)(System.Numerics.BitOperations.LeadingZeroCount(remainingBits | 1UL) - (64 - 50) + 1);
        if (rho > 64) rho = 64;

        lock (registers)
        {
            if (rho > registers[registerIndex])
            {
                registers[registerIndex] = rho;
                return true;
            }
        }
        return false;
    }

    public long HyperLogLogCount(string key)
    {
        if (!_hyperLogLogs.TryGetValue(key, out var registers)) return 0;

        double sum = 0.0;
        int zeroCount = 0;

        lock (registers)
        {
            for (int i = 0; i < HllRegistersCount; i++)
            {
                sum += Math.Pow(2.0, -registers[i]);
                if (registers[i] == 0) zeroCount++;
            }
        }

        // Raw harmonic mean estimate
        double estimate = AlphaM * HllRegistersCount * HllRegistersCount / sum;

        // Linear counting for small cardinalities
        if (estimate <= 2.5 * HllRegistersCount && zeroCount > 0)
        {
            estimate = HllRegistersCount * Math.Log((double)HllRegistersCount / zeroCount);
        }

        return (long)Math.Round(estimate);
    }
}
