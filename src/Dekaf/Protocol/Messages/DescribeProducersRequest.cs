namespace Dekaf.Protocol.Messages;

/// <summary>
/// DescribeProducers request (API key 61).
/// Lists active producers for topic partitions.
/// </summary>
public sealed class DescribeProducersRequest : IKafkaRequest<DescribeProducersResponse>
{
    public static ApiKey ApiKey => ApiKey.DescribeProducers;
    public static short LowestSupportedVersion => 0;
    public static short HighestSupportedVersion => 0;

    public required IReadOnlyList<DescribeProducersRequestTopic> Topics { get; init; }

    public void Write(ref KafkaProtocolWriter writer, short version)
    {
        writer.WriteCompactArray(
            Topics,
            static (ref KafkaProtocolWriter w, DescribeProducersRequestTopic topic) => topic.Write(ref w));

        writer.WriteEmptyTaggedFields();
    }
}

/// <summary>
/// Topic in a DescribeProducers request.
/// </summary>
public sealed class DescribeProducersRequestTopic
{
    public required string Name { get; init; }
    public required IReadOnlyList<int> PartitionIndexes { get; init; }

    public void Write(ref KafkaProtocolWriter writer)
    {
        writer.WriteCompactString(Name);

        writer.WriteCompactArray(
            PartitionIndexes,
            static (ref KafkaProtocolWriter w, int partitionIndex) => w.WriteInt32(partitionIndex));

        writer.WriteEmptyTaggedFields();
    }
}
