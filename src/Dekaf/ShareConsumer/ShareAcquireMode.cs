namespace Dekaf.ShareConsumer;

/// <summary>
/// Controls how a share consumer acquires records for each ShareFetch request.
/// Values match the Kafka ShareFetch v2 wire protocol.
/// </summary>
public enum ShareAcquireMode : sbyte
{
    /// <summary>
    /// Optimize broker-side acquisition around producer batch boundaries.
    /// Equivalent to Kafka's share.acquire.mode=batch_optimized.
    /// </summary>
    BatchOptimized = 0,

    /// <summary>
    /// Strictly limit acquired records to MaxPollRecords.
    /// Equivalent to Kafka's share.acquire.mode=record_limit.
    /// </summary>
    RecordLimit = 1
}
