namespace Dekaf.ShareConsumer;

/// <summary>
/// Reports the broker outcome for one topic-partition in a completed share-consumer
/// acknowledgement commit.
/// </summary>
public readonly struct ShareAcknowledgementCommitResult
{
    internal ShareAcknowledgementCommitResult(
        TopicPartition topicPartition,
        ShareAcknowledgedOffsets offsets,
        Exception? exception)
    {
        TopicPartition = topicPartition;
        Offsets = offsets;
        Exception = exception;
    }

    /// <summary>
    /// Gets the topic-partition whose acknowledgements completed.
    /// </summary>
    public TopicPartition TopicPartition { get; }

    /// <summary>
    /// Gets an allocation-free view of the acknowledged offsets in ascending order.
    /// </summary>
    public ShareAcknowledgedOffsets Offsets { get; }

    /// <summary>
    /// Gets the final exception after broker retries, or <see langword="null"/> when all
    /// acknowledgements for this topic-partition succeeded.
    /// </summary>
    public Exception? Exception { get; }

    /// <summary>
    /// Gets whether all acknowledgements for this topic-partition succeeded.
    /// </summary>
    public bool Succeeded => Exception is null;
}
