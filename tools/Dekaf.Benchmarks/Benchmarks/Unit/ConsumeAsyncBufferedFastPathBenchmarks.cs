using System.Text;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using Dekaf.Benchmarks.Infrastructure;
using Dekaf.Consumer;
using Dekaf.Protocol.Records;
using Dekaf.Serialization;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>
/// Docker-free benchmark for the streaming <c>ConsumeAsync</c> buffered drain loop.
/// Pending records are seeded directly so the result isolates per-record iteration,
/// deserialization, result construction, position tracking, and async-iterator overhead.
/// </summary>
/// <remarks>
/// The extended warmup is intentional: tiered PGO recompiles the async iterator through
/// several stages, and fewer warmups measure those transitions instead of steady state.
/// </remarks>
[MemoryDiagnoser]
[SimpleJob(RunStrategy.Throughput, launchCount: 1, warmupCount: 15, iterationCount: 10)]
public class ConsumeAsyncBufferedFastPathBenchmarks
{
    // Keep the total off the poll-refresh boundary (asserted against
    // KafkaConsumer.PollRefreshRecordInterval in Setup) so cancellation happens only after
    // the final pending fetch is exhausted, flushed, dequeued, and disposed.
    // ~64-90 ns/record × ~2M records keeps measured iterations comfortably above
    // BenchmarkDotNet's 100 ms recommendation (issue #2445) even if the per-record cost
    // drops further; the prior 100,100-record shape produced 6-9 ms iterations whose
    // tier-transition bimodality blurred 5-15% causal deltas. BatchCount must stay at or
    // below the RecordBatch pool capacity (2048) or every iteration setup allocates the
    // excess batches.
    private const int RecordsPerBatch = 1_001;
    private const int BatchCount = 2_000;
    private const int MessageCount = RecordsPerBatch * BatchCount;
    // Distinct Record[] seed arrays are cycled across batches: batch disposal only nulls the
    // batch's own record-list reference, never the array contents, so sharing is safe and
    // keeps GlobalSetup memory flat while BatchCount grows.
    private const int SeedArrayCount = 100;
    private const string Topic = "consume-async-fast-path";
    private const int Partition = 0;

    private Record[][] _batchRecords = null!;
    private KafkaConsumer<ReadOnlyMemory<byte>, ReadOnlyMemory<byte>> _consumer = null!;
    private CancellationTokenSource _iterationCancellation = null!;

    [Params(100, 1000)]
    public int MessageSize { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        if (MessageCount % KafkaConsumer<ReadOnlyMemory<byte>, ReadOnlyMemory<byte>>.PollRefreshRecordInterval == 0)
        {
            throw new InvalidOperationException(
                "MessageCount must stay off the poll-refresh boundary so cancellation lands after final fetch cleanup.");
        }

        var value = Encoding.UTF8.GetBytes(new string('x', MessageSize));
        _batchRecords = new Record[SeedArrayCount][];
        for (var batchIndex = 0; batchIndex < SeedArrayCount; batchIndex++)
        {
            var records = new Record[RecordsPerBatch];
            for (var recordIndex = 0; recordIndex < RecordsPerBatch; recordIndex++)
            {
                var messageIndex = batchIndex * RecordsPerBatch + recordIndex;
                records[recordIndex] = new Record
                {
                    OffsetDelta = recordIndex,
                    TimestampDelta = recordIndex,
                    Key = Encoding.UTF8.GetBytes($"key-{messageIndex}"),
                    IsKeyNull = false,
                    Value = value,
                    IsValueNull = false,
                    Headers = null,
                    HeaderCount = 0,
                };
            }

            _batchRecords[batchIndex] = records;
        }

        _consumer = new KafkaConsumer<ReadOnlyMemory<byte>, ReadOnlyMemory<byte>>(
            new ConsumerOptions
            {
                BootstrapServers = ["localhost:9092"],
                OffsetCommitMode = OffsetCommitMode.Auto,
                QueuedMinMessages = 1,
                FetchMaxWaitMs = 200,
            },
            Serializers.RawBytes,
            Serializers.RawBytes);
        BufferedConsumerHarness.InitializeForBufferedFastPath(_consumer, Topic, Partition);
    }

    [IterationSetup(Target = nameof(ConsumeBufferedStream))]
    public void UnboundedIterationSetup()
    {
        _iterationCancellation = new CancellationTokenSource();
        BufferedConsumerHarness.ReseedPendingFetches(
            _consumer, Topic, Partition, _batchRecords, BatchCount, RecordsPerBatch);
    }

    [IterationSetup(Target = nameof(ConsumeSnapshotBufferedStream))]
    public void SnapshotIterationSetup()
    {
        _iterationCancellation = new CancellationTokenSource();
        BufferedConsumerHarness.ReseedPendingFetches(
            _consumer, Topic, Partition, _batchRecords, BatchCount, RecordsPerBatch);
        BufferedConsumerHarness.ActivateSnapshotForBufferedFastPath(
            _consumer, Topic, Partition, MessageCount);
    }

    [IterationCleanup]
    public void IterationCleanup()
    {
        BufferedConsumerHarness.DeactivateSnapshotForBufferedFastPath(_consumer);
        _iterationCancellation.Dispose();
    }

    [Benchmark(OperationsPerInvoke = MessageCount)]
    public async Task<int> ConsumeBufferedStream()
    {
        var count = 0;
        try
        {
            await foreach (var _ in _consumer.ConsumeAsync(_iterationCancellation.Token).ConfigureAwait(false))
            {
                if (++count == MessageCount)
                    _iterationCancellation.Cancel();
            }
        }
        catch (OperationCanceledException) when (_iterationCancellation.IsCancellationRequested)
        {
            // ConsumeAsync polls indefinitely once buffered data is drained. The token ends
            // the invocation only after the pending-fetch cleanup measured above completes.
        }

        return count;
    }

    [Benchmark(OperationsPerInvoke = MessageCount)]
    public async Task<int> ConsumeSnapshotBufferedStream()
    {
        var count = 0;
        try
        {
            await foreach (var _ in _consumer.ConsumeAsync(_iterationCancellation.Token).ConfigureAwait(false))
            {
                if (++count == MessageCount)
                    _iterationCancellation.Cancel();
            }
        }
        catch (OperationCanceledException) when (_iterationCancellation.IsCancellationRequested)
        {
            // End only after the final bounded record completed validation and position tracking.
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
