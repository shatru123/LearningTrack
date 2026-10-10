using System;

namespace Day23.TinyUrl;

/// <summary>
/// Twitter Snowflake 64-bit Distributed Unique ID Generator.
/// Bit Layout:
/// - 1 bit: Unused sign bit (always 0)
/// - 41 bits: Millisecond timestamp since custom epoch (~69 years lifespan)
/// - 5 bits: Datacenter ID (0 - 31)
/// - 5 bits: Worker / Machine ID (0 - 31)
/// - 12 bits: Sequence counter (0 - 4095 per millisecond)
/// </summary>
public class SnowflakeIdGenerator
{
    // Epoch: 2026-01-01 00:00:00 UTC
    public static readonly DateTime CustomEpochUtc = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    public static readonly long CustomEpochMs = new DateTimeOffset(CustomEpochUtc).ToUnixTimeMilliseconds();

    private const int WorkerIdBits = 5;
    private const int DatacenterIdBits = 5;
    private const int SequenceBits = 12;

    public const long MaxWorkerId = (1L << WorkerIdBits) - 1; // 31
    public const long MaxDatacenterId = (1L << DatacenterIdBits) - 1; // 31
    public const long SequenceMask = (1L << SequenceBits) - 1; // 4095

    private const int WorkerIdShift = SequenceBits; // 12
    private const int DatacenterIdShift = SequenceBits + WorkerIdBits; // 17
    private const int TimestampLeftShift = SequenceBits + WorkerIdBits + DatacenterIdBits; // 22

    private readonly long _workerId;
    private readonly long _datacenterId;
    private readonly object _lock = new();

    private long _sequence = 0L;
    private long _lastTimestamp = -1L;

    public long WorkerId => _workerId;
    public long DatacenterId => _datacenterId;

    public SnowflakeIdGenerator(long datacenterId = 1, long workerId = 1)
    {
        if (datacenterId < 0 || datacenterId > MaxDatacenterId)
            throw new ArgumentOutOfRangeException(nameof(datacenterId), $"Datacenter ID must be between 0 and {MaxDatacenterId}");

        if (workerId < 0 || workerId > MaxWorkerId)
            throw new ArgumentOutOfRangeException(nameof(workerId), $"Worker ID must be between 0 and {MaxWorkerId}");

        _datacenterId = datacenterId;
        _workerId = workerId;
    }

    /// <summary>
    /// Generates a globally unique, k-ordered 64-bit integer ID.
    /// Thread-safe via synchronized spinlock.
    /// </summary>
    public long NextId(long? customTimestampMs = null)
    {
        lock (_lock)
        {
            var timestamp = customTimestampMs ?? CurrentTimeMillis();

            // Clock backwards drift check
            if (timestamp < _lastTimestamp)
            {
                var offset = _lastTimestamp - timestamp;
                if (offset <= 5) // Brief drift: spin-wait
                {
                    while (timestamp < _lastTimestamp)
                    {
                        timestamp = CurrentTimeMillis();
                    }
                }
                else
                {
                    throw new InvalidOperationException($"Clock moved backwards! Refusing to generate ID for {offset}ms.");
                }
            }

            if (_lastTimestamp == timestamp)
            {
                _sequence = (_sequence + 1) & SequenceMask;
                if (_sequence == 0)
                {
                    // Exhausted 4096 IDs in current ms; wait for next ms
                    timestamp = WaitNextMillis(_lastTimestamp);
                }
            }
            else
            {
                _sequence = 0L;
            }

            _lastTimestamp = timestamp;

            return ((timestamp - CustomEpochMs) << TimestampLeftShift) |
                   (_datacenterId << DatacenterIdShift) |
                   (_workerId << WorkerIdShift) |
                   _sequence;
        }
    }

    private static long CurrentTimeMillis()
    {
        return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }

    private static long WaitNextMillis(long lastTimestamp)
    {
        var timestamp = CurrentTimeMillis();
        while (timestamp <= lastTimestamp)
        {
            timestamp = CurrentTimeMillis();
        }
        return timestamp;
    }

    /// <summary>
    /// Extracts the original creation timestamp from a generated Snowflake ID.
    /// </summary>
    public static DateTime ExtractTimestampUtc(long snowflakeId)
    {
        var elapsedMs = snowflakeId >> TimestampLeftShift;
        var unixMs = elapsedMs + CustomEpochMs;
        return DateTimeOffset.FromUnixTimeMilliseconds(unixMs).UtcDateTime;
    }
}
