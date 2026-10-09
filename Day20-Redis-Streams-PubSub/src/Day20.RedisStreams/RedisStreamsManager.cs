using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Day20.RedisStreams;

/// <summary>
/// High-performance stream manager simulating Redis Streams engine internals:
/// - Append-only ordered message stream with Radix tree / ListPack semantics
/// - Monotonically increasing {timestamp}-{sequence} ID generation
/// - Consumer Groups with partitioned work distribution
/// - Pending Entries List (PEL) tracking in-flight message state
/// - Explicit ACK mechanism (XACK)
/// - Dead-letter and crash recovery via message claiming (XCLAIM / XAUTOCLAIM)
/// </summary>
public class RedisStreamsManager
{
    private class InternalGroup
    {
        public string Name { get; }
        public StreamEntryId LastDeliveredId { get; set; } = new(0, 0);
        public HashSet<string> ActiveConsumers { get; } = new();
        public Dictionary<string, PendingEntry> Pel { get; } = new(); // Keyed by MessageId

        public InternalGroup(string name, StreamEntryId startId)
        {
            Name = name;
            LastDeliveredId = startId;
        }
    }

    private readonly List<StreamMessage> _messages = new();
    private readonly ConcurrentDictionary<string, InternalGroup> _groups = new();
    private readonly object _syncLock = new();
    private long _lastTimestampMs = 0;
    private long _sequence = 0;

    public int StreamLength
    {
        get
        {
            lock (_syncLock) return _messages.Count;
        }
    }

    /// <summary>
    /// XADD: Appends a new message to the stream with automatic monotonic ID generation.
    /// </summary>
    public StreamMessage Add(IReadOnlyDictionary<string, string> fields, string? customId = null)
    {
        lock (_syncLock)
        {
            StreamEntryId entryId;
            if (customId != null && customId != "*")
            {
                entryId = StreamEntryId.Parse(customId);
            }
            else
            {
                long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                if (nowMs == _lastTimestampMs)
                {
                    _sequence++;
                }
                else
                {
                    _lastTimestampMs = Math.Max(nowMs, _lastTimestampMs);
                    _sequence = 0;
                }
                entryId = new StreamEntryId(_lastTimestampMs, _sequence);
            }

            var msg = new StreamMessage(entryId.ToString(), new Dictionary<string, string>(fields), DateTime.UtcNow);
            _messages.Add(msg);
            return msg;
        }
    }

    /// <summary>
    /// XGROUP CREATE: Creates a consumer group starting from a given stream entry ID (or "0" for beginning, "$" for latest).
    /// </summary>
    public bool CreateConsumerGroup(string groupName, string startId = "$")
    {
        lock (_syncLock)
        {
            if (_groups.ContainsKey(groupName)) return false;

            StreamEntryId start;
            if (startId == "$")
            {
                start = _messages.Count > 0 ? StreamEntryId.Parse(_messages.Last().Id) : new StreamEntryId(0, 0);
            }
            else if (startId == "0")
            {
                start = new StreamEntryId(0, 0);
            }
            else
            {
                start = StreamEntryId.Parse(startId);
            }

            return _groups.TryAdd(groupName, new InternalGroup(groupName, start));
        }
    }

