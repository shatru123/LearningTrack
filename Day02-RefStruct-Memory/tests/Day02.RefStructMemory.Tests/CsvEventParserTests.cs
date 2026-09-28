using Xunit;
using Day02.RefStructMemory;

namespace Day02.RefStructMemory.Tests;

public class CsvEventParserTests
{
    private const string SampleCsv = @"1001,Arsenal Match,1500,Available
1002,Chelsea Match,2000,SoldOut
1003,Liverpool Match,1800,Available";

    [Fact]
    public void TraditionalParser_ParsesAllRows_Correctly()
    {
        var records = TraditionalCsvParser.Parse(SampleCsv);

        Assert.Equal(3, records.Count);
        Assert.Equal(1001, records[0].EventId);
        Assert.Equal("Arsenal Match", records[0].Name);
        Assert.Equal(1500m, records[0].Price);
        Assert.Equal("Available", records[0].Status);

        Assert.Equal(1002, records[1].EventId);
        Assert.Equal("Chelsea Match", records[1].Name);
        Assert.Equal("SoldOut", records[1].Status);
    }

    [Fact]
    public void SpanParser_ParsesAllRows_Equivalently()
    {
        var spanRecords = SpanCsvParser.Parse(SampleCsv.AsSpan());
        var traditionalRecords = TraditionalCsvParser.Parse(SampleCsv);

        Assert.Equal(traditionalRecords.Count, spanRecords.Count);
        for (int i = 0; i < traditionalRecords.Count; i++)
        {
            Assert.Equal(traditionalRecords[i], spanRecords[i]);
        }
    }

    [Fact]
    public async Task AsyncMemoryParser_ParsesStreamAcrossAwaitBoundaries_Correctly()
    {
        var streamResults = new List<EventTicket>();
        await foreach (var ticket in AsyncMemoryCsvParser.ParseStreamAsync(SampleCsv.AsMemory(), simulatedDelayMs: 2))
        {
            streamResults.Add(ticket);
        }

        Assert.Equal(3, streamResults.Count);
        Assert.Equal(1003, streamResults[2].EventId);
        Assert.Equal("Liverpool Match", streamResults[2].Name);
    }

    [Fact]
    public void InParameter_ValidatesAffordability_WithoutCopying()
    {
        var affordable = new EventTicket(1001, "Arsenal Match", 1500m, "Available");
        var expensive = new EventTicket(1002, "VIP Box", 5000m, "Available");
        var soldOut = new EventTicket(1003, "Liverpool Match", 1800m, "SoldOut");

        decimal budget = 2000m;

        Assert.True(AsyncMemoryCsvParser.IsAffordable(in affordable, in budget));
        Assert.False(AsyncMemoryCsvParser.IsAffordable(in expensive, in budget));
        Assert.False(AsyncMemoryCsvParser.IsAffordable(in soldOut, in budget));
    }

    [Fact]
    public void HeapSafeEventBatch_HoldsMemoryOnHeap_Successfully()
    {
        var batch = new HeapSafeEventBatch(SampleCsv.AsMemory());
        Assert.Equal(SampleCsv.Length, batch.RawPayload.Length);
        Assert.Equal('1', batch.AsSpan()[0]);
    }
}
