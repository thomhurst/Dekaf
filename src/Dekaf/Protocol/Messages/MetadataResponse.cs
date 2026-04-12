namespace Dekaf.Protocol.Messages;

/// <summary>
/// Metadata response (API key 3).
/// Contains cluster topology, topic partitions, and leader information.
/// </summary>
public sealed class MetadataResponse : IKafkaResponse
{
    public static ApiKey ApiKey => ApiKey.Metadata;
    public static short LowestSupportedVersion => 9;
    public static short HighestSupportedVersion => 13;

    public int ThrottleTimeMs { get; init; }
    public required IReadOnlyList<BrokerMetadata> Brokers { get; init; }
    public string? ClusterId { get; init; }
    public int ControllerId { get; init; } = -1;
    public required IReadOnlyList<TopicMetadata> Topics { get; init; }
    public int ClusterAuthorizedOperations { get; init; } = int.MinValue;

    /// <summary>
    /// Top-level error code (v13+, KIP-1102). When set to <see cref="Protocol.ErrorCode.RebootstrapRequired"/>,
    /// the client should immediately trigger a rebootstrap.
    /// </summary>
    public ErrorCode ErrorCode { get; init; }

    public static IKafkaResponse Read(ref KafkaProtocolReader reader, short version)
    {
        var throttleTimeMs = reader.ReadInt32();

        var brokers = reader.ReadCompactArray(static (ref KafkaProtocolReader r, short v) => BrokerMetadata.Read(ref r, v), version);

        var clusterId = reader.ReadCompactString();

        var controllerId = reader.ReadInt32();

        var topics = reader.ReadCompactArray(static (ref KafkaProtocolReader r, short v) => TopicMetadata.Read(ref r, v), version);

        var clusterAuthorizedOperations = int.MinValue;
        if (version >= 8 && version <= 10)
        {
            clusterAuthorizedOperations = reader.ReadInt32();
        }

        var errorCode = ErrorCode.None;
        if (version >= 13)
        {
            errorCode = (ErrorCode)reader.ReadInt16();
        }

        reader.SkipTaggedFields();

        return new MetadataResponse
        {
            ThrottleTimeMs = throttleTimeMs,
            Brokers = brokers,
            ClusterId = clusterId,
            ControllerId = controllerId,
            Topics = topics,
            ClusterAuthorizedOperations = clusterAuthorizedOperations,
            ErrorCode = errorCode
        };
    }
}

/// <summary>
/// Broker metadata in a metadata response.
/// </summary>
public sealed class BrokerMetadata
{
    public required int NodeId { get; init; }
    public required string Host { get; init; }
    public required int Port { get; init; }
    public string? Rack { get; init; }

    public static BrokerMetadata Read(ref KafkaProtocolReader reader, short version)
    {
        var nodeId = reader.ReadInt32();
        var host = reader.ReadCompactNonNullableString();
        var port = reader.ReadInt32();
        var rack = reader.ReadCompactString();

        reader.SkipTaggedFields();

        return new BrokerMetadata
        {
            NodeId = nodeId,
            Host = host,
            Port = port,
            Rack = rack
        };
    }
}

/// <summary>
/// Topic metadata in a metadata response.
/// </summary>
public sealed class TopicMetadata
{
    public required ErrorCode ErrorCode { get; init; }
    public required string Name { get; init; }
    public Guid TopicId { get; init; }
    public bool IsInternal { get; init; }
    public required IReadOnlyList<PartitionMetadata> Partitions { get; init; }
    public int TopicAuthorizedOperations { get; init; } = int.MinValue;

    public static TopicMetadata Read(ref KafkaProtocolReader reader, short version)
    {
        var errorCode = (ErrorCode)reader.ReadInt16();
        var name = reader.ReadCompactNonNullableString();

        var topicId = Guid.Empty;
        if (version >= 10)
        {
            topicId = reader.ReadUuid();
        }

        var isInternal = reader.ReadBoolean();

        var partitions = reader.ReadCompactArray(static (ref KafkaProtocolReader r, short v) => PartitionMetadata.Read(ref r, v), version);

        var topicAuthorizedOperations = reader.ReadInt32();

        reader.SkipTaggedFields();

        return new TopicMetadata
        {
            ErrorCode = errorCode,
            Name = name,
            TopicId = topicId,
            IsInternal = isInternal,
            Partitions = partitions,
            TopicAuthorizedOperations = topicAuthorizedOperations
        };
    }
}

/// <summary>
/// Partition metadata in a metadata response.
/// </summary>
public sealed class PartitionMetadata
{
    public required ErrorCode ErrorCode { get; init; }
    public required int PartitionIndex { get; init; }
    public required int LeaderId { get; init; }
    public int LeaderEpoch { get; init; } = -1;
    public required IReadOnlyList<int> ReplicaNodes { get; init; }
    public required IReadOnlyList<int> IsrNodes { get; init; }
    public IReadOnlyList<int>? OfflineReplicas { get; init; }

    public static PartitionMetadata Read(ref KafkaProtocolReader reader, short version)
    {
        var errorCode = (ErrorCode)reader.ReadInt16();
        var partitionIndex = reader.ReadInt32();
        var leaderId = reader.ReadInt32();
        var leaderEpoch = reader.ReadInt32();

        var replicaNodes = reader.ReadCompactArray((ref KafkaProtocolReader r) => r.ReadInt32());
        var isrNodes = reader.ReadCompactArray((ref KafkaProtocolReader r) => r.ReadInt32());
        var offlineReplicas = reader.ReadCompactArray((ref KafkaProtocolReader r) => r.ReadInt32());

        reader.SkipTaggedFields();

        return new PartitionMetadata
        {
            ErrorCode = errorCode,
            PartitionIndex = partitionIndex,
            LeaderId = leaderId,
            LeaderEpoch = leaderEpoch,
            ReplicaNodes = replicaNodes,
            IsrNodes = isrNodes,
            OfflineReplicas = offlineReplicas
        };
    }
}
