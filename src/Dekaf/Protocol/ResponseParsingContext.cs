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
/// 4. Reset is called automatically when the returned <see cref="ParsingScope"/> is disposed
///
/// Memory ownership is NOT transferred to individual batches. Instead, the caller
/// (KafkaConnection) transfers ownership to PendingFetchData which disposes it
/// when all records have been consumed.
/// </remarks>
internal static class ResponseParsingContext
{
    [ThreadStatic]
    private static ParsingContextState? t_state;

    private sealed class ParsingContextState
    {
        public IPooledMemory? PooledMemory;
        public bool MemoryUsed;
    }

    /// <summary>
    /// Sets the pooled memory for the current parsing operation and returns a scope
    /// that automatically calls <see cref="Reset"/> on disposal.
    /// Must be called before parsing a FetchResponse. Use with <c>using</c>:
    /// <code>using var scope = ResponseParsingContext.SetPooledMemory(memory);</code>
    /// </summary>
    public static ParsingScope SetPooledMemory(IPooledMemory memory)
    {
        var state = t_state ??= new ParsingContextState();

        // Unconditionally overwrite: handles both fresh state and stale state
        // left behind by a previous call that did not reset (e.g., an exception
        // path that bypassed the scope disposal).
        state.PooledMemory = memory;
        state.MemoryUsed = false;
        return new ParsingScope();
    }

    /// <summary>
    /// Returns true if pooled memory is available for zero-copy parsing.
    /// </summary>
    public static bool HasPooledMemory => t_state?.PooledMemory is not null;

    /// <summary>
    /// Marks the pooled memory as being used by at least one batch.
    /// This signals to the caller that ownership should be transferred.
    /// </summary>
    public static void MarkMemoryUsed()
    {
        var state = t_state;
        if (state?.PooledMemory is not null)
        {
            state.MemoryUsed = true;
        }
    }

    /// <summary>
    /// Takes ownership of the pooled memory. Called once after parsing completes
    /// to transfer ownership to PendingFetchData.
    /// Returns null if no memory was set or it wasn't used.
    /// </summary>
    public static IPooledMemory? TakePooledMemory()
    {
        var state = t_state;
        if (state is null || !state.MemoryUsed || state.PooledMemory is null)
            return null;

        return state.PooledMemory;
    }

    /// <summary>
    /// Returns true if the pooled memory was used during parsing.
    /// If true, the caller should transfer ownership to PendingFetchData.
    /// </summary>
    public static bool WasMemoryUsed => t_state?.MemoryUsed ?? false;

    /// <summary>
    /// Resets the context after parsing completes.
    /// Prefer using the <see cref="ParsingScope"/> returned by <see cref="SetPooledMemory"/>
    /// instead of calling this directly.
    /// </summary>
    public static void Reset()
    {
        var state = t_state;
        if (state is not null)
        {
            state.PooledMemory = null;
            state.MemoryUsed = false;
        }
    }

    /// <summary>
    /// RAII scope that ensures <see cref="Reset"/> is called when disposed.
    /// Returned by <see cref="SetPooledMemory"/> to guarantee cleanup even if
    /// an exception occurs during parsing.
    /// </summary>
    public readonly struct ParsingScope : IDisposable
    {
        public void Dispose() => Reset();
    }
}
