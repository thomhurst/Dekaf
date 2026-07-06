using System.Globalization;
using Dekaf.Serialization;

namespace Dekaf.Consumer.DeadLetter;

/// <summary>
/// Builds and reads retry-topic metadata headers.
/// </summary>
public static class RetryTopicHeaders
{
    /// <summary>Header key for the original source topic.</summary>
    public const string SourceTopicKey = "retry.source.topic";
    /// <summary>Header key for the original source partition.</summary>
    public const string SourcePartitionKey = "retry.source.partition";
    /// <summary>Header key for the original source offset.</summary>
    public const string SourceOffsetKey = "retry.source.offset";
    /// <summary>Header key for the cumulative processing failure count.</summary>
    public const string FailureCountKey = "retry.failure.count";
    /// <summary>Header key for the retry delay in milliseconds.</summary>
    public const string DelayMsKey = "retry.delay.ms";
    /// <summary>Header key for the UTC due timestamp as Unix milliseconds.</summary>
    public const string DueTimestampMsKey = "retry.due.timestamp.ms";

    /// <summary>
    /// Builds retry-topic headers while preserving non-retry headers from the consumed record.
    /// </summary>
    public static Headers Build<TKey, TValue>(
        ConsumeResult<TKey, TValue> result,
        int failureCount,
        TimeSpan delay,
        DateTimeOffset dueAt)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(failureCount);
        RetryTopicOptions.ValidateDelay(delay);

        var sourceTopic = GetSourceTopic(result.Headers) ?? result.Topic;
        var sourcePartition = GetSourcePartition(result.Headers) ?? result.Partition;
        var sourceOffset = GetSourceOffset(result.Headers) ?? result.Offset;
        var originalCount = result.Headers?.Count ?? 0;
        var headers = new Headers(originalCount + 6);

        if (result.Headers is not null)
        {
            foreach (var header in result.Headers)
            {
                if (!IsRetryHeader(header.Key))
                    headers.Add(header);
            }
        }

        headers.Add(SourceTopicKey, sourceTopic);
        headers.Add(SourcePartitionKey, DeadLetterHeaderFormatting.FormatInt(sourcePartition));
        headers.Add(SourceOffsetKey, DeadLetterHeaderFormatting.FormatLong(sourceOffset));
        headers.Add(FailureCountKey, DeadLetterHeaderFormatting.FormatInt(failureCount));
        headers.Add(DelayMsKey, DeadLetterHeaderFormatting.FormatLong(delay.Ticks / TimeSpan.TicksPerMillisecond));
        headers.Add(DueTimestampMsKey, DeadLetterHeaderFormatting.FormatLong(dueAt.ToUnixTimeMilliseconds()));
        return headers;
    }

    /// <summary>
    /// Gets the cumulative processing failure count from retry headers, or zero when absent.
    /// </summary>
    public static int GetFailureCount(IReadOnlyList<Header>? headers)
        => TryGetInt(headers, FailureCountKey, out var value) && value > 0 ? value : 0;

    /// <summary>
    /// Gets the retry due timestamp from retry headers.
    /// </summary>
    public static bool TryGetDueAt(IReadOnlyList<Header>? headers, out DateTimeOffset dueAt)
    {
        if (TryGetLong(headers, DueTimestampMsKey, out var timestampMs))
        {
            dueAt = DateTimeOffset.FromUnixTimeMilliseconds(timestampMs);
            return true;
        }

        dueAt = default;
        return false;
    }

    /// <summary>
    /// Gets the original source topic from retry headers.
    /// </summary>
    public static string? GetSourceTopic(IReadOnlyList<Header>? headers)
        => GetHeaderValue(headers, SourceTopicKey);

    /// <summary>
    /// Gets the original source partition from retry headers.
    /// </summary>
    public static int? GetSourcePartition(IReadOnlyList<Header>? headers)
        => TryGetInt(headers, SourcePartitionKey, out var value) ? value : null;

    /// <summary>
    /// Gets the original source offset from retry headers.
    /// </summary>
    public static long? GetSourceOffset(IReadOnlyList<Header>? headers)
        => TryGetLong(headers, SourceOffsetKey, out var value) ? value : null;

    private static bool IsRetryHeader(string key)
        => key is SourceTopicKey or SourcePartitionKey or SourceOffsetKey or FailureCountKey or DelayMsKey or DueTimestampMsKey;

    private static string? GetHeaderValue(IReadOnlyList<Header>? headers, string key)
    {
        if (headers is null)
            return null;

        for (var i = 0; i < headers.Count; i++)
        {
            var header = headers[i];
            if (header.Key == key)
                return header.GetValueAsString();
        }

        return null;
    }

    private static bool TryGetInt(IReadOnlyList<Header>? headers, string key, out int value)
        => int.TryParse(GetHeaderValue(headers, key), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);

    private static bool TryGetLong(IReadOnlyList<Header>? headers, string key, out long value)
        => long.TryParse(GetHeaderValue(headers, key), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);

}
