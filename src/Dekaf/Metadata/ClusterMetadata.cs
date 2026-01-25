using System.Runtime.CompilerServices;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;

namespace Dekaf.Metadata;

/// <summary>
/// Immutable snapshot of cluster metadata.
/// Using an immutable snapshot with volatile reference swap eliminates lock overhead on reads.
/// Reads are lock-free (simple volatile read), writes create a new snapshot and atomically swap.
/// </summary>
internal sealed class ClusterMetadataSnapshot
{
    public static readonly ClusterMetadataSnapshot Empty = new(
        null, -1, default,
        new Dictionary<int, BrokerNode>(),
        new Dictionary<string, TopicInfo>(),
        new Dictionary<Guid, TopicInfo>());

    public string? ClusterId { get; }
    public int ControllerId { get; }
    public DateTimeOffset LastRefreshed { get; }
    public IReadOnlyDictionary<int, BrokerNode> Brokers { get; }
    public IReadOnlyDictionary<string, TopicInfo> Topics { get; }
    public IReadOnlyDictionary<Guid, TopicInfo> TopicsById { get; }

    public ClusterMetadataSnapshot(
        string? clusterId,
        int controllerId,
        DateTimeOffset lastRefreshed,
        Dictionary<int, BrokerNode> brokers,
        Dictionary<string, TopicInfo> topics,
        Dictionary<Guid, TopicInfo> topicsById)
    {
        ClusterId = clusterId;
        ControllerId = controllerId;
        LastRefreshed = lastRefreshed;
        Brokers = brokers;
        Topics = topics;
        TopicsById = topicsById;
    }
}

/// <summary>
/// Represents the current state of cluster metadata.
/// Uses an immutable snapshot pattern for lock-free reads (~100ns faster per lookup).
/// Writes atomically swap to a new snapshot, ensuring readers always see consistent state.
/// </summary>
public sealed class ClusterMetadata
{
    private volatile ClusterMetadataSnapshot _snapshot = ClusterMetadataSnapshot.Empty;
    private readonly object _writeLock = new(); // Only needed for concurrent writes (rare)

