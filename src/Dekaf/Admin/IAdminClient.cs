using Dekaf.Metadata;
using Dekaf.Producer;
using Dekaf.Telemetry;

namespace Dekaf.Admin;

/// <summary>
/// Interface for Kafka administrative operations.
/// </summary>
public interface IAdminClient : IAsyncDisposable
{
    /// <summary>
    /// Registers or replaces an application metric for broker telemetry subscriptions.
    /// </summary>
    /// <param name="metric">The application metric to register.</param>
    void RegisterMetricForSubscription(ApplicationTelemetryMetric metric);

    /// <summary>
    /// Unregisters an application metric from broker telemetry subscriptions.
    /// Missing names are ignored.
    /// </summary>
    /// <param name="name">The application metric name.</param>
    void UnregisterMetricFromSubscription(string name);

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
    /// Describes topic partitions using the paginated DescribeTopicPartitions API.
    /// Automatically follows broker cursors until all pages are read.
    /// </summary>
    ValueTask<IReadOnlyDictionary<string, TopicDescription>> DescribeTopicPartitionsAsync(
        IEnumerable<string> topicNames,
        DescribeTopicPartitionsOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Describes a single page of topic partitions using the DescribeTopicPartitions API.
    /// </summary>
    ValueTask<DescribeTopicPartitionsPage> DescribeTopicPartitionsPageAsync(
        IEnumerable<string> topicNames,
        DescribeTopicPartitionsPageOptions? options = null,
        CancellationToken cancellationToken = default);

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
    /// Alters or cancels partition reassignments.
    /// </summary>
    /// <param name="reassignments">
    /// The partition reassignments to alter. An empty optional value, null implicit value,
    /// or empty target replica list cancels an in-progress reassignment.
    /// </param>
    /// <param name="options">Options for the request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask AlterPartitionReassignmentsAsync(
        IReadOnlyDictionary<TopicPartition, Optional<NewPartitionReassignment>> reassignments,
        AlterPartitionReassignmentsOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists in-progress partition reassignments.
    /// </summary>
    /// <param name="partitions">The partitions to list, or null to list all in-progress reassignments.</param>
    /// <param name="options">Options for the request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask<IReadOnlyDictionary<TopicPartition, PartitionReassignment>> ListPartitionReassignmentsAsync(
        IEnumerable<TopicPartition>? partitions = null,
        ListPartitionReassignmentsOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Describes SCRAM credentials for one or more users.
    /// </summary>
    /// <param name="users">The users to describe, or null to describe all users.</param>
    /// <param name="options">Options for the request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A dictionary mapping user names to their SCRAM credential information.</returns>
    ValueTask<IReadOnlyDictionary<string, IReadOnlyList<ScramCredentialInfo>>> DescribeUserScramCredentialsAsync(
        IEnumerable<string>? users = null,
        DescribeUserScramCredentialsOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Alters SCRAM credentials for users.
    /// </summary>
    /// <param name="alterations">The credential alterations to perform.</param>
    /// <param name="options">Options for the request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask AlterUserScramCredentialsAsync(
        IEnumerable<UserScramCredentialAlteration> alterations,
        AlterUserScramCredentialsOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Describes configurations for the specified resources.
    /// </summary>
    /// <param name="resources">The resources to describe configurations for.</param>
    /// <param name="options">Optional configuration options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A dictionary mapping each resource to its configuration entries.</returns>
    ValueTask<IReadOnlyDictionary<ConfigResource, IReadOnlyList<ConfigEntry>>> DescribeConfigsAsync(
        IEnumerable<ConfigResource> resources,
        DescribeConfigsOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Alters configurations for the specified resources.
    /// This replaces the entire configuration for each resource.
    /// Consider using IncrementalAlterConfigsAsync for partial updates.
    /// </summary>
    /// <param name="configs">The configurations to set for each resource.</param>
    /// <param name="options">Optional configuration options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask AlterConfigsAsync(
        IReadOnlyDictionary<ConfigResource, IReadOnlyList<ConfigEntry>> configs,
        AlterConfigsOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Incrementally alters configurations for the specified resources.
    /// This allows setting, deleting, appending to, or subtracting from individual configuration values.
    /// </summary>
    /// <param name="configs">The configuration alterations for each resource.</param>
    /// <param name="options">Optional configuration options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask IncrementalAlterConfigsAsync(
        IReadOnlyDictionary<ConfigResource, IReadOnlyList<ConfigAlter>> configs,
        IncrementalAlterConfigsOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates ACL bindings.
    /// </summary>
    /// <param name="aclBindings">The ACL bindings to create.</param>
    /// <param name="options">Optional configuration options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask CreateAclsAsync(
        IEnumerable<AclBinding> aclBindings,
        CreateAclsOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes ACL bindings matching the filters.
    /// </summary>
    /// <param name="filters">The ACL binding filters to match for deletion.</param>
    /// <param name="options">Optional configuration options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The ACL bindings that were deleted.</returns>
    ValueTask<IReadOnlyList<AclBinding>> DeleteAclsAsync(
        IEnumerable<AclBindingFilter> filters,
        DeleteAclsOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Describes ACL bindings matching the filter.
    /// </summary>
    /// <param name="filter">The ACL binding filter to match.</param>
    /// <param name="options">Optional configuration options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The ACL bindings that match the filter.</returns>
    ValueTask<IReadOnlyList<AclBinding>> DescribeAclsAsync(
        AclBindingFilter filter,
        DescribeAclsOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes committed offsets for specific partitions in a consumer group.
    /// The group must not be actively consuming from the specified partitions.
    /// </summary>
    /// <param name="groupId">The consumer group ID.</param>
    /// <param name="partitions">The partitions to delete offsets for.</param>
    /// <param name="options">Optional configuration options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask DeleteConsumerGroupOffsetsAsync(
        string groupId,
        IEnumerable<TopicPartition> partitions,
        DeleteConsumerGroupOffsetsOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists offsets for partitions based on timestamp or special offset.
    /// </summary>
    /// <param name="specs">The offset specifications for each partition.</param>
    /// <param name="options">Optional configuration options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A dictionary mapping each partition to its offset information.</returns>
    ValueTask<IReadOnlyDictionary<TopicPartition, ListOffsetsResultInfo>> ListOffsetsAsync(
        IEnumerable<TopicPartitionOffsetSpec> specs,
        ListOffsetsOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Triggers leader election for specified partitions.
    /// </summary>
    /// <param name="electionType">The type of election to perform.</param>
    /// <param name="partitions">The partitions to elect leaders for, or null for all partitions.</param>
    /// <param name="options">Optional configuration options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Results of the leader election for each partition.</returns>
    ValueTask<IReadOnlyDictionary<TopicPartition, ElectLeadersResultInfo>> ElectLeadersAsync(
        ElectionType electionType,
        IEnumerable<TopicPartition>? partitions = null,
        ElectLeadersOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Describes log directories on the specified brokers.
    /// </summary>
    /// <param name="brokerIds">The broker IDs to query.</param>
    /// <param name="partitions">Optional partition filter, or null to describe all replicas on each broker.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A dictionary mapping broker ID to log directory path to directory description.</returns>
    ValueTask<IReadOnlyDictionary<int, IReadOnlyDictionary<string, LogDirDescription>>> DescribeLogDirsAsync(
        IEnumerable<int> brokerIds,
        IEnumerable<TopicPartition>? partitions = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Alters replica log directories on their assigned brokers.
    /// </summary>
    /// <param name="replicaAssignments">Replica-to-log-directory assignments.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Results for each requested replica.</returns>
    ValueTask<IReadOnlyDictionary<TopicPartitionReplica, AlterReplicaLogDirResultInfo>> AlterReplicaLogDirsAsync(
        IReadOnlyDictionary<TopicPartitionReplica, string> replicaAssignments,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Describes share groups.
    /// </summary>
    ValueTask<IReadOnlyDictionary<string, ShareGroupDescription>> DescribeShareGroupsAsync(
        IEnumerable<string> groupIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists share groups across the cluster.
    /// </summary>
    ValueTask<IReadOnlyList<GroupListing>> ListShareGroupsAsync(
        ListShareGroupsOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Describes share group offsets.
    /// </summary>
    /// <param name="groupId">The share group ID.</param>
    /// <param name="partitions">The partitions to describe offsets for, or null for all.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask<IReadOnlyList<ShareGroupOffsetDescription>> DescribeShareGroupOffsetsAsync(
        string groupId,
        IEnumerable<TopicPartition>? partitions = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Alters share group offsets.
    /// </summary>
    /// <param name="groupId">The share group ID.</param>
    /// <param name="offsets">The offsets to set.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask AlterShareGroupOffsetsAsync(
        string groupId,
        IEnumerable<ShareGroupOffsetAlteration> offsets,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes share group offsets for the specified topics.
    /// </summary>
    /// <param name="groupId">The share group ID.</param>
    /// <param name="topics">The topic names to delete offsets for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask DeleteShareGroupOffsetsAsync(
        string groupId,
        IEnumerable<string> topics,
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

    /// <summary>
    /// The error for this topic, or <see cref="Protocol.ErrorCode.None"/> if it was described
    /// successfully. A batch describe reports per-topic failures here (for example
    /// <see cref="Protocol.ErrorCode.TopicAuthorizationFailed"/>) rather than failing the whole call.
    /// </summary>
    public Protocol.ErrorCode ErrorCode { get; init; }

    /// <summary>
    /// 32-bit bitfield representing authorized operations for this topic. This is populated by
    /// DescribeTopicPartitions APIs; APIs that do not return authorized operations leave the default
    /// <see cref="int.MinValue"/> sentinel.
    /// </summary>
    public int TopicAuthorizedOperations { get; init; } = int.MinValue;
}

/// <summary>
/// Options for auto-paginated DescribeTopicPartitions.
/// </summary>
public sealed class DescribeTopicPartitionsOptions
{
    public int ResponsePartitionLimit { get; init; } = 2000;
}

/// <summary>
/// Options for a single DescribeTopicPartitions page.
/// </summary>
public sealed class DescribeTopicPartitionsPageOptions
{
    public int ResponsePartitionLimit { get; init; } = 2000;
    public DescribeTopicPartitionsCursor? Cursor { get; init; }
}

/// <summary>
/// A pagination cursor returned by DescribeTopicPartitions.
/// </summary>
public sealed class DescribeTopicPartitionsCursor
{
    public required string TopicName { get; init; }
    public int PartitionIndex { get; init; }
}

/// <summary>
/// A single DescribeTopicPartitions response page.
/// </summary>
public sealed class DescribeTopicPartitionsPage
{
    public int ThrottleTimeMs { get; init; }
    public required IReadOnlyDictionary<string, TopicDescription> Topics { get; init; }
    public DescribeTopicPartitionsCursor? NextCursor { get; init; }
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

    /// <summary>
    /// Selected protocol data from the classic DescribeGroups API. For KIP-848 consumer
    /// groups this mirrors <see cref="AssignorName"/> for compatibility.
    /// </summary>
    public string? ProtocolData { get; init; }
    public string State { get; init; } = "Unknown";
    public required IReadOnlyList<MemberDescription> Members { get; init; }
    public int? CoordinatorId { get; init; }
    public int? GroupEpoch { get; init; }
    public int? AssignmentEpoch { get; init; }

    /// <summary>
    /// Selected assignor reported by ConsumerGroupDescribe (API 69), or null when the
    /// group falls back to the classic DescribeGroups API.
    /// </summary>
    public string? AssignorName { get; init; }
    public int AuthorizedOperations { get; init; } = int.MinValue;
}

/// <summary>
/// Consumer group member description.
/// </summary>
public sealed class MemberDescription
{
    public required string MemberId { get; init; }
    public string? GroupInstanceId { get; init; }
    public string? RackId { get; init; }
    public int? MemberEpoch { get; init; }
    public string? ClientId { get; init; }
    public string? ClientHost { get; init; }
    public IReadOnlyList<string>? SubscribedTopicNames { get; init; }
    public string? SubscribedTopicRegex { get; init; }
    public IReadOnlyList<TopicPartition>? Assignment { get; init; }
    public IReadOnlyList<TopicPartition>? TargetAssignment { get; init; }
    public sbyte? MemberType { get; init; }
}

/// <summary>
/// Options for DeleteConsumerGroupOffsets.
/// </summary>
public sealed class DeleteConsumerGroupOffsetsOptions
{
    public int TimeoutMs { get; init; } = 30000;
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

/// <summary>
/// Identifies a replica of a topic partition on a specific broker.
/// </summary>
public readonly record struct TopicPartitionReplica(string Topic, int Partition, int BrokerId)
{
    /// <summary>
    /// The topic partition for this replica.
    /// </summary>
    public TopicPartition TopicPartition => new(Topic, Partition);
}

/// <summary>
/// Description of a broker log directory.
/// </summary>
public sealed class LogDirDescription
{
    /// <summary>
    /// Directory-level error code, or None if the directory was described successfully.
    /// </summary>
    public Protocol.ErrorCode ErrorCode { get; init; }

    /// <summary>
    /// Replica information keyed by topic partition.
    /// </summary>
    public required IReadOnlyDictionary<TopicPartition, ReplicaLogDirInfo> ReplicaInfos { get; init; }

    /// <summary>
    /// Total size in bytes of the volume containing the log directory, when returned by the broker.
    /// </summary>
    public long? TotalBytes { get; init; }

    /// <summary>
    /// Usable size in bytes of the volume containing the log directory, when returned by the broker.
    /// </summary>
    public long? UsableBytes { get; init; }

    /// <summary>
    /// True when the broker reports the log directory is cordoned.
    /// </summary>
    public bool? IsCordoned { get; init; }
}

/// <summary>
/// Replica placement and size information inside a log directory.
/// </summary>
public sealed class ReplicaLogDirInfo
{
    /// <summary>
    /// Size of the log segments for this replica in bytes.
    /// </summary>
    public required long Size { get; init; }

    /// <summary>
    /// Offset lag of this log relative to the current replica high watermark or log end offset.
    /// </summary>
    public required long OffsetLag { get; init; }

    /// <summary>
    /// True when this is a future log created by AlterReplicaLogDirs.
    /// </summary>
    public required bool IsFuture { get; init; }
}

/// <summary>
/// Result of altering a replica log directory.
/// </summary>
public sealed class AlterReplicaLogDirResultInfo
{
    /// <summary>
    /// The replica the result applies to.
    /// </summary>
    public required TopicPartitionReplica TopicPartitionReplica { get; init; }

    /// <summary>
    /// Per-replica error code, or None if the assignment succeeded.
    /// </summary>
    public Protocol.ErrorCode ErrorCode { get; init; }
}
