using System.Buffers;
using Dekaf.Compression;
using Dekaf.Protocol.Records;

namespace Dekaf.Protocol.Messages;

/// <summary>
/// Produce request (API key 0).
/// Sends records to topic partitions.
/// </summary>
public sealed class ProduceRequest : IKafkaRequest<ProduceResponse>, IKafkaRequestBodySizeHint
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

    private ProduceRequestTopicData[]? _topicDataScratch;
    private int _topicDataScratchCount;

    internal int RequestBodySizeHint { get; set; }

    int IKafkaRequestBodySizeHint.RequestBodySizeHint => RequestBodySizeHint;

    internal void SetTopicDataScratch(ProduceRequestTopicData[] topicData, int count)
    {
        _topicDataScratch = topicData;
        _topicDataScratchCount = count;
    }

    internal void ClearTopicDataScratch()
    {
        _topicDataScratch = null;
        _topicDataScratchCount = 0;
    }

    public void Write(ref KafkaProtocolWriter writer, short version)
    {
        writer.WriteCompactNullableString(TransactionalId);
        writer.WriteInt16(Acks);
        writer.WriteInt32(TimeoutMs);

        if (_topicDataScratch is { } topicDataScratch)
        {
            writer.WriteCompactArray(
                topicDataScratch.AsSpan(0, _topicDataScratchCount),
                static (ref KafkaProtocolWriter w, ProduceRequestTopicData t, short v) => t.Write(ref w, v),
                version);
        }
        else
        {
            writer.WriteCompactArray(
                TopicData,
                static (ref KafkaProtocolWriter w, ProduceRequestTopicData t, short v) => t.Write(ref w, v),
                version);
        }

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

    private ProduceRequestPartitionData[]? _partitionDataScratch;
    private int _partitionDataScratchStart;
    private int _partitionDataScratchCount;

    internal void SetPartitionDataScratch(ProduceRequestPartitionData[] partitionData, int start, int count)
    {
        _partitionDataScratch = partitionData;
        _partitionDataScratchStart = start;
        _partitionDataScratchCount = count;
    }

    internal void ClearPartitionDataScratch()
    {
        _partitionDataScratch = null;
        _partitionDataScratchStart = 0;
        _partitionDataScratchCount = 0;
    }

    public void Write(ref KafkaProtocolWriter writer, short version)
    {
        writer.WriteCompactString(Name);

        if (_partitionDataScratch is { } partitionDataScratch)
        {
            writer.WriteCompactArray(
                partitionDataScratch.AsSpan(_partitionDataScratchStart, _partitionDataScratchCount),
                static (ref KafkaProtocolWriter w, ProduceRequestPartitionData p, short v) => p.Write(ref w, v),
                version);
        }
        else
        {
            writer.WriteCompactArray(
                PartitionData,
                static (ref KafkaProtocolWriter w, ProduceRequestPartitionData p, short v) => p.Write(ref w, v),
                version);
        }

        writer.WriteEmptyTaggedFields();
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

        RecordBatch[]? temporaryPreCompressed = null;
        var temporaryPreCompressedCount = 0;

        try
        {
            var recordsLength = 0;
            for (var i = 0; i < Records.Count; i++)
            {
                var batch = Records[i];
                if (Compression != CompressionType.None && !batch.HasPreCompressedRecords)
                {
                    batch.PreCompress(Compression, CompressionCodecs);
                    temporaryPreCompressed ??= ArrayPool<RecordBatch>.Shared.Rent(Records.Count);
                    temporaryPreCompressed[temporaryPreCompressedCount++] = batch;
                }

                checked
                {
                    recordsLength += batch.GetEncodedSize(Compression);
                }
            }

            // COMPACT_RECORDS uses COMPACT_NULLABLE_BYTES encoding (length+1, 0 = null).
            writer.WriteUnsignedVarInt(checked(recordsLength + 1));
            var output = writer.BufferWriter;
            for (var i = 0; i < Records.Count; i++)
            {
                Records[i].Write(output, Compression, CompressionCodecs);
            }
            writer.AddBytesWritten(recordsLength);
        }
        finally
        {
            if (temporaryPreCompressed is not null)
            {
                for (var i = 0; i < temporaryPreCompressedCount; i++)
                {
                    temporaryPreCompressed[i].ReturnPreCompressedBuffer();
                }

                ArrayPool<RecordBatch>.Shared.Return(temporaryPreCompressed, clearArray: true);
            }
        }

        writer.WriteEmptyTaggedFields();
    }
}
