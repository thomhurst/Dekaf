namespace Dekaf.Protocol;

/// <summary>
/// Represents a reference to pooled memory that must be disposed when no longer needed.
/// This enables zero-copy parsing where the parsed data references the original pooled buffer
/// instead of copying to a new allocation.
/// </summary>
/// <remarks>
/// Used by FetchResponse/RecordBatch to avoid copying record data from the network buffer.
/// The memory remains valid until Dispose() is called.
/// </remarks>
internal interface IPooledMemory : IDisposable
{
    /// <summary>
    /// Gets the pooled memory. Valid until Dispose() is called.
    /// </summary>
    ReadOnlyMemory<byte> Memory { get; }
}
