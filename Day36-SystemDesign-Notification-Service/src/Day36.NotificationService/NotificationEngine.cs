namespace Day36.NotificationService;

public enum NotificationChannel
{
    Email,
    Sms,
    Push,
    InApp
}

public enum NotificationPriority
{
    High = 0,
    Normal = 1,
    Low = 2
}

public record NotificationMessage(
    string MessageId,
    string UserId,
    NotificationChannel Channel,
    NotificationPriority Priority,
    string Recipient,
    string Title,
    string Body,
    DateTime CreatedAtUtc,
    string? IdempotencyKey = null
);

public record NotificationResult(
    bool Success,
    string MessageId,
    string Channel,
    string? ProviderName,
    string? ErrorReason = null
);

public interface INotificationProvider
{
    NotificationChannel Channel { get; }
    string Name { get; }
    bool IsHealthy { get; set; }
    Task<bool> SendAsync(NotificationMessage message);
}

public class MockNotificationProvider : INotificationProvider
{
    public NotificationChannel Channel { get; }
    public string Name { get; }
    public bool IsHealthy { get; set; } = true;
    public List<NotificationMessage> DeliveredMessages { get; } = new();

    public MockNotificationProvider(NotificationChannel channel, string name)
    {
        Channel = channel;
        Name = name;
    }

    public Task<bool> SendAsync(NotificationMessage message)
    {
        if (!IsHealthy)
        {
            return Task.FromResult(false);
        }
        DeliveredMessages.Add(message);
        return Task.FromResult(true);
    }
}

public class UserNotificationPreferences
{
    public string UserId { get; set; } = string.Empty;
    public HashSet<NotificationChannel> OptedOutChannels { get; } = new();
}

public class NotificationRateLimiter
{
    private readonly Dictionary<string, List<DateTime>> _userChannelTimestamps = new();
    private readonly object _lock = new();

    public bool Allow(string userId, NotificationChannel channel, int maxPerWindow, TimeSpan window)
    {
        lock (_lock)
        {
            string key = $"{userId}:{channel}";
            var now = DateTime.UtcNow;
            if (!_userChannelTimestamps.TryGetValue(key, out var timestamps))
            {
                timestamps = new List<DateTime>();
                _userChannelTimestamps[key] = timestamps;
            }

            // Purge expired entries
            timestamps.RemoveAll(t => now - t > window);

            if (timestamps.Count >= maxPerWindow)
            {
                return false;
            }

            timestamps.Add(now);
            return true;
        }
    }
}

public class NotificationDispatcher
{
    private readonly List<INotificationProvider> _providers;
    private readonly Dictionary<string, UserNotificationPreferences> _preferences = new();
    private readonly NotificationRateLimiter _rateLimiter;
    private readonly HashSet<string> _seenIdempotencyKeys = new();
    private readonly PriorityQueue<NotificationMessage, int> _priorityQueue = new();
    private readonly object _lock = new();

    public NotificationDispatcher(IEnumerable<INotificationProvider> providers, NotificationRateLimiter? rateLimiter = null)
    {
        _providers = providers.ToList();
        _rateLimiter = rateLimiter ?? new NotificationRateLimiter();
    }

    public void SetUserPreferences(string userId, IEnumerable<NotificationChannel> optOuts)
    {
        lock (_lock)
        {
            var pref = new UserNotificationPreferences { UserId = userId };
            foreach (var ch in optOuts)
            {
                pref.OptedOutChannels.Add(ch);
            }
            _preferences[userId] = pref;
        }
    }

    public void Enqueue(NotificationMessage message)
    {
        lock (_lock)
        {
            _priorityQueue.Enqueue(message, (int)message.Priority);
        }
    }

    public int PendingQueueCount
    {
        get
        {
            lock (_lock) return _priorityQueue.Count;
        }
    }

    public async Task<NotificationResult> DispatchNextAsync()
    {
        NotificationMessage message;
        lock (_lock)
        {
            if (_priorityQueue.Count == 0)
            {
                return new NotificationResult(false, string.Empty, string.Empty, null, "Queue is empty");
            }
            message = _priorityQueue.Dequeue();
        }

        return await ProcessMessageAsync(message);
    }

    public async Task<NotificationResult> ProcessMessageAsync(NotificationMessage message)
    {
        // 1. Idempotency Check
        if (!string.IsNullOrEmpty(message.IdempotencyKey))
        {
            lock (_lock)
            {
                if (!_seenIdempotencyKeys.Add(message.IdempotencyKey))
                {
                    return new NotificationResult(true, message.MessageId, message.Channel.ToString(), null, "Deduplicated idempotent request");
                }
            }
        }

        // 2. User Preferences (Opt-out)
        lock (_lock)
        {
            if (_preferences.TryGetValue(message.UserId, out var pref) && pref.OptedOutChannels.Contains(message.Channel))
            {
                return new NotificationResult(false, message.MessageId, message.Channel.ToString(), null, "User opted out of this channel");
            }
        }

        // 3. Rate Limiting (e.g. Max 5 SMS per 1 minute in tests)
        if (message.Channel == NotificationChannel.Sms)
        {
            if (!_rateLimiter.Allow(message.UserId, message.Channel, maxPerWindow: 5, window: TimeSpan.FromMinutes(1)))
            {
                return new NotificationResult(false, message.MessageId, message.Channel.ToString(), null, "Rate limit exceeded for channel");
            }
        }

        // 4. Provider Selection & Fallback
        var eligibleProviders = _providers.Where(p => p.Channel == message.Channel).ToList();
        if (eligibleProviders.Count == 0)
        {
            return new NotificationResult(false, message.MessageId, message.Channel.ToString(), null, "No providers registered for channel");
        }

        foreach (var provider in eligibleProviders)
        {
            try
            {
                bool success = await provider.SendAsync(message);
                if (success)
                {
                    return new NotificationResult(true, message.MessageId, message.Channel.ToString(), provider.Name);
                }
            }
            catch
            {
                // Fall through to secondary provider
            }
        }

        return new NotificationResult(false, message.MessageId, message.Channel.ToString(), null, "All providers failed or unavailable");
    }
}
