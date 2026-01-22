using Dekaf.Protocol.Records;

namespace Dekaf.Protocol.Messages;

/// <summary>
/// Produce request (API key 0).
/// Sends records to topic partitions.
/// </summary>
public sealed class ProduceRequest : IKafkaRequest<ProduceResponse>
{
    public static ApiKey ApiKey => ApiKey.Produce;
    public static short LowestSupportedVersion => 0;
    public static short HighestSupportedVersion => 11;

    /// <summary>
    /// Transactional ID for exactly-once semantics (v3+).
    /// </summary>
    public string? TransactionalId { get; init; }

    /// <summary>
    /// Required acknowledgments.
    /// -1 = all in-sync replicas
    ///  0 = no acknowledgments (fire and forget)
    ///  1 = leader only
    /// </summary>
    public required short Acks { get; init; }

    /// <summary>
    /// Timeout in milliseconds.
    /// </summary>
    public required int TimeoutMs { get; init; }

    /// <summary>
    /// Topic data to produce.
    /// </summary>
    public required IReadOnlyList<ProduceRequestTopicData> TopicData { get; init; }

    public static bool IsFlexibleVersion(short version) => version >= 9;
    public static short GetRequestHeaderVersion(short version) => version >= 9 ? (short)2 : (short)1;
    public static short GetResponseHeaderVersion(short version) => version >= 9 ? (short)1 : (short)0;

    public void Write(ref KafkaProtocolWriter writer, short version)
    {
        var isFlexible = version >= 9;

        if (version >= 3)
        {
            if (isFlexible)
                writer.WriteCompactNullableString(TransactionalId);
            else
                writer.WriteString(TransactionalId);
        }

        writer.WriteInt16(Acks);
        writer.WriteInt32(TimeoutMs);

        if (isFlexible)
        {
            writer.WriteCompactArray(
                TopicData,
                (ref KafkaProtocolWriter w, ProduceRequestTopicData t) => t.Write(ref w, version));
        }
        else
        {
            writer.WriteArray(
                TopicData,
                (ref KafkaProtocolWriter w, ProduceRequestTopicData t) => t.Write(ref w, version));
        }

        if (isFlexible)
        {
            writer.WriteEmptyTaggedFields();
        }
    }
}

/// <summary>
/// Topic data in a produce request.
/// </summary>
public sealed class ProduceRequestTopicData
{
    /// <summary>
    /// Topic name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Partition data.
    /// </summary>
    public required IReadOnlyList<ProduceRequestPartitionData> PartitionData { get; init; }

    public void Write(ref KafkaProtocolWriter writer, short version)
    {
        var isFlexible = version >= 9;

        if (isFlexible)
            writer.WriteCompactString(Name);
        else
            writer.WriteString(Name);

        if (isFlexible)
        {
            writer.WriteCompactArray(
                PartitionData,
                (ref KafkaProtocolWriter w, ProduceRequestPartitionData p) => p.Write(ref w, version));
        }
        else
        {
            writer.WriteArray(
                PartitionData,
                (ref KafkaProtocolWriter w, ProduceRequestPartitionData p) => p.Write(ref w, version));
        }

        if (isFlexible)
        {
            writer.WriteEmptyTaggedFields();
        }
    }
}

/// <summary>
/// Partition data in a produce request.
/// </summary>
public sealed class ProduceRequestPartitionData
{
    /// <summary>
    /// Partition index.
    /// </summary>
    public required int Index { get; init; }

    /// <summary>
    /// Record batches to produce.
    /// </summary>
    public required IReadOnlyList<RecordBatch> Records { get; init; }

    public void Write(ref KafkaProtocolWriter writer, short version)
    {
        var isFlexible = version >= 9;

        writer.WriteInt32(Index);

        // Serialize records to a buffer first to get the length
        var recordsBuffer = new System.Buffers.ArrayBufferWriter<byte>();
        foreach (var batch in Records)
        {
            batch.Write(recordsBuffer);
        }

        if (isFlexible)
        {
            // COMPACT_RECORDS uses COMPACT_NULLABLE_BYTES encoding (length+1, 0 = null)
            writer.WriteCompactNullableBytes(recordsBuffer.WrittenSpan, isNull: false);
        }
        else
        {
            // RECORDS uses NULLABLE_BYTES encoding
            writer.WriteNullableBytes(recordsBuffer.WrittenSpan, isNull: false);
        }

        if (isFlexible)
        {
            writer.WriteEmptyTaggedFields();
        }
    }
}
