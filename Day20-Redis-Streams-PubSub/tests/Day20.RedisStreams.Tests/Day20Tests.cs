using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace Day20.RedisStreams.Tests;

public class Day20Tests
{
    // ==========================================
    // 1. REDIS PUB/SUB TESTS
    // ==========================================

    [Fact]
    public async Task PubSub_MultipleSubscribers_ReceivesBroadcastMessage()
    {
        var pubsub = new RedisPubSubCoordinator();
        var received1 = new List<string>();
        var received2 = new List<string>();

        using var sub1 = pubsub.Subscribe("orders.created", msg =>
        {
            received1.Add(msg);
            return Task.CompletedTask;
        });

        using var sub2 = pubsub.Subscribe("orders.created", msg =>
        {
            received2.Add(msg);
            return Task.CompletedTask;
        });

        pubsub.GetSubscriberCount("orders.created").Should().Be(2);

        int delivered = await pubsub.PublishAsync("orders.created", "Order #101 placed");
        delivered.Should().Be(2);

        received1.Should().ContainSingle().Which.Should().Be("Order #101 placed");
        received2.Should().ContainSingle().Which.Should().Be("Order #101 placed");
    }

    [Fact]
    public async Task PubSub_EphemeralDrop_MessageDroppedWhenNoSubscribersExist()
    {
        var pubsub = new RedisPubSubCoordinator();
        int delivered = await pubsub.PublishAsync("notifications", "Hello World");

        delivered.Should().Be(0);
        pubsub.GetSubscriberCount("notifications").Should().Be(0);
    }

    // ==========================================
    // 2. REDIS STREAMS TESTS
    // ==========================================

    [Fact]
    public void Streams_XAdd_AppendsMessagesWithMonotonicIds()
    {
        var manager = new RedisStreamsManager();
        var m1 = manager.Add(new Dictionary<string, string> { ["event"] = "login", ["userId"] = "42" });
        var m2 = manager.Add(new Dictionary<string, string> { ["event"] = "purchase", ["amount"] = "99.9" });

        manager.StreamLength.Should().Be(2);
        m1.Id.Should().NotBeNullOrEmpty();
        m2.Id.Should().NotBeNullOrEmpty();

        var id1 = StreamEntryId.Parse(m1.Id);
        var id2 = StreamEntryId.Parse(m2.Id);
        id1.CompareTo(id2).Should().BeNegative();
    }

    [Fact]
    public void Streams_ConsumerGroup_DistributesMessagesAcrossMultipleWorkers()
    {
        var manager = new RedisStreamsManager();
        manager.CreateConsumerGroup("order-processors", startId: "0");

        // Add 4 messages
        for (int i = 1; i <= 4; i++)
        {
            manager.Add(new Dictionary<string, string> { ["orderId"] = i.ToString() });
        }

        // Worker A reads 2 messages
        var batchA = manager.ReadGroup("order-processors", "worker-A", count: 2);
        batchA.Should().HaveCount(2);
        batchA[0].Fields["orderId"].Should().Be("1");
        batchA[1].Fields["orderId"].Should().Be("2");

        // Worker B reads next 2 messages (partitioned non-overlapping)
        var batchB = manager.ReadGroup("order-processors", "worker-B", count: 2);
        batchB.Should().HaveCount(2);
        batchB[0].Fields["orderId"].Should().Be("3");
        batchB[1].Fields["orderId"].Should().Be("4");

        // PEL should track all 4 in-flight messages
        var pending = manager.GetPendingEntries("order-processors");
        pending.Should().HaveCount(4);
    }

    [Fact]
    public void Streams_XAck_RemovesAcknowledgedMessagesFromPel()
    {
        var manager = new RedisStreamsManager();
        manager.CreateConsumerGroup("email-workers", startId: "0");

        var msg1 = manager.Add(new Dictionary<string, string> { ["email"] = "a@test.com" });
        var msg2 = manager.Add(new Dictionary<string, string> { ["email"] = "b@test.com" });

        var read = manager.ReadGroup("email-workers", "worker-1", count: 2);
        read.Should().HaveCount(2);

        manager.GetPendingEntries("email-workers").Should().HaveCount(2);

        // Acknowledge msg1
        int acked = manager.Ack("email-workers", msg1.Id);
        acked.Should().Be(1);

        // PEL now only contains msg2
        var pending = manager.GetPendingEntries("email-workers");
        pending.Should().HaveCount(1);
        pending[0].MessageId.Should().Be(msg2.Id);

        // Double ACK returns 0
        manager.Ack("email-workers", msg1.Id).Should().Be(0);
    }

