namespace Dekaf.Protocol.Messages;

/// <summary>
/// DescribeQuorum response (API key 55).
/// Contains KRaft quorum state.
/// </summary>
public sealed class DescribeQuorumResponse : IKafkaResponse
{
    public static ApiKey ApiKey => ApiKey.DescribeQuorum;
    public static short LowestSupportedVersion => 0;
    public static short HighestSupportedVersion => 2;

    public ErrorCode ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }
    public required IReadOnlyList<DescribeQuorumResponseTopic> Topics { get; init; }
    public IReadOnlyList<DescribeQuorumResponseNode> Nodes { get; init; } = [];

    public static IKafkaResponse Read(ref KafkaProtocolReader reader, short version)
    {
        var errorCode = (ErrorCode)reader.ReadInt16();
        var errorMessage = version >= 2 ? reader.ReadCompactString() : null;
        var topics = reader.ReadCompactArray(
            static (ref KafkaProtocolReader r, short v) => DescribeQuorumResponseTopic.Read(ref r, v),
            version);
        var nodes = version >= 2
            ? reader.ReadCompactArray(static (ref KafkaProtocolReader r) => DescribeQuorumResponseNode.Read(ref r))
            : [];

        reader.SkipTaggedFields();

        return new DescribeQuorumResponse
        {
            ErrorCode = errorCode,
            ErrorMessage = errorMessage,
            Topics = topics,
            Nodes = nodes
        };
    }
}

public sealed class DescribeQuorumResponseTopic
{
    public required string TopicName { get; init; }
    public required IReadOnlyList<DescribeQuorumResponsePartition> Partitions { get; init; }

    public static DescribeQuorumResponseTopic Read(ref KafkaProtocolReader reader, short version)
    {
        var topicName = reader.ReadCompactString() ?? string.Empty;
        var partitions = reader.ReadCompactArray(
            static (ref KafkaProtocolReader r, short v) => DescribeQuorumResponsePartition.Read(ref r, v),
            version);

        reader.SkipTaggedFields();

        return new DescribeQuorumResponseTopic
        {
            TopicName = topicName,
            Partitions = partitions
        };
    }
}

public sealed class DescribeQuorumResponsePartition
{
    public int PartitionIndex { get; init; }
    public ErrorCode ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }
    public int LeaderId { get; init; }
    public int LeaderEpoch { get; init; }
    public long HighWatermark { get; init; }
    public required IReadOnlyList<DescribeQuorumReplicaState> CurrentVoters { get; init; }
    public required IReadOnlyList<DescribeQuorumReplicaState> Observers { get; init; }

    public static DescribeQuorumResponsePartition Read(ref KafkaProtocolReader reader, short version)
    {
        var partitionIndex = reader.ReadInt32();
        var errorCode = (ErrorCode)reader.ReadInt16();
        var errorMessage = version >= 2 ? reader.ReadCompactString() : null;
        var leaderId = reader.ReadInt32();
        var leaderEpoch = reader.ReadInt32();
        var highWatermark = reader.ReadInt64();
        var currentVoters = reader.ReadCompactArray(
            static (ref KafkaProtocolReader r, short v) => DescribeQuorumReplicaState.Read(ref r, v),
            version);
        var observers = reader.ReadCompactArray(
            static (ref KafkaProtocolReader r, short v) => DescribeQuorumReplicaState.Read(ref r, v),
            version);

        reader.SkipTaggedFields();

        return new DescribeQuorumResponsePartition
        {
            PartitionIndex = partitionIndex,
            ErrorCode = errorCode,
            ErrorMessage = errorMessage,
            LeaderId = leaderId,
            LeaderEpoch = leaderEpoch,
            HighWatermark = highWatermark,
            CurrentVoters = currentVoters,
            Observers = observers
        };
    }
}

public sealed class DescribeQuorumReplicaState
{
    public int ReplicaId { get; init; }
    public Guid? ReplicaDirectoryId { get; init; }
    public long LogEndOffset { get; init; }
    public long LastFetchTimestamp { get; init; } = -1;
    public long LastCaughtUpTimestamp { get; init; } = -1;

    public static DescribeQuorumReplicaState Read(ref KafkaProtocolReader reader, short version)
    {
        var replicaId = reader.ReadInt32();
        Guid? replicaDirectoryId = version >= 2 ? reader.ReadUuid() : null;
        var logEndOffset = reader.ReadInt64();
        var lastFetchTimestamp = version >= 1 ? reader.ReadInt64() : -1;
        var lastCaughtUpTimestamp = version >= 1 ? reader.ReadInt64() : -1;

        reader.SkipTaggedFields();

        return new DescribeQuorumReplicaState
        {
            ReplicaId = replicaId,
            ReplicaDirectoryId = replicaDirectoryId,
            LogEndOffset = logEndOffset,
            LastFetchTimestamp = lastFetchTimestamp,
            LastCaughtUpTimestamp = lastCaughtUpTimestamp
        };
    }
}

public sealed class DescribeQuorumResponseNode
{
    public int NodeId { get; init; }
    public required IReadOnlyList<RaftVoterEndpointData> Listeners { get; init; }

    public static DescribeQuorumResponseNode Read(ref KafkaProtocolReader reader)
    {
        var nodeId = reader.ReadInt32();
        var listeners = reader.ReadCompactArray(static (ref KafkaProtocolReader r) => RaftVoterEndpointData.Read(ref r));

        reader.SkipTaggedFields();

        return new DescribeQuorumResponseNode
        {
            NodeId = nodeId,
            Listeners = listeners
        };
    }
}
