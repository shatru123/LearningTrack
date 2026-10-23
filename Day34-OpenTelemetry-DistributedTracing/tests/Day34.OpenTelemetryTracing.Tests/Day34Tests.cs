using System.Diagnostics;
using Xunit;

namespace Day34.OpenTelemetryTracing.Tests;

public class Day34Tests
{
    [Fact]
    public void TraceContext_InjectAndExtract_RoundTripPreservesTraceAndSpanId()
    {
        using var manager = new TracingPipelineManager();
        var traceId = ActivityTraceId.CreateRandom();
        var spanId = ActivitySpanId.CreateRandom();
        var context = new ActivityContext(traceId, spanId, ActivityTraceFlags.Recorded, "rojo=1");

        using var activity = TracingPipelineManager.ActivitySource.StartActivity(
            "TestOperation",
            ActivityKind.Internal,
            parentContext: context);

        Assert.NotNull(activity);
        var carrier = new Dictionary<string, string>();
        TracingPipelineManager.InjectTraceContext(activity, carrier);

        Assert.True(carrier.ContainsKey("traceparent"));
        Assert.StartsWith("00-", carrier["traceparent"]);

        var extracted = TracingPipelineManager.ExtractTraceContext(carrier, out _);

        Assert.Equal(activity.TraceId, extracted.TraceId);
        Assert.Equal(activity.SpanId, extracted.SpanId);
        Assert.Equal(ActivityTraceFlags.Recorded, extracted.TraceFlags);
    }

    [Fact]
    public void TraceContext_Extract_InvalidHeader_ReturnsDefaultContext()
    {
        var invalidCarriers = new[]
        {
            new Dictionary<string, string>(),
            new Dictionary<string, string> { ["traceparent"] = "invalid-format" },
            new Dictionary<string, string> { ["traceparent"] = "01-1234-5678-00" } // unsupported version
        };

        foreach (var carrier in invalidCarriers)
        {
            var ctx = TracingPipelineManager.ExtractTraceContext(carrier, out _);
            Assert.Equal(default, ctx);
        }
    }

    [Fact]
    public void TraceContext_BaggagePropagation_PreservesKeyValues()
    {
        using var manager = new TracingPipelineManager();
        var carrier = new Dictionary<string, string>
        {
            ["traceparent"] = "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01",
            ["baggage"] = "userId=usr_42,tier=enterprise,region=us-east-1"
        };

        var ctx = TracingPipelineManager.ExtractTraceContext(carrier, out var baggage);

        Assert.Equal("4bf92f3577b34da6a3ce929d0e0e4736", ctx.TraceId.ToHexString());
        Assert.Equal(3, baggage.Count);
        Assert.Equal("usr_42", baggage["userId"]);
        Assert.Equal("enterprise", baggage["tier"]);
        Assert.Equal("us-east-1", baggage["region"]);
    }

    [Fact]
    public void TracingPipelineManager_ActivityListener_CapturesCompletedSpans()
    {
        using var manager = new TracingPipelineManager();
        manager.ClearSpans();

        using (var act = TracingPipelineManager.ActivitySource.StartActivity("UnitOperation"))
        {
            act?.SetTag("test.key", "test.val");
            act?.AddEvent(new ActivityEvent("MilestoneReached"));
        }

        var spans = manager.CompletedSpans;
        Assert.Single(spans);
        Assert.Equal("UnitOperation", spans[0].OperationName);
        Assert.Equal("test.val", spans[0].Tags["test.key"]);
        Assert.Contains("MilestoneReached", spans[0].Events);
    }

