namespace Dekaf.Protocol.Messages;

/// <summary>
/// WriteTxnMarkers response (API key 27).
/// Contains per-partition transaction marker write results.
/// </summary>
public sealed class WriteTxnMarkersResponse : IKafkaResponse
{
    public static ApiKey ApiKey => ApiKey.WriteTxnMarkers;
    public static short LowestSupportedVersion => 1;
    public static short HighestSupportedVersion => 2;

    public required IReadOnlyList<WriteTxnMarkersResponseMarker> Markers { get; init; }

    public static IKafkaResponse Read(ref KafkaProtocolReader reader, short version)
    {
        var markers = reader.ReadCompactArray(
            static (ref KafkaProtocolReader r, short v) => WriteTxnMarkersResponseMarker.Read(ref r, v),
            version);

        reader.SkipTaggedFields();

        return new WriteTxnMarkersResponse
        {
            Markers = markers
        };
    }
}

/// <summary>
/// Transaction marker result in a WriteTxnMarkers response.
/// </summary>
public sealed class WriteTxnMarkersResponseMarker
{
    public long ProducerId { get; init; }
    public required IReadOnlyList<WriteTxnMarkersResponseTopic> Topics { get; init; }

    public static WriteTxnMarkersResponseMarker Read(ref KafkaProtocolReader reader, short version)
    {
        var producerId = reader.ReadInt64();
        var topics = reader.ReadCompactArray(
            static (ref KafkaProtocolReader r, short v) => WriteTxnMarkersResponseTopic.Read(ref r, v),
            version);

        reader.SkipTaggedFields();

        return new WriteTxnMarkersResponseMarker
        {
            ProducerId = producerId,
            Topics = topics
        };
    }
}

/// <summary>
/// Topic result in a WriteTxnMarkers response marker.
/// </summary>
public sealed class WriteTxnMarkersResponseTopic
{
    public required string Name { get; init; }
    public required IReadOnlyList<WriteTxnMarkersResponsePartition> Partitions { get; init; }

    public static WriteTxnMarkersResponseTopic Read(ref KafkaProtocolReader reader, short version)
    {
        var name = reader.ReadCompactNonNullableString();
        var partitions = reader.ReadCompactArray(
            static (ref KafkaProtocolReader r, short v) => WriteTxnMarkersResponsePartition.Read(ref r, v),
            version);

        reader.SkipTaggedFields();

        return new WriteTxnMarkersResponseTopic
        {
            Name = name,
            Partitions = partitions
        };
    }
}

/// <summary>
/// Partition result in a WriteTxnMarkers response topic.
/// </summary>
public sealed class WriteTxnMarkersResponsePartition
{
    public int PartitionIndex { get; init; }
    public ErrorCode ErrorCode { get; init; }

    public static WriteTxnMarkersResponsePartition Read(ref KafkaProtocolReader reader, short version)
    {
        var partitionIndex = reader.ReadInt32();
        var errorCode = (ErrorCode)reader.ReadInt16();

        reader.SkipTaggedFields();

        return new WriteTxnMarkersResponsePartition
        {
            PartitionIndex = partitionIndex,
            ErrorCode = errorCode
        };
    }
}
