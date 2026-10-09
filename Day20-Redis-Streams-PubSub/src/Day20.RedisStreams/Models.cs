using System;
using System.Collections.Generic;

namespace Day20.RedisStreams;

/// <summary>
/// Represents a structured Redis Stream Entry ID formatted as: {timestampMs}-{sequence}
/// </summary>
public record StreamEntryId(long TimestampMs, long Sequence) : IComparable<StreamEntryId>
{
    public override string ToString() => $"{TimestampMs}-{Sequence}";

    public static StreamEntryId Parse(string id)
    {
        var parts = id.Split('-');
        if (parts.Length != 2 || !long.TryParse(parts[0], out var ts) || !long.TryParse(parts[1], out var seq))
        {
            throw new FormatException($"Invalid StreamEntryId format: '{id}'. Expected 'timestamp-sequence'.");
        }
        return new StreamEntryId(ts, seq);
    }

    public int CompareTo(StreamEntryId? other)
    {
        if (other is null) return 1;
        int cmp = TimestampMs.CompareTo(other.TimestampMs);
        return cmp != 0 ? cmp : Sequence.CompareTo(other.Sequence);
    }
}

/// <summary>
/// A persistent message record in a Redis Stream.
/// </summary>
public record StreamMessage(
    string Id,
    IReadOnlyDictionary<string, string> Fields,
    DateTime IngestedUtc);

/// <summary>
/// An unacknowledged message tracked in a Consumer Group's Pending Entries List (PEL).
/// </summary>
public record PendingEntry(
    string MessageId,
    string ConsumerName,
    TimeSpan IdleTime,
    int DeliveryCount,
    DateTime LastDeliveredUtc);

/// <summary>
/// Summary statistics for an active consumer group.
/// </summary>
public record ConsumerGroupStats(
    string GroupName,
    int ConsumersCount,
    int PendingCount,
    string LastDeliveredId);
