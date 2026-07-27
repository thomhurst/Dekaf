namespace Dekaf.Protocol.Messages;

/// <summary>
/// DescribeTopicPartitions response (API key 75).
/// Contains topic partition details and an optional pagination cursor.
/// </summary>
public sealed class DescribeTopicPartitionsResponse : IKafkaResponse
{
    internal const int MaxTopicCount = 1_000_000;
    internal const int MaxPartitionCount = 1_000_000;
    internal const int MaxReplicaCount = 100_000;

    public static ApiKey ApiKey => ApiKey.DescribeTopicPartitions;
    public static short LowestSupportedVersion => 0;
    public static short HighestSupportedVersion => 0;

    public int ThrottleTimeMs { get; init; }
    public required IReadOnlyList<DescribeTopicPartitionsResponseTopic> Topics { get; init; }
    public DescribeTopicPartitionsResponseCursor? NextCursor { get; init; }

    public static IKafkaResponse Read(ref KafkaProtocolReader reader, short version)
    {
        var throttleTimeMs = reader.ReadInt32();
        var topics = reader.ReadCompactArray(
            static (ref KafkaProtocolReader r) => DescribeTopicPartitionsResponseTopic.Read(ref r),
            minElementSize: 26,
            maxCount: MaxTopicCount);
        var nextCursor = DescribeTopicPartitionsResponseCursor.ReadNullable(ref reader);

        reader.SkipTaggedFields();

        return new DescribeTopicPartitionsResponse
        {
            ThrottleTimeMs = throttleTimeMs,
            Topics = topics,
            NextCursor = nextCursor
        };
    }
}

public sealed class DescribeTopicPartitionsResponseTopic
{
    public ErrorCode ErrorCode { get; init; }
    public string? Name { get; init; }
    public Guid TopicId { get; init; }
    public bool IsInternal { get; init; }
    public required IReadOnlyList<DescribeTopicPartitionsResponsePartition> Partitions { get; init; }
    public int TopicAuthorizedOperations { get; init; } = int.MinValue;

    public static DescribeTopicPartitionsResponseTopic Read(ref KafkaProtocolReader reader)
    {
        var errorCode = (ErrorCode)reader.ReadInt16();
        var name = reader.ReadCompactString();
        var topicId = reader.ReadUuid();
        var isInternal = reader.ReadBoolean();
        var partitions = reader.ReadCompactArray(
            static (ref KafkaProtocolReader r) => DescribeTopicPartitionsResponsePartition.Read(ref r),
            minElementSize: 20,
            maxCount: DescribeTopicPartitionsResponse.MaxPartitionCount);
        var topicAuthorizedOperations = reader.ReadInt32();

        reader.SkipTaggedFields();

        return new DescribeTopicPartitionsResponseTopic
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

public sealed class DescribeTopicPartitionsResponsePartition
{
    public ErrorCode ErrorCode { get; init; }
    public int PartitionIndex { get; init; }
    public int LeaderId { get; init; }
    public int LeaderEpoch { get; init; } = -1;
    public required IReadOnlyList<int> ReplicaNodes { get; init; }
    public required IReadOnlyList<int> IsrNodes { get; init; }
    public IReadOnlyList<int>? EligibleLeaderReplicas { get; init; }
    public IReadOnlyList<int>? LastKnownElr { get; init; }
    public required IReadOnlyList<int> OfflineReplicas { get; init; }

    public static DescribeTopicPartitionsResponsePartition Read(ref KafkaProtocolReader reader)
    {
        var errorCode = (ErrorCode)reader.ReadInt16();
        var partitionIndex = reader.ReadInt32();
        var leaderId = reader.ReadInt32();
        var leaderEpoch = reader.ReadInt32();
        var replicaNodes = reader.ReadCompactArray(
            static (ref KafkaProtocolReader r) => r.ReadInt32(),
            minElementSize: 4,
            maxCount: DescribeTopicPartitionsResponse.MaxReplicaCount);
        var isrNodes = reader.ReadCompactArray(
            static (ref KafkaProtocolReader r) => r.ReadInt32(),
            minElementSize: 4,
            maxCount: DescribeTopicPartitionsResponse.MaxReplicaCount);
        var eligibleLeaderReplicas = reader.ReadCompactNullableArray(
            static (ref KafkaProtocolReader r) => r.ReadInt32(),
            minElementSize: 4,
            maxCount: DescribeTopicPartitionsResponse.MaxReplicaCount);
        var lastKnownElr = reader.ReadCompactNullableArray(
            static (ref KafkaProtocolReader r) => r.ReadInt32(),
            minElementSize: 4,
            maxCount: DescribeTopicPartitionsResponse.MaxReplicaCount);
        var offlineReplicas = reader.ReadCompactArray(
            static (ref KafkaProtocolReader r) => r.ReadInt32(),
            minElementSize: 4,
            maxCount: DescribeTopicPartitionsResponse.MaxReplicaCount);

        reader.SkipTaggedFields();

        return new DescribeTopicPartitionsResponsePartition
        {
            ErrorCode = errorCode,
            PartitionIndex = partitionIndex,
            LeaderId = leaderId,
            LeaderEpoch = leaderEpoch,
            ReplicaNodes = replicaNodes,
            IsrNodes = isrNodes,
            EligibleLeaderReplicas = eligibleLeaderReplicas,
            LastKnownElr = lastKnownElr,
            OfflineReplicas = offlineReplicas
        };
    }
}

public sealed class DescribeTopicPartitionsResponseCursor
{
    public required string TopicName { get; init; }
    public int PartitionIndex { get; init; }

    public static DescribeTopicPartitionsResponseCursor? ReadNullable(ref KafkaProtocolReader reader)
    {
        var marker = reader.ReadInt8();
        if (marker < 0)
        {
            return null;
        }

        var topicName = reader.ReadCompactString() ?? string.Empty;
        var partitionIndex = reader.ReadInt32();
        reader.SkipTaggedFields();

        return new DescribeTopicPartitionsResponseCursor
        {
            TopicName = topicName,
            PartitionIndex = partitionIndex
        };
    }
}