    /// <summary>
    /// Cluster ID.
    /// </summary>
    public string? ClusterId
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _snapshot.ClusterId;
    }

    /// <summary>
    /// Controller broker ID.
    /// </summary>
    public int ControllerId
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _snapshot.ControllerId;
    }

    /// <summary>
    /// When the metadata was last refreshed.
    /// </summary>
    public DateTimeOffset LastRefreshed
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _snapshot.LastRefreshed;
    }

    /// <summary>
    /// Gets all brokers in the cluster.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IReadOnlyList<BrokerNode> GetBrokers()
    {
        var snapshot = _snapshot;
        // Pre-allocate exact size to avoid list resizing allocations
        var result = new BrokerNode[snapshot.Brokers.Count];
        var index = 0;
        foreach (var broker in snapshot.Brokers.Values)
        {
            result[index++] = broker;
        }
        return result;
    }

    /// <summary>
    /// Gets a broker by ID.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public BrokerNode? GetBroker(int brokerId)
    {
        return _snapshot.Brokers.TryGetValue(brokerId, out var broker) ? broker : null;
    }

    /// <summary>
    /// Gets all known topics.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IReadOnlyList<TopicInfo> GetTopics()
    {
        var snapshot = _snapshot;
        // Pre-allocate exact size to avoid list resizing allocations
        var result = new TopicInfo[snapshot.Topics.Count];
        var index = 0;
        foreach (var topic in snapshot.Topics.Values)
        {
            result[index++] = topic;
        }
        return result;
    }

    /// <summary>
    /// Gets topic metadata by name.
    /// Lock-free: simple volatile read of immutable snapshot.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TopicInfo? GetTopic(string topicName)
    {
        return _snapshot.Topics.TryGetValue(topicName, out var topic) ? topic : null;
    }

    /// <summary>
    /// Gets topic metadata by ID.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TopicInfo? GetTopic(Guid topicId)
    {
        return _snapshot.TopicsById.TryGetValue(topicId, out var topic) ? topic : null;
    }

    /// <summary>
    /// Gets the leader broker for a partition.
    /// Lock-free: reads from immutable snapshot.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public BrokerNode? GetPartitionLeader(string topicName, int partition)
    {
        var snapshot = _snapshot;

        if (!snapshot.Topics.TryGetValue(topicName, out var topic))
            return null;

        // Manual loop to avoid closure allocation from FirstOrDefault lambda
        PartitionInfo? partitionInfo = null;
        foreach (var p in topic.Partitions)
        {
            if (p.PartitionIndex == partition)
            {
                partitionInfo = p;
                break;
            }
        }

        if (partitionInfo is null)
            return null;

        return snapshot.Brokers.TryGetValue(partitionInfo.LeaderId, out var broker) ? broker : null;
    }

    /// <summary>
    /// Updates metadata from a MetadataResponse.
    /// Creates a new immutable snapshot and atomically swaps the reference.
    /// </summary>
    public void Update(MetadataResponse response)
    {
        // Build new snapshot outside of any lock for maximum parallelism
        var brokers = new Dictionary<int, BrokerNode>(response.Brokers.Count);
        foreach (var broker in response.Brokers)
        {
            brokers[broker.NodeId] = new BrokerNode
            {
                NodeId = broker.NodeId,
                Host = broker.Host,
                Port = broker.Port,
                Rack = broker.Rack
            };
        }

        var topics = new Dictionary<string, TopicInfo>(response.Topics.Count);
        var topicsById = new Dictionary<Guid, TopicInfo>();

        foreach (var topic in response.Topics)
        {
            // Use explicit loop instead of LINQ Select to avoid enumerator allocation
            var partitions = new List<PartitionInfo>(topic.Partitions.Count);
            foreach (var p in topic.Partitions)
            {
                partitions.Add(new PartitionInfo
                {
                    PartitionIndex = p.PartitionIndex,
                    LeaderId = p.LeaderId,
                    LeaderEpoch = p.LeaderEpoch,
                    ReplicaNodes = p.ReplicaNodes,
                    IsrNodes = p.IsrNodes,
                    OfflineReplicas = p.OfflineReplicas,
                    ErrorCode = p.ErrorCode
                });
            }

            var topicInfo = new TopicInfo
            {
                Name = topic.Name,
                TopicId = topic.TopicId,
                ErrorCode = topic.ErrorCode,
                IsInternal = topic.IsInternal,
                Partitions = partitions
            };

            topics[topic.Name] = topicInfo;
            if (topic.TopicId != Guid.Empty)
            {
                topicsById[topic.TopicId] = topicInfo;
            }
        }

        var newSnapshot = new ClusterMetadataSnapshot(
            response.ClusterId,
            response.ControllerId,
            DateTimeOffset.UtcNow,
            brokers,
            topics,
            topicsById);

        // Lock only needed if multiple threads could call Update() concurrently
        // In practice, only the background refresh task calls this, so the lock is rarely contended
        lock (_writeLock)
        {
            // Atomic reference swap - readers see either old or new snapshot, never partial state
            _snapshot = newSnapshot;
        }
    }

    /// <summary>
    /// Returns true if the metadata is stale.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsStale(TimeSpan maxAge)
    {
        return DateTimeOffset.UtcNow - _snapshot.LastRefreshed > maxAge;
    }
}

/// <summary>
/// Represents a broker node.
/// </summary>
public sealed class BrokerNode
{
    public required int NodeId { get; init; }
    public required string Host { get; init; }
    public required int Port { get; init; }
    public string? Rack { get; init; }

    public override string ToString() => $"Broker({NodeId}, {Host}:{Port})";
}

/// <summary>
/// Represents topic metadata.
/// </summary>
public sealed class TopicInfo
{
    public required string Name { get; init; }
    public Guid TopicId { get; init; }
    public ErrorCode ErrorCode { get; init; }
    public bool IsInternal { get; init; }
    public required IReadOnlyList<PartitionInfo> Partitions { get; init; }

    /// <summary>
    /// Gets the number of partitions.
    /// </summary>
    public int PartitionCount => Partitions.Count;
}

/// <summary>
/// Represents partition metadata.
/// </summary>
public sealed class PartitionInfo
{
    public required int PartitionIndex { get; init; }
    public required int LeaderId { get; init; }
    public int LeaderEpoch { get; init; } = -1;
    public required IReadOnlyList<int> ReplicaNodes { get; init; }
    public required IReadOnlyList<int> IsrNodes { get; init; }
    public IReadOnlyList<int>? OfflineReplicas { get; init; }
    public ErrorCode ErrorCode { get; init; }
}
