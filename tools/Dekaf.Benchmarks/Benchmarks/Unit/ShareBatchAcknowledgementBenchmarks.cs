using System.Buffers;
using BenchmarkDotNet.Attributes;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.Protocol.Records;
using Dekaf.Serialization;
using Dekaf.ShareConsumer;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>
/// Measures fresh acknowledgement state in a repeatable batch lifecycle. Parsing, registration
/// and disposal are included so every invocation exercises first-use writes in steady state;
/// allocation results describe the complete batch, not acknowledgement writes alone.
/// </summary>
[MemoryDiagnoser]
public class ShareBatchAcknowledgementBenchmarks
{
    private readonly TopicPartition _partition = new("share-benchmark", 0);
    private KafkaShareConsumer<int, int> _consumer = null!;
    private ReadOnlyMemory<byte> _bytes;
    private ShareFetchAcquiredRecords[] _acquired = null!;

    [Params(64, 1024)]
    public int RecordCount { get; set; }

    [Params(ShareAcknowledgementMode.Implicit, ShareAcknowledgementMode.Explicit)]
    public ShareAcknowledgementMode Mode { get; set; }

    [GlobalSetup]
    public async ValueTask Setup()
    {
        var value = new ArrayBufferWriter<byte>();
        Serializers.Int32.Serialize(42, ref value, default);
        var records = new Record[RecordCount];
        for (var index = 0; index < records.Length; index++)
            records[index] = new Record { OffsetDelta = index, IsKeyNull = true, Value = value.WrittenMemory };
        var buffer = new ArrayBufferWriter<byte>();
        using (var batch = new RecordBatch { Records = records })
            batch.Write(buffer);
        _bytes = buffer.WrittenMemory;
        _acquired = [new ShareFetchAcquiredRecords { FirstOffset = 0, LastOffset = RecordCount - 1, DeliveryCount = 1 }];
        _consumer = new KafkaShareConsumer<int, int>(new ShareConsumerOptions
        {
            BootstrapServers = ["localhost:9092"],
            GroupId = "share-batch-acknowledgements",
            AcknowledgementMode = Mode
        }, Serializers.Int32, Serializers.Int32);

        using var tracked = new ShareBatchAcknowledgements<int, int>();
        using var parsed = await ParseAsync();
        tracked.Register(parsed);
        Acknowledge(parsed);
        var wire = tracked.Flush()[_partition];
        if (wire.Count != 1 || wire[0].FirstOffset != 0 || wire[0].LastOffset != RecordCount - 1)
            throw new InvalidOperationException("The batch acknowledgement range changed.");
        for (var index = 0; index < RecordCount; index++)
        {
            var expected = Mode == ShareAcknowledgementMode.Implicit ? AcknowledgeType.Accept : Disposition(index);
            if (wire[0].AcknowledgeTypes[index] != (byte)expected)
                throw new InvalidOperationException("A first-use acknowledgement changed disposition.");
        }
    }

    [Benchmark]
    public async ValueTask<int> ParseTrackAndAcknowledgeBatch()
    {
        using var tracker = new ShareBatchAcknowledgements<int, int>();
        using var batch = await ParseAsync();
        tracker.Register(batch);
        return Acknowledge(batch);
    }

    [Benchmark]
    public async ValueTask<int> ParseTrackAndCommitBatch()
    {
        using var tracker = new ShareBatchAcknowledgements<int, int>();
        using var batch = await ParseAsync();
        tracker.Register(batch);
        var delivered = Acknowledge(batch);
        var wire = tracker.Flush();
        tracker.ApplySuccessfulAcknowledgements(wire);
        return delivered;
    }

    [Benchmark]
    public async ValueTask<int> ParseTrackRedeliveryAndCommitBatch()
    {
        using var tracker = new ShareBatchAcknowledgements<int, int>();
        using var original = await ParseAsync();
        tracker.Register(original);
        Acknowledge(original);
        var older = tracker.Flush();

        using var redelivery = await ParseAsync();
        tracker.Register(redelivery);
        var delivered = Acknowledge(redelivery);
        var newer = tracker.Flush();
        tracker.ApplySuccessfulAcknowledgements(older);
        tracker.ApplySuccessfulAcknowledgements(newer);
        return delivered;
    }

    [Benchmark]
    public async ValueTask<int> ParseTrackAndAbandonBatch()
    {
        using var tracker = new ShareBatchAcknowledgements<int, int>();
        using var batch = await ParseAsync();
        tracker.Register(batch);
        batch.Dispose();
        return tracker.RetainedBatchCount;
    }

    [GlobalCleanup]
    public ValueTask Cleanup() => _consumer.DisposeAsync();

    private ValueTask<ShareConsumeBatch<int, int>> ParseAsync()
    {
        var reader = new KafkaProtocolReader(_bytes);
        return _consumer.ParseRecordBatchAsync(_partition, RecordBatch.Read(ref reader), _acquired,
            RecordCount, CancellationToken.None);
    }

    private int Acknowledge(ShareConsumeBatch<int, int> batch)
    {
        foreach (var record in batch)
        {
            if (Mode == ShareAcknowledgementMode.Explicit)
                batch.Acknowledge(record, Disposition((int)record.Offset));
        }
        return batch.DeliveredCount;
    }

    private static AcknowledgeType Disposition(int index) => (AcknowledgeType)(1 + index % 3);
}
