namespace Dekaf.Protocol;

/// <summary>
/// Thread-local context for passing pooled memory ownership through response parsing.
/// This allows zero-copy parsing where RecordBatch can reference the original network buffer
/// instead of copying the data.
/// </summary>
/// <remarks>
/// Usage pattern:
/// 1. Caller sets the pooled memory via SetPooledMemory() before parsing
/// 2. RecordBatch.Read() takes ownership via TakePooledMemory()
/// 3. After parsing, caller checks WasMemoryTaken to determine if it should dispose
/// 4. Always call Reset() after parsing completes
/// </remarks>
internal static class ResponseParsingContext
{
    [ThreadStatic]
    private static IPooledMemory? t_pooledMemory;

    [ThreadStatic]
    private static bool t_memoryTaken;

    /// <summary>
    /// Sets the pooled memory for the current parsing operation.
    /// Must be called before parsing a FetchResponse.
    /// </summary>
    public static void SetPooledMemory(IPooledMemory memory)
    {
        t_pooledMemory = memory;
        t_memoryTaken = false;
    }

    /// <summary>
    /// Takes ownership of the pooled memory. Should only be called once per parsing operation.
    /// Returns null if no memory was set or it was already taken.
    /// </summary>
    public static IPooledMemory? TakePooledMemory()
    {
        if (t_memoryTaken || t_pooledMemory is null)
            return null;

        t_memoryTaken = true;
        return t_pooledMemory;
    }

    /// <summary>
    /// Gets the current pooled memory without taking ownership.
    /// Used by Record parsing to create slices from the original buffer.
    /// </summary>
    public static IPooledMemory? CurrentMemory => t_pooledMemory;

    /// <summary>
    /// Returns true if the pooled memory was taken during parsing.
    /// If true, the caller should NOT dispose the original buffer.
    /// </summary>
    public static bool WasMemoryTaken => t_memoryTaken;

    /// <summary>
    /// Resets the context after parsing completes.
    /// Always call this in a finally block.
    /// </summary>
    public static void Reset()
    {
        t_pooledMemory = null;
        t_memoryTaken = false;
    }
}
