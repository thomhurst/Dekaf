using Dekaf.Protocol;
using Dekaf.Protocol.Messages;

namespace Dekaf.Metadata;

/// <summary>
/// Represents the current state of cluster metadata.
/// </summary>
public sealed class ClusterMetadata
{
    private readonly Dictionary<int, BrokerNode> _brokers = [];
    private readonly Dictionary<string, TopicInfo> _topics = [];
    private readonly Dictionary<Guid, TopicInfo> _topicsById = [];
    private readonly ReaderWriterLockSlim _lock = new();

    /// <summary>
    /// Cluster ID.
    /// </summary>
    public string? ClusterId { get; private set; }

    /// <summary>
    /// Controller broker ID.
    /// </summary>
    public int ControllerId { get; private set; } = -1;

    /// <summary>
    /// When the metadata was last refreshed.
    /// </summary>
    public DateTimeOffset LastRefreshed { get; private set; }

    /// <summary>
    /// Gets all brokers in the cluster.
    /// </summary>
    public IReadOnlyList<BrokerNode> GetBrokers()
    {
        _lock.EnterReadLock();
        try
        {
            return _brokers.Values.ToList();
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    /// <summary>
    /// Gets a broker by ID.
    /// </summary>
    public BrokerNode? GetBroker(int brokerId)
    {
        _lock.EnterReadLock();
        try
        {
            return _brokers.GetValueOrDefault(brokerId);
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    /// <summary>
    /// Gets all known topics.
    /// </summary>
    public IReadOnlyList<TopicInfo> GetTopics()
    {
        _lock.EnterReadLock();
        try
        {
            return _topics.Values.ToList();
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    /// <summary>
    /// Gets topic metadata by name.
    /// </summary>
    public TopicInfo? GetTopic(string topicName)
    {
        _lock.EnterReadLock();
        try
        {
            return _topics.GetValueOrDefault(topicName);
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    /// <summary>
    /// Gets topic metadata by ID.
    /// </summary>
    public TopicInfo? GetTopic(Guid topicId)
    {
        _lock.EnterReadLock();
        try
        {
            return _topicsById.GetValueOrDefault(topicId);
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    /// <summary>
    /// Gets the leader broker for a partition.
    /// </summary>
    public BrokerNode? GetPartitionLeader(string topicName, int partition)
    {
        _lock.EnterReadLock();
        try
        {
            if (!_topics.TryGetValue(topicName, out var topic))
                return null;

            var partitionInfo = topic.Partitions.FirstOrDefault(p => p.PartitionIndex == partition);
            if (partitionInfo is null)
                return null;

            return _brokers.GetValueOrDefault(partitionInfo.LeaderId);
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    /// <summary>
    /// Updates metadata from a MetadataResponse.
    /// </summary>
    public void Update(MetadataResponse response)
    {
        _lock.EnterWriteLock();
        try
        {
            ClusterId = response.ClusterId;
            ControllerId = response.ControllerId;
            LastRefreshed = DateTimeOffset.UtcNow;

            // Update brokers
            _brokers.Clear();
            foreach (var broker in response.Brokers)
            {
                _brokers[broker.NodeId] = new BrokerNode
                {
                    NodeId = broker.NodeId,
                    Host = broker.Host,
                    Port = broker.Port,
                    Rack = broker.Rack
                };
            }

            // Update topics
            _topics.Clear();
            _topicsById.Clear();
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

                _topics[topic.Name] = topicInfo;
                if (topic.TopicId != Guid.Empty)
                {
                    _topicsById[topic.TopicId] = topicInfo;
                }
            }
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Returns true if the metadata is stale.
    /// </summary>
    public bool IsStale(TimeSpan maxAge)
    {
        return DateTimeOffset.UtcNow - LastRefreshed > maxAge;
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
