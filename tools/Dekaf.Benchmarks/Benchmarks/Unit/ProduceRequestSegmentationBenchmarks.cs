using BenchmarkDotNet.Attributes;
using Dekaf.Internal;
using Dekaf.Networking;
using Dekaf.Protocol.Messages;
using Dekaf.Protocol.Records;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>
/// Guards zero-copy single-batch Produce framing across legacy and flexible versions.
/// The ArrayPool-backed metadata buffer is warmed and returned on every invocation.
/// </summary>
[MemoryDiagnoser]
[ShortRunJob]
public class ProduceRequestSegmentationBenchmarks
{
    private readonly KafkaConnection _connection = new("localhost", 9092, "benchmark-client");
    private readonly RecordBatch _batch;
    private readonly ProduceRequest _request;

    public ProduceRequestSegmentationBenchmarks()
    {
        _batch = new RecordBatch
        {
            BaseOffset = 17,
            PartitionLeaderEpoch = 3,
            LastOffsetDelta = 0,
            BaseTimestamp = 1234,
            MaxTimestamp = 1234,
            ProducerId = 42,
            ProducerEpoch = 2,
            BaseSequence = 7,
            Records = [new Record { IsKeyNull = true, Value = "value"u8.ToArray() }]
        };
        _batch.SetPreEncodedRecords("arena-backed-records"u8.ToArray());

        var partition = new ProduceRequestPartitionData
        {
            Index = 5,
            Records = [_batch]
        };
        var topic = new ProduceRequestTopicData
        {
            Name = "segment-topic",
            TopicId = new Guid("00112233-4455-6677-8899-aabbccddeeff")
        };
        topic.SetPartitionDataScratch([partition], 0, 1);
        _request = new ProduceRequest
        {
            TransactionalId = "tx-id",
            Acks = -1,
            TimeoutMs = 30_000
        };
        _request.SetTopicDataScratch([topic], 1);
    }

    [Params((short)7, (short)13)]
    public short Version { get; set; }

    [Benchmark]
    public int SerializeSingleBatch()
    {
        var segmented = _connection.TryPreSerializeSingleBatchProduceRequest(
            _request,
            correlationId: 123,
            Version,
            ProduceRequest.GetRequestHeaderVersion(Version),
            out var metadataArray,
            out var prefixLength,
            out var encodedRecords,
            out _,
            out var suffixLength);

        if (!segmented)
            throw new InvalidOperationException("Expected zero-copy Produce framing.");

        DekafPools.SerializationBuffers.Return(metadataArray, clearArray: false);
        return checked(prefixLength + (int)encodedRecords.Length + suffixLength);
    }
}
