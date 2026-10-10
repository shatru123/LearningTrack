using System;
using System.IO;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Day22.RateLimiter.Tests;

public class Day22Tests
{
    // ==========================================
    // 1. TOKEN BUCKET TESTS
    // ==========================================

    [Fact]
    public void TokenBucket_AllowsBurstUpToCapacity_AndEnforcesRefillRate()
    {
        // 5 tokens capacity, 1 token per second refill
        var limiter = new TokenBucketRateLimiter(capacity: 5, refillRatePerSecond: 1.0);
        var t0 = DateTime.UtcNow;

        // Drain 5 tokens instantaneously
        for (int i = 0; i < 5; i++)
        {
            var res = limiter.ShouldAllow("client1", tokensRequested: 1, customNow: t0);
            res.IsAllowed.Should().BeTrue();
            res.Remaining.Should().Be(4 - i);
        }

        // 6th request at t0 must be rejected
        var rejected = limiter.ShouldAllow("client1", tokensRequested: 1, customNow: t0);
        rejected.IsAllowed.Should().BeFalse();
        rejected.Remaining.Should().Be(0);

        // Advance 2 seconds -> 2 tokens should be refilled
        var t2 = t0.AddSeconds(2);
        var after2Sec = limiter.ShouldAllow("client1", tokensRequested: 1, customNow: t2);
        after2Sec.IsAllowed.Should().BeTrue();
        after2Sec.Remaining.Should().Be(1); // 2 refilled - 1 consumed = 1 remaining
    }

    [Fact]
    public void TokenBucket_RejectsWhenExhausted_AndReturnsAccurateRetryAfter()
    {
        var limiter = new TokenBucketRateLimiter(capacity: 2, refillRatePerSecond: 0.5); // 1 token every 2 seconds
        var t0 = DateTime.UtcNow;

        limiter.ShouldAllow("client2", tokensRequested: 2, customNow: t0); // Drain bucket

        var res = limiter.ShouldAllow("client2", tokensRequested: 1, customNow: t0);
        res.IsAllowed.Should().BeFalse();
        // Needs 1 token, refill rate is 0.5/s -> retry after should be 2.0 seconds
        res.RetryAfter.TotalSeconds.Should().BeApproximately(2.0, 0.05);
    }

    // ==========================================
    // 2. LEAKY BUCKET TESTS
    // ==========================================

    [Fact]
    public void LeakyBucket_LeaksAtConstantRate_AndRejectsWhenBufferFull()
    {
        // Capacity 3 requests buffer, leaks 1 request per second
        var limiter = new LeakyBucketRateLimiter(capacity: 3, leakRatePerSecond: 1.0);
        var t0 = DateTime.UtcNow;

        // Fill buffer to capacity
        limiter.ShouldAllow("userA", tokensRequested: 1, customNow: t0).IsAllowed.Should().BeTrue();
        limiter.ShouldAllow("userA", tokensRequested: 1, customNow: t0).IsAllowed.Should().BeTrue();
        limiter.ShouldAllow("userA", tokensRequested: 1, customNow: t0).IsAllowed.Should().BeTrue();

        // 4th request overflows buffer
        limiter.ShouldAllow("userA", tokensRequested: 1, customNow: t0).IsAllowed.Should().BeFalse();

        // Advance 1.5 seconds -> 1.5 requests leaked, buffer water is 1.5. Capacity remaining is 1.5.
        var t15 = t0.AddSeconds(1.5);
        limiter.ShouldAllow("userA", tokensRequested: 1, customNow: t15).IsAllowed.Should().BeTrue();
    }

    // ==========================================
    // 3. FIXED WINDOW & BOUNDARY BURST
    // ==========================================

    [Fact]
    public void FixedWindow_DemonstratesBoundaryBurstVulnerability()
    {
        // Limit: 5 requests per 1-minute window
        var limiter = new FixedWindowCounterRateLimiter(limit: 5, window: TimeSpan.FromMinutes(1));
        var baseTime = new DateTime(2026, 10, 10, 12, 0, 0, DateTimeKind.Utc);

        // 5 requests at end of window 1 (12:00:58)
        var tEndWindow1 = baseTime.AddSeconds(58);
        for (int i = 0; i < 5; i++)
        {
            limiter.ShouldAllow("client_burst", tokensRequested: 1, customNow: tEndWindow1).IsAllowed.Should().BeTrue();
        }

        // 6th request at 12:00:59 rejected
        limiter.ShouldAllow("client_burst", tokensRequested: 1, customNow: baseTime.AddSeconds(59)).IsAllowed.Should().BeFalse();

        // Window resets at 12:01:00! Another 5 requests allowed at 12:01:01
        var tStartWindow2 = baseTime.AddSeconds(61);
        for (int i = 0; i < 5; i++)
        {
            limiter.ShouldAllow("client_burst", tokensRequested: 1, customNow: tStartWindow2).IsAllowed.Should().BeTrue();
        }

        // In the 3-second period from 12:00:58 to 12:01:01, 10 total requests passed through (2x the 5 req/min limit!)
    }

