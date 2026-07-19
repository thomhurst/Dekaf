using Dekaf.Errors;
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
        // Hoisted: each Headers access on the readonly struct constructs a new lazy wrapper.
        var originalHeaders = result.Headers;
        return Build(
            originalHeaders,
            result.Topic,
            result.Partition,
            result.Offset,
            exception,
            failureCount,
            includeException);
    }

    /// <summary>
    /// Builds DLQ headers directly from a failed deserialization record.
    /// </summary>
    /// <param name="exception">The record deserialization failure.</param>
    /// <param name="failureCount">The number of processing failures.</param>
    /// <param name="includeException">Whether to include exception details in headers.</param>
    /// <returns>A Headers collection with original headers preserved and DLQ metadata appended.</returns>
    public static Headers Build(
        RecordDeserializationException exception,
        int failureCount,
        bool includeException)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return Build(
            exception.Headers,
            exception.TopicPartition.Topic,
            exception.TopicPartition.Partition,
            exception.Offset,
            exception,
            failureCount,
            includeException);
    }

    private static Headers Build(
        IReadOnlyList<Header> originalHeaders,
        string topic,
        int partition,
        long offset,
        Exception exception,
        int failureCount,
        bool includeException)
    {
        var dlqHeaderCount = includeException ? 7 : 5;
        var headers = new Headers(originalHeaders.Count + dlqHeaderCount);

        // Preserve original headers first
        foreach (var header in originalHeaders)
        {
            headers.Add(header);
        }

        // Source metadata
        var sourceTopic = RetryTopicHeaders.GetSourceTopic(originalHeaders) ?? topic;
        var sourcePartition = RetryTopicHeaders.GetSourcePartition(originalHeaders) ?? partition;
        var sourceOffset = RetryTopicHeaders.GetSourceOffset(originalHeaders) ?? offset;
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
