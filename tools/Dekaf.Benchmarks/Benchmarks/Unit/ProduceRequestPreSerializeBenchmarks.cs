using System.Buffers;
using System.Buffers.Binary;
using BenchmarkDotNet.Attributes;
using Dekaf.Benchmarks.Infrastructure;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.Protocol.Records;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>
/// Compares the two ways KafkaConnection can turn a coalesced (multi-batch) produce request
/// into wire bytes:
/// <list type="bullet">
/// <item><description><see cref="CopyingPreSerialize"/> — the fallback path: serialize the
/// whole frame (record bytes included) into one rented array, mirroring
/// <c>KafkaConnection.PreSerializeRequest</c>.</description></item>
/// <item><description><see cref="SegmentedPreSerialize"/> — the zero-body-copy path:
/// <c>TryPreSerializeSegmentedProduceRequest</c> serializes only metadata and references the
/// record bytes in place.</description></item>
/// </list>
/// The workload mirrors the coalescing stress lane: one topic, 4 partitions, one sealed
/// ~148KB batch each (~592KB of record bytes per request). The delta is the send-loop CPU and
/// memory traffic the segmented path saves per produce request; both paths must report 0 B
/// allocated (all buffers pooled).
/// </summary>
[MemoryDiagnoser]
[ThroughputJob]
public class ProduceRequestPreSerializeBenchmarks
{
    private const short ApiVersion = 12;
    private const int PartitionCount = 4;
    private const int RecordBytesPerBatch = 148 * 1024;

    private KafkaConnection _connection = null!;
    private ProduceRequest _request = null!;
    private RequestHeader _header;
    private short _headerVersion;
    private int _frameLength;

    [GlobalSetup]
    public void Setup()
    {
        var partitions = new ProduceRequestPartitionData[PartitionCount];
        for (var p = 0; p < PartitionCount; p++)
        {
            var recordBytes = new byte[RecordBytesPerBatch];
            Random.Shared.NextBytes(recordBytes);
            var batch = new RecordBatch
            {
                BaseOffset = p * 1000,
                LastOffsetDelta = 999,
                BaseTimestamp = 1_700_000_000_000,
                MaxTimestamp = 1_700_000_000_999,
                ProducerId = 42,
                ProducerEpoch = 2,
                BaseSequence = p * 1000,
                Records = [new Record { IsKeyNull = true, Value = "value"u8.ToArray() }]
            };
            batch.SetPreEncodedRecords(recordBytes);
            partitions[p] = new ProduceRequestPartitionData
            {
                Index = p,
                Records = [batch]
            };
        }

        _request = new ProduceRequest
        {
            Acks = -1,
            TimeoutMs = 30_000,
            TopicData =
            [
                new ProduceRequestTopicData
                {
                    Name = "bench-topic",
                    PartitionData = partitions
                }
            ]
        };

        _headerVersion = KafkaMessageMetadata<ProduceRequest, ProduceResponse>
            .GetRequestHeaderVersion(ApiVersion);
        _header = new RequestHeader
        {
            ApiKey = ApiKey.Produce,
            ApiVersion = ApiVersion,
            CorrelationId = 7,
            ClientId = "bench-client",
            HeaderVersion = _headerVersion
        };

        _connection = new KafkaConnection("localhost", 9092, "bench-client");

        // Size the copying path's initial rent like production does (the request size hint
        // covers the whole body), so neither benchmark pays RentedBufferWriter growth copies.
        _frameLength = CopyingPreSerialize();
    }

    [GlobalCleanup]
    public void Cleanup()
        => _connection.DisposeAsync().AsTask().GetAwaiter().GetResult();

    [Benchmark(Baseline = true)]
    public int CopyingPreSerialize()
    {
        var initialCapacity = _frameLength > 0 ? _frameLength : 4096;
        using var writer = new RentedBufferWriter(initialCapacity, 4);
        var protocolWriter = new KafkaProtocolWriter(writer);
        _header.Write(ref protocolWriter);
        _request.Write(ref protocolWriter, ApiVersion);
        var (array, length) = writer.DetachBuffer();
        BinaryPrimitives.WriteInt32BigEndian(array, protocolWriter.BytesWritten);
        DekafPools.SerializationBuffers.Return(array, clearArray: false);
        return length;
    }

    [Benchmark]
    public int SegmentedPreSerialize()
    {
        if (!_connection.TryPreSerializeSegmentedProduceRequest(
                _request,
                correlationId: 7,
                ApiVersion,
                _headerVersion,
                out var metadataArray,
                out var frameSegments,
                out var frameSegmentCount))
        {
            throw new InvalidOperationException("Segmented produce pre-serialization unexpectedly fell back.");
        }

        var totalLength = 0;
        for (var i = 0; i < frameSegmentCount; i++)
            totalLength += frameSegments[i].Count;

        // Mirror the production return contract: clear only the written prefix.
        Array.Clear(frameSegments, 0, frameSegmentCount);
        ArrayPool<KafkaConnection.SegmentedFrameSegment>.Shared.Return(frameSegments, clearArray: false);
        DekafPools.SerializationBuffers.Return(metadataArray, clearArray: false);
        return totalLength;
    }
}
