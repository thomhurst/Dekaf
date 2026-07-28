using System.Collections.Concurrent;
using System.Reflection;
using System.Text;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
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
    // Keep the total off the 32-record poll-refresh boundary so cancellation happens
    // only after the final pending fetch is exhausted, flushed, dequeued, and disposed.
    private const int RecordsPerBatch = 1_001;
    private const int BatchCount = 100;
    private const int MessageCount = RecordsPerBatch * BatchCount;
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
        var value = Encoding.UTF8.GetBytes(new string('x', MessageSize));
        _batchRecords = new Record[BatchCount][];
        for (var batchIndex = 0; batchIndex < BatchCount; batchIndex++)
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
        InitializeForBufferedFastPath(_consumer);
    }

    [IterationSetup]
    public void IterationSetup()
    {
        DrainPendingFetches();
        _iterationCancellation = new CancellationTokenSource();

        var batches = new RecordBatch[BatchCount];
        for (var batchIndex = 0; batchIndex < BatchCount; batchIndex++)
        {
            var batch = RecordBatch.RentFromPool();
            batch.BaseOffset = (long)batchIndex * RecordsPerBatch;
            batch.BaseTimestamp = 1_700_000_000_000L;
            batch.MaxTimestamp = 1_700_000_000_000L + RecordsPerBatch - 1;
            batch.LastOffsetDelta = RecordsPerBatch - 1;
            batch.Attributes = RecordBatchAttributes.None;
            batch.Records = _batchRecords[batchIndex];
            batches[batchIndex] = batch;
        }

        GetPendingFetches().Enqueue(PendingFetchData.Create(Topic, Partition, batches));
    }

    [IterationCleanup]
    public void IterationCleanup() => _iterationCancellation.Dispose();

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

    [GlobalCleanup]
    public async Task Cleanup()
    {
        DrainPendingFetches();
        await _consumer.DisposeAsync().ConfigureAwait(false);
    }

    private void DrainPendingFetches()
    {
        var pendingFetches = GetPendingFetches();
        while (pendingFetches.Count > 0)
            pendingFetches.Dequeue().Dispose();
    }

    private static void InitializeForBufferedFastPath(
        KafkaConsumer<ReadOnlyMemory<byte>, ReadOnlyMemory<byte>> consumer)
    {
        SetPrivateField(consumer, "_initialized", true);

        var topicPartition = new TopicPartition(Topic, Partition);
        consumer.Assign(topicPartition);
        GetFetchPositions(consumer)[topicPartition] = 0;

        var ensureVersion = GetPrivateField(consumer, "_assignmentEnsureVersion");
        SetPrivateField(consumer, "_lastManualAssignmentEnsureVersion", ensureVersion);
    }

    private Queue<PendingFetchData> GetPendingFetches() =>
        (Queue<PendingFetchData>)GetPrivateField(_consumer, "_pendingFetches")!;

    private static ConcurrentDictionary<TopicPartition, long> GetFetchPositions(
        KafkaConsumer<ReadOnlyMemory<byte>, ReadOnlyMemory<byte>> consumer) =>
        (ConcurrentDictionary<TopicPartition, long>)GetPrivateField(consumer, "_fetchPositions")!;

    private static object? GetPrivateField(
        KafkaConsumer<ReadOnlyMemory<byte>, ReadOnlyMemory<byte>> consumer,
        string fieldName) =>
        RequireField(fieldName).GetValue(consumer);

    private static void SetPrivateField(
        KafkaConsumer<ReadOnlyMemory<byte>, ReadOnlyMemory<byte>> consumer,
        string fieldName,
        object? value) =>
        RequireField(fieldName).SetValue(consumer, value);

    private static FieldInfo RequireField(string fieldName) =>
        typeof(KafkaConsumer<ReadOnlyMemory<byte>, ReadOnlyMemory<byte>>).GetField(
            fieldName,
            BindingFlags.NonPublic | BindingFlags.Instance)
        ?? throw new InvalidOperationException($"{fieldName} field not found.");
}
