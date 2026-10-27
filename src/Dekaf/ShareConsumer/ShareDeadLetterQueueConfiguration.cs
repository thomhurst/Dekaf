namespace Dekaf.ShareConsumer;

/// <summary>Group-level broker dead-letter queue configuration for share groups (KIP-1191).</summary>
internal static class ShareDeadLetterQueueConfiguration
{
    internal const string TopicNameConfig = "errors.deadletterqueue.topic.name";
    internal const string CopyRecordConfig = "errors.deadletterqueue.copy.record.enable";

    private const int MaxTopicNameLength = 249;

    internal static void Validate(ShareConsumerOptions options)
    {
        if (options.DeadLetterQueueTopic is { } topicName)
            ValidateTopicName(topicName, nameof(options));
    }

    /// <summary>Applies Kafka's topic-name rules and the KIP-1191 ban on internal ("__") names.</summary>
    internal static void ValidateTopicName(string? topicName, string paramName)
    {
        if (topicName is not { Length: > 0 })
            throw new ArgumentException("The dead-letter queue topic name must not be empty.", paramName);
        if (topicName is "." or ".." || topicName.Length > MaxTopicNameLength)
            throw new ArgumentException($"'{topicName}' is not a valid topic name.", paramName);
        if (topicName.StartsWith("__", StringComparison.Ordinal))
            throw new ArgumentException($"The dead-letter queue topic name '{topicName}' must not start with \"__\".", paramName);
        foreach (var c in topicName)
        {
            if (c is not ((>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or (>= '0' and <= '9') or '.' or '_' or '-'))
                throw new ArgumentException($"'{topicName}' is not a valid topic name. Use only ASCII letters, digits, '.', '_', and '-'.", paramName);
        }
    }
}
