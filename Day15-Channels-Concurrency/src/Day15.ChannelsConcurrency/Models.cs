using System;

namespace Day15.ChannelsConcurrency;

public record WorkItem(
    int Id,
    string Payload,
    DateTime CreatedAtUtc);

public record ProcessingResult(
    int WorkItemId,
    string ProcessedByWorker,
    bool Success,
    TimeSpan ProcessingDuration);

public record ChannelPipelineMetrics(
    int TotalProduced,
    int TotalConsumed,
    int DroppedItems,
    TimeSpan TotalElapsed);
