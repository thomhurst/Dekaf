using Dekaf.Serialization;

namespace Dekaf.Consumer.DeadLetter;

/// <summary>
/// Builds DLQ metadata headers for dead-lettered messages.
/// </summary>
public static class DeadLetterHeaders
{
    /// <summary>Header key for the source topic name.</summary>
    public const string SourceTopicKey = "dlq.source.topic";
    /// <summary>Header key for the source partition.</summary>
    public const string SourcePartitionKey = "dlq.source.partition";
    /// <summary>Header key for the source offset.</summary>
    public const string SourceOffsetKey = "dlq.source.offset";
    /// <summary>Header key for the exception message.</summary>
    public const string ErrorMessageKey = "dlq.error.message";
    /// <summary>Header key for the exception type name.</summary>
    public const string ErrorTypeKey = "dlq.error.type";
    /// <summary>Header key for the failure count.</summary>
    public const string FailureCountKey = "dlq.failure.count";
    /// <summary>Header key for the DLQ routing timestamp.</summary>
    public const string TimestampKey = "dlq.timestamp";

    /// <summary>
    /// Builds DLQ headers containing source metadata, error details, and original headers.
    /// </summary>
    /// <typeparam name="TKey">The message key type.</typeparam>
    /// <typeparam name="TValue">The message value type.</typeparam>
    /// <param name="result">The consume result being dead-lettered.</param>
    /// <param name="exception">The exception that caused the failure.</param>
    /// <param name="failureCount">The number of processing failures.</param>
    /// <param name="includeException">Whether to include exception details in headers.</param>
    /// <returns>A Headers collection with original headers preserved and DLQ metadata appended.</returns>
    public static Headers Build<TKey, TValue>(
        ConsumeResult<TKey, TValue> result,
        Exception exception,
        int failureCount,
        bool includeException)
    {
        var originalCount = result.Headers?.Count ?? 0;
        var dlqHeaderCount = includeException ? 7 : 5;
        var headers = new Headers(originalCount + dlqHeaderCount);

        // Preserve original headers first
        if (result.Headers is not null)
        {
            foreach (var header in result.Headers)
            {
                headers.Add(header);
            }
        }

        // Source metadata
        var sourceTopic = RetryTopicHeaders.GetSourceTopic(result.Headers) ?? result.Topic;
        var sourcePartition = RetryTopicHeaders.GetSourcePartition(result.Headers) ?? result.Partition;
        var sourceOffset = RetryTopicHeaders.GetSourceOffset(result.Headers) ?? result.Offset;
        headers.Add(SourceTopicKey, sourceTopic);
        headers.Add(SourcePartitionKey, DeadLetterHeaderFormatting.FormatInt(sourcePartition));
        headers.Add(SourceOffsetKey, DeadLetterHeaderFormatting.FormatLong(sourceOffset));
        headers.Add(FailureCountKey, DeadLetterHeaderFormatting.FormatInt(failureCount));
        headers.Add(TimestampKey, DateTimeOffset.UtcNow.ToString("O"));

        // Error details (optional)
        if (includeException)
        {
            headers.Add(ErrorMessageKey, exception.Message);
            headers.Add(ErrorTypeKey, exception.GetType().Name);
        }

        return headers;
    }
}
