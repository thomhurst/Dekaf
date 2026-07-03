namespace Dekaf.Protocol.Messages;

/// <summary>
/// DescribeProducers response (API key 61).
/// Contains active producers for topic partitions.
/// </summary>
public sealed class DescribeProducersResponse : IKafkaResponse
{
    public static ApiKey ApiKey => ApiKey.DescribeProducers;
    public static short LowestSupportedVersion => 0;
    public static short HighestSupportedVersion => 0;

    public int ThrottleTimeMs { get; init; }
    public required IReadOnlyList<DescribeProducersResponseTopic> Topics { get; init; }

    public static IKafkaResponse Read(ref KafkaProtocolReader reader, short version)
    {
        var throttleTimeMs = reader.ReadInt32();
        var topics = reader.ReadCompactArray(
            static (ref KafkaProtocolReader r) => DescribeProducersResponseTopic.Read(ref r));

        reader.SkipTaggedFields();

        return new DescribeProducersResponse
        {
            ThrottleTimeMs = throttleTimeMs,
            Topics = topics
        };
    }
}

/// <summary>
/// Topic in a DescribeProducers response.
/// </summary>
public sealed class DescribeProducersResponseTopic
{
    public required string Name { get; init; }
    public required IReadOnlyList<DescribeProducersResponsePartition> Partitions { get; init; }

    public static DescribeProducersResponseTopic Read(ref KafkaProtocolReader reader)
    {
        var name = reader.ReadCompactNonNullableString();
        var partitions = reader.ReadCompactArray(
            static (ref KafkaProtocolReader r) => DescribeProducersResponsePartition.Read(ref r));

        reader.SkipTaggedFields();

        return new DescribeProducersResponseTopic
        {
            Name = name,
            Partitions = partitions
        };
    }
}

/// <summary>
/// Partition in a DescribeProducers response.
/// </summary>
public sealed class DescribeProducersResponsePartition
{
    public int PartitionIndex { get; init; }
    public ErrorCode ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }
    public required IReadOnlyList<DescribeProducersResponseProducer> ActiveProducers { get; init; }

    public static DescribeProducersResponsePartition Read(ref KafkaProtocolReader reader)
    {
        var partitionIndex = reader.ReadInt32();
        var errorCode = (ErrorCode)reader.ReadInt16();
        var errorMessage = reader.ReadCompactString();
        var activeProducers = reader.ReadCompactArray(
            static (ref KafkaProtocolReader r) => DescribeProducersResponseProducer.Read(ref r));

        reader.SkipTaggedFields();

        return new DescribeProducersResponsePartition
        {
            PartitionIndex = partitionIndex,
            ErrorCode = errorCode,
            ErrorMessage = errorMessage,
            ActiveProducers = activeProducers
        };
    }
}

/// <summary>
/// Active producer in a DescribeProducers response partition.
/// </summary>
public sealed class DescribeProducersResponseProducer
{
    public long ProducerId { get; init; }
    public int ProducerEpoch { get; init; }
    public int LastSequence { get; init; }
    public long LastTimestamp { get; init; }
    public int CoordinatorEpoch { get; init; }
    public long CurrentTxnStartOffset { get; init; }

    public static DescribeProducersResponseProducer Read(ref KafkaProtocolReader reader)
    {
        var producerId = reader.ReadInt64();
        var producerEpoch = reader.ReadInt32();
        var lastSequence = reader.ReadInt32();
        var lastTimestamp = reader.ReadInt64();
        var coordinatorEpoch = reader.ReadInt32();
        var currentTxnStartOffset = reader.ReadInt64();

        reader.SkipTaggedFields();

        return new DescribeProducersResponseProducer
        {
            ProducerId = producerId,
            ProducerEpoch = producerEpoch,
            LastSequence = lastSequence,
            LastTimestamp = lastTimestamp,
            CoordinatorEpoch = coordinatorEpoch,
            CurrentTxnStartOffset = currentTxnStartOffset
        };
    }
}
