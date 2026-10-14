using Day29.RabbitMQCoreExchanges;
using Xunit;

namespace Day29.RabbitMQCoreExchanges.Tests;

public class Day29Tests
{
    [Fact]
    public void DirectExchange_RoutesToExactMatchingQueue()
    {
        var broker = new AmqpBrokerSimulator();
        broker.DeclareExchange("orders.direct", ExchangeType.Direct);
        broker.DeclareQueue("orders.created.queue");
        broker.DeclareQueue("orders.cancelled.queue");

        broker.BindQueue("orders.created.queue", "orders.direct", routingKey: "order.created");
        broker.BindQueue("orders.cancelled.queue", "orders.direct", routingKey: "order.cancelled");

        broker.Publish("orders.direct", "order.created", "{ id: 101, status: 'created' }");

        var msgCreated = broker.BasicGet("orders.created.queue");
        var msgCancelled = broker.BasicGet("orders.cancelled.queue");

        Assert.NotNull(msgCreated);
        Assert.Equal("order.created", msgCreated.RoutingKey);
        Assert.Null(msgCancelled); // Did not route to cancelled queue
    }

    [Fact]
    public void FanoutExchange_BroadcastsToAllBoundQueues()
    {
        var broker = new AmqpBrokerSimulator();
        broker.DeclareExchange("notifications.fanout", ExchangeType.Fanout);
        broker.DeclareQueue("email.notifications");
        broker.DeclareQueue("sms.notifications");

        broker.BindQueue("email.notifications", "notifications.fanout");
        broker.BindQueue("sms.notifications", "notifications.fanout");

        broker.Publish("notifications.fanout", routingKey: "irrelevant.key", "{ alert: 'Server Down' }");

        var emailMsg = broker.BasicGet("email.notifications");
        var smsMsg = broker.BasicGet("sms.notifications");

        Assert.NotNull(emailMsg);
        Assert.NotNull(smsMsg);
        Assert.Equal(emailMsg.Payload, smsMsg.Payload);
    }

    [Fact]
    public void TopicExchange_RoutesWithWildcards_StarAndHash()
    {
        var broker = new AmqpBrokerSimulator();
        broker.DeclareExchange("events.topic", ExchangeType.Topic);
        broker.DeclareQueue("us.orders.queue");
        broker.DeclareQueue("all.critical.queue");

        // * matches exactly one word; # matches zero or more words
        broker.BindQueue("us.orders.queue", "events.topic", routingKey: "orders.us.*");
        broker.BindQueue("all.critical.queue", "events.topic", routingKey: "#.critical");

        // 1. Matches us.orders.queue only
        broker.Publish("events.topic", "orders.us.created", "Order 1");
        // 2. Matches both
        broker.Publish("events.topic", "orders.us.critical", "Order 2 Critical");
        // 3. Matches all.critical.queue only
        broker.Publish("events.topic", "europe.payments.failed.critical", "Payment Critical");

        var q1 = broker.GetQueue("us.orders.queue")!;
        var q2 = broker.GetQueue("all.critical.queue")!;

        Assert.Equal(2, q1.ReadyMessageCount); // orders.us.created, orders.us.critical
        Assert.Equal(2, q2.ReadyMessageCount); // orders.us.critical, europe.payments.failed.critical
    }

    [Fact]
    public void HeadersExchange_RoutesMatchingHeaders_AllAndAny()
    {
        var broker = new AmqpBrokerSimulator();
        broker.DeclareExchange("headers.exchange", ExchangeType.Headers);
        broker.DeclareQueue("vip.finance.queue");
        broker.DeclareQueue("any.alert.queue");

        broker.BindQueue(
            "vip.finance.queue",
            "headers.exchange",
            headers: new Dictionary<string, object> { ["department"] = "finance", ["vip"] = true },
            matchMode: HeadersMatchMode.All);

        broker.BindQueue(
            "any.alert.queue",
            "headers.exchange",
            headers: new Dictionary<string, object> { ["urgent"] = true, ["p1"] = true },
            matchMode: HeadersMatchMode.Any);

        // 1. Matches vip.finance.queue (all match)
        broker.Publish("headers.exchange", "", "Finance VIP report", new Dictionary<string, object>
        {
            ["department"] = "finance",
            ["vip"] = true
        });

        // 2. Matches any.alert.queue (urgent matches)
        broker.Publish("headers.exchange", "", "Urgent System Ping", new Dictionary<string, object>
        {
            ["urgent"] = true
        });

        Assert.Equal(1, broker.GetQueue("vip.finance.queue")!.ReadyMessageCount);
        Assert.Equal(1, broker.GetQueue("any.alert.queue")!.ReadyMessageCount);
    }

    [Fact]
    public void PublisherConfirms_ReturnsAckOnRoutedMessage_AndNackOnMandatoryUnrouted()
    {
        var broker = new AmqpBrokerSimulator();
        broker.DeclareExchange("direct.ack", ExchangeType.Direct);
        broker.DeclareQueue("target.queue");
        broker.BindQueue("target.queue", "direct.ack", "valid.key");

        // Routed message returns true (ACK)
        var confirmed = broker.Publish("direct.ack", "valid.key", "Payload", mandatory: true);
        Assert.True(confirmed);

        // Unrouted message with mandatory=true returns false (NACK)
        var unroutedConfirmed = broker.Publish("direct.ack", "unbound.key", "Lost Payload", mandatory: true);
        Assert.False(unroutedConfirmed);
    }

