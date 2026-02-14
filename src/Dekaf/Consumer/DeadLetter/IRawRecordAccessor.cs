namespace Dekaf.Consumer.DeadLetter;

/// <summary>
/// Provides access to raw key/value bytes of the current record being consumed.
/// Used internally by DLQ routing to capture original bytes without eager copying.
/// </summary>
internal interface IRawRecordAccessor
{
    /// <summary>
    /// Gets the raw key and value bytes for the most recently yielded record.
    /// Only valid during the current ProcessAsync scope (before MoveNextAsync advances).
    /// </summary>
    /// <returns>True if raw bytes are available; false if tracking is not enabled or no current record.</returns>
    bool TryGetCurrentRawRecord(out ReadOnlyMemory<byte> rawKey, out ReadOnlyMemory<byte> rawValue);

    /// <summary>
    /// Enables raw record byte tracking. When enabled, the consumer retains references to
    /// the current record's raw key/value bytes from the fetch buffer.
    /// Zero overhead when not enabled.
    /// </summary>
    void EnableRawRecordTracking();
}
