using BenchmarkDotNet.Attributes;
using Dekaf.Benchmarks.Infrastructure;
using Dekaf.Consumer;
using Dekaf.Protocol.Records;
using Dekaf.Serialization;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>
/// One batch stream resumed across several queued fetches, each fully enumerated. Every
/// resumption runs the batch-completion path that decides whether the caller made progress
/// (a skipped batch is rewound for redelivery). The unit is one stream of FetchCount x 64
/// records; fetch seeding and the iterator are per stream, the progress check per batch.
/// </summary>
[MemoryDiagnoser]
public class ConsumerBatchResumeBenchmarks
{
    private const string Topic = "batch-resume";
    private const int RecordsPerFetch = 64;
    private KafkaConsumer<Ignore, ReadOnlyMemory<byte>> _consumer = null!;
    private Record[][] _records = null!;

    [Params(2, 16)]
    public int FetchCount { get; set; }

    [Params(false, true)]
    public bool Raw { get; set; }

    [GlobalSetup]
    public async Task Setup()
    {
        _records = [new Record[RecordsPerFetch]];
        for (var i = 0; i < RecordsPerFetch; i++)
            _records[0][i] = new Record { OffsetDelta = i, IsKeyNull = true, Value = new byte[16] };
        _consumer = new KafkaConsumer<Ignore, ReadOnlyMemory<byte>>(new ConsumerOptions
        {
            BootstrapServers = ["localhost:9092"], OffsetCommitMode = OffsetCommitMode.Manual,
            QueuedMinMessages = 1, MaxPollRecords = RecordsPerFetch
        }, Serializers.Ignore, Serializers.RawBytes);
        BufferedConsumerHarness.InitializeForBufferedFastPath(_consumer, Topic, 0);
        if (await Resume() != (long)FetchCount * RecordsPerFetch)
            throw new InvalidOperationException("Every queued fetch must be delivered once, in order.");
    }

    [Benchmark]
    public ValueTask<long> Resume() => Raw ? RawStream() : TypedStream();

    private void Seed()
    {
        BufferedConsumerHarness.DrainPendingFetches(_consumer);
        for (var fetch = 0; fetch < FetchCount; fetch++)
        {
            BufferedConsumerHarness.AppendPendingFetch(
                _consumer, Topic, 0, _records, 1, RecordsPerFetch, (long)fetch * RecordsPerFetch);
        }
    }

    private async ValueTask<long> TypedStream()
    {
        Seed();
        long next = 0;
        await foreach (var batch in _consumer.ConsumeBatchAsync())
        {
            foreach (var record in batch)
            {
                if (record.Offset != next)
                    throw new InvalidOperationException("Records must be delivered in order without gaps.");
                next++;
            }
            if (next == (long)FetchCount * RecordsPerFetch)
                break;
        }
        return next;
    }

    private async ValueTask<long> RawStream()
    {
        Seed();
        long next = 0;
        await foreach (var batch in _consumer.ConsumeRawBatchAsync())
        {
            foreach (var record in batch)
            {
                if (record.Offset != next)
                    throw new InvalidOperationException("Records must be delivered in order without gaps.");
                next++;
            }
            if (next == (long)FetchCount * RecordsPerFetch)
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
