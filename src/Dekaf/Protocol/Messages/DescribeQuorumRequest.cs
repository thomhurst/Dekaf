namespace Dekaf.Protocol.Messages;

/// <summary>
/// DescribeQuorum request (API key 55).
/// Describes KRaft quorum state for topic partitions.
/// </summary>
public sealed class DescribeQuorumRequest : IKafkaRequest<DescribeQuorumResponse>
{
    public static ApiKey ApiKey => ApiKey.DescribeQuorum;
    public static short LowestSupportedVersion => 0;
    public static short HighestSupportedVersion => 2;

    public required IReadOnlyList<DescribeQuorumRequestTopic> Topics { get; init; }

    public void Write(ref KafkaProtocolWriter writer, short version)
    {
        writer.WriteCompactArray(
            Topics,
            static (ref KafkaProtocolWriter w, DescribeQuorumRequestTopic topic) => topic.Write(ref w));
        writer.WriteEmptyTaggedFields();
    }
}

public sealed class DescribeQuorumRequestTopic
{
    public required string TopicName { get; init; }
    public required IReadOnlyList<DescribeQuorumRequestPartition> Partitions { get; init; }

    public void Write(ref KafkaProtocolWriter writer)
    {
        writer.WriteCompactString(TopicName);
        writer.WriteCompactArray(
            Partitions,
            static (ref KafkaProtocolWriter w, DescribeQuorumRequestPartition partition) => partition.Write(ref w));
        writer.WriteEmptyTaggedFields();
    }
}

public sealed class DescribeQuorumRequestPartition
{
    public int PartitionIndex { get; init; }

    public void Write(ref KafkaProtocolWriter writer)
    {
        writer.WriteInt32(PartitionIndex);
        writer.WriteEmptyTaggedFields();
    }
}
