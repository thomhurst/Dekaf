namespace Dekaf.Protocol.Messages;

/// <summary>
/// Metadata response (API key 3).
/// Contains cluster topology, topic partitions, and leader information.
/// </summary>
public sealed class MetadataResponse : IKafkaResponse
{
    public static ApiKey ApiKey => ApiKey.Metadata;
    public static short LowestSupportedVersion => 0;
    public static short HighestSupportedVersion => 12;

    public int ThrottleTimeMs { get; init; }
    public required IReadOnlyList<BrokerMetadata> Brokers { get; init; }
    public string? ClusterId { get; init; }
    public int ControllerId { get; init; } = -1;
    public required IReadOnlyList<TopicMetadata> Topics { get; init; }
    public int ClusterAuthorizedOperations { get; init; } = int.MinValue;

    public static IKafkaResponse Read(ref KafkaProtocolReader reader, short version)
    {
        var isFlexible = version >= 9;

        var throttleTimeMs = version >= 3 ? reader.ReadInt32() : 0;

        var brokers = isFlexible
            ? reader.ReadCompactArray((ref KafkaProtocolReader r) => BrokerMetadata.Read(ref r, version))
            : reader.ReadArray((ref KafkaProtocolReader r) => BrokerMetadata.Read(ref r, version));

        string? clusterId = null;
        if (version >= 2)
        {
            clusterId = isFlexible ? reader.ReadCompactString() : reader.ReadString();
        }

        var controllerId = version >= 1 ? reader.ReadInt32() : -1;

        var topics = isFlexible
            ? reader.ReadCompactArray((ref KafkaProtocolReader r) => TopicMetadata.Read(ref r, version))
            : reader.ReadArray((ref KafkaProtocolReader r) => TopicMetadata.Read(ref r, version));

        var clusterAuthorizedOperations = int.MinValue;
        if (version >= 8 && version <= 10)
        {
            clusterAuthorizedOperations = reader.ReadInt32();
        }

        if (isFlexible)
        {
            reader.SkipTaggedFields();
        }

        return new MetadataResponse
        {
            ThrottleTimeMs = throttleTimeMs,
            Brokers = brokers,
            ClusterId = clusterId,
            ControllerId = controllerId,
            Topics = topics,
            ClusterAuthorizedOperations = clusterAuthorizedOperations
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
        var isFlexible = version >= 9;

        var nodeId = reader.ReadInt32();
        var host = isFlexible ? reader.ReadCompactNonNullableString() : reader.ReadString() ?? string.Empty;
        var port = reader.ReadInt32();

        string? rack = null;
        if (version >= 1)
        {
            rack = isFlexible ? reader.ReadCompactString() : reader.ReadString();
        }

        if (isFlexible)
        {
            reader.SkipTaggedFields();
        }

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
        var isFlexible = version >= 9;

        var errorCode = (ErrorCode)reader.ReadInt16();
        var name = isFlexible ? reader.ReadCompactNonNullableString() : reader.ReadString() ?? string.Empty;

        var topicId = Guid.Empty;
        if (version >= 10)
        {
            topicId = reader.ReadUuid();
        }

        var isInternal = version >= 1 && reader.ReadBoolean();

        var partitions = isFlexible
            ? reader.ReadCompactArray((ref KafkaProtocolReader r) => PartitionMetadata.Read(ref r, version))
            : reader.ReadArray((ref KafkaProtocolReader r) => PartitionMetadata.Read(ref r, version));

        var topicAuthorizedOperations = int.MinValue;
        if (version >= 8)
        {
            topicAuthorizedOperations = reader.ReadInt32();
        }

        if (isFlexible)
        {
            reader.SkipTaggedFields();
        }

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
        var isFlexible = version >= 9;

        var errorCode = (ErrorCode)reader.ReadInt16();
        var partitionIndex = reader.ReadInt32();
        var leaderId = reader.ReadInt32();

        var leaderEpoch = -1;
        if (version >= 7)
        {
            leaderEpoch = reader.ReadInt32();
        }

        var replicaNodes = isFlexible
            ? reader.ReadCompactArray((ref KafkaProtocolReader r) => r.ReadInt32())
            : reader.ReadArray((ref KafkaProtocolReader r) => r.ReadInt32());

        var isrNodes = isFlexible
            ? reader.ReadCompactArray((ref KafkaProtocolReader r) => r.ReadInt32())
            : reader.ReadArray((ref KafkaProtocolReader r) => r.ReadInt32());

        int[]? offlineReplicas = null;
        if (version >= 5)
        {
            offlineReplicas = isFlexible
                ? reader.ReadCompactArray((ref KafkaProtocolReader r) => r.ReadInt32())
                : reader.ReadArray((ref KafkaProtocolReader r) => r.ReadInt32());
        }

        if (isFlexible)
        {
            reader.SkipTaggedFields();
        }

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
