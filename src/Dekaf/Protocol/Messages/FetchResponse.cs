using Dekaf.Protocol.Records;

namespace Dekaf.Protocol.Messages;

/// <summary>
/// Fetch response (API key 1).
/// Contains records fetched from topic partitions.
/// </summary>
public sealed class FetchResponse : IKafkaResponse
{
    public static ApiKey ApiKey => ApiKey.Fetch;
    public static short LowestSupportedVersion => 0;
    public static short HighestSupportedVersion => 16;

    /// <summary>
    /// Throttle time in milliseconds.
    /// </summary>
    public int ThrottleTimeMs { get; init; }

    /// <summary>
    /// Top-level error code.
    /// </summary>
    public ErrorCode ErrorCode { get; init; }

    /// <summary>
    /// Fetch session ID.
    /// </summary>
    public int SessionId { get; init; }

    /// <summary>
    /// Responses per topic.
    /// </summary>
    public required IReadOnlyList<FetchResponseTopic> Responses { get; init; }

    public static IKafkaResponse Read(ref KafkaProtocolReader reader, short version)
    {
        var isFlexible = version >= 12;

        var throttleTimeMs = version >= 1 ? reader.ReadInt32() : 0;
        var errorCode = version >= 7 ? (ErrorCode)reader.ReadInt16() : ErrorCode.None;
        var sessionId = version >= 7 ? reader.ReadInt32() : 0;

        var responses = isFlexible
            ? reader.ReadCompactArray((ref KafkaProtocolReader r) => FetchResponseTopic.Read(ref r, version))
            : reader.ReadArray((ref KafkaProtocolReader r) => FetchResponseTopic.Read(ref r, version));

        if (isFlexible)
        {
            reader.SkipTaggedFields();
        }

        return new FetchResponse
        {
            ThrottleTimeMs = throttleTimeMs,
            ErrorCode = errorCode,
            SessionId = sessionId,
            Responses = responses
        };
    }
}

/// <summary>
/// Topic response in a fetch response.
/// </summary>
public sealed class FetchResponseTopic
{
    /// <summary>
    /// Topic name (v0-v12).
    /// </summary>
    public string? Topic { get; init; }

    /// <summary>
    /// Topic ID (v13+).
    /// </summary>
    public Guid TopicId { get; init; }

    /// <summary>
    /// Partition responses.
    /// </summary>
    public required IReadOnlyList<FetchResponsePartition> Partitions { get; init; }

    public static FetchResponseTopic Read(ref KafkaProtocolReader reader, short version)
    {
        var isFlexible = version >= 12;

        Guid topicId = Guid.Empty;
        string? topic = null;

        if (version >= 13)
        {
            topicId = reader.ReadUuid();
        }
        else
        {
            topic = isFlexible ? reader.ReadCompactString() : reader.ReadString();
        }

        var partitions = isFlexible
            ? reader.ReadCompactArray((ref KafkaProtocolReader r) => FetchResponsePartition.Read(ref r, version))
            : reader.ReadArray((ref KafkaProtocolReader r) => FetchResponsePartition.Read(ref r, version));

        if (isFlexible)
        {
            reader.SkipTaggedFields();
        }

        return new FetchResponseTopic
        {
            Topic = topic,
            TopicId = topicId,
            Partitions = partitions
        };
    }
}

/// <summary>
/// Partition response in a fetch response.
/// </summary>
public sealed class FetchResponsePartition
{
    /// <summary>
    /// Partition index.
    /// </summary>
    public required int PartitionIndex { get; init; }

    /// <summary>
    /// Error code.
    /// </summary>
    public required ErrorCode ErrorCode { get; init; }

    /// <summary>
    /// High watermark.
    /// </summary>
    public required long HighWatermark { get; init; }

    /// <summary>
    /// Last stable offset (for transactions).
    /// </summary>
    public long LastStableOffset { get; init; } = -1;

    /// <summary>
    /// Log start offset.
    /// </summary>
    public long LogStartOffset { get; init; } = -1;

    /// <summary>
    /// Diverging epoch (v12+).
    /// </summary>
    public EpochEndOffset? DivergingEpoch { get; init; }

    /// <summary>
    /// Current leader (v12+).
    /// </summary>
    public LeaderIdAndEpoch? CurrentLeader { get; init; }

