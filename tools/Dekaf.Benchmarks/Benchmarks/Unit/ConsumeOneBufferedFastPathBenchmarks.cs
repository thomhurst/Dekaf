using System.Collections.Concurrent;
using System.Reflection;
using System.Text;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Jobs;
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
    private const int PollsPerIteration = 10_000;
    private const int RecordsPerBatch = 1_000;
    private const int BatchCount = PollsPerIteration / RecordsPerBatch;
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
    private KafkaConsumer<string, string> _groupedConsumer = null!;
    private KafkaConsumer<string, string> _noGroupConsumer = null!;
    private TaskCompletionSource _autoCommitSurrogate = null!;

    [GlobalSetup]
    public void Setup()
    {
        var value = Encoding.UTF8.GetBytes(new string('x', MessageSize));

        _batchRecords = new Record[BatchCount][];
        for (var b = 0; b < BatchCount; b++)
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

        _groupedConsumer = new KafkaConsumer<string, string>(
            new ConsumerOptions
            {
                BootstrapServers = ["localhost:9092"],
                GroupId = "consume-one-fast-path-benchmark",
                OffsetCommitMode = OffsetCommitMode.Auto,
                QueuedMinMessages = 1,
                FetchMaxWaitMs = 200,
            },
            Serializers.String,
            Serializers.String);
        InitializeForBufferedFastPath(_groupedConsumer);

        // CanUseBufferedConsumeOneFastPath requires a live auto-commit loop in Auto mode.
        // A surrogate incomplete task satisfies IsAutoCommitRunning without network I/O.
        _autoCommitSurrogate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        SetPrivateField(_groupedConsumer, "_autoCommitTask", _autoCommitSurrogate.Task);

        _noGroupConsumer = new KafkaConsumer<string, string>(
            new ConsumerOptions
            {
                BootstrapServers = ["localhost:9092"],
                OffsetCommitMode = OffsetCommitMode.Manual,
                QueuedMinMessages = 1,
                FetchMaxWaitMs = 200,
            },
            Serializers.String,
            Serializers.String);
        InitializeForBufferedFastPath(_noGroupConsumer);
    }

    [IterationSetup(Targets = [nameof(PollOne_Grouped_AutoCommit)])]
    public void GroupedIterationSetup() => ReseedPendingFetches(_groupedConsumer);

    [IterationSetup(Targets = [nameof(PollOne_NoGroup_ManualCommit)])]
    public void NoGroupIterationSetup() => ReseedPendingFetches(_noGroupConsumer);

    [BenchmarkCategory("PollOneFastPath")]
    [Benchmark(Baseline = true)]
    public ValueTask<ConsumeResult<string, string>?> PollOne_Grouped_AutoCommit()
        => _groupedConsumer.ConsumeOneAsync(PollTimeout);

    [BenchmarkCategory("PollOneFastPath")]
    [Benchmark]
    public ValueTask<ConsumeResult<string, string>?> PollOne_NoGroup_ManualCommit()
        => _noGroupConsumer.ConsumeOneAsync(PollTimeout);

    [GlobalCleanup]
    public void Cleanup()
    {
        _autoCommitSurrogate.TrySetResult();
        DrainPendingFetches(_groupedConsumer);
        DrainPendingFetches(_noGroupConsumer);
        _groupedConsumer.DisposeAsync().AsTask().GetAwaiter().GetResult();
        _noGroupConsumer.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    private void ReseedPendingFetches(KafkaConsumer<string, string> consumer)
    {
        DrainPendingFetches(consumer);

        var batches = new RecordBatch[BatchCount];
        for (var b = 0; b < BatchCount; b++)
        {
            var batch = RecordBatch.RentFromPool();
            batch.BaseOffset = (long)b * RecordsPerBatch;
            batch.BaseTimestamp = 1_700_000_000_000L;
            batch.MaxTimestamp = 1_700_000_000_000L + RecordsPerBatch - 1;
            batch.LastOffsetDelta = RecordsPerBatch - 1;
            batch.Attributes = RecordBatchAttributes.None;
            batch.Records = _batchRecords[b];
            batches[b] = batch;
        }

        GetPendingFetches(consumer).Enqueue(PendingFetchData.Create(Topic, Partition, batches));
    }

    private static void DrainPendingFetches(KafkaConsumer<string, string> consumer)
    {
        var pendingFetches = GetPendingFetches(consumer);
        while (pendingFetches.Count > 0)
            pendingFetches.Dequeue().Dispose();
    }

    private static void InitializeForBufferedFastPath(KafkaConsumer<string, string> consumer)
    {
        SetPrivateField(consumer, "_initialized", true);

        var tp = new TopicPartition(Topic, Partition);
        consumer.Assign(tp);
        GetFetchPositions(consumer)[tp] = 0;

        // Mark the manual assignment as acknowledged so the fast path's currency check passes.
        var ensureVersion = GetPrivateField(consumer, "_assignmentEnsureVersion");
        SetPrivateField(consumer, "_lastManualAssignmentEnsureVersion", ensureVersion);
    }

    private static Queue<PendingFetchData> GetPendingFetches(KafkaConsumer<string, string> consumer)
        => (Queue<PendingFetchData>)GetPrivateField(consumer, "_pendingFetches")!;

    private static ConcurrentDictionary<TopicPartition, long> GetFetchPositions(
        KafkaConsumer<string, string> consumer)
        => (ConcurrentDictionary<TopicPartition, long>)GetPrivateField(consumer, "_fetchPositions")!;

    private static object? GetPrivateField(KafkaConsumer<string, string> consumer, string fieldName)
        => RequireField(fieldName).GetValue(consumer);

    private static void SetPrivateField(KafkaConsumer<string, string> consumer, string fieldName, object? value)
        => RequireField(fieldName).SetValue(consumer, value);

    private static FieldInfo RequireField(string fieldName)
        => typeof(KafkaConsumer<string, string>).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)
           ?? throw new InvalidOperationException($"{fieldName} field not found.");
}
