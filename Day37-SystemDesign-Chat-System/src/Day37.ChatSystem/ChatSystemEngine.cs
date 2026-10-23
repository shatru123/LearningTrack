using System.Collections.Concurrent;

namespace Day37.ChatSystem;

public enum UserPresenceState
{
    Offline,
    Online,
    Away
}

public record UserPresence(string UserId, UserPresenceState State, DateTime LastSeenUtc);

public class PresenceTracker
{
    private readonly ConcurrentDictionary<string, DateTime> _heartbeats = new();
    private readonly TimeSpan _onlineTimeout;

    public PresenceTracker(TimeSpan? onlineTimeout = null)
    {
        _onlineTimeout = onlineTimeout ?? TimeSpan.FromSeconds(30);
    }

    public void Heartbeat(string userId)
    {
        _heartbeats[userId] = DateTime.UtcNow;
    }

    public void ExplicitDisconnect(string userId)
    {
        _heartbeats.TryRemove(userId, out _);
    }

    public UserPresence GetPresence(string userId)
    {
        if (_heartbeats.TryGetValue(userId, out var lastSeen))
        {
            if (DateTime.UtcNow - lastSeen <= _onlineTimeout)
            {
                return new UserPresence(userId, UserPresenceState.Online, lastSeen);
            }
        }
        return new UserPresence(userId, UserPresenceState.Offline, lastSeen == default ? DateTime.MinValue : lastSeen);
    }
}

public record ChatMessage(
    string MessageId,
    string ChannelId,
    string SenderId,
    string Content,
    long SequenceNumber,
    DateTime TimestampUtc
);

public class ChannelSequencer
{
    private readonly ConcurrentDictionary<string, long> _channelSequences = new();

    public long GetNextSequenceNumber(string channelId)
    {
        return _channelSequences.AddOrUpdate(channelId, 1, (_, current) => current + 1);
    }

    public long GetCurrentSequence(string channelId)
    {
        return _channelSequences.TryGetValue(channelId, out var seq) ? seq : 0;
    }
}

public interface IChatClientConnection
{
    string ConnectionId { get; }
    string UserId { get; }
    Task ReceiveMessageAsync(ChatMessage message);
    Task ReceiveTypingIndicatorAsync(string channelId, string userId, bool isTyping);
}

public class MockWebSocketClient : IChatClientConnection
{
    public string ConnectionId { get; } = Guid.NewGuid().ToString("N");
    public string UserId { get; }
    public List<ChatMessage> ReceivedMessages { get; } = new();
    public List<(string ChannelId, string UserId, bool IsTyping)> TypingEvents { get; } = new();

    public MockWebSocketClient(string userId)
    {
        UserId = userId;
    }

    public Task ReceiveMessageAsync(ChatMessage message)
    {
        ReceivedMessages.Add(message);
        return Task.CompletedTask;
    }

    public Task ReceiveTypingIndicatorAsync(string channelId, string userId, bool isTyping)
    {
        TypingEvents.Add((channelId, userId, isTyping));
        return Task.CompletedTask;
    }
}

public class DistributedChatHub
{
    public string ServerId { get; }
    private readonly ChannelSequencer _sequencer;
    private readonly ConcurrentDictionary<string, List<IChatClientConnection>> _channelSubscribers = new();
    private readonly ConcurrentDictionary<string, long> _userReadReceipts = new(); // key: "userId:channelId" -> seq
    private readonly List<ChatMessage> _messageHistory = new();
    private readonly object _historyLock = new();

    // Event bus simulating Redis Pub/Sub for cross-server message replication
    public static event Action<string, ChatMessage>? CrossServerMessagePublished;
    public static event Action<string, string, bool>? CrossServerTypingPublished;

    public DistributedChatHub(string serverId, ChannelSequencer sequencer)
    {
        ServerId = serverId;
        _sequencer = sequencer;

        CrossServerMessagePublished += HandleCrossServerMessage;
        CrossServerTypingPublished += HandleCrossServerTyping;
    }

    public void Subscribe(string channelId, IChatClientConnection client)
    {
        _channelSubscribers.AddOrUpdate(
            channelId,
            _ => [client],
            (_, list) => { lock (list) { list.Add(client); } return list; }
        );
    }

    public void Unsubscribe(string channelId, IChatClientConnection client)
    {
        if (_channelSubscribers.TryGetValue(channelId, out var list))
        {
            lock (list)
            {
                list.Remove(client);
            }
        }
    }

    public async Task<ChatMessage> SendMessageAsync(string channelId, string senderId, string content)
    {
        long seq = _sequencer.GetNextSequenceNumber(channelId);
        var message = new ChatMessage(
            MessageId: Guid.NewGuid().ToString("N"),
            ChannelId: channelId,
            SenderId: senderId,
            Content: content,
            SequenceNumber: seq,
            TimestampUtc: DateTime.UtcNow
        );

        // Store to channel history
        lock (_historyLock)
        {
            _messageHistory.Add(message);
        }

        // Broadcast to Redis pub/sub
        CrossServerMessagePublished?.Invoke(channelId, message);

        return await Task.FromResult(message);
    }

    public void SendTyping(string channelId, string userId, bool isTyping)
    {
        CrossServerTypingPublished?.Invoke(channelId, userId, isTyping);
    }

    public void MarkRead(string userId, string channelId, long sequenceNumber)
    {
        _userReadReceipts[$"{userId}:{channelId}"] = sequenceNumber;
    }

    public long GetLastReadSequence(string userId, string channelId)
    {
        return _userReadReceipts.TryGetValue($"{userId}:{channelId}", out var seq) ? seq : 0;
    }

    public IReadOnlyList<ChatMessage> GetMessageHistory(string channelId)
    {
        lock (_historyLock)
        {
            return _messageHistory.Where(m => m.ChannelId == channelId).OrderBy(m => m.SequenceNumber).ToList();
        }
    }

    private void HandleCrossServerMessage(string channelId, ChatMessage message)
    {
        if (_channelSubscribers.TryGetValue(channelId, out var list))
        {
            List<IChatClientConnection> clients;
            lock (list)
            {
                clients = list.ToList();
            }

            foreach (var client in clients)
            {
                _ = client.ReceiveMessageAsync(message);
            }
        }
    }

    private void HandleCrossServerTyping(string channelId, string userId, bool isTyping)
    {
        if (_channelSubscribers.TryGetValue(channelId, out var list))
        {
            List<IChatClientConnection> clients;
            lock (list)
            {
                clients = list.ToList();
            }

            foreach (var client in clients)
            {
                if (client.UserId != userId) // don't echo to self
                {
                    _ = client.ReceiveTypingIndicatorAsync(channelId, userId, isTyping);
                }
            }
        }
    }
}
