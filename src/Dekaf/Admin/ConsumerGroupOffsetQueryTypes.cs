using Dekaf.Protocol;

namespace Dekaf.Admin;

/// <summary>Selects committed offsets for one consumer group.</summary>
public sealed class ListConsumerGroupOffsetsSpec
{
    /// <summary>Partitions to query, null for all committed partitions, or an empty list for none.</summary>
    public IReadOnlyList<TopicPartition>? TopicPartitions { get; init; }
}

/// <summary>Options for querying complete consumer-group checkpoints.</summary>
public sealed class ListConsumerGroupOffsetsOptions
{
    /// <summary>Waits for pending transactional offset commits to commit or abort.</summary>
    public bool RequireStable { get; init; }

    /// <summary>Total operation budget in milliseconds, including stability waits and retries.</summary>
    public int TimeoutMs { get; init; } = 30000;
}

/// <summary>Group-level status and partition results from a consumer-group offset query.</summary>
public sealed class ConsumerGroupOffsetsResult
{
    public required string GroupId { get; init; }
    public ErrorCode ErrorCode { get; init; }
    public required IReadOnlyDictionary<TopicPartition, ConsumerGroupOffsetResult> Offsets { get; init; }

    internal static ConsumerGroupOffsetsResult FromStreamsResult(StreamsGroupOffsetsResult source)
    {
        var offsets = new Dictionary<TopicPartition, ConsumerGroupOffsetResult>(source.Offsets.Count);
        foreach (var (partition, offset) in source.Offsets)
        {
            offsets.Add(partition, new ConsumerGroupOffsetResult
            {
                ErrorCode = offset.ErrorCode,
                Offset = offset.ErrorCode == ErrorCode.None && offset.Offset >= 0
                    ? new TopicPartitionOffset(partition.Topic, partition.Partition, offset.Offset, offset.LeaderEpoch)
                    {
                        Metadata = offset.Metadata
                    }
                    : null
            });
        }
        return new ConsumerGroupOffsetsResult { GroupId = source.GroupId, ErrorCode = source.ErrorCode, Offsets = offsets };
    }
}

/// <summary>A committed checkpoint, absent commit, or partition lookup error.</summary>
public sealed class ConsumerGroupOffsetResult
{
    /// <summary>The complete checkpoint; null when absent or when the lookup failed. Check ErrorCode first.</summary>
    public TopicPartitionOffset? Offset { get; init; }
    public ErrorCode ErrorCode { get; init; }
}
