using System;

namespace Day16.SynchronizationLocks;

public record DistributedLockToken(
    string ResourceName,
    string OwnerId,
    long FencingToken,
    DateTime ExpiresAtUtc)
{
    public bool IsExpired => DateTime.UtcNow >= ExpiresAtUtc;
}

public record FencingToken(long Value);

public record LockAcquisitionResult(
    bool Success,
    DistributedLockToken? Token,
    string? Reason);
