using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Day34.OpenTelemetryTracing;

public record TraceSpanSnapshot(
    string TraceId,
    string SpanId,
    string? ParentSpanId,
    string OperationName,
    ActivityKind Kind,
    Dictionary<string, string?> Tags,
    Dictionary<string, string?> Baggage,
    List<string> Events
);

public class TracingPipelineManager : IDisposable
{
    public const string SourceName = "Day34.OpenTelemetryTracing";
    public const string Version = "1.0.0";
    public static readonly ActivitySource ActivitySource = new(SourceName, Version);

    private readonly ActivityListener _listener;
    private readonly List<TraceSpanSnapshot> _completedSpans = new();
    private readonly object _lock = new();

    public IReadOnlyList<TraceSpanSnapshot> CompletedSpans
    {
        get
        {
            lock (_lock)
            {
                return _completedSpans.ToList();
            }
        }
    }

    public TracingPipelineManager()
    {
        Activity.DefaultIdFormat = ActivityIdFormat.W3C;
        Activity.ForceDefaultIdFormat = true;

        _listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity =>
            {
                lock (_lock)
                {
                    var snapshot = new TraceSpanSnapshot(
                        TraceId: activity.TraceId.ToHexString(),
                        SpanId: activity.SpanId.ToHexString(),
                        ParentSpanId: activity.ParentSpanId.ToHexString(),
                        OperationName: activity.OperationName,
                        Kind: activity.Kind,
                        Tags: activity.Tags.ToDictionary(kv => kv.Key, kv => kv.Value),
                        Baggage: activity.Baggage.ToDictionary(kv => kv.Key, kv => kv.Value),
                        Events: activity.Events.Select(e => e.Name).ToList()
                    );
                    _completedSpans.Add(snapshot);
                }
            }
        };

        ActivitySource.AddActivityListener(_listener);
    }

    public void ClearSpans()
    {
        lock (_lock)
        {
            _completedSpans.Clear();
        }
    }

    /// <summary>
    /// Injects W3C traceparent and tracestate into a string-keyed carrier (HTTP headers, message properties).
    /// Format: traceparent: 00-{trace_id}-{span_id}-{trace_flags}
    /// </summary>
    public static void InjectTraceContext(Activity activity, IDictionary<string, string> carrier)
    {
        if (activity == null) return;

        carrier["traceparent"] = $"00-{activity.TraceId.ToHexString()}-{activity.SpanId.ToHexString()}-01";

        if (!string.IsNullOrEmpty(activity.TraceStateString))
        {
            carrier["tracestate"] = activity.TraceStateString;
        }

        // Propagate Baggage headers if present
        var baggageEntries = activity.Baggage.ToList();
        if (baggageEntries.Count > 0)
        {
            carrier["baggage"] = string.Join(",", baggageEntries.Select(b => $"{b.Key}={b.Value}"));
        }
    }

    /// <summary>
    /// Extracts W3C TraceContext from carrier headers and parses into an ActivityContext.
    /// </summary>
    public static ActivityContext ExtractTraceContext(IDictionary<string, string> carrier, out Dictionary<string, string> baggage)
    {
        baggage = new Dictionary<string, string>();

        if (carrier.TryGetValue("baggage", out var baggageHeader) && !string.IsNullOrWhiteSpace(baggageHeader))
        {
            foreach (var item in baggageHeader.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var parts = item.Split('=', 2);
                if (parts.Length == 2)
                {
                    baggage[parts[0].Trim()] = parts[1].Trim();
                }
            }
        }

        if (!carrier.TryGetValue("traceparent", out var traceparent) || string.IsNullOrWhiteSpace(traceparent))
        {
            return default;
        }

        var match = Regex.Match(traceparent.Trim(), @"^00-([0-9a-fA-F]{32})-([0-9a-fA-F]{16})-([0-9a-fA-F]{2})$");
        if (!match.Success)
        {
            return default;
        }

        var traceId = ActivityTraceId.CreateFromString(match.Groups[1].ValueSpan);
        var spanId = ActivitySpanId.CreateFromString(match.Groups[2].ValueSpan);
        var flags = match.Groups[3].Value == "01" ? ActivityTraceFlags.Recorded : ActivityTraceFlags.None;

        carrier.TryGetValue("tracestate", out var traceState);

        return new ActivityContext(traceId, spanId, flags, traceState);
    }

    /// <summary>
    /// Simulates a 3-hop distributed transaction across Gateway, Order API, and RabbitMQ Worker.
    /// Demonstrates propagation, span hierarchy, baggage, and span events.
    /// </summary>
    public async Task<string> SimulateMultiHopTraceAsync(string tenantId, string orderId)
    {
        var httpCarrier = new Dictionary<string, string>();
        var messageQueueCarrier = new Dictionary<string, string>();

        // Hop 1: API Gateway (Client / Server boundary)
        using (var gatewayActivity = ActivitySource.StartActivity("Gateway.ForwardRequest", ActivityKind.Server))
        {
            gatewayActivity?.SetTag("http.route", "/api/orders");
            gatewayActivity?.SetTag("http.method", "POST");
            gatewayActivity?.AddBaggage("tenant.id", tenantId);
            gatewayActivity?.AddEvent(new ActivityEvent("GatewayRoutingStarted"));

            await Task.Delay(5);

            // Inject trace context into outbound HTTP request headers
            if (gatewayActivity != null)
            {
                InjectTraceContext(gatewayActivity, httpCarrier);
            }
        }

        // Hop 2: Order Service receives HTTP request
        var parentHttpCtx = ExtractTraceContext(httpCarrier, out var orderBaggage);
        using (var orderActivity = ActivitySource.StartActivity(
            "OrderService.CreateOrder",
            ActivityKind.Server,
            parentContext: parentHttpCtx))
        {
            orderActivity?.SetTag("order.id", orderId);
            foreach (var (k, v) in orderBaggage)
            {
                orderActivity?.AddBaggage(k, v);
            }

            orderActivity?.AddEvent(new ActivityEvent("OrderPersistedToDatabase"));
            await Task.Delay(5);

            // Hop 2b: Order Service publishes event to RabbitMQ
            using (var publishActivity = ActivitySource.StartActivity(
                "RabbitMQ.PublishOrderCreated",
                ActivityKind.Producer,
                parentContext: orderActivity?.Context ?? default))
            {
                publishActivity?.SetTag("messaging.system", "rabbitmq");
                publishActivity?.SetTag("messaging.destination", "order-created-exchange");
                if (orderActivity != null)
                {
                    foreach (var (k, v) in orderActivity.Baggage)
                    {
                        publishActivity?.AddBaggage(k, v);
                    }
                }

                if (publishActivity != null)
                {
                    InjectTraceContext(publishActivity, messageQueueCarrier);
                }
            }
        }

        // Hop 3: Payment/Inventory Consumer receives message from RabbitMQ
        var parentMqCtx = ExtractTraceContext(messageQueueCarrier, out var workerBaggage);
        using (var consumerActivity = ActivitySource.StartActivity(
            "InventoryService.ReserveStock",
            ActivityKind.Consumer,
            parentContext: parentMqCtx))
        {
            consumerActivity?.SetTag("messaging.operation", "process");
            consumerActivity?.SetTag("order.id", orderId);
            foreach (var (k, v) in workerBaggage)
            {
                consumerActivity?.AddBaggage(k, v);
            }

            consumerActivity?.AddEvent(new ActivityEvent("StockReservedSuccessfully"));
            await Task.Delay(5);
        }

        return parentHttpCtx.TraceId.ToHexString();
    }

    public void Dispose()
    {
        _listener.Dispose();
    }
}
