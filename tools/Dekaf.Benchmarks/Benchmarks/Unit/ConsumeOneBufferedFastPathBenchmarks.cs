using System.Text;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Jobs;
using Dekaf.Benchmarks.Infrastructure;
using Dekaf.Consumer;
using Dekaf.Protocol.Records;
using Dekaf.Serialization;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>
/// Micro-benchmark for the <c>ConsumeOneAsync</c> buffered fast path (issue #2211) —
/// no Docker, no network. A consumer is put into the initialized state using the same
/// reflection-seeding pattern as the fast-path unit tests, pending fetches are seeded
/// directly, and each invocation measures one full public <c>ConsumeOneAsync</c> call
/// that drains a buffered record.
/// </summary>
/// <remarks>
/// The grouped variant mirrors the state the real <c>ConsumerPollBenchmarks</c> consumer
/// reaches in steady state: coordinator present (poll accounting via
/// <c>TryRecordPollFast</c>), auto-commit mode with a running auto-commit loop
/// (surrogate incomplete task), and the per-record seqlock position publish. The
/// no-group variant is the floor: manual commit mode, no coordinator, no publish.
/// </remarks>
[MemoryDiagnoser]
[Config(typeof(FastPathJobConfig))]
public class ConsumeOneBufferedFastPathBenchmarks
{
    // ~130 ns/poll × 1.5M polls keeps measured iterations comfortably above BenchmarkDotNet's
    // 100 ms recommendation (issue #2445) even if the per-poll cost drops further; shorter
    // iterations made 5-15% causal deltas unreadable. The old 10k-poll shape also warmed up
    // on only 30k calls, so it measured partly-tiered code and reported ~2.5x the
    // steady-state per-poll cost. PollsPerIteration is derived as a product so the seeded
    // record count can never silently truncate away from the invocation count.
    private const int RecordsPerBatch = 1_000;
    private const int BatchCount = 1_500;
    private const int PollsPerIteration = BatchCount * RecordsPerBatch;
    // Distinct Record[] seed arrays are cycled across batches: batch disposal only nulls the
    // batch's own record-list reference, never the array contents, so sharing is safe and
    // keeps GlobalSetup memory flat while BatchCount grows.
    private const int SeedArrayCount = 10;
    private const string Topic = "consume-one-fast-path";
    private const int Partition = 0;
    private static readonly TimeSpan PollTimeout = TimeSpan.FromSeconds(10);

    private sealed class FastPathJobConfig : ManualConfig
    {
        public FastPathJobConfig()
        {
            AddJob(Job.Default
                .WithStrategy(RunStrategy.Throughput)
                .WithLaunchCount(1)
                .WithWarmupCount(3)
                .WithIterationCount(10)
                .WithInvocationCount(PollsPerIteration)
                .WithUnrollFactor(1));
        }
    }

    [Params(100, 1000)]
    public int MessageSize { get; set; }

    private Record[][] _batchRecords = null!;
    private KafkaConsumer<ReadOnlyMemory<byte>, ReadOnlyMemory<byte>> _groupedConsumer = null!;
    private KafkaConsumer<ReadOnlyMemory<byte>, ReadOnlyMemory<byte>> _noGroupConsumer = null!;
    private TaskCompletionSource _autoCommitSurrogate = null!;

    [GlobalSetup]
    public void Setup()
    {
        var value = Encoding.UTF8.GetBytes(new string('x', MessageSize));

        _batchRecords = new Record[SeedArrayCount][];
        for (var b = 0; b < SeedArrayCount; b++)
        {
            var records = new Record[RecordsPerBatch];
            for (var i = 0; i < RecordsPerBatch; i++)
            {
                records[i] = new Record
                {
                    OffsetDelta = i,
                    TimestampDelta = i,
                    Key = Encoding.UTF8.GetBytes($"key-{b * RecordsPerBatch + i}"),
                    IsKeyNull = false,
                    Value = value,
                    IsValueNull = false,
                    Headers = null,
                    HeaderCount = 0,
                };
            }
            _batchRecords[b] = records;
        }

        _groupedConsumer = new KafkaConsumer<ReadOnlyMemory<byte>, ReadOnlyMemory<byte>>(
            new ConsumerOptions
            {
                BootstrapServers = ["localhost:9092"],
                GroupId = "consume-one-fast-path-benchmark",
                OffsetCommitMode = OffsetCommitMode.Auto,
                QueuedMinMessages = 1,
                FetchMaxWaitMs = 200,
            },
            Serializers.RawBytes,
            Serializers.RawBytes);
        BufferedConsumerHarness.InitializeForBufferedFastPath(_groupedConsumer, Topic, Partition);

        // CanUseBufferedConsumeOneFastPath requires a live auto-commit loop in Auto mode.
        // A surrogate incomplete task satisfies IsAutoCommitRunning without network I/O.
        _autoCommitSurrogate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        BufferedConsumerHarness.SetPrivateField(_groupedConsumer, "_autoCommitTask", _autoCommitSurrogate.Task);

        _noGroupConsumer = new KafkaConsumer<ReadOnlyMemory<byte>, ReadOnlyMemory<byte>>(
            new ConsumerOptions
            {
                BootstrapServers = ["localhost:9092"],
                OffsetCommitMode = OffsetCommitMode.Manual,
                QueuedMinMessages = 1,
                FetchMaxWaitMs = 200,
            },
            Serializers.RawBytes,
            Serializers.RawBytes);
        BufferedConsumerHarness.InitializeForBufferedFastPath(_noGroupConsumer, Topic, Partition);
    }

    [IterationSetup(Targets = [nameof(PollOne_Grouped_AutoCommit)])]
    public void GroupedIterationSetup() => ReseedPendingFetches(_groupedConsumer);

    [IterationSetup(Targets = [nameof(PollOne_NoGroup_ManualCommit)])]
    public void NoGroupIterationSetup() => ReseedPendingFetches(_noGroupConsumer);

    [BenchmarkCategory("PollOneFastPath")]
    [Benchmark(Baseline = true)]
    public ValueTask<ConsumeResult<ReadOnlyMemory<byte>, ReadOnlyMemory<byte>>?> PollOne_Grouped_AutoCommit()
        => _groupedConsumer.ConsumeOneAsync(PollTimeout);

    [BenchmarkCategory("PollOneFastPath")]
    [Benchmark]
    public ValueTask<ConsumeResult<ReadOnlyMemory<byte>, ReadOnlyMemory<byte>>?> PollOne_NoGroup_ManualCommit()
        => _noGroupConsumer.ConsumeOneAsync(PollTimeout);

    [GlobalCleanup]
    public void Cleanup()
    {
        _autoCommitSurrogate.TrySetResult();
        BufferedConsumerHarness.DrainPendingFetches(_groupedConsumer);
        BufferedConsumerHarness.DrainPendingFetches(_noGroupConsumer);
        _groupedConsumer.DisposeAsync().AsTask().GetAwaiter().GetResult();
        _noGroupConsumer.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    private void ReseedPendingFetches(
        KafkaConsumer<ReadOnlyMemory<byte>, ReadOnlyMemory<byte>> consumer)
        => BufferedConsumerHarness.ReseedPendingFetches(
            consumer, Topic, Partition, _batchRecords, BatchCount, RecordsPerBatch);
}