    // ==========================================
    // 4. SLIDING WINDOW LOG TESTS
    // ==========================================

    [Fact]
    public void SlidingWindowLog_PurgesOldTimestamps_AndMaintainsExactPrecision()
    {
        var limiter = new SlidingWindowLogRateLimiter(limit: 3, window: TimeSpan.FromSeconds(10));
        var t0 = DateTime.UtcNow;

        limiter.ShouldAllow("client_log", tokensRequested: 1, customNow: t0).IsAllowed.Should().BeTrue();
        limiter.ShouldAllow("client_log", tokensRequested: 1, customNow: t0.AddSeconds(2)).IsAllowed.Should().BeTrue();
        limiter.ShouldAllow("client_log", tokensRequested: 1, customNow: t0.AddSeconds(4)).IsAllowed.Should().BeTrue();

        // 4th request at t0 + 6s rejected (all 3 earlier are in 10s window)
        limiter.ShouldAllow("client_log", tokensRequested: 1, customNow: t0.AddSeconds(6)).IsAllowed.Should().BeFalse();

        // Advance to t0 + 11s -> 1st request (at t0) has slid out of 10s window. 1 slot available!
        var allowed = limiter.ShouldAllow("client_log", tokensRequested: 1, customNow: t0.AddSeconds(11));
        allowed.IsAllowed.Should().BeTrue();
        allowed.Remaining.Should().Be(0);
    }

    // ==========================================
    // 5. SLIDING WINDOW COUNTER (CLOUDFLARE HYBRID)
    // ==========================================

    [Fact]
    public void SlidingWindowCounter_WeightedEstimation_SmoothsWindowTransitions()
    {
        // 10 requests per 60-second window
        var limiter = new SlidingWindowCounterRateLimiter(limit: 10, window: TimeSpan.FromSeconds(60));
        var t0 = new DateTime(2026, 10, 10, 12, 0, 0, DateTimeKind.Utc);

        // Previous window (12:00:00 - 12:00:59): 8 requests
        for (int i = 0; i < 8; i++)
        {
            limiter.ShouldAllow("user_hybrid", 1, t0.AddSeconds(30)).IsAllowed.Should().BeTrue();
        }

        // Current window starts at 12:01:00.
        // At 12:01:15 (25% into new window): weight of prev window is 75% (0.75).
        // Estimated prev = 8 * 0.75 = 6.0 requests.
        // Remaining capacity = 10 - 6.0 = 4 requests.
        var tNew25 = new DateTime(2026, 10, 10, 12, 1, 15, DateTimeKind.Utc);
        limiter.ShouldAllow("user_hybrid", 1, tNew25).IsAllowed.Should().BeTrue(); // Curr = 1, total = 7
        limiter.ShouldAllow("user_hybrid", 1, tNew25).IsAllowed.Should().BeTrue(); // Curr = 2, total = 8
        limiter.ShouldAllow("user_hybrid", 1, tNew25).IsAllowed.Should().BeTrue(); // Curr = 3, total = 9
        limiter.ShouldAllow("user_hybrid", 1, tNew25).IsAllowed.Should().BeTrue(); // Curr = 4, total = 10

        // 5th request at 12:01:15 exceeds 10 limit!
        limiter.ShouldAllow("user_hybrid", 1, tNew25).IsAllowed.Should().BeFalse();
    }

    // ==========================================
    // 6. ASP.NET CORE MIDDLEWARE INTEGRATION TESTS
    // ==========================================

