namespace Dekaf.Protocol.Messages;

/// <summary>
/// OffsetFetch response (API key 9).
/// </summary>
public sealed class OffsetFetchResponse : IKafkaResponse
{
    public static ApiKey ApiKey => ApiKey.OffsetFetch;
    public static short LowestSupportedVersion => 0;
    public static short HighestSupportedVersion => 9;

    /// <summary>
    /// Throttle time in milliseconds.
    /// </summary>
    public int ThrottleTimeMs { get; init; }

    /// <summary>
    /// Topics with offsets (v0-v7).
    /// </summary>
    public IReadOnlyList<OffsetFetchResponseTopic>? Topics { get; init; }

    /// <summary>
    /// Error code (v2-v7).
    /// </summary>
    public ErrorCode ErrorCode { get; init; }

    /// <summary>
    /// Groups (v8+).
    /// </summary>
    public IReadOnlyList<OffsetFetchResponseGroup>? Groups { get; init; }

    public static IKafkaResponse Read(ref KafkaProtocolReader reader, short version)
    {
        var isFlexible = version >= 6;

        var throttleTimeMs = version >= 3 ? reader.ReadInt32() : 0;

        IReadOnlyList<OffsetFetchResponseTopic>? topics = null;
        var errorCode = ErrorCode.None;
        IReadOnlyList<OffsetFetchResponseGroup>? groups = null;

        if (version < 8)
        {
            topics = isFlexible
                ? reader.ReadCompactArray((ref KafkaProtocolReader r) => OffsetFetchResponseTopic.Read(ref r, version))
                : reader.ReadArray((ref KafkaProtocolReader r) => OffsetFetchResponseTopic.Read(ref r, version));

            if (version >= 2)
            {
                errorCode = (ErrorCode)reader.ReadInt16();
            }
        }
        else
        {
            groups = reader.ReadCompactArray((ref KafkaProtocolReader r) => OffsetFetchResponseGroup.Read(ref r, version));
        }

        if (isFlexible)
        {
            reader.SkipTaggedFields();
        }

        return new OffsetFetchResponse
        {
            ThrottleTimeMs = throttleTimeMs,
            Topics = topics,
            ErrorCode = errorCode,
            Groups = groups
        };
    }
}

/// <summary>
/// Topic in an OffsetFetch response.
/// </summary>
public sealed class OffsetFetchResponseTopic
{
    public required string Name { get; init; }
    public required IReadOnlyList<OffsetFetchResponsePartition> Partitions { get; init; }

    public static OffsetFetchResponseTopic Read(ref KafkaProtocolReader reader, short version)
    {
        var isFlexible = version >= 6;

        var name = isFlexible ? reader.ReadCompactNonNullableString() : reader.ReadString() ?? string.Empty;

        var partitions = isFlexible
            ? reader.ReadCompactArray((ref KafkaProtocolReader r) => OffsetFetchResponsePartition.Read(ref r, version))
            : reader.ReadArray((ref KafkaProtocolReader r) => OffsetFetchResponsePartition.Read(ref r, version));

        if (isFlexible)
        {
            reader.SkipTaggedFields();
        }

        return new OffsetFetchResponseTopic
        {
            Name = name,
            Partitions = partitions
        };
    }
}

/// <summary>
/// Partition in an OffsetFetch response.
/// </summary>
public sealed class OffsetFetchResponsePartition
{
    public required int PartitionIndex { get; init; }
    public required long CommittedOffset { get; init; }
    public int CommittedLeaderEpoch { get; init; } = -1;
    public string? Metadata { get; init; }
    public required ErrorCode ErrorCode { get; init; }

    public static OffsetFetchResponsePartition Read(ref KafkaProtocolReader reader, short version)
    {
        var isFlexible = version >= 6;

        var partitionIndex = reader.ReadInt32();
        var committedOffset = reader.ReadInt64();

        var committedLeaderEpoch = version >= 5 ? reader.ReadInt32() : -1;

        var metadata = isFlexible ? reader.ReadCompactString() : reader.ReadString();
        var errorCode = (ErrorCode)reader.ReadInt16();

        if (isFlexible)
        {
            reader.SkipTaggedFields();
        }

        return new OffsetFetchResponsePartition
        {
            PartitionIndex = partitionIndex,
            CommittedOffset = committedOffset,
            CommittedLeaderEpoch = committedLeaderEpoch,
            Metadata = metadata,
            ErrorCode = errorCode
        };
    }
}

/// <summary>
/// Group in an OffsetFetch response (v8+).
/// </summary>
public sealed class OffsetFetchResponseGroup
{
    public required string GroupId { get; init; }
    public required IReadOnlyList<OffsetFetchResponseTopic> Topics { get; init; }
    public required ErrorCode ErrorCode { get; init; }

    public static OffsetFetchResponseGroup Read(ref KafkaProtocolReader reader, short version)
    {
        var groupId = reader.ReadCompactNonNullableString();

        var topics = reader.ReadCompactArray((ref KafkaProtocolReader r) => OffsetFetchResponseTopic.Read(ref r, version));

        var errorCode = (ErrorCode)reader.ReadInt16();

        reader.SkipTaggedFields();

        return new OffsetFetchResponseGroup
        {
            GroupId = groupId,
            Topics = topics,
            ErrorCode = errorCode
        };
    }
}