    /// <summary>
    /// Snapshot ID (v12+).
    /// </summary>
    public SnapshotId? SnapshotId { get; init; }

    /// <summary>
    /// Aborted transactions (for read committed isolation).
    /// </summary>
    public IReadOnlyList<AbortedTransaction>? AbortedTransactions { get; init; }

    /// <summary>
    /// Preferred read replica.
    /// </summary>
    public int PreferredReadReplica { get; init; } = -1;

    /// <summary>
    /// Record batches.
    /// </summary>
    public IReadOnlyList<RecordBatch>? Records { get; init; }

    public static FetchResponsePartition Read(ref KafkaProtocolReader reader, short version)
    {
        var isFlexible = version >= 12;

        var partitionIndex = reader.ReadInt32();
        var errorCode = (ErrorCode)reader.ReadInt16();
        var highWatermark = reader.ReadInt64();

        var lastStableOffset = version >= 4 ? reader.ReadInt64() : -1;
        var logStartOffset = version >= 5 ? reader.ReadInt64() : -1;

        EpochEndOffset? divergingEpoch = null;
        LeaderIdAndEpoch? currentLeader = null;
        SnapshotId? snapshotId = null;

        if (version >= 12)
        {
            // These are in tagged fields for flexible versions
        }

        IReadOnlyList<AbortedTransaction>? abortedTransactions = null;
        if (version >= 4)
        {
            abortedTransactions = isFlexible
                ? reader.ReadCompactArray((ref KafkaProtocolReader r) => AbortedTransaction.Read(ref r, version))
                : reader.ReadArray((ref KafkaProtocolReader r) => AbortedTransaction.Read(ref r, version));
        }

        var preferredReadReplica = version >= 11 ? reader.ReadInt32() : -1;

        // Read record batches
        // Note: COMPACT_RECORDS uses plain varint length (not length-1 like COMPACT_BYTES)
        var recordsLength = isFlexible
            ? reader.ReadUnsignedVarInt()
            : reader.ReadInt32();

        List<RecordBatch>? records = null;
        if (recordsLength > 0)
        {
            records = [];
            var recordsEndPosition = reader.Consumed + recordsLength;

            while (reader.Consumed < recordsEndPosition && !reader.End)
            {
                try
                {
                    records.Add(RecordBatch.Read(ref reader));
                }
                catch
                {
                    // Partial batch at end, skip remaining
                    break;
                }
            }

            // Skip any remaining bytes
            var remaining = (int)(recordsEndPosition - reader.Consumed);
            if (remaining > 0)
            {
                reader.Skip(remaining);
            }
        }

        if (isFlexible)
        {
            reader.SkipTaggedFields();
        }

        return new FetchResponsePartition
        {
            PartitionIndex = partitionIndex,
            ErrorCode = errorCode,
            HighWatermark = highWatermark,
            LastStableOffset = lastStableOffset,
            LogStartOffset = logStartOffset,
            DivergingEpoch = divergingEpoch,
            CurrentLeader = currentLeader,
            SnapshotId = snapshotId,
            AbortedTransactions = abortedTransactions,
            PreferredReadReplica = preferredReadReplica,
            Records = records
        };
    }
}

/// <summary>
/// Aborted transaction information.
/// </summary>
public sealed class AbortedTransaction
{
    public required long ProducerId { get; init; }
    public required long FirstOffset { get; init; }

    public static AbortedTransaction Read(ref KafkaProtocolReader reader, short version)
    {
        var producerId = reader.ReadInt64();
        var firstOffset = reader.ReadInt64();

        if (version >= 12)
        {
            reader.SkipTaggedFields();
        }

        return new AbortedTransaction
        {
            ProducerId = producerId,
            FirstOffset = firstOffset
        };
    }
}

/// <summary>
/// Epoch end offset for diverging epoch.
/// </summary>
public sealed class EpochEndOffset
{
    public int Epoch { get; init; } = -1;
    public long EndOffset { get; init; } = -1;
}

/// <summary>
/// Leader ID and epoch.
/// </summary>
public sealed class LeaderIdAndEpoch
{
    public int LeaderId { get; init; } = -1;
    public int LeaderEpoch { get; init; } = -1;
}

/// <summary>
/// Snapshot ID.
/// </summary>
public sealed class SnapshotId
{
    public long EndOffset { get; init; } = -1;
    public int Epoch { get; init; } = -1;
}