    [Fact]
    public async Task TracingPipelineManager_MultiHopTrace_PreservesSameTraceIdAcrossAllServices()
    {
        using var manager = new TracingPipelineManager();
        manager.ClearSpans();

        var returnedTraceId = await manager.SimulateMultiHopTraceAsync("tenant-abc-999", "ord-555");
        var spans = manager.CompletedSpans;

        // Expect 4 completed spans: Gateway, RabbitMQ Publish, OrderService, InventoryService
        Assert.Equal(4, spans.Count);

        // Every span must share the exact same TraceId
        var firstTraceId = spans[0].TraceId;
        Assert.Equal(returnedTraceId, firstTraceId);
        Assert.All(spans, s => Assert.Equal(firstTraceId, s.TraceId));
    }

    [Fact]
    public async Task TracingPipelineManager_MultiHopTrace_FormsCorrectParentChildSpanHierarchy()
    {
        using var manager = new TracingPipelineManager();
        manager.ClearSpans();

        await manager.SimulateMultiHopTraceAsync("tenant-hierarchy", "ord-hierarchy-1");
        var spans = manager.CompletedSpans;

        var gatewaySpan = spans.First(s => s.OperationName == "Gateway.ForwardRequest");
        var orderSpan = spans.First(s => s.OperationName == "OrderService.CreateOrder");
        var publishSpan = spans.First(s => s.OperationName == "RabbitMQ.PublishOrderCreated");
        var consumerSpan = spans.First(s => s.OperationName == "InventoryService.ReserveStock");

        // OrderService's parent should be Gateway
        Assert.Equal(gatewaySpan.SpanId, orderSpan.ParentSpanId);

        // RabbitMQ Producer's parent should be OrderService
        Assert.Equal(orderSpan.SpanId, publishSpan.ParentSpanId);

        // InventoryService Consumer's parent should be RabbitMQ Producer
        Assert.Equal(publishSpan.SpanId, consumerSpan.ParentSpanId);
    }

    [Fact]
    public async Task TracingPipelineManager_MultiHopTrace_PropagatesBaggageAcrossHops()
    {
        using var manager = new TracingPipelineManager();
        manager.ClearSpans();

        string expectedTenant = "tenant-enterprise-gold";
        await manager.SimulateMultiHopTraceAsync(expectedTenant, "ord-888");

        var spans = manager.CompletedSpans;
        var consumerSpan = spans.First(s => s.OperationName == "InventoryService.ReserveStock");

        Assert.True(consumerSpan.Baggage.ContainsKey("tenant.id"));
        Assert.Equal(expectedTenant, consumerSpan.Baggage["tenant.id"]);
    }

    [Fact]
    public void TaskScheduler_Math_Example1_ReturnsCorrectIntervals()
    {
        char[] tasks = ['A', 'A', 'A', 'B', 'B', 'B'];
        int n = 2;

        int result = TaskSchedulerSolver.LeastIntervalMath(tasks, n);

        // Explanation: A -> B -> idle -> A -> B -> idle -> A -> B (Total = 8)
        Assert.Equal(8, result);
    }

    [Fact]
    public void TaskScheduler_Math_NoIdleNeeded_ReturnsTasksLength()
    {
        char[] tasks = ['A', 'A', 'A', 'B', 'B', 'B'];
        int n = 0;

        int result = TaskSchedulerSolver.LeastIntervalMath(tasks, n);

        Assert.Equal(6, result);
    }

    [Fact]
    public void TaskScheduler_Simulation_MatchesMathApproach_AcrossMultipleInputs()
    {
        var testCases = new (char[] tasks, int n)[]
        {
            (['A', 'A', 'A', 'B', 'B', 'B'], 2),
            (['A', 'C', 'A', 'B', 'D', 'B'], 1),
            (['A', 'A', 'A', 'B', 'B', 'B'], 3),
            (['A', 'A', 'A', 'A', 'A', 'A', 'B', 'C', 'D', 'E', 'F', 'G'], 2)
        };

        foreach (var (tasks, n) in testCases)
        {
            int mathRes = TaskSchedulerSolver.LeastIntervalMath(tasks, n);
            int simRes = TaskSchedulerSolver.LeastIntervalSimulation(tasks, n);

            Assert.Equal(mathRes, simRes);
        }
    }
}
