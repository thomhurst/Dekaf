using BenchmarkDotNet.Attributes;
using Dekaf.Benchmarks.Infrastructure;
using Dekaf.Consumer;
using Dekaf.Protocol.Records;
using Dekaf.Serialization;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>
/// Steady-state stream and Batch(n) delivery across released fetches. Reseeding is
/// amortized per fetch; no iteration setup or broker is required. Existing adjacent
/// stream fixtures use iteration setup, and the checkpoint fixture already has 12 cases.
/// </summary>
[MemoryDiagnoser]
public class ConsumerBufferedBatchBenchmarks
{
    private const string Topic = "buffered-batch-proof";
    private const int RecordsPerFetch = 1025;
    private const int FetchCount = 65;
    private const int MessageCount = RecordsPerFetch * FetchCount;
    private KafkaConsumer<Ignore, ReadOnlyMemory<byte>> _consumer = null!;
    private Record[][] _records = null!;

    [Params(65, 1025)]
    public int BatchSize { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        if (MessageCount % KafkaConsumer<Ignore, ReadOnlyMemory<byte>>.PollRefreshRecordInterval == 0
            || MessageCount % BatchSize != 0)
            throw new InvalidOperationException("End after a complete batch and off the poll-refresh boundary.");
        _records = [new Record[RecordsPerFetch]];
        var payload = new byte[16];
        for (var i = 0; i < RecordsPerFetch; i++)
            _records[0][i] = new Record { OffsetDelta = i, IsKeyNull = true, Value = payload };
        _consumer = new KafkaConsumer<Ignore, ReadOnlyMemory<byte>>(new ConsumerOptions
        {
            BootstrapServers = ["localhost:9092"], OffsetCommitMode = OffsetCommitMode.Auto,
            QueuedMinMessages = 1
        }, Serializers.Ignore, Serializers.RawBytes);
        BufferedConsumerHarness.InitializeForBufferedFastPath(_consumer, Topic, 0);
    }

    private void Seed()
    {
        BufferedConsumerHarness.DrainPendingFetches(_consumer);
        for (var i = 0; i < FetchCount; i++)
            BufferedConsumerHarness.AppendPendingFetch(_consumer, Topic, 0, _records, 1,
                RecordsPerFetch, (long)i * RecordsPerFetch);
    }

    [Benchmark(OperationsPerInvoke = MessageCount)]
    public async Task<int> Records()
    {
        Seed();
        using var cancellation = new CancellationTokenSource();
        var count = 0;
        await foreach (var record in _consumer.ConsumeAsync(cancellation.Token).ConfigureAwait(false))
            if (++count == MessageCount)
                cancellation.Cancel();
        return count;
    }

    [Benchmark(OperationsPerInvoke = MessageCount)]
    public async Task<int> BufferedBatches()
    {
        Seed();
        using var cancellation = new CancellationTokenSource();
        var count = 0;
        // MessageCount is divisible by both batch sizes, so the final pull proves the
        // last batch and cancellation stops before another broker fetch.
        await foreach (var batch in _consumer.ConsumeAsync(cancellation.Token).Batch(BatchSize).ConfigureAwait(false))
        {
            count += batch.Count;
            if (count == MessageCount)
                cancellation.Cancel();
        }
        return count;
    }

    [GlobalCleanup]
    public async Task Cleanup()
    {
        BufferedConsumerHarness.DrainPendingFetches(_consumer);
        await _consumer.DisposeAsync().ConfigureAwait(false);
    }
}
