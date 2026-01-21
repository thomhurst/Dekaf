namespace Dekaf.Protocol.Messages;

/// <summary>
/// DeleteTopics response (API key 20).
/// Contains the results of topic deletion requests.
/// </summary>
public sealed class DeleteTopicsResponse : IKafkaResponse
{
    public static ApiKey ApiKey => ApiKey.DeleteTopics;
    public static short LowestSupportedVersion => 0;
    public static short HighestSupportedVersion => 6;

    /// <summary>
    /// The duration in milliseconds for which the request was throttled due to quota violation
    /// (v1+, zero if not throttled).
    /// </summary>
    public int ThrottleTimeMs { get; init; }

    /// <summary>
    /// Results for each topic.
    /// </summary>
    public required IReadOnlyList<DeleteTopicsResponseTopic> Responses { get; init; }

    public static IKafkaResponse Read(ref KafkaProtocolReader reader, short version)
    {
        var isFlexible = version >= 4;

        var throttleTimeMs = version >= 1 ? reader.ReadInt32() : 0;

        IReadOnlyList<DeleteTopicsResponseTopic> responses;
        if (isFlexible)
        {
            responses = reader.ReadCompactArray(
                (ref KafkaProtocolReader r) => DeleteTopicsResponseTopic.Read(ref r, version)) ?? [];
        }
        else
        {
            responses = reader.ReadArray(
                (ref KafkaProtocolReader r) => DeleteTopicsResponseTopic.Read(ref r, version)) ?? [];
        }

        if (isFlexible)
        {
            reader.SkipTaggedFields();
        }

        return new DeleteTopicsResponse
        {
            ThrottleTimeMs = throttleTimeMs,
            Responses = responses
        };
    }
}

/// <summary>
/// Per-topic response for DeleteTopics.
/// </summary>
public sealed class DeleteTopicsResponseTopic
{
    /// <summary>
    /// The topic name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The topic ID (v6+).
    /// </summary>
    public Guid TopicId { get; init; }

    /// <summary>
    /// The error code, or 0 if there was no error.
    /// </summary>
    public ErrorCode ErrorCode { get; init; }

    /// <summary>
    /// The error message, or null if there was no error (v5+).
    /// </summary>
    public string? ErrorMessage { get; init; }

    public static DeleteTopicsResponseTopic Read(ref KafkaProtocolReader reader, short version)
    {
        var isFlexible = version >= 4;

        var name = isFlexible
            ? reader.ReadCompactString() ?? string.Empty
            : reader.ReadString() ?? string.Empty;

        var topicId = version >= 6 ? reader.ReadUuid() : Guid.Empty;

        var errorCode = (ErrorCode)reader.ReadInt16();

        string? errorMessage = null;
        if (version >= 5)
        {
            errorMessage = isFlexible
                ? reader.ReadCompactString()
                : reader.ReadString();
        }

        if (isFlexible)
        {
            reader.SkipTaggedFields();
        }

        return new DeleteTopicsResponseTopic
        {
            Name = name,
            TopicId = topicId,
            ErrorCode = errorCode,
            ErrorMessage = errorMessage
        };
    }
}
