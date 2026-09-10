using System.Buffers;
using BenchmarkDotNet.Attributes;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.Protocol.Records;
using Dekaf.Serialization;
using Dekaf.ShareConsumer;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>Measures response registration and bounded renewal replay as retained work grows.</summary>
[MemoryDiagnoser]
public class ShareBatchScalingBenchmarks
{
    private readonly TopicPartition _partition = new("share-scaling", 0);
    private KafkaShareConsumer<int, int> _consumer = null!;
    private ReadOnlyMemory<byte> _singleRecord;
    private ReadOnlyMemory<byte> _records;
    private ShareFetchAcquiredRecords[] _acquired = null!;
    private long[] _offsets = null!;

    [Params(64, 1024)]
    public int RecordCount { get; set; }

    [Params(ShareAcquisitionShape.Contiguous, ShareAcquisitionShape.SparseRanges, ShareAcquisitionShape.InterleavedRanges)]
    public ShareAcquisitionShape AcquisitionShape { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _consumer = new KafkaShareConsumer<int, int>(new ShareConsumerOptions
        {
            BootstrapServers = ["127.0.0.1:9092"], GroupId = "share-scaling",
            AcknowledgementMode = ShareAcknowledgementMode.Explicit
        }, Serializers.Int32, Serializers.Int32);
        _singleRecord = Encode(1);
        _records = Encode(RecordCount);
        _acquired = ShareAcquisitionFixture.Create(0, RecordCount, AcquisitionShape);
        _offsets = ShareAcquisitionFixture.Offsets(_acquired);
        if (RegisterResponse() != _offsets.Length || ReplayInChunks() != _offsets.Length)
            throw new InvalidOperationException("The scaling fixture lost records.");
    }

    [Benchmark]
    public int RegisterResponse()
    {
        using var tracker = new ShareBatchAcknowledgements<int, int>();
        using (tracker.BeginDelivery())
        {
            foreach (var offset in _offsets)
            {
                using var batch = Parse(_singleRecord, offset, 1);
                tracker.Register(batch);
                foreach (var record in batch)
                    batch.Acknowledge(record);
            }
        }
        var retained = tracker.RetainedBatchCount;
        tracker.ApplySuccessfulAcknowledgements(tracker.Flush());
        if (tracker.RetainedBatchCount != 0)
            throw new InvalidOperationException("Completed response storage was retained.");
        return retained;
    }

    [Benchmark]
    public int ReplayInChunks()
    {
        using var tracker = new ShareBatchAcknowledgements<int, int>();
        using var batch = Parse(_records, 0, RecordCount);
        tracker.Register(batch);
        foreach (var record in batch)
            batch.Acknowledge(record, AcknowledgeType.Renew);
        tracker.ApplySuccessfulAcknowledgements(tracker.Flush());
        var completed = 0;
        while (tracker.GetReplays(16) is { } replays)
        {
            using (tracker.BeginDelivery())
            {
                foreach (var replay in replays)
                {
                    using (replay)
                    {
                        foreach (var record in replay)
                        {
                            if (completed >= _offsets.Length || record.Offset != _offsets[completed++])
                                throw new InvalidOperationException("Replay order or cardinality changed.");
                            replay.Acknowledge(record);
                        }
                    }
                }
            }
        }
        tracker.ApplySuccessfulAcknowledgements(tracker.Flush());
        if (tracker.RetainedBatchCount != 0)
            throw new InvalidOperationException("Completed replay storage was retained.");
        return completed;
    }

    private ShareConsumeBatch<int, int> Parse(ReadOnlyMemory<byte> bytes, long offset, int count)
    {
        var reader = new KafkaProtocolReader(bytes);
        var source = RecordBatch.Read(ref reader);
        source.BaseOffset = offset;
        var parsed = _consumer.ParseRecordBatchAsync(_partition, source, _acquired, count, default);
        if (!parsed.IsCompletedSuccessfully)
            throw new InvalidOperationException("The scaling fixture must parse synchronously.");
        return parsed.GetAwaiter().GetResult();
    }

    private static ReadOnlyMemory<byte> Encode(int count)
    {
        var value = new ArrayBufferWriter<byte>();
        Serializers.Int32.Serialize(42, ref value, default);
        var records = new Record[count];
        for (var index = 0; index < count; index++)
            records[index] = new Record { OffsetDelta = index, IsKeyNull = true, Value = value.WrittenMemory };
        var buffer = new ArrayBufferWriter<byte>();
        using var batch = new RecordBatch { Records = records };
        batch.Write(buffer);
        return buffer.WrittenMemory;
    }

    [GlobalCleanup]
    public ValueTask Cleanup() => _consumer.DisposeAsync();
}
