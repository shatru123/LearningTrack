using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;

namespace Day33.DistributedIdempotency;

#region Exceptions & Models

public class IdempotencyConflictException : Exception
{
    public IdempotencyConflictException(string message) : base(message) { }
}

public class IdempotencyPayloadMismatchException : Exception
{
    public IdempotencyPayloadMismatchException(string message) : base(message) { }
}

public sealed record IdempotentResult(int StatusCode, string ResponseBody, bool FromCache = false);

public sealed class IdempotencyRecord
{
    public string IdempotencyKey { get; set; } = string.Empty;
    public string RequestPayloadHash { get; set; } = string.Empty;
    public string Status { get; set; } = "Processing"; // Processing, Completed, Failed
    public int? ResponseStatusCode { get; set; }
    public string? ResponseBody { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? LockedUntilUtc { get; set; }
}

#endregion

#region DbContext

public class IdempotencyDbContext : DbContext
{
    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();

    public IdempotencyDbContext(DbContextOptions<IdempotencyDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<IdempotencyRecord>(b =>
        {
            b.HasKey(x => x.IdempotencyKey);
            b.Property(x => x.IdempotencyKey).IsRequired().HasMaxLength(128);
            b.Property(x => x.RequestPayloadHash).IsRequired().HasMaxLength(64);
            b.Property(x => x.Status).IsRequired().HasMaxLength(32);
        });
    }
}

#endregion

#region Idempotency Manager

public class IdempotencyManager
{
    private readonly IdempotencyDbContext _dbContext;

    public IdempotencyManager(IdempotencyDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public static string ComputeSha256(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input ?? string.Empty));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    /// <summary>
    /// Executes an operation with full distributed idempotency semantics.
    /// Handles in-flight concurrency, payload verification, and cached response returns.
    /// </summary>
    public async Task<IdempotentResult> ExecuteAsync(
        string idempotencyKey,
        string requestPayload,
        Func<Task<IdempotentResult>> operation,
        TimeSpan? leaseDuration = null)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return await operation();
        }

        var lease = leaseDuration ?? TimeSpan.FromSeconds(30);
        var payloadHash = ComputeSha256(requestPayload);
        var now = DateTime.UtcNow;

        var existing = await _dbContext.IdempotencyRecords
            .FirstOrDefaultAsync(r => r.IdempotencyKey == idempotencyKey);

        if (existing != null)
        {
            // 1. Verify payload integrity: same key cannot be used with different payload
            if (existing.RequestPayloadHash != payloadHash)
            {
                throw new IdempotencyPayloadMismatchException(
                    $"Idempotency key '{idempotencyKey}' was already used with a different request payload.");
            }

            // 2. If already completed, return cached response
            if (existing.Status == "Completed")
            {
                return new IdempotentResult(
                    existing.ResponseStatusCode ?? 200,
                    existing.ResponseBody ?? string.Empty,
                    FromCache: true
                );
            }

            // 3. If currently processing and lease has NOT expired, return 409 Conflict
            if (existing.Status == "Processing" && existing.LockedUntilUtc > now)
            {
                throw new IdempotencyConflictException(
                    $"A request with idempotency key '{idempotencyKey}' is currently in progress.");
            }

            // 4. Lease expired (prior worker crashed) -> re-lease
            existing.Status = "Processing";
            existing.LockedUntilUtc = now.Add(lease);
            await _dbContext.SaveChangesAsync();
        }
        else
        {
            // Atomically reserve in-flight state
            existing = new IdempotencyRecord
            {
                IdempotencyKey = idempotencyKey,
                RequestPayloadHash = payloadHash,
                Status = "Processing",
                CreatedAtUtc = now,
                LockedUntilUtc = now.Add(lease)
            };
            _dbContext.IdempotencyRecords.Add(existing);
            await _dbContext.SaveChangesAsync();
        }

        // Execute target business operation
        try
        {
            var result = await operation();

            existing.Status = "Completed";
            existing.ResponseStatusCode = result.StatusCode;
            existing.ResponseBody = result.ResponseBody;
            existing.LockedUntilUtc = null;

            await _dbContext.SaveChangesAsync();
            return result with { FromCache = false };
        }
        catch (Exception)
        {
            existing.Status = "Failed";
            existing.LockedUntilUtc = null;
            await _dbContext.SaveChangesAsync();
            throw;
        }
    }
}

#endregion
