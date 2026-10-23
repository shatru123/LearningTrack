using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Day35.ObservabilityStack;

public record LatencyPercentiles(double P50, double P90, double P95, double P99, int TotalCount, double Mean);

public record HistogramBucket(double UpperBound, int CumulativeCount);

public class ObservabilityMetricsManager : IDisposable
{
    public const string MeterName = "Day35.ObservabilityStack";
    public const string Version = "1.0.0";
    public static readonly Meter Meter = new(MeterName, Version);

    // RED Metrics
    public readonly Counter<long> RequestCounter;
    public readonly Counter<long> ErrorCounter;
    public readonly Histogram<double> RequestDurationHistogram;

    // USE Metrics
    public double SimulatedCpuUtilization { get; set; } = 42.5;
    public long SimulatedThreadPoolQueueLength { get; set; } = 12;
    public readonly Counter<long> HardwareErrorCounter;

    private readonly MeterListener _listener;
    private readonly List<double> _observedDurations = new();
    private long _totalRequests = 0;
    private long _totalErrors = 0;
    private readonly object _lock = new();

    public long TotalRequests => Interlocked.Read(ref _totalRequests);
    public long TotalErrors => Interlocked.Read(ref _totalErrors);
    public IReadOnlyList<double> ObservedDurations
    {
        get
        {
            lock (_lock) return _observedDurations.ToList();
        }
    }

    public ObservabilityMetricsManager()
    {
        RequestCounter = Meter.CreateCounter<long>("http.server.requests.count", "requests", "Total incoming HTTP requests");
        ErrorCounter = Meter.CreateCounter<long>("http.server.requests.errors", "errors", "Total HTTP requests resulting in 5xx errors");
        RequestDurationHistogram = Meter.CreateHistogram<double>("http.server.request.duration.ms", "ms", "HTTP request processing duration in milliseconds");

        Meter.CreateObservableGauge("system.cpu.utilization", () => SimulatedCpuUtilization, "%", "Simulated CPU Utilization");
        Meter.CreateObservableGauge("threadpool.queue.saturation", () => SimulatedThreadPoolQueueLength, "items", "ThreadPool queued work items");
        HardwareErrorCounter = Meter.CreateCounter<long>("system.hardware.errors", "errors", "Hardware / disk / network drop errors");

        _listener = new MeterListener
        {
            InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == MeterName)
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            }
        };

        _listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) =>
        {
            if (instrument.Name == "http.server.requests.count")
            {
                Interlocked.Add(ref _totalRequests, measurement);
            }
            else if (instrument.Name == "http.server.requests.errors")
            {
                Interlocked.Add(ref _totalErrors, measurement);
            }
        });

        _listener.SetMeasurementEventCallback<double>((instrument, measurement, tags, state) =>
        {
            if (instrument.Name == "http.server.request.duration.ms")
            {
                lock (_lock)
                {
                    _observedDurations.Add(measurement);
                }
            }
        });

        _listener.Start();
    }

    public void RecordRequest(double durationMs, bool isError = false, string route = "/api/v1/resource", int statusCode = 200)
    {
        var tagList = new TagList
        {
            { "http.route", route },
            { "http.status_code", statusCode }
        };

        RequestCounter.Add(1, tagList);
        RequestDurationHistogram.Record(durationMs, tagList);

        if (isError)
        {
            ErrorCounter.Add(1, tagList);
        }
    }

    /// <summary>
    /// Computes P50, P90, P95, P99 percentiles from a collection of recorded latencies.
    /// Uses the standard Nearest-Rank percentile method.
    /// </summary>
    public static LatencyPercentiles CalculatePercentiles(IEnumerable<double> latencies)
    {
        var list = latencies.OrderBy(x => x).ToList();
        if (list.Count == 0)
        {
            return new LatencyPercentiles(0, 0, 0, 0, 0, 0);
        }

        double p50 = GetPercentile(list, 50);
        double p90 = GetPercentile(list, 90);
        double p95 = GetPercentile(list, 95);
        double p99 = GetPercentile(list, 99);
        double mean = list.Average();

        return new LatencyPercentiles(p50, p90, p95, p99, list.Count, Math.Round(mean, 2));
    }

    private static double GetPercentile(List<double> sorted, double percentile)
    {
        if (sorted.Count == 1) return sorted[0];
        double rank = (percentile / 100.0) * (sorted.Count - 1);
        int lower = (int)Math.Floor(rank);
        int upper = (int)Math.Ceiling(rank);
        if (lower == upper) return sorted[lower];
        double weight = rank - lower;
        return Math.Round(sorted[lower] * (1 - weight) + sorted[upper] * weight, 2);
    }

    /// <summary>
    /// Simulates Prometheus-style cumulative histogram buckets.
    /// Standard Prometheus boundaries: 5ms, 10ms, 25ms, 50ms, 100ms, 250ms, 500ms, 1000ms, +Inf.
    /// </summary>
    public static List<HistogramBucket> BuildPrometheusBuckets(IEnumerable<double> latencies, double[]? boundaries = null)
    {
        boundaries ??= [5.0, 10.0, 25.0, 50.0, 100.0, 250.0, 500.0, 1000.0, double.PositiveInfinity];
        var list = latencies.ToList();
        var buckets = new List<HistogramBucket>();

        foreach (var boundary in boundaries)
        {
            int count = list.Count(v => v <= boundary);
            buckets.Add(new HistogramBucket(boundary, count));
        }

        return buckets;
    }

    /// <summary>
    /// Correlates telemetry by generating structured log payloads with trace_id and span_id.
    /// </summary>
    public static string FormatStructuredLog(string message, string traceId, string spanId, string logLevel = "Information")
    {
        return $"{{\"timestamp\":\"{DateTime.UtcNow:O}\",\"level\":\"{logLevel}\",\"trace_id\":\"{traceId}\",\"span_id\":\"{spanId}\",\"message\":\"{message}\"}}";
    }

    public void Dispose()
    {
        _listener.Dispose();
    }
}
