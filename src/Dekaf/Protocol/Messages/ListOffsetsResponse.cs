using System.Buffers;

namespace Dekaf.Protocol.Messages;

/// <summary>
/// ListOffsets response (API key 2).
/// </summary>
public sealed class ListOffsetsResponse : IKafkaResponse
{
    public static ApiKey ApiKey => ApiKey.ListOffsets;
    public static short LowestSupportedVersion => 0;
    public static short HighestSupportedVersion => 8;

    /// <summary>
    /// Throttle time in milliseconds (v2+).
    /// </summary>
    public int ThrottleTimeMs { get; init; }

    /// <summary>
    /// Topics with partition offset information.
    /// </summary>
    public required IReadOnlyList<ListOffsetsResponseTopic> Topics { get; init; }

    public static IKafkaResponse Read(ref KafkaProtocolReader reader, short version)
    {
        var isFlexible = version >= 6;

        var throttleTimeMs = version >= 2 ? reader.ReadInt32() : 0;

        IReadOnlyList<ListOffsetsResponseTopic> topics;
        if (isFlexible)
        {
            topics = reader.ReadCompactArray((ref KafkaProtocolReader r) =>
                ListOffsetsResponseTopic.Read(ref r, version));
            reader.SkipTaggedFields();
        }
        else
        {
            topics = reader.ReadArray((ref KafkaProtocolReader r) =>
                ListOffsetsResponseTopic.Read(ref r, version));
        }

        return new ListOffsetsResponse
        {
            ThrottleTimeMs = throttleTimeMs,
            Topics = topics
        };
    }
}

/// <summary>
/// Topic in a ListOffsets response.
/// </summary>
public sealed class ListOffsetsResponseTopic
{
    public required string Name { get; init; }
    public required IReadOnlyList<ListOffsetsResponsePartition> Partitions { get; init; }

    public static ListOffsetsResponseTopic Read(ref KafkaProtocolReader reader, short version)
    {
        var isFlexible = version >= 6;

        var name = isFlexible ? reader.ReadCompactString()! : reader.ReadString()!;

        IReadOnlyList<ListOffsetsResponsePartition> partitions;
        if (isFlexible)
        {
            partitions = reader.ReadCompactArray((ref KafkaProtocolReader r) =>
                ListOffsetsResponsePartition.Read(ref r, version));
            reader.SkipTaggedFields();
        }
        else
        {
            partitions = reader.ReadArray((ref KafkaProtocolReader r) =>
                ListOffsetsResponsePartition.Read(ref r, version));
        }

        return new ListOffsetsResponseTopic
        {
            Name = name,
            Partitions = partitions
        };
    }
}

/// <summary>
/// Partition in a ListOffsets response.
/// </summary>
public sealed class ListOffsetsResponsePartition
{
    public required int PartitionIndex { get; init; }
    public required ErrorCode ErrorCode { get; init; }

    /// <summary>
    /// Timestamps returned (v0 only, deprecated).
    /// </summary>
    public IReadOnlyList<long>? OldStyleOffsets { get; init; }

    /// <summary>
    /// The timestamp associated with the offset (v1+).
    /// </summary>
    public long Timestamp { get; init; }

    /// <summary>
    /// The returned offset (v1+).
    /// </summary>
    public long Offset { get; init; }

    /// <summary>
    /// Leader epoch (v4+).
    /// </summary>
    public int LeaderEpoch { get; init; } = -1;

    public static ListOffsetsResponsePartition Read(ref KafkaProtocolReader reader, short version)
    {
        var isFlexible = version >= 6;

        var partitionIndex = reader.ReadInt32();
        var errorCode = (ErrorCode)reader.ReadInt16();

        IReadOnlyList<long>? oldStyleOffsets = null;
        long timestamp = -1;
        long offset = -1;
        var leaderEpoch = -1;

        if (version == 0)
        {
            // v0 has array of offsets
            oldStyleOffsets = reader.ReadArray((ref KafkaProtocolReader r) => r.ReadInt64());
            if (oldStyleOffsets.Count > 0)
            {
                offset = oldStyleOffsets[0];
            }
        }
        else
        {
            timestamp = reader.ReadInt64();
            offset = reader.ReadInt64();

            if (version >= 4)
            {
                leaderEpoch = reader.ReadInt32();
            }
        }

        if (isFlexible)
        {
            reader.SkipTaggedFields();
        }

        return new ListOffsetsResponsePartition
        {
            PartitionIndex = partitionIndex,
            ErrorCode = errorCode,
            OldStyleOffsets = oldStyleOffsets,
            Timestamp = timestamp,
            Offset = offset,
            LeaderEpoch = leaderEpoch
        };
    }
}
