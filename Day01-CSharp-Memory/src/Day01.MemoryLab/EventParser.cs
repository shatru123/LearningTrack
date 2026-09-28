namespace Day01.MemoryLab;

public record EventRecord(int EventId, string Name, string Venue, decimal Price, string Status);

public interface IEventParser
{
    EventRecord Parse(string rawData);
}

/// <summary>
/// Version 1: Traditional String Parsing APIs.
/// Causes high heap allocations due to string.Split, Substring, and string array creations.
/// </summary>
public class TraditionalStringEventParser : IEventParser
{
    public EventRecord Parse(string rawData)
    {
        if (string.IsNullOrWhiteSpace(rawData))
            throw new ArgumentException("Input data cannot be null or empty.", nameof(rawData));

        int eventId = 0;
        string name = string.Empty;
        string venue = string.Empty;
        decimal price = 0m;
        string status = string.Empty;

        // Allocates string[] and multiple string objects for segments
        string[] pairs = rawData.Split('|');
        foreach (string pair in pairs)
        {
            // Allocates another string[] and two substring instances
            string[] kv = pair.Split('=');
            if (kv.Length != 2) continue;

            string key = kv[0].Trim();
            string value = kv[1].Trim();

            switch (key)
            {
                case "EventId":
                    eventId = int.Parse(value);
                    break;
                case "Name":
                    name = value;
                    break;
                case "Venue":
                    venue = value;
                    break;
                case "Price":
                    price = decimal.Parse(value);
                    break;
                case "Status":
                    status = value;
                    break;
            }
        }

        return new EventRecord(eventId, name, venue, price, status);
    }
}

/// <summary>
/// Version 2: Span&lt;char&gt; Parser.
/// Demonstrates mutable stack-allocated or sliceable character memory without intermediate substring allocations.
/// </summary>
public class SpanEventParser
{
    public static EventRecord Parse(Span<char> span)
    {
        if (span.IsEmpty)
            throw new ArgumentException("Input span cannot be empty.", nameof(span));

        int eventId = 0;
        string name = string.Empty;
        string venue = string.Empty;
        decimal price = 0m;
        string status = string.Empty;

        Span<char> remaining = span;

        while (!remaining.IsEmpty)
        {
            int delimiterIndex = remaining.IndexOf('|');
            Span<char> token;

            if (delimiterIndex >= 0)
            {
                token = remaining.Slice(0, delimiterIndex);
                remaining = remaining.Slice(delimiterIndex + 1);
            }
            else
            {
                token = remaining;
                remaining = Span<char>.Empty;
            }

            int equalIndex = token.IndexOf('=');
            if (equalIndex < 0) continue;

            Span<char> keySpan = token.Slice(0, equalIndex).Trim();
            Span<char> valueSpan = token.Slice(equalIndex + 1).Trim();

            if (keySpan.SequenceEqual("EventId".AsSpan()))
            {
                eventId = int.Parse(valueSpan);
            }
            else if (keySpan.SequenceEqual("Name".AsSpan()))
            {
                name = valueSpan.ToString();
            }
            else if (keySpan.SequenceEqual("Venue".AsSpan()))
            {
                venue = valueSpan.ToString();
            }
            else if (keySpan.SequenceEqual("Price".AsSpan()))
            {
                price = decimal.Parse(valueSpan);
            }
            else if (keySpan.SequenceEqual("Status".AsSpan()))
            {
                status = valueSpan.ToString();
            }
        }

        return new EventRecord(eventId, name, venue, price, status);
    }
}

/// <summary>
/// Version 3: ReadOnlySpan&lt;char&gt; Parser.
/// Demonstrates zero-copy immutable slicing directly over string memory or unmanaged buffers.
/// </summary>
public class ReadOnlySpanEventParser : IEventParser
{
    public EventRecord Parse(string rawData)
    {
        return Parse(rawData.AsSpan());
    }

    public static EventRecord Parse(ReadOnlySpan<char> span)
    {
        if (span.IsEmpty)
            throw new ArgumentException("Input span cannot be empty.", nameof(span));

        int eventId = 0;
        string name = string.Empty;
        string venue = string.Empty;
        decimal price = 0m;
        string status = string.Empty;

        ReadOnlySpan<char> remaining = span;

        while (!remaining.IsEmpty)
        {
            int delimiterIndex = remaining.IndexOf('|');
            ReadOnlySpan<char> token;

            if (delimiterIndex >= 0)
            {
                token = remaining.Slice(0, delimiterIndex);
                remaining = remaining.Slice(delimiterIndex + 1);
            }
            else
            {
                token = remaining;
                remaining = ReadOnlySpan<char>.Empty;
            }

            int equalIndex = token.IndexOf('=');
            if (equalIndex < 0) continue;

            ReadOnlySpan<char> keySpan = token.Slice(0, equalIndex).Trim();
            ReadOnlySpan<char> valueSpan = token.Slice(equalIndex + 1).Trim();

            if (keySpan.SequenceEqual("EventId"))
            {
                eventId = int.Parse(valueSpan);
            }
            else if (keySpan.SequenceEqual("Name"))
            {
                name = valueSpan.ToString();
            }
            else if (keySpan.SequenceEqual("Venue"))
            {
                venue = valueSpan.ToString();
            }
            else if (keySpan.SequenceEqual("Price"))
            {
                price = decimal.Parse(valueSpan);
            }
            else if (keySpan.SequenceEqual("Status"))
            {
                status = valueSpan.ToString();
            }
        }

        return new EventRecord(eventId, name, venue, price, status);
    }
}

/// <summary>
/// Allocation measurement utility to demonstrate memory impact in production.
/// </summary>
public static class AllocationTracker
{
    public static (T Result, long AllocatedBytes) Measure<T>(Func<T> action)
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        long startBytes = GC.GetAllocatedBytesForCurrentThread();
        T result = action();
        long endBytes = GC.GetAllocatedBytesForCurrentThread();

        return (result, Math.Max(0, endBytes - startBytes));
    }
}