    [Fact]
    public void Streams_XClaim_RecoversAbandonedMessagesFromDeadConsumer()
    {
        var manager = new RedisStreamsManager();
        manager.CreateConsumerGroup("payment-workers", startId: "0");

        var msg = manager.Add(new Dictionary<string, string> { ["invoice"] = "INV-990" });

        // Worker-Dead receives message at t0
        var t0 = new DateTime(2026, 10, 10, 10, 0, 0, DateTimeKind.Utc);
        manager.ReadGroup("payment-workers", "worker-dead", count: 1);

        // Worker-Dead crashes without ACK. At t0 + 60s, worker-healthy checks PEL and claims it
        var tEval = t0.AddSeconds(60);
        var pendingBefore = manager.GetPendingEntries("payment-workers", evaluationTime: tEval);
        pendingBefore.Should().ContainSingle();
        pendingBefore[0].ConsumerName.Should().Be("worker-dead");

        // Claim messages idle > 30s
        var claimed = manager.Claim("payment-workers", "worker-healthy", minIdleTime: TimeSpan.FromSeconds(30), evaluationTime: tEval);
        claimed.Should().ContainSingle();
        claimed[0].Id.Should().Be(msg.Id);

        // Verify PEL reassignment and delivery count increment
        var pendingAfter = manager.GetPendingEntries("payment-workers", evaluationTime: tEval);
        pendingAfter[0].ConsumerName.Should().Be("worker-healthy");
        pendingAfter[0].DeliveryCount.Should().Be(2);

        // Worker-healthy completes and ACKs
        manager.Ack("payment-workers", msg.Id).Should().Be(1);
        manager.GetPendingEntries("payment-workers").Should().BeEmpty();
    }

    // ==========================================
    // 3. DSA TREE TRAVERSAL TESTS (LC #235 & #102)
    // ==========================================

    [Fact]
    public void LC235_LowestCommonAncestor_StandardBst_ReturnsCorrectAncestor()
    {
        var solvers = new TreeTraversalSolvers();
        // BST: [6, 2, 8, 0, 4, 7, 9, null, null, 3, 5]
        var root = TreeTraversalSolvers.BuildTree(new int?[] { 6, 2, 8, 0, 4, 7, 9, null, null, 3, 5 });
        var p = TreeTraversalSolvers.FindNode(root, 2);
        var q = TreeTraversalSolvers.FindNode(root, 8);

        var lca = solvers.LowestCommonAncestor(root, p, q);
        lca.Should().NotBeNull();
        lca!.val.Should().Be(6);
    }

    [Fact]
    public void LC235_LowestCommonAncestor_NodeIsItsOwnAncestor_ReturnsNode()
    {
        var solvers = new TreeTraversalSolvers();
        // BST: [6, 2, 8, 0, 4, 7, 9, null, null, 3, 5]
        var root = TreeTraversalSolvers.BuildTree(new int?[] { 6, 2, 8, 0, 4, 7, 9, null, null, 3, 5 });
        var p = TreeTraversalSolvers.FindNode(root, 2);
        var q = TreeTraversalSolvers.FindNode(root, 4);

        var lca = solvers.LowestCommonAncestor(root, p, q);
        lca.Should().NotBeNull();
        lca!.val.Should().Be(2);
    }

    [Fact]
    public void LC102_LevelOrder_StandardTree_ReturnsGroupedLevels()
    {
        var solvers = new TreeTraversalSolvers();
        // Tree: [3, 9, 20, null, null, 15, 7]
        var root = TreeTraversalSolvers.BuildTree(new int?[] { 3, 9, 20, null, null, 15, 7 });

        var levels = solvers.LevelOrder(root);
        levels.Should().HaveCount(3);
        levels[0].Should().Equal(new[] { 3 });
        levels[1].Should().Equal(new[] { 9, 20 });
        levels[2].Should().Equal(new[] { 15, 7 });
    }

    [Fact]
    public void LC102_LevelOrder_SingleNodeAndEmptyTree_ReturnsExpectedList()
    {
        var solvers = new TreeTraversalSolvers();
        solvers.LevelOrder(null).Should().BeEmpty();

        var single = solvers.LevelOrder(new TreeNode(42));
        single.Should().HaveCount(1);
        single[0].Should().Equal(new[] { 42 });
    }
}
