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
/// Docker-free micro-benchmark for the <c>ConsumeOneAsync</c> buffered fast path with the
/// string serdes the published <c>ConsumerPollBenchmarks.PollSingle</c> comparison uses
/// (issue #2443). The RawBytes fast-path benchmark measures the zero-allocation floor;
/// this lane isolates what the string deserialization and result materialization add on
/// top of it, so residual-gap work has a local causal gate for the path the published
/// ratio actually exercises. A RawBytes row runs in the same job as an in-run control.
/// </summary>
/// <remarks>
/// Same reflection-seeding pattern as <see cref="ConsumeOneBufferedFastPathBenchmarks"/>:
/// initialized consumer, seeded pending fetches, each invocation drains one buffered
/// record through the public <c>ConsumeOneAsync</c>.
/// </remarks>
[MemoryDiagnoser]
[Config(typeof(StringSerdeJobConfig))]
public class ConsumeOneStringSerdeBenchmarks
{
    // ~125-400 ns/poll × 800k polls keeps measured iterations above BenchmarkDotNet's
    // 100 ms recommendation (issue #2445), including the RawBytes control row. Derived as
    // a product so the seeded record count can never silently truncate away from the
    // invocation count.
    private const int RecordsPerBatch = 1_000;
    private const int BatchCount = 800;
    private const int PollsPerIteration = BatchCount * RecordsPerBatch;
    // Distinct Record[] seed arrays are cycled across batches: batch disposal only nulls
    // the batch's own record-list reference, never the array contents, so sharing is safe
    // and keeps GlobalSetup memory flat while BatchCount grows.
    private const int SeedArrayCount = 10;
    private const string Topic = "consume-one-string-serde";
    private const int Partition = 0;
    private static readonly TimeSpan PollTimeout = TimeSpan.FromSeconds(10);

    private sealed class StringSerdeJobConfig : ManualConfig
    {
        public StringSerdeJobConfig()
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
    private KafkaConsumer<string, string> _stringGroupedConsumer = null!;
    private KafkaConsumer<string, string> _stringNoGroupConsumer = null!;
    private KafkaConsumer<ReadOnlyMemory<byte>, ReadOnlyMemory<byte>> _rawBytesConsumer = null!;
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

        _stringGroupedConsumer = new KafkaConsumer<string, string>(
            new ConsumerOptions
            {
                BootstrapServers = ["localhost:9092"],
                GroupId = "consume-one-string-serde-benchmark",
                OffsetCommitMode = OffsetCommitMode.Auto,
                QueuedMinMessages = 1,
                FetchMaxWaitMs = 200,
            },
            CreatePollSingleKeyDeserializer(),
            Serializers.String);
        BufferedConsumerHarness.InitializeForBufferedFastPath(_stringGroupedConsumer, Topic, Partition);

        // CanUseBufferedConsumeOneFastPath requires a live auto-commit loop in Auto mode.
        // A surrogate incomplete task satisfies IsAutoCommitRunning without network I/O.
        _autoCommitSurrogate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        BufferedConsumerHarness.SetPrivateField(_stringGroupedConsumer, "_autoCommitTask", _autoCommitSurrogate.Task);

        _stringNoGroupConsumer = new KafkaConsumer<string, string>(
            new ConsumerOptions
            {
                BootstrapServers = ["localhost:9092"],
                OffsetCommitMode = OffsetCommitMode.Manual,
                QueuedMinMessages = 1,
                FetchMaxWaitMs = 200,
            },
            CreatePollSingleKeyDeserializer(),
            Serializers.String);
        BufferedConsumerHarness.InitializeForBufferedFastPath(_stringNoGroupConsumer, Topic, Partition);

        _rawBytesConsumer = new KafkaConsumer<ReadOnlyMemory<byte>, ReadOnlyMemory<byte>>(
            new ConsumerOptions
            {
                BootstrapServers = ["localhost:9092"],
                OffsetCommitMode = OffsetCommitMode.Manual,
                QueuedMinMessages = 1,
                FetchMaxWaitMs = 200,
            },
            Serializers.RawBytes,
            Serializers.RawBytes);
        BufferedConsumerHarness.InitializeForBufferedFastPath(_rawBytesConsumer, Topic, Partition);
    }

    [IterationSetup(Targets = [nameof(String_Grouped_AutoCommit)])]
    public void StringGroupedIterationSetup() => ReseedPendingFetches(_stringGroupedConsumer);

    [IterationSetup(Targets = [nameof(String_NoGroup_ManualCommit)])]
    public void StringNoGroupIterationSetup() => ReseedPendingFetches(_stringNoGroupConsumer);

    [IterationSetup(Targets = [nameof(RawBytes_NoGroup_Control)])]
    public void RawBytesIterationSetup() => ReseedPendingFetches(_rawBytesConsumer);

    [BenchmarkCategory("PollOneStringSerde")]
    [Benchmark(Baseline = true)]
    public ValueTask<ConsumeResult<string, string>?> String_Grouped_AutoCommit()
        => _stringGroupedConsumer.ConsumeOneAsync(PollTimeout);

    [BenchmarkCategory("PollOneStringSerde")]
    [Benchmark]
    public ValueTask<ConsumeResult<string, string>?> String_NoGroup_ManualCommit()
        => _stringNoGroupConsumer.ConsumeOneAsync(PollTimeout);

    [BenchmarkCategory("PollOneStringSerde")]
    [Benchmark]
    public ValueTask<ConsumeResult<ReadOnlyMemory<byte>, ReadOnlyMemory<byte>>?> RawBytes_NoGroup_Control()
        => _rawBytesConsumer.ConsumeOneAsync(PollTimeout);

    [GlobalCleanup]
    public void Cleanup()
    {
        _autoCommitSurrogate.TrySetResult();
        DrainPendingFetches(_stringGroupedConsumer);
        DrainPendingFetches(_stringNoGroupConsumer);
        DrainPendingFetches(_rawBytesConsumer);
        _stringGroupedConsumer.DisposeAsync().AsTask().GetAwaiter().GetResult();
        _stringNoGroupConsumer.DisposeAsync().AsTask().GetAwaiter().GetResult();
        _rawBytesConsumer.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    private void ReseedPendingFetches<TKey, TValue>(KafkaConsumer<TKey, TValue> consumer)
        => BufferedConsumerHarness.ReseedPendingFetches(
            consumer, Topic, Partition, _batchRecords, BatchCount, RecordsPerBatch);

    // Mirrors the ConsumerBuilder default key wrap; the constants are shared so a builder
    // retune cannot leave this benchmark silently measuring a stale configuration.
    private static CachingStringDeserializer CreatePollSingleKeyDeserializer() =>
        new(Serializers.String,
            CachingStringDeserializer.DefaultKeyCacheMaxBytes,
            CachingStringDeserializer.DefaultKeyCacheMaxEntries);

    private static void DrainPendingFetches<TKey, TValue>(KafkaConsumer<TKey, TValue> consumer)
        => BufferedConsumerHarness.DrainPendingFetches(consumer);
}
