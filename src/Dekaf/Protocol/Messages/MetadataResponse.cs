namespace Dekaf.Protocol.Messages;

/// <summary>
/// Metadata response (API key 3).
/// Contains cluster topology, topic partitions, and leader information.
/// </summary>
public sealed class MetadataResponse : IKafkaResponse
{
    // Absolute parse caps bound frame-valid object amplification while remaining far
    // above practical Kafka cluster, topic, partition, and replication-factor limits.
    internal const int MaxBrokerCount = 100_000;
    internal const int MaxTopicCount = 1_000_000;
    internal const int MaxPartitionCount = 1_000_000;
    internal const int MaxReplicaCount = 100_000;

    public static ApiKey ApiKey => ApiKey.Metadata;
    public static short LowestSupportedVersion => 5;
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

        var brokers = version >= 9
            ? reader.ReadCompactArray(
                static (ref KafkaProtocolReader r, short v) => BrokerMetadata.Read(ref r, v),
                version,
                minElementSize: 11,
                maxCount: MaxBrokerCount)
            : reader.ReadArray(
                static (ref KafkaProtocolReader r, short v) => BrokerMetadata.Read(ref r, v),
                version,
                minElementSize: 12,
                maxCount: MaxBrokerCount);

        var clusterId = version >= 9 ? reader.ReadCompactString() : reader.ReadString();

        var controllerId = reader.ReadInt32();

        var topics = version >= 9
            ? reader.ReadCompactArray(
                static (ref KafkaProtocolReader r, short v) => TopicMetadata.Read(ref r, v),
                version,
                minElementSize: version >= 10 ? 26 : 10,
                maxCount: MaxTopicCount)
            : reader.ReadArray(
                static (ref KafkaProtocolReader r, short v) => TopicMetadata.Read(ref r, v),
                version,
                minElementSize: version >= 8 ? 13 : 9,
                maxCount: MaxTopicCount);

        var clusterAuthorizedOperations = int.MinValue;
        if (version is >= 8 and <= 10)
        {
            clusterAuthorizedOperations = reader.ReadInt32();
        }

        var errorCode = ErrorCode.None;
        if (version >= 13)
        {
            errorCode = (ErrorCode)reader.ReadInt16();
        }

        if (version >= 9)
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
        var host = version >= 9
            ? reader.ReadCompactNonNullableString()
            : reader.ReadString() ?? string.Empty;
        var port = reader.ReadInt32();
        var rack = version >= 9 ? reader.ReadCompactString() : reader.ReadString();

        if (version >= 9)
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
        var name = version >= 9
            ? reader.ReadCompactNonNullableString()
            : reader.ReadString() ?? string.Empty;

        var topicId = Guid.Empty;
        if (version >= 10)
        {
            topicId = reader.ReadUuid();
        }

        var isInternal = reader.ReadBoolean();

        var partitions = version >= 9
            ? reader.ReadCompactArray(
                static (ref KafkaProtocolReader r, short v) => PartitionMetadata.Read(ref r, v),
                version,
                minElementSize: 18,
                maxCount: MetadataResponse.MaxPartitionCount)
            : reader.ReadArray(
                static (ref KafkaProtocolReader r, short v) => PartitionMetadata.Read(ref r, v),
                version,
                minElementSize: version >= 7 ? 26 : 22,
                maxCount: MetadataResponse.MaxPartitionCount);

        var topicAuthorizedOperations = version >= 8 ? reader.ReadInt32() : int.MinValue;

        if (version >= 9)
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
        var leaderEpoch = version >= 7 ? reader.ReadInt32() : -1;

        var replicaNodes = ReadBrokerIds(ref reader, version);
        var isrNodes = ReadBrokerIds(ref reader, version);
        var offlineReplicas = version >= 5 ? ReadBrokerIds(ref reader, version) : null;

        if (version >= 9)
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

    private static int[] ReadBrokerIds(ref KafkaProtocolReader reader, short version) => version >= 9
        ? reader.ReadCompactArray(
            static (ref KafkaProtocolReader r) => r.ReadInt32(),
            minElementSize: 4,
            maxCount: MetadataResponse.MaxReplicaCount)
        : reader.ReadArray(
            static (ref KafkaProtocolReader r) => r.ReadInt32(),
            minElementSize: 4,
            maxCount: MetadataResponse.MaxReplicaCount);
}
