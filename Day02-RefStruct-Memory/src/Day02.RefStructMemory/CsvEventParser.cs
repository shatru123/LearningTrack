namespace Day02.RefStructMemory;

public record EventTicket(int EventId, string Name, decimal Price, string Status);

/// <summary>
/// Parser 1: Traditional string CSV parser.
/// Incurs allocations for lines array, line strings, comma splits, and substrings.
/// </summary>
public class TraditionalCsvParser
{
    public static List<EventTicket> Parse(string csvContent)
    {
        if (string.IsNullOrWhiteSpace(csvContent))
            return new List<EventTicket>();

        List<EventTicket> results = new();
        string[] lines = csvContent.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);

        foreach (string line in lines)
        {
            string[] cols = line.Split(',');
            if (cols.Length != 4) continue;

            int eventId = int.Parse(cols[0].Trim());
            string name = cols[1].Trim();
            decimal price = decimal.Parse(cols[2].Trim());
            string status = cols[3].Trim();

            results.Add(new EventTicket(eventId, name, price, status));
        }

        return results;
    }
}

/// <summary>
/// Parser 2: Span-based CSV parser.
/// Slices ReadOnlySpan&lt;char&gt; for zero intermediate allocations during line and column extraction.
/// </summary>
public class SpanCsvParser
{
    public static List<EventTicket> Parse(ReadOnlySpan<char> csvSpan)
    {
        List<EventTicket> results = new();
        ReadOnlySpan<char> remaining = csvSpan;

        while (!remaining.IsEmpty)
        {
            int lineEndIndex = remaining.IndexOfAny('\r', '\n');
            ReadOnlySpan<char> line;

            if (lineEndIndex >= 0)
            {
                line = remaining.Slice(0, lineEndIndex);
                // Skip newline sequence (\r\n or single \n or \r)
                int skipCount = 1;
                if (lineEndIndex + 1 < remaining.Length && remaining[lineEndIndex] == '\r' && remaining[lineEndIndex + 1] == '\n')
                {
                    skipCount = 2;
                }
                remaining = remaining.Slice(lineEndIndex + skipCount);
            }
            else
            {
                line = remaining;
                remaining = ReadOnlySpan<char>.Empty;
            }

            line = line.Trim();
            if (line.IsEmpty) continue;

            // Slicing columns: 1001,Arsenal Match,1500,Available
            int c1 = line.IndexOf(',');
            if (c1 < 0) continue;
            ReadOnlySpan<char> col1 = line.Slice(0, c1).Trim();
            ReadOnlySpan<char> rest1 = line.Slice(c1 + 1);

            int c2 = rest1.IndexOf(',');
            if (c2 < 0) continue;
            ReadOnlySpan<char> col2 = rest1.Slice(0, c2).Trim();
            ReadOnlySpan<char> rest2 = rest1.Slice(c2 + 1);

            int c3 = rest2.IndexOf(',');
            if (c3 < 0) continue;
            ReadOnlySpan<char> col3 = rest2.Slice(0, c3).Trim();
            ReadOnlySpan<char> col4 = rest2.Slice(c3 + 1).Trim();

            int eventId = int.Parse(col1);
            string name = col2.ToString();
            decimal price = decimal.Parse(col3);
            string status = col4.ToString();

            results.Add(new EventTicket(eventId, name, price, status));
        }

        return results;
    }
}

/// <summary>
/// Parser 3: Asynchronous Memory&lt;T&gt; parser.
/// Demonstrates that ReadOnlyMemory&lt;char&gt; is heap-safe, can cross await boundaries,
/// and can be stored in async state machines without stack corruption.
/// </summary>
public class AsyncMemoryCsvParser
{
    public static async IAsyncEnumerable<EventTicket> ParseStreamAsync(ReadOnlyMemory<char> csvMemory, int simulatedDelayMs = 1)
    {
        ReadOnlyMemory<char> remaining = csvMemory;

        while (!remaining.IsEmpty)
        {
            if (simulatedDelayMs > 0)
            {
                await Task.Delay(simulatedDelayMs).ConfigureAwait(false);
            }

            int newlineIndex = IndexOfNewline(remaining);
            ReadOnlyMemory<char> lineMemory;

            if (newlineIndex >= 0)
            {
                lineMemory = remaining.Slice(0, newlineIndex);
                int skip = IsCrLf(remaining, newlineIndex) ? 2 : 1;
                remaining = remaining.Slice(newlineIndex + skip);
            }
            else
            {
                lineMemory = remaining;
                remaining = ReadOnlyMemory<char>.Empty;
            }

            EventTicket? ticket = ParseSingleLine(lineMemory);
            if (ticket != null)
            {
                yield return ticket;
            }
        }
    }

    private static int IndexOfNewline(ReadOnlyMemory<char> memory)
    {
        ReadOnlySpan<char> span = memory.Span;
        return span.IndexOfAny('\r', '\n');
    }

    private static bool IsCrLf(ReadOnlyMemory<char> memory, int newlineIndex)
    {
        ReadOnlySpan<char> span = memory.Span;
        return newlineIndex + 1 < span.Length && span[newlineIndex] == '\r' && span[newlineIndex + 1] == '\n';
    }

    private static EventTicket? ParseSingleLine(ReadOnlyMemory<char> lineMemory)
    {
        ReadOnlySpan<char> lineSpan = lineMemory.Span.Trim();
        if (lineSpan.IsEmpty) return null;

        int c1 = lineSpan.IndexOf(',');
        if (c1 < 0) return null;
        ReadOnlySpan<char> col1 = lineSpan.Slice(0, c1).Trim();
        ReadOnlySpan<char> rest1 = lineSpan.Slice(c1 + 1);

        int c2 = rest1.IndexOf(',');
        if (c2 < 0) return null;
        ReadOnlySpan<char> col2 = rest1.Slice(0, c2).Trim();
        ReadOnlySpan<char> rest2 = rest1.Slice(c2 + 1);

        int c3 = rest2.IndexOf(',');
        if (c3 < 0) return null;
        ReadOnlySpan<char> col3 = rest2.Slice(0, c3).Trim();
        ReadOnlySpan<char> col4 = rest2.Slice(c3 + 1).Trim();

        int eventId = int.Parse(col1);
        string name = col2.ToString();
        decimal price = decimal.Parse(col3);
        string status = col4.ToString();

        return new EventTicket(eventId, name, price, status);
    }

    /// <summary>
    /// Demonstrates 'in' parameters for read-only pass-by-reference.
    /// Avoids copying large structs/records while guaranteeing immutability.
    /// </summary>
    public static bool IsAffordable(in EventTicket ticket, in decimal maxBudget)
    {
        return ticket.Price <= maxBudget && ticket.Status.Equals("Available", StringComparison.OrdinalIgnoreCase);
    }
}