    [Fact]
    public void ConsumerAck_RemovesMessageFromUnackedQueue()
    {
        var broker = new AmqpBrokerSimulator();
        broker.DeclareExchange("work.direct", ExchangeType.Direct);
        broker.DeclareQueue("work.queue");
        broker.BindQueue("work.queue", "work.direct", "task");

        broker.Publish("work.direct", "task", "Job 1");
        var msg = broker.BasicGet("work.queue");

        Assert.NotNull(msg);
        var q = broker.GetQueue("work.queue")!;
        Assert.Equal(0, q.ReadyMessageCount);
        Assert.Equal(1, q.UnackedMessageCount);

        var acked = broker.BasicAck("work.queue", msg.DeliveryTag);
        Assert.True(acked);
        Assert.Equal(0, q.UnackedMessageCount);
        Assert.Equal(1, broker.AckedCount);
    }

    [Fact]
    public void ConsumerNack_WithRequeue_PushesBackToHeadOfQueue()
    {
        var broker = new AmqpBrokerSimulator();
        broker.DeclareExchange("work.direct", ExchangeType.Direct);
        broker.DeclareQueue("retry.queue");
        broker.BindQueue("retry.queue", "work.direct", "task");

        broker.Publish("work.direct", "task", "Job A");
        var msg = broker.BasicGet("retry.queue")!;

        var nacked = broker.BasicNack("retry.queue", msg.DeliveryTag, requeue: true);
        Assert.True(nacked);
        Assert.Equal(1, broker.NackedCount);

        // Redelivered message is available again
        var redelivered = broker.BasicGet("retry.queue");
        Assert.NotNull(redelivered);
        Assert.Equal(msg.Payload, redelivered.Payload);
        Assert.True(redelivered.Redelivered);
    }

    [Fact]
    public void DeadLetterExchange_RoutesRejectedMessageToDlx()
    {
        var broker = new AmqpBrokerSimulator();
        broker.DeclareExchange("primary.exchange", ExchangeType.Direct);
        broker.DeclareExchange("dlx.exchange", ExchangeType.Fanout);

        broker.DeclareQueue("dead.letter.queue");
        broker.BindQueue("dead.letter.queue", "dlx.exchange");

        broker.DeclareQueue("main.work.queue", deadLetterExchange: "dlx.exchange");
        broker.BindQueue("main.work.queue", "primary.exchange", "work");

        broker.Publish("primary.exchange", "work", "Poison Message");
        var msg = broker.BasicGet("main.work.queue")!;

        // Reject without requeue -> routed to DLX
        broker.BasicNack("main.work.queue", msg.DeliveryTag, requeue: false);

        Assert.Equal(1, broker.DeadLetteredCount);
        var poisonMsg = broker.BasicGet("dead.letter.queue");
        Assert.NotNull(poisonMsg);
        Assert.Equal("Poison Message", poisonMsg.Payload);
    }

    [Fact]
    public void SurroundedRegions_CapturesEnclosedO_AndPreservesBorderConnected()
    {
        var solver = new SurroundedRegionsAndOrangesSolver();

        // Standard LeetCode #130 test matrix:
        // X X X X
        // X O O X
        // X X O X
        // X O X X
        var board = new char[][]
        {
            new[] { 'X', 'X', 'X', 'X' },
            new[] { 'X', 'O', 'O', 'X' },
            new[] { 'X', 'X', 'O', 'X' },
            new[] { 'X', 'O', 'X', 'X' }
        };

        solver.SolveSurroundedRegions(board);

        // Expected output:
        // X X X X
        // X X X X
        // X X X X
        // X O X X
        Assert.Equal('X', board[1][1]);
        Assert.Equal('X', board[1][2]);
        Assert.Equal('X', board[2][2]);
        Assert.Equal('O', board[3][1]); // Connected to bottom border
    }

    [Fact]
    public void RottingOranges_MultiSourceBfs_CalculatesMinimumMinutesOrNegativeOne()
    {
        var solver = new SurroundedRegionsAndOrangesSolver();

        // Standard LeetCode #994 test grid:
        // 2 1 1
        // 1 1 0
        // 0 1 1
        // Expected minutes: 4
        var grid = new int[][]
        {
            new[] { 2, 1, 1 },
            new[] { 1, 1, 0 },
            new[] { 0, 1, 1 }
        };

        var minutes = solver.OrangesRotting(grid);
        Assert.Equal(4, minutes);

        // Impossible grid with isolated fresh orange:
        // 2 1 0
        // 0 2 0
        // 0 0 1
        var impossibleGrid = new int[][]
        {
            new[] { 2, 1, 0 },
            new[] { 0, 2, 0 },
            new[] { 0, 0, 1 }
        };
        Assert.Equal(-1, solver.OrangesRotting(impossibleGrid));

        // Already fully rotten or all empty
        var zeroGrid = new int[][] { new[] { 0, 2 } };
        Assert.Equal(0, solver.OrangesRotting(zeroGrid));
    }
}
