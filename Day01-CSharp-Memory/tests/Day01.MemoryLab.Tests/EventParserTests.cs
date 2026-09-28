using Xunit;
using Day01.MemoryLab;

namespace Day01.MemoryLab.Tests;

public class EventParserTests
{
    private const string SampleRawData = "EventId=1001|Name=Arsenal Match|Venue=Emirates Stadium|Price=1500|Status=Available";

    [Fact]
    public void TraditionalStringParser_ParsesValidData_Correctly()
    {
        var parser = new TraditionalStringEventParser();
        var record = parser.Parse(SampleRawData);

        Assert.Equal(1001, record.EventId);
        Assert.Equal("Arsenal Match", record.Name);
        Assert.Equal("Emirates Stadium", record.Venue);
        Assert.Equal(1500m, record.Price);
        Assert.Equal("Available", record.Status);
    }

    [Fact]
    public void SpanParser_ParsesValidData_Correctly()
    {
        Span<char> span = stackalloc char[SampleRawData.Length];
        SampleRawData.AsSpan().CopyTo(span);

        var record = SpanEventParser.Parse(span);

        Assert.Equal(1001, record.EventId);
        Assert.Equal("Arsenal Match", record.Name);
        Assert.Equal("Emirates Stadium", record.Venue);
        Assert.Equal(1500m, record.Price);
        Assert.Equal("Available", record.Status);
    }

    [Fact]
    public void ReadOnlySpanParser_ParsesValidData_Correctly()
    {
        var parser = new ReadOnlySpanEventParser();
        var record = parser.Parse(SampleRawData);

        Assert.Equal(1001, record.EventId);
        Assert.Equal("Arsenal Match", record.Name);
        Assert.Equal("Emirates Stadium", record.Venue);
        Assert.Equal(1500m, record.Price);
        Assert.Equal("Available", record.Status);
    }

    [Fact]
    public void Parsers_ProduceEquivalentResults()
    {
        var traditional = new TraditionalStringEventParser().Parse(SampleRawData);
        var readOnlySpan = new ReadOnlySpanEventParser().Parse(SampleRawData);

        Assert.Equal(traditional, readOnlySpan);
    }

    [Fact]
    public void MemoryModel_DemonstratesNuancedValueTypePlacement()
    {
        var (boxed, container, arrayOnHeap, closure) = MemoryModelInspector.DemonstrateValueTypeLocations();

        // 1. Boxed struct is a reference on heap
        Assert.True(boxed.GetType().IsValueType);
        Assert.False(boxed.GetType().IsClass);

        // 2. Struct inside class lives within the class heap object
        Assert.Equal(51.5549, container.Coordinates.X);

        // 3. Array of structs lives on the heap
        Assert.Equal(2, arrayOnHeap.Length);

        // 4. Closure executes correctly with captured struct field
        Assert.Equal(51.5549 * 2, closure());
    }
}
