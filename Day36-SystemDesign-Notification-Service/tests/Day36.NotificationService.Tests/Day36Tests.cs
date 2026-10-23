using Xunit;

namespace Day36.NotificationService.Tests;

public class Day36Tests
{
    [Fact]
    public async Task NotificationDispatcher_PriorityQueue_DispatchesHighBeforeLowPriority()
    {
        var emailProvider = new MockNotificationProvider(NotificationChannel.Email, "SendGrid");
        var dispatcher = new NotificationDispatcher([emailProvider]);

        var lowMsg = new NotificationMessage("msg-low", "u1", NotificationChannel.Email, NotificationPriority.Low, "u1@test.com", "Newsletter", "body", DateTime.UtcNow);
        var highMsg = new NotificationMessage("msg-high", "u2", NotificationChannel.Email, NotificationPriority.High, "u2@test.com", "Security Alert", "body", DateTime.UtcNow);
        var normMsg = new NotificationMessage("msg-norm", "u3", NotificationChannel.Email, NotificationPriority.Normal, "u3@test.com", "Invoice", "body", DateTime.UtcNow);

        dispatcher.Enqueue(lowMsg);
        dispatcher.Enqueue(highMsg);
        dispatcher.Enqueue(normMsg);

        Assert.Equal(3, dispatcher.PendingQueueCount);

        var first = await dispatcher.DispatchNextAsync();
        var second = await dispatcher.DispatchNextAsync();
        var third = await dispatcher.DispatchNextAsync();

        Assert.Equal("msg-high", first.MessageId);
        Assert.Equal("msg-norm", second.MessageId);
        Assert.Equal("msg-low", third.MessageId);
    }

    [Fact]
    public async Task NotificationDispatcher_ProviderFailover_SwitchesToSecondaryWhenPrimaryFails()
    {
        var primarySms = new MockNotificationProvider(NotificationChannel.Sms, "Twilio") { IsHealthy = false };
        var secondarySms = new MockNotificationProvider(NotificationChannel.Sms, "AwsSns") { IsHealthy = true };
        var dispatcher = new NotificationDispatcher([primarySms, secondarySms]);

        var msg = new NotificationMessage("sms-100", "u1", NotificationChannel.Sms, NotificationPriority.High, "+15550001", "OTP", "123456", DateTime.UtcNow);

        var result = await dispatcher.ProcessMessageAsync(msg);

        Assert.True(result.Success);
        Assert.Equal("AwsSns", result.ProviderName);
        Assert.Empty(primarySms.DeliveredMessages);
        Assert.Single(secondarySms.DeliveredMessages);
    }

    [Fact]
    public async Task NotificationDispatcher_UserPreferences_BlocksOptedOutChannel()
    {
        var emailProvider = new MockNotificationProvider(NotificationChannel.Email, "SendGrid");
        var dispatcher = new NotificationDispatcher([emailProvider]);

        dispatcher.SetUserPreferences("user-opt-out", [NotificationChannel.Email]);

        var msg = new NotificationMessage("msg-opt", "user-opt-out", NotificationChannel.Email, NotificationPriority.Normal, "opt@test.com", "Promo", "50% off", DateTime.UtcNow);
        var result = await dispatcher.ProcessMessageAsync(msg);

        Assert.False(result.Success);
        Assert.Contains("opted out", result.ErrorReason);
        Assert.Empty(emailProvider.DeliveredMessages);
    }

    [Fact]
    public async Task NotificationDispatcher_RateLimiter_RejectsExcessRequestsInWindow()
    {
        var smsProvider = new MockNotificationProvider(NotificationChannel.Sms, "Twilio");
        var dispatcher = new NotificationDispatcher([smsProvider]);

        string userId = "rate-limited-user";

        // Max is 5 SMS per minute
        for (int i = 0; i < 5; i++)
        {
            var msg = new NotificationMessage($"sms-{i}", userId, NotificationChannel.Sms, NotificationPriority.Normal, "+15559999", "Alert", "text", DateTime.UtcNow);
            var res = await dispatcher.ProcessMessageAsync(msg);
            Assert.True(res.Success);
        }

        // 6th message should be throttled
        var blockedMsg = new NotificationMessage("sms-overflow", userId, NotificationChannel.Sms, NotificationPriority.Normal, "+15559999", "Alert", "text", DateTime.UtcNow);
        var blockedRes = await dispatcher.ProcessMessageAsync(blockedMsg);

        Assert.False(blockedRes.Success);
        Assert.Contains("Rate limit exceeded", blockedRes.ErrorReason);
    }

