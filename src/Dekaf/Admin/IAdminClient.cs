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
