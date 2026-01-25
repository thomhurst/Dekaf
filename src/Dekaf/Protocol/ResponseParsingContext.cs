namespace Dekaf.Protocol;

/// <summary>
/// Thread-local context for passing pooled memory through response parsing.
/// This allows zero-copy parsing where ALL RecordBatches can reference the original network buffer
/// instead of copying the data.
/// </summary>
/// <remarks>
/// Usage pattern:
/// 1. Caller sets the pooled memory via SetPooledMemory() before parsing
/// 2. RecordBatch.Read() checks HasPooledMemory and uses zero-copy if available
/// 3. After parsing, caller checks WasMemoryUsed to determine if it should transfer ownership
/// 4. Always call Reset() after parsing completes
///
/// Memory ownership is NOT transferred to individual batches. Instead, the caller
/// (KafkaConnection) transfers ownership to PendingFetchData which disposes it
/// when all records have been consumed.
/// </remarks>
internal static class ResponseParsingContext
{
    [ThreadStatic]
    private static IPooledMemory? t_pooledMemory;

    [ThreadStatic]
    private static bool t_memoryUsed;

    /// <summary>
    /// Sets the pooled memory for the current parsing operation.
    /// Must be called before parsing a FetchResponse.
    /// </summary>
    public static void SetPooledMemory(IPooledMemory memory)
    {
        t_pooledMemory = memory;
        t_memoryUsed = false;
    }

    /// <summary>
    /// Returns true if pooled memory is available for zero-copy parsing.
    /// </summary>
    public static bool HasPooledMemory => t_pooledMemory is not null;

    /// <summary>
    /// Marks the pooled memory as being used by at least one batch.
    /// This signals to the caller that ownership should be transferred.
    /// </summary>
    public static void MarkMemoryUsed()
    {
        if (t_pooledMemory is not null)
        {
            t_memoryUsed = true;
        }
    }

    /// <summary>
    /// Takes ownership of the pooled memory. Called once after parsing completes
    /// to transfer ownership to PendingFetchData.
    /// Returns null if no memory was set or it wasn't used.
    /// </summary>
    public static IPooledMemory? TakePooledMemory()
    {
        if (!t_memoryUsed || t_pooledMemory is null)
            return null;

        return t_pooledMemory;
    }

    /// <summary>
    /// Returns true if the pooled memory was used during parsing.
    /// If true, the caller should transfer ownership to PendingFetchData.
    /// </summary>
    public static bool WasMemoryUsed => t_memoryUsed;

    /// <summary>
    /// Resets the context after parsing completes.
    /// Always call this in a finally block.
    /// </summary>
    public static void Reset()
    {
        t_pooledMemory = null;
        t_memoryUsed = false;
    }
}
