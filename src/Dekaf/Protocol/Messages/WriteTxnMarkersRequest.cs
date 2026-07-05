namespace Dekaf.Protocol.Messages;

/// <summary>
/// WriteTxnMarkers request (API key 27).
/// Writes transaction commit or abort markers to topic partitions.
/// </summary>
public sealed class WriteTxnMarkersRequest : IKafkaRequest<WriteTxnMarkersResponse>
{
    public static ApiKey ApiKey => ApiKey.WriteTxnMarkers;
    public static short LowestSupportedVersion => 1;
    public static short HighestSupportedVersion => 2;

    public required IReadOnlyList<WriteTxnMarkersRequestMarker> Markers { get; init; }

    public void Write(ref KafkaProtocolWriter writer, short version)
    {
        writer.WriteCompactArray(
            Markers,
            static (ref KafkaProtocolWriter w, WriteTxnMarkersRequestMarker marker, short v) => marker.Write(ref w, v),
            version);

        writer.WriteEmptyTaggedFields();
    }
}

/// <summary>
/// Transaction marker in a WriteTxnMarkers request.
/// </summary>
public sealed class WriteTxnMarkersRequestMarker
{
    public long ProducerId { get; init; }
    public short ProducerEpoch { get; init; }
    public bool TransactionResult { get; init; }
    public required IReadOnlyList<WriteTxnMarkersRequestTopic> Topics { get; init; }
    public int CoordinatorEpoch { get; init; }
    public sbyte TransactionVersion { get; init; }

    public void Write(ref KafkaProtocolWriter writer, short version)
    {
        writer.WriteInt64(ProducerId);
        writer.WriteInt16(ProducerEpoch);
        writer.WriteBoolean(TransactionResult);

        writer.WriteCompactArray(
            Topics,
            static (ref KafkaProtocolWriter w, WriteTxnMarkersRequestTopic topic) => topic.Write(ref w));

        writer.WriteInt32(CoordinatorEpoch);

        if (version >= 2)
        {
            writer.WriteInt8(TransactionVersion);
        }

        writer.WriteEmptyTaggedFields();
    }
}

/// <summary>
/// Topic in a WriteTxnMarkers request marker.
/// </summary>
public sealed class WriteTxnMarkersRequestTopic
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