    [Fact]
    public async Task NotificationDispatcher_Idempotency_DeduplicatesDuplicateKey()
    {
        var pushProvider = new MockNotificationProvider(NotificationChannel.Push, "FirebaseFCM");
        var dispatcher = new NotificationDispatcher([pushProvider]);

        var msg1 = new NotificationMessage("msg-1", "u1", NotificationChannel.Push, NotificationPriority.High, "dev-token-1", "Title", "Body", DateTime.UtcNow, IdempotencyKey: "idem-key-99");
        var msg2 = new NotificationMessage("msg-2", "u1", NotificationChannel.Push, NotificationPriority.High, "dev-token-1", "Title", "Body", DateTime.UtcNow, IdempotencyKey: "idem-key-99");

        var res1 = await dispatcher.ProcessMessageAsync(msg1);
        var res2 = await dispatcher.ProcessMessageAsync(msg2);

        Assert.True(res1.Success);
        Assert.True(res2.Success);
        Assert.Contains("Deduplicated", res2.ErrorReason);
        Assert.Single(pushProvider.DeliveredMessages);
    }

    [Fact]
    public void Subsets_UniqueElements_ReturnsAllPowerSetElements()
    {
        int[] nums = [1, 2, 3];
        var subsets = SubsetsSolver.Subsets(nums);

        // 2^3 = 8 subsets
        Assert.Equal(8, subsets.Count);
        Assert.Contains(subsets, s => s.Count == 0);
        Assert.Contains(subsets, s => s.SequenceEqual([1, 2, 3]));
        Assert.Contains(subsets, s => s.SequenceEqual([1]));
        Assert.Contains(subsets, s => s.SequenceEqual([2, 3]));
    }

    [Fact]
    public void Subsets_Bitmask_MatchesBacktrackingResult()
    {
        int[] nums = [4, 5, 6];
        var bt = SubsetsSolver.Subsets(nums);
        var bm = SubsetsSolver.SubsetsBitmask(nums);

        Assert.Equal(bt.Count, bm.Count);
        Assert.Equal(8, bm.Count);
    }

    [Fact]
    public void Subsets_EmptyArray_ReturnsSingleEmptySubset()
    {
        int[] nums = [];
        var subsets = SubsetsSolver.Subsets(nums);

        Assert.Single(subsets);
        Assert.Empty(subsets[0]);
    }

    [Fact]
    public void SubsetsWithDup_ArrayWithDuplicates_GeneratesNoDuplicateSubsets()
    {
        int[] nums = [1, 2, 2];
        var subsets = SubsetsSolver.SubsetsWithDup(nums);

        // Subsets of [1,2,2]: [], [1], [1,2], [1,2,2], [2], [2,2] -> Total 6
        Assert.Equal(6, subsets.Count);

        // Verify uniqueness
        var stringified = subsets.Select(s => string.Join(",", s)).ToList();
        Assert.Equal(stringified.Distinct().Count(), subsets.Count);
    }

    [Fact]
    public void SubsetsWithDup_AllIdenticalElements_ReturnsCorrectSubsets()
    {
        int[] nums = [2, 2, 2, 2];
        var subsets = SubsetsSolver.SubsetsWithDup(nums);

        // Lengths 0, 1, 2, 3, 4 -> Exactly 5 subsets
        Assert.Equal(5, subsets.Count);
        for (int len = 0; len <= 4; len++)
        {
            Assert.Contains(subsets, s => s.Count == len && s.All(x => x == 2));
        }
    }
}
