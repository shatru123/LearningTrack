using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace Day15.ChannelsConcurrency.Tests;

public class Day15Tests
{
    [Fact]
    public void LeetCode206_ReverseList_Iterative_ReversesCorrectly()
    {
        var head = ListNode.FromArray(new[] { 1, 2, 3, 4, 5 });
        var reversed = LinkedListSolvers.ReverseList(head);
        reversed.Should().NotBeNull();
        reversed!.ToArray().Should().Equal(new[] { 5, 4, 3, 2, 1 });
    }

    [Fact]
    public void LeetCode206_ReverseList_EmptyAndSingleNode()
    {
        LinkedListSolvers.ReverseList(null).Should().BeNull();

        var single = ListNode.FromArray(new[] { 42 });
        var res = LinkedListSolvers.ReverseList(single);
        res!.ToArray().Should().Equal(new[] { 42 });
    }

    [Fact]
    public void LeetCode206_ReverseListRecursive_MatchesIterative()
    {
        var head = ListNode.FromArray(new[] { 10, 20, 30 });
        var reversed = LinkedListSolvers.ReverseListRecursive(head);
        reversed!.ToArray().Should().Equal(new[] { 30, 20, 10 });
    }

    [Fact]
    public void LeetCode21_MergeTwoLists_Iterative_MergesInSortedOrder()
    {
        var l1 = ListNode.FromArray(new[] { 1, 2, 4 });
        var l2 = ListNode.FromArray(new[] { 1, 3, 4 });

        var merged = LinkedListSolvers.MergeTwoLists(l1, l2);
        merged!.ToArray().Should().Equal(new[] { 1, 1, 2, 3, 4, 4 });
    }

    [Fact]
    public void LeetCode21_MergeTwoLists_HandlesNullLists()
    {
        var l1 = ListNode.FromArray(new[] { 5, 6 });
        LinkedListSolvers.MergeTwoLists(l1, null)!.ToArray().Should().Equal(new[] { 5, 6 });
        LinkedListSolvers.MergeTwoLists(null, l1)!.ToArray().Should().Equal(new[] { 5, 6 });
        LinkedListSolvers.MergeTwoLists(null, null).Should().BeNull();
    }

    [Fact]
    public void LeetCode21_MergeTwoListsRecursive_MatchesIterative()
    {
        var l1 = ListNode.FromArray(new[] { 2, 5, 8 });
        var l2 = ListNode.FromArray(new[] { 1, 6, 9 });

        var merged = LinkedListSolvers.MergeTwoListsRecursive(l1, l2);
        merged!.ToArray().Should().Equal(new[] { 1, 2, 5, 6, 8, 9 });
    }

    [Fact]
    public async Task ChannelPipeline_BoundedWait_DeliversAllItemsWithoutLoss()
    {
        var service = new ChannelPipelineService();
        const int totalItems = 200;
        const int producers = 4;
        const int consumers = 2;
        const int capacity = 20;

        var metrics = await service.RunPipelineAsync(
            totalItems: totalItems,
            producerCount: producers,
            consumerCount: consumers,
            channelCapacity: capacity,
            fullMode: BoundedChannelFullMode.Wait);

        metrics.TotalProduced.Should().Be(totalItems);
        metrics.TotalConsumed.Should().Be(totalItems);
        metrics.DroppedItems.Should().Be(0);
    }

    [Fact]
    public async Task ChannelPipeline_BoundedDrop_DiscardsExcessOnBackpressure()
    {
        var service = new ChannelPipelineService();
        var channel = service.CreateBoundedChannel(capacity: 5, fullMode: BoundedChannelFullMode.DropOldest);

        // Fill buffer
        for (int i = 0; i < 5; i++)
        {
            channel.Writer.TryWrite(new WorkItem(i, $"Item-{i}", DateTime.UtcNow)).Should().BeTrue();
        }

        // Write additional items - DropOldest allows writing, evicting the oldest
        channel.Writer.TryWrite(new WorkItem(99, "Item-99", DateTime.UtcNow)).Should().BeTrue();

        channel.Writer.Complete();

        // Drain channel
        var items = new System.Collections.Generic.List<WorkItem>();
        await foreach (var item in channel.Reader.ReadAllAsync())
        {
            items.Add(item);
        }

        items.Should().HaveCount(5);
        items.Should().Contain(x => x.Id == 99);
        items.Should().NotContain(x => x.Id == 0); // 0 was dropped
    }

    [Fact]
    public async Task ChannelPipeline_CancellationToken_AbortsGracefully()
    {
        var service = new ChannelPipelineService();
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        // Start large workload with slow consumers
        Func<Task> act = async () =>
        {
            await service.RunPipelineAsync(
                totalItems: 10000,
                producerCount: 2,
                consumerCount: 1,
                channelCapacity: 10,
                simulatedProcessingDelayMs: 20,
                ct: cts.Token);
        };

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