    /// <summary>
    /// XREADGROUP: Reads new messages (ID ">") or pending unacknowledged messages for a consumer in a group.
    /// Automatically records in-flight messages in the Pending Entries List (PEL).
    /// </summary>
    public IReadOnlyList<StreamMessage> ReadGroup(
        string groupName,
        string consumerName,
        int count = 10,
        string id = ">")
    {
        lock (_syncLock)
        {
            if (!_groups.TryGetValue(groupName, out var group))
            {
                throw new InvalidOperationException($"Consumer group '{groupName}' does not exist.");
            }

            group.ActiveConsumers.Add(consumerName);
            var now = DateTime.UtcNow;
            var dispatched = new List<StreamMessage>();

            if (id == ">")
            {
                // Deliver brand new unconsumed messages
                var candidates = _messages
                    .Where(m => StreamEntryId.Parse(m.Id).CompareTo(group.LastDeliveredId) > 0)
                    .Take(count)
                    .ToList();

                foreach (var msg in candidates)
                {
                    var msgId = msg.Id;
                    group.LastDeliveredId = StreamEntryId.Parse(msgId);

                    // Add to PEL
                    group.Pel[msgId] = new PendingEntry(
                        MessageId: msgId,
                        ConsumerName: consumerName,
                        IdleTime: TimeSpan.Zero,
                        DeliveryCount: 1,
                        LastDeliveredUtc: now);

                    dispatched.Add(msg);
                }
            }
            else
            {
                // Deliver currently pending messages assigned to this consumer
                var pendingForConsumer = group.Pel.Values
                    .Where(p => p.ConsumerName == consumerName)
                    .Take(count)
                    .ToList();

                foreach (var pend in pendingForConsumer)
                {
                    var msg = _messages.FirstOrDefault(m => m.Id == pend.MessageId);
                    if (msg != null)
                    {
                        // Increment delivery count
                        group.Pel[pend.MessageId] = pend with
                        {
                            DeliveryCount = pend.DeliveryCount + 1,
                            LastDeliveredUtc = now
                        };
                        dispatched.Add(msg);
                    }
                }
            }

            return dispatched;
        }
    }

    /// <summary>
    /// XACK: Acknowledges receipt of messages by removing them from the Consumer Group's Pending Entries List (PEL).
    /// </summary>
    public int Ack(string groupName, params string[] messageIds)
    {
        lock (_syncLock)
        {
            if (!_groups.TryGetValue(groupName, out var group)) return 0;

            int acked = 0;
            foreach (var id in messageIds)
            {
                if (group.Pel.Remove(id))
                {
                    acked++;
                }
            }
            return acked;
        }
    }

    /// <summary>
    /// XPENDING: Inspects currently pending unacknowledged messages in the consumer group.
    /// </summary>
    public IReadOnlyList<PendingEntry> GetPendingEntries(string groupName, DateTime? evaluationTime = null)
    {
        lock (_syncLock)
        {
            if (!_groups.TryGetValue(groupName, out var group)) return Array.Empty<PendingEntry>();

            var now = evaluationTime ?? DateTime.UtcNow;
            return group.Pel.Values
                .Select(p => p with { IdleTime = now - p.LastDeliveredUtc })
                .ToList();
        }
    }

    /// <summary>
    /// XCLAIM / XAUTOCLAIM: Recovers abandoned messages from a dead/failed consumer after an idle duration.
    /// Reassigns ownership to a new consumer, increments delivery count, and returns the claimed messages.
    /// </summary>
    public IReadOnlyList<StreamMessage> Claim(
        string groupName,
        string newConsumerName,
        TimeSpan minIdleTime,
        DateTime? evaluationTime = null,
        int count = 10)
    {
        lock (_syncLock)
        {
            if (!_groups.TryGetValue(groupName, out var group)) return Array.Empty<StreamMessage>();

            var now = evaluationTime ?? DateTime.UtcNow;
            group.ActiveConsumers.Add(newConsumerName);

            // Find pending messages exceeding idle time
            var staleEntries = group.Pel.Values
                .Where(p => (now - p.LastDeliveredUtc) >= minIdleTime)
                .Take(count)
                .ToList();

            var claimedMessages = new List<StreamMessage>();

            foreach (var entry in staleEntries)
            {
                var msg = _messages.FirstOrDefault(m => m.Id == entry.MessageId);
                if (msg != null)
                {
                    // Reassign to new consumer and update PEL
                    group.Pel[entry.MessageId] = entry with
                    {
                        ConsumerName = newConsumerName,
                        DeliveryCount = entry.DeliveryCount + 1,
                        LastDeliveredUtc = now,
                        IdleTime = TimeSpan.Zero
                    };
                    claimedMessages.Add(msg);
                }
            }

            return claimedMessages;
        }
    }

    /// <summary>
    /// Fetches group statistics (consumer count, pending count, last delivered ID).
    /// </summary>
    public ConsumerGroupStats? GetGroupStats(string groupName)
    {
        lock (_syncLock)
        {
            if (!_groups.TryGetValue(groupName, out var group)) return null;

            return new ConsumerGroupStats(
                GroupName: group.Name,
                ConsumersCount: group.ActiveConsumers.Count,
                PendingCount: group.Pel.Count,
                LastDeliveredId: group.LastDeliveredId.ToString());
        }
    }
}
