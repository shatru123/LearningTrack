using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Day15.ChannelsConcurrency;

public class ChannelPipelineService
{
    public Channel<WorkItem> CreateBoundedChannel(
        int capacity,
        BoundedChannelFullMode fullMode = BoundedChannelFullMode.Wait,
        bool singleWriter = false,
        bool singleReader = false)
    {
        var options = new BoundedChannelOptions(capacity)
        {
            FullMode = fullMode,
            SingleWriter = singleWriter,
            SingleReader = singleReader,
            AllowSynchronousContinuations = false
        };

        return Channel.CreateBounded<WorkItem>(options);
    }

    public Channel<WorkItem> CreateUnboundedChannel()
    {
        var options = new UnboundedChannelOptions
        {
            AllowSynchronousContinuations = false
        };
        return Channel.CreateUnbounded<WorkItem>(options);
    }

    public async Task<ChannelPipelineMetrics> RunPipelineAsync(
        int totalItems,
        int producerCount,
        int consumerCount,
        int channelCapacity,
        BoundedChannelFullMode fullMode = BoundedChannelFullMode.Wait,
        int simulatedProcessingDelayMs = 0,
        CancellationToken ct = default)
    {
        var channel = CreateBoundedChannel(channelCapacity, fullMode);
        var stopwatch = Stopwatch.StartNew();

        int producedCount = 0;
        int consumedCount = 0;
        int droppedCount = 0;

        var itemsPerProducer = totalItems / producerCount;
        var remainingItems = totalItems % producerCount;

        // Producer Tasks
        var producerTasks = new List<Task>();
        for (int p = 0; p < producerCount; p++)
        {
            int producerId = p;
            int countToProduce = itemsPerProducer + (p == 0 ? remainingItems : 0);
            int startOffset = p * itemsPerProducer;

            producerTasks.Add(Task.Run(async () =>
            {
                for (int i = 0; i < countToProduce; i++)
                {
                    if (ct.IsCancellationRequested) break;

                    var item = new WorkItem(startOffset + i, $"Data-{startOffset + i} from P{producerId}", DateTime.UtcNow);

                    if (fullMode == BoundedChannelFullMode.Wait)
                    {
                        await channel.Writer.WriteAsync(item, ct);
                        Interlocked.Increment(ref producedCount);
                    }
                    else
                    {
                        // TryWrite with dropped count tracking for drop modes
                        if (channel.Writer.TryWrite(item))
                        {
                            Interlocked.Increment(ref producedCount);
                        }
                        else
                        {
                            Interlocked.Increment(ref droppedCount);
                        }
                    }
                }
            }, ct));
        }

        // Wait for all producers to finish, then complete the writer
        var producersCompletion = Task.WhenAll(producerTasks).ContinueWith(_ =>
        {
            channel.Writer.Complete();
        }, TaskScheduler.Default);

        // Consumer Tasks
        var consumerTasks = new List<Task>();
        var results = new ConcurrentBag<ProcessingResult>();

        for (int c = 0; c < consumerCount; c++)
        {
            int consumerId = c;
            consumerTasks.Add(Task.Run(async () =>
            {
                var workerName = $"Worker-{consumerId}";
                // ReadAllAsync automatically exits when channel.Writer is completed and buffer is empty
                await foreach (var item in channel.Reader.ReadAllAsync(ct))
                {
                    var sw = Stopwatch.StartNew();
                    if (simulatedProcessingDelayMs > 0)
                    {
                        await Task.Delay(simulatedProcessingDelayMs, ct);
                    }
                    sw.Stop();

                    results.Add(new ProcessingResult(item.Id, workerName, true, sw.Elapsed));
                    Interlocked.Increment(ref consumedCount);
                }
            }, ct));
        }

        await Task.WhenAll(producersCompletion, Task.WhenAll(consumerTasks));
        stopwatch.Stop();

        return new ChannelPipelineMetrics(
            producedCount,
            consumedCount,
            droppedCount,
            stopwatch.Elapsed);
    }
}
