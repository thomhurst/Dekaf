using Dekaf.Compression;
using Dekaf.Protocol.Records;

namespace Dekaf.Protocol.Messages;

/// <summary>
/// Produce request (API key 0).
/// Sends records to topic partitions.
/// </summary>
public sealed class ProduceRequest : IKafkaRequest<ProduceResponse>
{
    public static ApiKey ApiKey => ApiKey.Produce;
    public static short LowestSupportedVersion => 9;
    public static short HighestSupportedVersion => 11;

    /// <summary>
    /// Transactional ID for exactly-once semantics (v3+).
    /// </summary>
    public string? TransactionalId { get; internal set; }

    /// <summary>
    /// Required acknowledgments.
    /// -1 = all in-sync replicas
    ///  0 = no acknowledgments (fire and forget)
    ///  1 = leader only
    /// </summary>
    public short Acks { get; internal set; }

    /// <summary>
    /// Timeout in milliseconds.
    /// </summary>
    public int TimeoutMs { get; internal set; }

    /// <summary>
    /// Topic data to produce.
    /// </summary>
    public IReadOnlyList<ProduceRequestTopicData> TopicData { get; internal set; } = [];

    public void Write(ref KafkaProtocolWriter writer, short version)
    {
        writer.WriteCompactNullableString(TransactionalId);
        writer.WriteInt16(Acks);
        writer.WriteInt32(TimeoutMs);

        writer.WriteCompactArray(
            TopicData,
            static (ref KafkaProtocolWriter w, ProduceRequestTopicData t, short v) => t.Write(ref w, v),
            version);

        writer.WriteEmptyTaggedFields();
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
    public string Name { get; internal set; } = string.Empty;

    /// <summary>
    /// Partition data.
    /// </summary>
    public IReadOnlyList<ProduceRequestPartitionData> PartitionData { get; internal set; } = [];

    public void Write(ref KafkaProtocolWriter writer, short version)
    {
        writer.WriteCompactString(Name);

        writer.WriteCompactArray(
            PartitionData,
            static (ref KafkaProtocolWriter w, ProduceRequestPartitionData p, short v) => p.Write(ref w, v),
            version);

        writer.WriteEmptyTaggedFields();
    }
}

/// <summary>
/// Partition data in a produce request.
/// </summary>
public sealed class ProduceRequestPartitionData
{
    // Thread-local reusable buffer for record serialization
    [ThreadStatic]
    private static System.Buffers.ArrayBufferWriter<byte>? t_recordsBuffer;

    private static System.Buffers.ArrayBufferWriter<byte> GetRecordsBuffer()
    {
        var buffer = t_recordsBuffer;
        if (buffer is null)
        {
            buffer = new System.Buffers.ArrayBufferWriter<byte>(8192);
            t_recordsBuffer = buffer;
        }
        else
        {
            buffer.Clear();
        }
        return buffer;
    }

    /// <summary>
    /// Partition index.
    /// </summary>
    public int Index { get; internal set; }

    /// <summary>
    /// Record batches to produce.
    /// </summary>
    public IReadOnlyList<RecordBatch> Records { get; internal set; } = [];

    /// <summary>
    /// Compression type to apply to record batches.
    /// </summary>
    public CompressionType Compression { get; internal set; } = CompressionType.None;

    /// <summary>
    /// Compression codec registry to use for compression.
    /// When null, the default registry is used.
    /// </summary>
    public CompressionCodecRegistry? CompressionCodecs { get; internal set; }

    public void Write(ref KafkaProtocolWriter writer, short version)
    {
        writer.WriteInt32(Index);

        // Serialize records to a thread-local buffer to avoid per-partition allocation
        var recordsBuffer = GetRecordsBuffer();
        foreach (var batch in Records)
        {
            batch.Write(recordsBuffer, Compression, CompressionCodecs);
        }

        // COMPACT_RECORDS uses COMPACT_NULLABLE_BYTES encoding (length+1, 0 = null)
        writer.WriteCompactNullableBytes(recordsBuffer.WrittenSpan, isNull: false);

        writer.WriteEmptyTaggedFields();
    }
}
