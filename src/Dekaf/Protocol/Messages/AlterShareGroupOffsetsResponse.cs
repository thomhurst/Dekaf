namespace Dekaf.Protocol.Messages;

/// <summary>
/// AlterShareGroupOffsets response (API key 91).
/// Contains the results of altering offsets for a share group (KIP-932).
/// </summary>
public sealed class AlterShareGroupOffsetsResponse : IKafkaResponse
{
    public static ApiKey ApiKey => ApiKey.AlterShareGroupOffsets;
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
    public required IReadOnlyList<AlterShareGroupOffsetsResponseTopic> Responses { get; init; }

    public static IKafkaResponse Read(ref KafkaProtocolReader reader, short version)
    {
        var throttleTimeMs = reader.ReadInt32();
        var errorCode = (ErrorCode)reader.ReadInt16();
        var errorMessage = reader.ReadCompactString();

        var responses = reader.ReadCompactArray(
            static (ref KafkaProtocolReader r) => AlterShareGroupOffsetsResponseTopic.Read(ref r));

        reader.SkipTaggedFields();

        return new AlterShareGroupOffsetsResponse
        {
            ThrottleTimeMs = throttleTimeMs,
            ErrorCode = errorCode,
            ErrorMessage = errorMessage,
            Responses = responses
        };
    }
}

/// <summary>
/// Topic in an AlterShareGroupOffsets response.
/// </summary>
public sealed class AlterShareGroupOffsetsResponseTopic
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
    /// The partition responses.
    /// </summary>
    public required IReadOnlyList<AlterShareGroupOffsetsResponsePartition> Partitions { get; init; }

    public void Write(ref KafkaProtocolWriter writer)
    {
        writer.WriteCompactString(TopicName);
        writer.WriteUuid(TopicId);

        writer.WriteCompactArray(
            Partitions,
            static (ref KafkaProtocolWriter w, AlterShareGroupOffsetsResponsePartition partition) => partition.Write(ref w));

        writer.WriteEmptyTaggedFields();
    }

    public static AlterShareGroupOffsetsResponseTopic Read(ref KafkaProtocolReader reader)
    {
        var topicName = reader.ReadCompactNonNullableString();
        var topicId = reader.ReadUuid();

        var partitions = reader.ReadCompactArray(
            static (ref KafkaProtocolReader r) => AlterShareGroupOffsetsResponsePartition.Read(ref r));

        reader.SkipTaggedFields();

        return new AlterShareGroupOffsetsResponseTopic
        {
            TopicName = topicName,
            TopicId = topicId,
            Partitions = partitions
        };
    }
}

/// <summary>
/// Partition in an AlterShareGroupOffsets response.
/// </summary>
public sealed class AlterShareGroupOffsetsResponsePartition
{
    /// <summary>
    /// The partition index.
    /// </summary>
    public int PartitionIndex { get; init; }

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
        writer.WriteInt32(PartitionIndex);
        writer.WriteInt16((short)ErrorCode);
        writer.WriteCompactNullableString(ErrorMessage);

        writer.WriteEmptyTaggedFields();
    }

    public static AlterShareGroupOffsetsResponsePartition Read(ref KafkaProtocolReader reader)
    {
        var partitionIndex = reader.ReadInt32();
        var errorCode = (ErrorCode)reader.ReadInt16();
        var errorMessage = reader.ReadCompactString();

        reader.SkipTaggedFields();

        return new AlterShareGroupOffsetsResponsePartition
        {
            PartitionIndex = partitionIndex,
            ErrorCode = errorCode,
            ErrorMessage = errorMessage
        };
    }
}
