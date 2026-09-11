using BenchmarkDotNet.Attributes;
using Dekaf.Benchmarks.Infrastructure;
using Dekaf.Consumer;
using Dekaf.Protocol.Records;
using Dekaf.Serialization;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>
/// Measures typed/raw traversal with and without capturing each window's next offset.
/// Larger fetches span two windows, exercising both resumption and disposal invalidation.
/// Older revisions reconstruct the same checkpoint from delivered records because the API
/// is additive. Delegate binding and correctness validation occur outside measurement.
/// </summary>
[MemoryDiagnoser]
public class ConsumerBatchCheckpointBenchmarks
{
    private delegate bool TypedCapture(ConsumeBatch<Ignore, ReadOnlyMemory<byte>> batch, out TopicPartitionOffset checkpoint);
    private delegate bool RawCapture(ConsumeRawBatch batch, out TopicPartitionOffset checkpoint);
    private TypedCapture? _typedCapture;
    private RawCapture? _rawCapture;
    private KafkaConsumer<Ignore, ReadOnlyMemory<byte>> _consumer = null!;
    private Record[][] _records = null!;
    private const string Topic = "batch-checkpoint";

    [Params(1, 64, 1024)]
    public int RecordCount { get; set; }

    [Params(false, true)]
    public bool Capture { get; set; }

    [GlobalSetup]
    public async Task Setup()
    {
        _records = [new Record[RecordCount]];
        for (var i = 0; i < RecordCount; i++)
            _records[0][i] = new Record { OffsetDelta = i, IsKeyNull = true, Value = new byte[16] };
        _consumer = new KafkaConsumer<Ignore, ReadOnlyMemory<byte>>(new ConsumerOptions
        {
            BootstrapServers = ["localhost:9092"], OffsetCommitMode = OffsetCommitMode.Manual,
            QueuedMinMessages = 1, MaxPollRecords = Math.Max(1, RecordCount / 2)
        }, Serializers.Ignore, Serializers.RawBytes);
        BufferedConsumerHarness.InitializeForBufferedFastPath(_consumer, Topic, 0);
        _typedCapture = typeof(ConsumeBatch<Ignore, ReadOnlyMemory<byte>>).GetMethod("TryGetNextOffset")
            ?.CreateDelegate<TypedCapture>();
        _rawCapture = typeof(ConsumeRawBatch).GetMethod("TryGetNextOffset")?.CreateDelegate<RawCapture>();
        if (await TypedWindow() != RecordCount || await RawWindow() != RecordCount)
            throw new InvalidOperationException("Each window must consume and checkpoint all seeded records.");
    }

    [Benchmark]
    public async ValueTask<long> TypedWindow()
    {
        BufferedConsumerHarness.ReseedPendingFetches(_consumer, Topic, 0, _records, 1, RecordCount);
        long next = 0;
        await foreach (var batch in _consumer.ConsumeBatchAsync())
        {
            foreach (var record in batch)
                next = record.Offset + 1;
            if (Capture && _typedCapture is { } capture)
            {
                if (!capture(batch, out var checkpoint))
                    throw new InvalidOperationException("A fully traversed window must provide a checkpoint.");
                next = checkpoint.Offset;
            }
            if (next == RecordCount)
                break;
        }
        return next;
    }

    [Benchmark]
    public async ValueTask<long> RawWindow()
    {
        BufferedConsumerHarness.ReseedPendingFetches(_consumer, Topic, 0, _records, 1, RecordCount);
        long next = 0;
        await foreach (var batch in _consumer.ConsumeRawBatchAsync())
        {
            foreach (var record in batch)
                next = record.Offset + 1;
            if (Capture && _rawCapture is { } capture)
            {
                if (!capture(batch, out var checkpoint))
                    throw new InvalidOperationException("A fully traversed window must provide a checkpoint.");
                next = checkpoint.Offset;
            }
            if (next == RecordCount)
                break;
        }
        return next;
    }

    [GlobalCleanup]
    public async Task Cleanup()
    {
        BufferedConsumerHarness.DrainPendingFetches(_consumer);
        await _consumer.DisposeAsync();
    }
}