    [Fact]
    public async Task Middleware_AllowsRequestsWithinLimit_AndInjectsStandardHeaders()
    {
        var limiter = new TokenBucketRateLimiter(capacity: 10, refillRatePerSecond: 1.0);
        var middleware = new DistributedRateLimiterMiddleware(
            next: ctx =>
            {
                ctx.Response.StatusCode = 200;
                return Task.CompletedTask;
            },
            limiter: limiter
        );

        var context = new DefaultHttpContext();
        context.Request.Headers["X-Api-Key"] = "premium_client_123";
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(200);
        context.Response.Headers["X-RateLimit-Limit"].ToString().Should().Be("10");
        context.Response.Headers["X-RateLimit-Remaining"].ToString().Should().Be("9");
        context.Response.Headers["X-RateLimit-Reset"].ToString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Middleware_RejectsExceededRequests_With429AndProblemDetails()
    {
        var limiter = new TokenBucketRateLimiter(capacity: 1, refillRatePerSecond: 0.1);
        var middleware = new DistributedRateLimiterMiddleware(
            next: ctx =>
            {
                ctx.Response.StatusCode = 200;
                return Task.CompletedTask;
            },
            limiter: limiter
        );

        // 1st request allowed
        var ctx1 = new DefaultHttpContext();
        ctx1.Request.Headers["X-Api-Key"] = "test_key";
        ctx1.Response.Body = new MemoryStream();
        await middleware.InvokeAsync(ctx1);
        ctx1.Response.StatusCode.Should().Be(200);

        // 2nd request immediately rejected with 429
        var ctx2 = new DefaultHttpContext();
        ctx2.Request.Headers["X-Api-Key"] = "test_key";
        ctx2.Response.Body = new MemoryStream();
        await middleware.InvokeAsync(ctx2);

        ctx2.Response.StatusCode.Should().Be(429);
        ctx2.Response.ContentType.Should().Be("application/problem+json");
        ctx2.Response.Headers["Retry-After"].ToString().Should().NotBeNullOrEmpty();

        ctx2.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(ctx2.Response.Body);
        var body = await reader.ReadToEndAsync();
        body.Should().Contain("Too Many Requests");
        body.Should().Contain("Quota exceeded");
    }

    // ==========================================
    // 7. DSA: VALIDATE BST (LC #98) TESTS
    // ==========================================

    [Fact]
    public void LC98_ValidateBST_DetectsValidAndInvalidSubtreeViolations()
    {
        var solvers = new BstValidationSolvers();

        // Valid BST: [2, 1, 3]
        //      2
        //     / \
        //    1   3
        var validTree = new TreeNode(2, new TreeNode(1), new TreeNode(3));
        solvers.IsValidBST(validTree).Should().BeTrue();
        solvers.IsValidBstInOrder(validTree).Should().BeTrue();

        // Invalid BST: [5, 1, 4, null, null, 3, 6]
        //      5
        //     / \
        //    1   4
        //       / \
        //      3   6 (3 is in right subtree of 5, but 3 < 5! Invalid!)
        var invalidTree = new TreeNode(5,
            new TreeNode(1),
            new TreeNode(4, new TreeNode(3), new TreeNode(6))
        );
        solvers.IsValidBST(invalidTree).Should().BeFalse();
        solvers.IsValidBstInOrder(invalidTree).Should().BeFalse();

        // Edge case: Node values near Int32.MinValue and Int32.MaxValue
        var boundaryTree = new TreeNode(int.MaxValue, new TreeNode(int.MaxValue - 1), null);
        solvers.IsValidBST(boundaryTree).Should().BeTrue();
        solvers.IsValidBstInOrder(boundaryTree).Should().BeTrue();

        // Duplicate value violation in BST
        var dupTree = new TreeNode(2, new TreeNode(2), new TreeNode(3));
        solvers.IsValidBST(dupTree).Should().BeFalse();
        solvers.IsValidBstInOrder(dupTree).Should().BeFalse();
    }

    // ==========================================
    // 8. DSA: KTH SMALLEST IN BST (LC #230) TESTS
    // ==========================================

    [Fact]
    public void LC230_KthSmallest_ReturnsCorrectKthElement_WithEarlyExit()
    {
        var solvers = new BstValidationSolvers();

        // Tree: [5, 3, 6, 2, 4, null, null, 1]
        //        5
        //       / \
        //      3   6
        //     / \
        //    2   4
        //   /
        //  1
        var root = new TreeNode(5,
            new TreeNode(3,
                new TreeNode(2, new TreeNode(1)),
                new TreeNode(4)
            ),
            new TreeNode(6)
        );

        // Sorted order: 1, 2, 3, 4, 5, 6
        solvers.KthSmallest(root, 1).Should().Be(1);
        solvers.KthSmallest(root, 2).Should().Be(2);
        solvers.KthSmallest(root, 3).Should().Be(3);
        solvers.KthSmallest(root, 4).Should().Be(4);
        solvers.KthSmallest(root, 5).Should().Be(5);
        solvers.KthSmallest(root, 6).Should().Be(6);

        // Invalid k throws
        Action act = () => solvers.KthSmallest(root, 10);
        act.Should().Throw<ArgumentException>();
    }
}
