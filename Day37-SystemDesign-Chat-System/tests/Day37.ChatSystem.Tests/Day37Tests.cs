using Xunit;

namespace Day37.ChatSystem.Tests;

public class Day37Tests
{
    [Fact]
    public void PresenceTracker_Heartbeat_RecordsUserOnline()
    {
        var tracker = new PresenceTracker(TimeSpan.FromSeconds(10));
        tracker.Heartbeat("alice");

        var presence = tracker.GetPresence("alice");

        Assert.Equal(UserPresenceState.Online, presence.State);
        Assert.Equal("alice", presence.UserId);
        Assert.True(DateTime.UtcNow - presence.LastSeenUtc < TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void PresenceTracker_ExpiredHeartbeat_ReportsOffline()
    {
        var tracker = new PresenceTracker(TimeSpan.FromMilliseconds(1));
        tracker.Heartbeat("bob");

        Thread.Sleep(5); // allow 1ms timeout to elapse

        var presence = tracker.GetPresence("bob");
        Assert.Equal(UserPresenceState.Offline, presence.State);
    }

    [Fact]
    public void PresenceTracker_ExplicitDisconnect_RemovesPresenceInstantly()
    {
        var tracker = new PresenceTracker(TimeSpan.FromMinutes(5));
        tracker.Heartbeat("charlie");
        Assert.Equal(UserPresenceState.Online, tracker.GetPresence("charlie").State);

        tracker.ExplicitDisconnect("charlie");
        Assert.Equal(UserPresenceState.Offline, tracker.GetPresence("charlie").State);
    }

    [Fact]
    public void ChannelSequencer_MonotonicallyIncrementsPerChannel()
    {
        var sequencer = new ChannelSequencer();
        string c1 = "chan-tech";
        string c2 = "chan-random";

        Assert.Equal(1, sequencer.GetNextSequenceNumber(c1));
        Assert.Equal(2, sequencer.GetNextSequenceNumber(c1));
        Assert.Equal(1, sequencer.GetNextSequenceNumber(c2));
        Assert.Equal(3, sequencer.GetNextSequenceNumber(c1));
        Assert.Equal(2, sequencer.GetNextSequenceNumber(c2));
    }

    [Fact]
    public async Task DistributedChatHub_CrossServerBroadcast_DeliversToSubscribersOnDifferentServers()
    {
        var sequencer = new ChannelSequencer();
        var serverEast = new DistributedChatHub("server-us-east-1", sequencer);
        var serverWest = new DistributedChatHub("server-us-west-1", sequencer);

        var clientEast = new MockWebSocketClient("user-alice");
        var clientWest = new MockWebSocketClient("user-bob");

        string channelId = "general-room";
        serverEast.Subscribe(channelId, clientEast);
        serverWest.Subscribe(channelId, clientWest);

        // Alice sends message on East server
        var sentMsg = await serverEast.SendMessageAsync(channelId, "user-alice", "Hello from US East!");

        // Assert both Alice (East) and Bob (West) received it via simulated Redis pub/sub
        Assert.Single(clientEast.ReceivedMessages);
        Assert.Single(clientWest.ReceivedMessages);
        Assert.Equal("Hello from US East!", clientEast.ReceivedMessages[0].Content);
        Assert.Equal("Hello from US East!", clientWest.ReceivedMessages[0].Content);
        Assert.Equal(sentMsg.SequenceNumber, clientWest.ReceivedMessages[0].SequenceNumber);
    }

    [Fact]
    public async Task DistributedChatHub_ReadReceiptsAndHistory_TracksProgression()
    {
        var sequencer = new ChannelSequencer();
        var hub = new DistributedChatHub("server-1", sequencer);
        string channelId = "dev-room";

        await hub.SendMessageAsync(channelId, "dev1", "Commit 1");
        await hub.SendMessageAsync(channelId, "dev2", "Commit 2");
        var m3 = await hub.SendMessageAsync(channelId, "dev1", "Commit 3");

        var history = hub.GetMessageHistory(channelId);
        Assert.Equal(3, history.Count);
        Assert.Equal([1, 2, 3], history.Select(h => h.SequenceNumber).ToList());

        hub.MarkRead("dev2", channelId, m3.SequenceNumber);
        Assert.Equal(3, hub.GetLastReadSequence("dev2", channelId));
    }

    [Fact]
    public void DistributedChatHub_TypingIndicator_BroadcastsToOtherUsersOnly()
    {
        var sequencer = new ChannelSequencer();
        var hub = new DistributedChatHub("server-hub", sequencer);
        string channelId = "pair-chat";

        var clientA = new MockWebSocketClient("user-a");
        var clientB = new MockWebSocketClient("user-b");

        hub.Subscribe(channelId, clientA);
        hub.Subscribe(channelId, clientB);

        hub.SendTyping(channelId, "user-a", true);

        // Client A shouldn't receive their own typing event
        Assert.Empty(clientA.TypingEvents);
        // Client B should receive Client A's typing event
        Assert.Single(clientB.TypingEvents);
        Assert.Equal("user-a", clientB.TypingEvents[0].UserId);
        Assert.True(clientB.TypingEvents[0].IsTyping);
    }

    [Fact]
    public void CombinationSum1_StandardTarget_FindsAllValidCombinations()
    {
        int[] candidates = [2, 3, 6, 7];
        int target = 7;

        var result = CombinationSumSolver.CombinationSum(candidates, target);

        // Expected: [2, 2, 3] and [7]
        Assert.Equal(2, result.Count);
        Assert.Contains(result, c => c.SequenceEqual([2, 2, 3]));
        Assert.Contains(result, c => c.SequenceEqual([7]));
    }

    [Fact]
    public void CombinationSum2_WithDuplicates_FindsUniqueCombinations()
    {
        int[] candidates = [10, 1, 2, 7, 6, 1, 5];
        int target = 8;

        var result = CombinationSumSolver.CombinationSum2(candidates, target);

        // Expected combinations for sum 8:
        // [1, 1, 6], [1, 2, 5], [1, 7], [2, 6]
        Assert.Equal(4, result.Count);
        Assert.Contains(result, c => c.SequenceEqual([1, 1, 6]));
        Assert.Contains(result, c => c.SequenceEqual([1, 2, 5]));
        Assert.Contains(result, c => c.SequenceEqual([1, 7]));
        Assert.Contains(result, c => c.SequenceEqual([2, 6]));
    }

    [Fact]
    public void CombinationSum_UnachievableTarget_ReturnsEmpty()
    {
        int[] candidates = [5, 10, 15];
        int target = 7;

        var res1 = CombinationSumSolver.CombinationSum(candidates, target);
        var res2 = CombinationSumSolver.CombinationSum2(candidates, target);

        Assert.Empty(res1);
        Assert.Empty(res2);
    }
}
