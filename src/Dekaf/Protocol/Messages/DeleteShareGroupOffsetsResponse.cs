namespace Dekaf.Protocol.Messages;

/// <summary>
/// DeleteShareGroupOffsets response (API key 92).
/// Contains the results of deleting offsets for a share group (KIP-932).
/// </summary>
public sealed class DeleteShareGroupOffsetsResponse : IKafkaResponse
{
    public static ApiKey ApiKey => ApiKey.DeleteShareGroupOffsets;
    public static short LowestSupportedVersion => 0;
    public static short HighestSupportedVersion => 0;

    /// <summary>
    /// The duration in milliseconds for which the request was throttled due to quota violation
    /// (zero if not throttled).
    /// </summary>
    public int ThrottleTimeMs { get; init; }

    /// <summary>
    /// The top-level error code, or 0 if there was no error.
    /// </summary>
    public ErrorCode ErrorCode { get; init; }

    /// <summary>
    /// The top-level error message, or null if there was no error.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// The topic responses.
    /// </summary>
    public required IReadOnlyList<DeleteShareGroupOffsetsResponseTopic> Responses { get; init; }

    public static IKafkaResponse Read(ref KafkaProtocolReader reader, short version)
    {
        var throttleTimeMs = reader.ReadInt32();
        var errorCode = (ErrorCode)reader.ReadInt16();
        var errorMessage = reader.ReadCompactString();

        var responses = reader.ReadCompactArray(
            static (ref KafkaProtocolReader r) => DeleteShareGroupOffsetsResponseTopic.Read(ref r));

        reader.SkipTaggedFields();

        return new DeleteShareGroupOffsetsResponse
        {
            ThrottleTimeMs = throttleTimeMs,
            ErrorCode = errorCode,
            ErrorMessage = errorMessage,
            Responses = responses
        };
    }
}

/// <summary>
/// Topic in a DeleteShareGroupOffsets response.
/// </summary>
public sealed class DeleteShareGroupOffsetsResponseTopic
{
    /// <summary>
    /// The topic name.
    /// </summary>
    public required string TopicName { get; init; }

    /// <summary>
    /// The topic ID (UUID).
    /// </summary>
    public Guid TopicId { get; init; }

    /// <summary>
    /// The error code, or 0 if there was no error.
    /// </summary>
    public ErrorCode ErrorCode { get; init; }

    /// <summary>
    /// The error message, or null if there was no error.
    /// </summary>
    public string? ErrorMessage { get; init; }

    public void Write(ref KafkaProtocolWriter writer)
    {
        writer.WriteCompactString(TopicName);
        writer.WriteUuid(TopicId);
        writer.WriteInt16((short)ErrorCode);
        writer.WriteCompactNullableString(ErrorMessage);

        writer.WriteEmptyTaggedFields();
    }

    public static DeleteShareGroupOffsetsResponseTopic Read(ref KafkaProtocolReader reader)
    {
        var topicName = reader.ReadCompactNonNullableString();
        var topicId = reader.ReadUuid();
        var errorCode = (ErrorCode)reader.ReadInt16();
        var errorMessage = reader.ReadCompactString();

        reader.SkipTaggedFields();

        return new DeleteShareGroupOffsetsResponseTopic
        {
            TopicName = topicName,
            TopicId = topicId,
            ErrorCode = errorCode,
            ErrorMessage = errorMessage
        };
    }
}
