namespace Day02.RefStructMemory;

/// <summary>
/// Custom ref struct demonstrating stack-only semantics.
/// Useful for high-performance stack-only state machines or zero-allocation lexers.
/// </summary>
public ref struct StackOnlyEventBuffer
{
    private ReadOnlySpan<char> _buffer;
    public int Length => _buffer.Length;

    public StackOnlyEventBuffer(ReadOnlySpan<char> buffer)
    {
        _buffer = buffer;
    }

    public ReadOnlySpan<char> Peek(int length)
    {
        return _buffer.Slice(0, Math.Min(length, _buffer.Length));
    }

    // COMPILER RESTRICTIONS ON ref struct:
    // 1. Cannot implement interfaces (pre-C# 13).
    // 2. Cannot be boxed: `object obj = this;` -> CS4013
    // 3. Cannot be a field of a non-ref struct or class: `class Host { StackOnlyEventBuffer buf; }` -> CS8345
    // 4. Cannot be an array element: `StackOnlyEventBuffer[] arr;` -> CS8345
    // 5. Cannot be used inside async methods across await boundaries or iterators (yield return).
}

/// <summary>
/// MemoryHolder demonstrates how Memory&lt;T&gt; resolves ref struct limitations.
/// Because Memory&lt;T&gt; is a standard value type (not a ref struct), it can safely live on the heap.
/// </summary>
public class HeapSafeEventBatch
{
    // Storing memory as a class field is fully allowed for Memory<T>, but impossible for Span<T>!
    public ReadOnlyMemory<char> RawPayload { get; }
    public DateTime ReceivedAtUtc { get; }

    public HeapSafeEventBatch(ReadOnlyMemory<char> rawPayload)
    {
        RawPayload = rawPayload;
        ReceivedAtUtc = DateTime.UtcNow;
    }

    public ReadOnlySpan<char> AsSpan() => RawPayload.Span;
}
