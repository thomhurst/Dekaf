using System.Buffers;
using Dekaf.Compression;
using Dekaf.Errors;
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
    public static short HighestSupportedVersion => 12;
    internal const short ImplicitTransactionPartitionEnrollmentVersion = 12;

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

    internal bool TryGetSingleBatch(
        out ProduceRequestTopicData topic,
        out ProduceRequestPartitionData partition,
        out RecordBatch batch)
    {
        topic = null!;
        partition = null!;
        batch = null!;

        if (_topicDataScratch is { } topicDataScratch)
        {
            if (_topicDataScratchCount != 1)
                return false;

            topic = topicDataScratch[0];
        }
        else
        {
            if (TopicData.Count != 1)
                return false;

            topic = TopicData[0];
        }

        if (!topic.TryGetSinglePartition(out partition) || partition.Records.Count != 1)
            return false;

        batch = partition.Records[0];
        return true;
    }

    /// <summary>
    /// Number of topic entries regardless of whether the request was populated through
    /// <see cref="TopicData"/> or the internal scratch arrays. Pairs with
    /// <see cref="GetTopicEntry"/>; exists so tests can observe scratch-built requests
    /// without reflecting into private fields.
    /// </summary>
    internal int TopicEntryCount => _topicDataScratch is not null ? _topicDataScratchCount : TopicData.Count;

    internal ProduceRequestTopicData GetTopicEntry(int index) =>
        _topicDataScratch is { } topicDataScratch ? topicDataScratch[index] : TopicData[index];

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

    /// <summary>
    /// Number of partition entries regardless of whether the topic was populated through
    /// <see cref="PartitionData"/> or the internal scratch arrays. Pairs with
    /// <see cref="GetPartitionEntry"/>; exists so tests can observe scratch-built requests
    /// without reflecting into private fields.
    /// </summary>
    internal int PartitionEntryCount => _partitionDataScratch is not null ? _partitionDataScratchCount : PartitionData.Count;

    internal ProduceRequestPartitionData GetPartitionEntry(int index) =>
        _partitionDataScratch is { } partitionDataScratch
            ? partitionDataScratch[_partitionDataScratchStart + index]
            : PartitionData[index];

    internal bool TryGetSinglePartition(out ProduceRequestPartitionData partition)
    {
        partition = null!;
        if (_partitionDataScratch is { } partitionDataScratch)
        {
            if (_partitionDataScratchCount != 1)
                return false;

            partition = partitionDataScratch[_partitionDataScratchStart];
            return true;
        }

        if (PartitionData.Count != 1)
            return false;

        partition = PartitionData[0];
        return true;
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
            var actualRecordsLength = 0;
            for (var i = 0; i < Records.Count; i++)
            {
                checked
                {
                    actualRecordsLength += Records[i].Write(output, Compression, CompressionCodecs);
                }
            }

            if (actualRecordsLength != recordsLength)
            {
                // A batch changed between the size pass and the write pass (concurrent
                // mutation or pooled-object recycling). The declared records length no longer
                // matches the emitted bytes; sending this frame would desync the connection's
                // outgoing byte stream and the broker would misparse every subsequent request
                // (InvalidRequestException + socket close). Fail before it reaches the wire.
                throw new KafkaException(
                    ErrorCode.CorruptMessage,
                    $"PRODUCE framing mismatch for partition {Index}: declared records length {recordsLength} but serialized {actualRecordsLength} bytes across {Records.Count} batch(es); request discarded before reaching the wire.");
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
