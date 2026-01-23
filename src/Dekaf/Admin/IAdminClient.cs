using Dekaf.Metadata;
using Dekaf.Producer;

namespace Dekaf.Admin;

/// <summary>
/// Interface for Kafka administrative operations.
/// </summary>
public interface IAdminClient : IAsyncDisposable
{
    /// <summary>
    /// Creates topics.
    /// </summary>
    ValueTask CreateTopicsAsync(IEnumerable<NewTopic> topics, CreateTopicsOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes topics.
    /// </summary>
    ValueTask DeleteTopicsAsync(IEnumerable<string> topicNames, DeleteTopicsOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists topics.
    /// </summary>
    ValueTask<IReadOnlyList<TopicListing>> ListTopicsAsync(ListTopicsOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Describes topics.
    /// </summary>
    ValueTask<IReadOnlyDictionary<string, TopicDescription>> DescribeTopicsAsync(IEnumerable<string> topicNames, CancellationToken cancellationToken = default);

    /// <summary>
    /// Describes the cluster.
    /// </summary>
    ValueTask<ClusterDescription> DescribeClusterAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Describes consumer groups.
    /// </summary>
    ValueTask<IReadOnlyDictionary<string, GroupDescription>> DescribeConsumerGroupsAsync(IEnumerable<string> groupIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists consumer groups.
    /// </summary>
    ValueTask<IReadOnlyList<GroupListing>> ListConsumerGroupsAsync(ListConsumerGroupsOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes consumer groups.
    /// </summary>
    ValueTask DeleteConsumerGroupsAsync(IEnumerable<string> groupIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists consumer group offsets.
    /// </summary>
    ValueTask<IReadOnlyDictionary<TopicPartition, long>> ListConsumerGroupOffsetsAsync(string groupId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Alters consumer group offsets.
    /// </summary>
    ValueTask AlterConsumerGroupOffsetsAsync(string groupId, IEnumerable<TopicPartitionOffset> offsets, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes records up to the specified offset.
    /// </summary>
    ValueTask<IReadOnlyDictionary<TopicPartition, long>> DeleteRecordsAsync(IReadOnlyDictionary<TopicPartition, long> offsets, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates partitions for existing topics.
    /// </summary>
    ValueTask CreatePartitionsAsync(IReadOnlyDictionary<string, int> newPartitionCounts, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists offsets for the specified topic partitions.
    /// </summary>
    /// <param name="specs">The topic partition offset specifications.</param>
    /// <param name="options">Optional settings for the request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A dictionary mapping topic partitions to their offset information.</returns>
    ValueTask<IReadOnlyDictionary<TopicPartition, ListOffsetsResultInfo>> ListOffsetsAsync(
        IEnumerable<TopicPartitionOffsetSpec> specs,
        ListOffsetsOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Triggers leader election for partitions.
    /// </summary>
    /// <param name="electionType">The type of election (Preferred or Unclean).</param>
    /// <param name="partitions">Partitions to elect leaders for. If null, all partitions are elected.</param>
    /// <param name="options">Optional settings for the request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Results for each partition.</returns>
    ValueTask<IReadOnlyList<ElectLeadersResultInfo>> ElectLeadersAsync(
        ElectionType electionType,
        IEnumerable<TopicPartition>? partitions = null,
        ElectLeadersOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the cluster metadata.
    /// </summary>
    ClusterMetadata Metadata { get; }
}

/// <summary>
/// Specification for a new topic.
/// </summary>
public sealed class NewTopic
{
    public required string Name { get; init; }
    public int NumPartitions { get; init; } = 1;
    public short ReplicationFactor { get; init; } = 1;
    public IReadOnlyDictionary<string, string>? Configs { get; init; }
    public IReadOnlyDictionary<int, IReadOnlyList<int>>? ReplicaAssignments { get; init; }
}

/// <summary>
/// Options for CreateTopics.
/// </summary>
public sealed class CreateTopicsOptions
{
    public int TimeoutMs { get; init; } = 30000;
    public bool ValidateOnly { get; init; }
}

/// <summary>
/// Options for DeleteTopics.
/// </summary>
public sealed class DeleteTopicsOptions
{
    public int TimeoutMs { get; init; } = 30000;
}

/// <summary>
/// Options for ListTopics.
/// </summary>
public sealed class ListTopicsOptions
{
    public bool ListInternal { get; init; }
    public int TimeoutMs { get; init; } = 30000;
}

/// <summary>
/// Options for ListConsumerGroups.
/// </summary>
public sealed class ListConsumerGroupsOptions
{
    public IReadOnlyList<string>? States { get; init; }
    public int TimeoutMs { get; init; } = 30000;
}

/// <summary>
/// Topic listing information.
/// </summary>
public sealed class TopicListing
{
    public required string Name { get; init; }
    public Guid TopicId { get; init; }
    public bool IsInternal { get; init; }
}

/// <summary>
/// Detailed topic description.
/// </summary>
public sealed class TopicDescription
{
    public required string Name { get; init; }
    public Guid TopicId { get; init; }
    public bool IsInternal { get; init; }
    public required IReadOnlyList<PartitionInfo> Partitions { get; init; }
}

/// <summary>
/// Cluster description.
/// </summary>
public sealed class ClusterDescription
{
    public string? ClusterId { get; init; }
    public int ControllerId { get; init; }
    public required IReadOnlyList<BrokerNode> Nodes { get; init; }
}

/// <summary>
/// Consumer group listing.
/// </summary>
public sealed class GroupListing
{
    public required string GroupId { get; init; }
    public string? ProtocolType { get; init; }
    public string? State { get; init; }
}

/// <summary>
/// Consumer group description.
/// </summary>
public sealed class GroupDescription
{
    public required string GroupId { get; init; }
    public string? ProtocolType { get; init; }
    public string? ProtocolData { get; init; }
    public string State { get; init; } = "Unknown";
    public required IReadOnlyList<MemberDescription> Members { get; init; }
    public int? CoordinatorId { get; init; }
}

/// <summary>
/// Consumer group member description.
/// </summary>
public sealed class MemberDescription
{
    public required string MemberId { get; init; }
    public string? GroupInstanceId { get; init; }
    public string? ClientId { get; init; }
    public string? ClientHost { get; init; }
    public IReadOnlyList<TopicPartition>? Assignment { get; init; }
}

/// <summary>
/// Specifies a topic partition and the offset spec for ListOffsets.
/// </summary>
public sealed class TopicPartitionOffsetSpec
{
    /// <summary>
    /// The topic partition.
    /// </summary>
    public required TopicPartition TopicPartition { get; init; }

    /// <summary>
    /// The offset specification.
    /// </summary>
    public required OffsetSpec Spec { get; init; }

    /// <summary>
    /// Timestamp to query when Spec is Timestamp.
    /// </summary>
    public long? Timestamp { get; init; }
}

/// <summary>
/// Specification for which offset to retrieve.
/// </summary>
public enum OffsetSpec
{
    /// <summary>
    /// The earliest available offset.
    /// </summary>
    Earliest,

    /// <summary>
    /// The latest available offset (end of log).
    /// </summary>
    Latest,

    /// <summary>
    /// The offset of the record with the maximum timestamp.
    /// </summary>
    MaxTimestamp,

    /// <summary>
    /// Query by timestamp. Use with TopicPartitionOffsetSpec.Timestamp.
    /// </summary>
    Timestamp
}

/// <summary>
/// Result of a ListOffsets query for a single partition.
/// </summary>
public sealed class ListOffsetsResultInfo
{
    /// <summary>
    /// The offset.
    /// </summary>
    public required long Offset { get; init; }

    /// <summary>
    /// The timestamp associated with the offset (-1 if not available).
    /// </summary>
    public long Timestamp { get; init; } = -1;

    /// <summary>
    /// The leader epoch, or null if not available.
    /// </summary>
    public int? LeaderEpoch { get; init; }
}

/// <summary>
/// Options for ListOffsets.
/// </summary>
public sealed class ListOffsetsOptions
{
    /// <summary>
    /// Timeout in milliseconds.
    /// </summary>
    public int TimeoutMs { get; init; } = 30000;

    /// <summary>
    /// Isolation level for the request.
    /// </summary>
    public Protocol.Messages.IsolationLevel IsolationLevel { get; init; } = Protocol.Messages.IsolationLevel.ReadUncommitted;
}

/// <summary>
/// Type of leader election.
/// </summary>
public enum ElectionType : byte
{
    /// <summary>
    /// Elect the preferred replica as leader if possible.
    /// </summary>
    Preferred = 0,

    /// <summary>
    /// Elect an unclean leader (may cause data loss).
    /// </summary>
    Unclean = 1
}

/// <summary>
/// Options for ElectLeaders.
/// </summary>
public sealed class ElectLeadersOptions
{
    /// <summary>
    /// Timeout in milliseconds.
    /// </summary>
    public int TimeoutMs { get; init; } = 30000;
}

/// <summary>
/// Result of ElectLeaders for a single partition.
/// </summary>
public sealed class ElectLeadersResultInfo
{
    /// <summary>
    /// The topic partition.
    /// </summary>
    public required TopicPartition TopicPartition { get; init; }

    /// <summary>
    /// Error code for this partition (None if successful).
    /// </summary>
    public Protocol.ErrorCode ErrorCode { get; init; }

    /// <summary>
    /// Error message, if any.
    /// </summary>
    public string? ErrorMessage { get; init; }
}
