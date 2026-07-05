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

    /// <summary>
    /// Reverse index: broker node ID → list of TopicPartitions led by that broker.
    /// Built at snapshot creation time so the sender loop's drain algorithm can look up
    /// partitions for a broker in O(1) instead of scanning all topics × partitions.
    /// </summary>
    public IReadOnlyDictionary<int, IReadOnlyList<TopicPartition>> PartitionsByBroker { get; }

    /// <summary>
    /// Topic name → partition metadata array indexed by PartitionIndex.
    /// Built at snapshot creation time so partition-leader lookup is O(1).
    /// </summary>
    public IReadOnlyDictionary<string, PartitionInfo?[]> PartitionsByTopicIndex { get; }

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
        PartitionsByBroker = BuildPartitionsByBroker(topics);
        PartitionsByTopicIndex = BuildPartitionsByTopicIndex(topics);
    }

    private static Dictionary<int, IReadOnlyList<TopicPartition>> BuildPartitionsByBroker(
        Dictionary<string, TopicInfo> topics)
    {
        var builder = new Dictionary<int, List<TopicPartition>>();
        foreach (var topic in topics.Values)
        {
            foreach (var partition in topic.Partitions)
            {
                if (!builder.TryGetValue(partition.LeaderId, out var list))
                    builder[partition.LeaderId] = list = [];
                list.Add(new TopicPartition(topic.Name, partition.PartitionIndex));
            }
        }

        // Freeze mutable lists into arrays for the immutable snapshot
        var result = new Dictionary<int, IReadOnlyList<TopicPartition>>(builder.Count);
        foreach (var (key, list) in builder)
            result[key] = list.ToArray();
        return result;
    }

    private static Dictionary<string, PartitionInfo?[]> BuildPartitionsByTopicIndex(
        Dictionary<string, TopicInfo> topics)
    {
        var result = new Dictionary<string, PartitionInfo?[]>(topics.Count);
        foreach (var topic in topics.Values)
        {
            var maxPartitionIndex = -1;
            foreach (var partition in topic.Partitions)
            {
                if (partition.PartitionIndex > maxPartitionIndex)
                    maxPartitionIndex = partition.PartitionIndex;
            }

            if (maxPartitionIndex < 0)
            {
                result[topic.Name] = [];
                continue;
            }

            var partitionsByIndex = new PartitionInfo?[maxPartitionIndex + 1];
            foreach (var partition in topic.Partitions)
            {
                if ((uint)partition.PartitionIndex < (uint)partitionsByIndex.Length)
                    partitionsByIndex[partition.PartitionIndex] = partition;
            }

            result[topic.Name] = partitionsByIndex;
        }

        return result;
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
    /// Gets all TopicPartitions led by the given broker, using the pre-built reverse index.
    /// Returns an empty list if the broker has no partitions.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal IReadOnlyList<TopicPartition> GetPartitionsForBroker(int brokerId)
    {
        return _snapshot.PartitionsByBroker.TryGetValue(brokerId, out var partitions)
            ? partitions
            : Array.Empty<TopicPartition>();
    }

    /// <summary>
    /// Gets partition metadata by topic and partition index.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal PartitionInfo? GetPartitionInfo(string topicName, int partition)
    {
        return GetPartitionInfo(_snapshot, topicName, partition);
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
        var partitionInfo = GetPartitionInfo(snapshot, topicName, partition);

        if (partitionInfo is null)
            return null;

        return snapshot.Brokers.TryGetValue(partitionInfo.LeaderId, out var broker) ? broker : null;
    }

    /// <summary>
    /// Updates a single partition leader from inline Produce/Fetch response metadata.
    /// The update is accepted only when the leader epoch advances the cached view.
    /// </summary>
    internal bool TryUpdatePartitionLeader(
        string topicName,
        int partition,
        int leaderId,
        int leaderEpoch,
        BrokerNode? leaderEndpoint)
    {
        if (leaderId < 0 || leaderEpoch < 0)
            return false;

        lock (_writeLock)
        {
            var existing = _snapshot;
            if (!existing.Topics.TryGetValue(topicName, out var topic))
                return false;

            var currentPartition = GetPartitionInfo(existing, topicName, partition);
            if (currentPartition is null)
                return false;

            if (currentPartition.LeaderEpoch >= 0 && leaderEpoch <= currentPartition.LeaderEpoch)
                return false;

            var brokers = existing.Brokers.ToDictionary(static pair => pair.Key, static pair => pair.Value);
            if (leaderEndpoint is not null)
            {
                if (leaderEndpoint.NodeId != leaderId)
                    return false;

                brokers[leaderEndpoint.NodeId] = leaderEndpoint;
            }
            else if (!brokers.ContainsKey(leaderId))
            {
                return false;
            }

            var partitions = new PartitionInfo[topic.Partitions.Count];
            var updated = false;
            for (var i = 0; i < topic.Partitions.Count; i++)
            {
                var existingPartition = topic.Partitions[i];
                if (existingPartition.PartitionIndex != partition)
                {
                    partitions[i] = existingPartition;
                    continue;
                }

                partitions[i] = new PartitionInfo
                {
                    PartitionIndex = existingPartition.PartitionIndex,
                    LeaderId = leaderId,
                    LeaderEpoch = leaderEpoch,
                    ReplicaNodes = existingPartition.ReplicaNodes,
                    IsrNodes = existingPartition.IsrNodes,
                    OfflineReplicas = existingPartition.OfflineReplicas,
                    ErrorCode = existingPartition.ErrorCode
                };
                updated = true;
            }

            if (!updated)
                return false;

            var topics = existing.Topics.ToDictionary(static pair => pair.Key, static pair => pair.Value);
            var topicsById = existing.TopicsById.ToDictionary(static pair => pair.Key, static pair => pair.Value);
            var updatedTopic = new TopicInfo
            {
                Name = topic.Name,
                TopicId = topic.TopicId,
                ErrorCode = topic.ErrorCode,
                IsInternal = topic.IsInternal,
                Partitions = partitions
            };

            topics[topicName] = updatedTopic;
            if (updatedTopic.TopicId != Guid.Empty)
                topicsById[updatedTopic.TopicId] = updatedTopic;

            _snapshot = new ClusterMetadataSnapshot(
                existing.ClusterId,
                existing.ControllerId,
                DateTimeOffset.UtcNow,
                brokers,
                topics,
                topicsById);

            return true;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static PartitionInfo? GetPartitionInfo(
        ClusterMetadataSnapshot snapshot,
        string topicName,
        int partition)
    {
        if (!snapshot.PartitionsByTopicIndex.TryGetValue(topicName, out var partitionsByIndex))
            return null;

        return (uint)partition < (uint)partitionsByIndex.Length
            ? partitionsByIndex[partition]
            : null;
    }

    /// <summary>
    /// Updates metadata from a MetadataResponse.
    /// Creates a new immutable snapshot and atomically swaps the reference.
    /// </summary>
    /// <param name="response">The metadata response from the broker.</param>
    /// <param name="mergeTopics">
    /// When true, merges response topics into the existing snapshot (preserving metadata for
    /// topics not in the response). When false, replaces the entire snapshot.
    /// Use merge mode for topic-specific metadata requests so that previously cached topic
    /// metadata is not lost. This matches the Java client's incremental metadata update behavior.
    /// </param>
    public void Update(MetadataResponse response, bool mergeTopics = false)
    {
        // Build broker dictionary outside the lock (pure transformation, no shared state)
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

        if (mergeTopics)
        {
            // Merge mode: the snapshot read must happen inside the lock to prevent TOCTOU —
            // two concurrent merge calls reading _snapshot outside the lock could each build
            // a merged dictionary from the same base, and the second swap would lose the
            // first call's topics.
            lock (_writeLock)
            {
                var existing = _snapshot;
                var topics = new Dictionary<string, TopicInfo>(existing.Topics.Count + response.Topics.Count);
                var topicsById = new Dictionary<Guid, TopicInfo>(existing.TopicsById.Count + response.Topics.Count);

                foreach (var (name, info) in existing.Topics)
                    topics[name] = info;
                foreach (var (id, info) in existing.TopicsById)
                    topicsById[id] = info;

                AddResponseTopics(response, topics, topicsById, existing, brokers);

                _snapshot = new ClusterMetadataSnapshot(
                    response.ClusterId,
                    response.ControllerId,
                    DateTimeOffset.UtcNow,
                    brokers,
                    topics,
                    topicsById);
            }
        }
        else
        {
            lock (_writeLock)
            {
                var existing = _snapshot;
                var topics = new Dictionary<string, TopicInfo>(response.Topics.Count);
                var topicsById = new Dictionary<Guid, TopicInfo>();

                AddResponseTopics(response, topics, topicsById, existing, brokers);

                _snapshot = new ClusterMetadataSnapshot(
                    response.ClusterId,
                    response.ControllerId,
                    DateTimeOffset.UtcNow,
                    brokers,
                    topics,
                    topicsById);
            }
        }
    }

    /// <summary>
    /// Adds topics from a MetadataResponse into the given dictionaries.
    /// </summary>
    private static void AddResponseTopics(
        MetadataResponse response,
        Dictionary<string, TopicInfo> topics,
        Dictionary<Guid, TopicInfo> topicsById,
        ClusterMetadataSnapshot existing,
        Dictionary<int, BrokerNode> brokers)
    {
        foreach (var topic in response.Topics)
        {
            // Use explicit loop instead of LINQ Select to avoid enumerator allocation
            var partitions = new List<PartitionInfo>(topic.Partitions.Count);
            foreach (var p in topic.Partitions)
            {
                var existingPartition = GetPartitionInfo(existing, topic.Name, p.PartitionIndex);
                if (existingPartition is not null && ShouldPreserveExistingPartition(existingPartition, p))
                {
                    partitions.Add(existingPartition);
                    EnsureBrokerPresent(brokers, existing, existingPartition.LeaderId);
                    continue;
                }

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
    }

    private static bool ShouldPreserveExistingPartition(PartitionInfo existing, PartitionMetadata incoming)
    {
        if (existing.LeaderEpoch < 0)
            return false;

        if (incoming.LeaderEpoch >= 0)
        {
            return incoming.LeaderEpoch < existing.LeaderEpoch
                || (incoming.LeaderEpoch == existing.LeaderEpoch && incoming.LeaderId != existing.LeaderId);
        }

        return true;
    }

    private static void EnsureBrokerPresent(
        Dictionary<int, BrokerNode> brokers,
        ClusterMetadataSnapshot existing,
        int leaderId)
    {
        if (leaderId >= 0
            && !brokers.ContainsKey(leaderId)
            && existing.Brokers.TryGetValue(leaderId, out var broker))
        {
            brokers[leaderId] = broker;
        }
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
    public IReadOnlyList<int>? EligibleLeaderReplicas { get; init; }
    public IReadOnlyList<int>? LastKnownElr { get; init; }
    public IReadOnlyList<int>? OfflineReplicas { get; init; }
    public ErrorCode ErrorCode { get; init; }
}
