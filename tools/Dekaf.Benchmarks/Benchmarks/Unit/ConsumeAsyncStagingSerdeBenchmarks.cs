using System.Text;
using BenchmarkDotNet.Attributes;
using Dekaf.Benchmarks.Infrastructure;
using Dekaf.Consumer;
using Dekaf.Protocol.Records;
using Dekaf.Serialization;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>
/// Docker-free decomposition of the streaming <c>ConsumeAsync</c> per-message CPU budget
/// for issue #2485: isolates (a) proven-offset staging overhead (at-least-once default,
/// added by #2097) and (b) string value materialization for 1000-byte payloads (the
/// post-#2097 plain-decode cost), against the raw drain-loop floor.
/// </summary>
/// <remarks>
/// Row deltas, not absolute values, are the measurement:
/// <list type="bullet">
/// <item><c>Raw_AutoCommitAtLeastOnce</c> − <c>Raw_ManualCommit</c> = the at-least-once
/// default's per-message staging cost: the active-position seqlock publish (gated on
/// <c>OffsetCommitMode.Auto</c>) plus the fetch-boundary proven-offset store (the
/// baseline runs with <c>EnableAutoOffsetStore = false</c>, so the auto row pays it
/// alone). The store executes once per pending fetch — once per seeded 1.5M-record fetch
/// here, once per ~1000-record fetch in production — so its amortized share is sub-0.1
/// ns/msg in either shape; the per-message component is the publish. The proven-offset
/// marks themselves (<c>MarkYieldedProcessed</c>, two plain field writes) run
/// unconditionally on every consume path and are therefore part of every row's floor,
/// not of this delta.</item>
/// <item><c>Raw_AutoCommitAtMostOnce</c> − <c>Raw_AutoCommitAtLeastOnce</c> = the opt-in
/// OnDelivery eager per-message offset store, for comparison with the default.</item>
/// <item><c>ConstantStringValue_AutoCommit</c> − <c>Raw_AutoCommitAtLeastOnce</c> = the
/// <c>string</c>-vs-value-type generic instantiation shape (shared-generic dispatch,
/// result-struct width) with no decode or allocation.</item>
/// <item><c>StringValue_AutoCommit</c> − <c>ConstantStringValue_AutoCommit</c> = pure
/// value materialization at 1000 B (UTF-8 decode + ~2 KB string allocation + GC).</item>
/// <item><c>StressShape_AutoCommit</c> − <c>StringValue_AutoCommit</c> = key-side cost at
/// key-cache steady state (bounded 10,010-key set mirroring the stress lane's
/// PreAllocatedKeys(10_000); promotion is forced in setup so the row cannot silently
/// measure the observe regime).</item>
/// <item><c>Utf8Decode_Control</c> = the irreducible cost of
/// <see cref="Encoding.UTF8.GetString(ReadOnlySpan{byte})"/> for the same 1000 B payload
/// under the same job and engine configuration.</item>
/// </list>
/// Seeding and warmup mirror <see cref="ConsumeAsyncBufferedFastPathBenchmarks"/>: the
/// extended warmup lets tiered PGO finish recompiling the async iterator, and the batch
/// count stays under the RecordBatch pool's pre-ratchet capacity (2048; consumer
/// construction ratchets it higher).
/// </remarks>
[MemoryDiagnoser]
[ThroughputJob(warmupCount: 15)]
public class ConsumeAsyncStagingSerdeBenchmarks
{
    // 1,501,500 records/iteration keeps the raw rows above BenchmarkDotNet's 100 ms
    // recommendation (~70-90 ns/record) and stays off the poll-refresh boundary
    // (asserted in Setup) so cancellation lands after final fetch cleanup.
    private const int RecordsPerBatch = 1_001;
    private const int BatchCount = 1_500;
    private const int MessageCount = RecordsPerBatch * BatchCount;
    // 10 arrays × 1,001 records = 10,010 distinct keys, matching the stress lane's
    // bounded PreAllocatedKeys(10_000) working set so the default key cache serves the
    // stress-shape row from hit steady state.
    private const int SeedArrayCount = 10;
    // The stress consumer lane's message size (1 broker / 1000 B / consumer): the shape
    // issue #2485 targets.
    private const int MessageSize = 1_000;
    private const string Topic = "consume-async-staging-serde";
    private const int Partition = 0;

    private Record[][] _batchRecords = null!;
    private byte[] _valueBytes = null!;
    private KafkaConsumer<Ignore, ReadOnlyMemory<byte>> _rawManualConsumer = null!;
    private KafkaConsumer<Ignore, ReadOnlyMemory<byte>> _rawAtLeastOnceConsumer = null!;
    private KafkaConsumer<Ignore, ReadOnlyMemory<byte>> _rawAtMostOnceConsumer = null!;
    private KafkaConsumer<Ignore, string> _constantStringConsumer = null!;
    private KafkaConsumer<Ignore, string> _stringValueConsumer = null!;
    private KafkaConsumer<string, string> _stressShapeConsumer = null!;
    private CancellationTokenSource? _iterationCancellation;

    /// <summary>
    /// Returns one cached string without touching the payload: the value-side null
    /// measurement that separates the <c>string</c> instantiation shape from the actual
    /// UTF-8 decode + allocation.
    /// </summary>
    private sealed class ConstantStringDeserializer : IDeserializer<string>
    {
        public string Deserialize(ReadOnlyMemory<byte> data, SerializationContext context) => "constant";
    }

    [GlobalSetup]
    public void Setup()
    {
        if (MessageCount % KafkaConsumer<Ignore, ReadOnlyMemory<byte>>.PollRefreshRecordInterval == 0)
        {
            throw new InvalidOperationException(
                "MessageCount must stay off the poll-refresh boundary so cancellation lands after final fetch cleanup.");
        }

        // One identical value instance across every record, like the seeded stress topic.
        _valueBytes = Encoding.UTF8.GetBytes(new string('x', MessageSize));
        var keyPayloads = new ReadOnlyMemory<byte>[SeedArrayCount * RecordsPerBatch];
        _batchRecords = new Record[SeedArrayCount][];
        for (var batchIndex = 0; batchIndex < SeedArrayCount; batchIndex++)
        {
            var records = new Record[RecordsPerBatch];
            for (var recordIndex = 0; recordIndex < RecordsPerBatch; recordIndex++)
            {
                var keyIndex = batchIndex * RecordsPerBatch + recordIndex;
                var key = Encoding.UTF8.GetBytes($"key-{keyIndex}");
                keyPayloads[keyIndex] = key;
                records[recordIndex] = new Record
                {
                    OffsetDelta = recordIndex,
                    TimestampDelta = recordIndex,
                    Key = key,
                    IsKeyNull = false,
                    Value = _valueBytes,
                    IsValueNull = false,
                    Headers = null,
                    HeaderCount = 0,
                };
            }

            _batchRecords[batchIndex] = records;
        }

        // The floor row disables offset storage entirely so the auto rows' deltas carry
        // the whole at-least-once staging cost (publish + fetch-boundary store).
        _rawManualConsumer = CreateConsumer(
            OffsetCommitMode.Manual, OffsetStoreTiming.AfterProcessing, Serializers.Ignore, Serializers.RawBytes,
            enableAutoOffsetStore: false);
        _rawAtLeastOnceConsumer = CreateConsumer(
            OffsetCommitMode.Auto, OffsetStoreTiming.AfterProcessing, Serializers.Ignore, Serializers.RawBytes);
        _rawAtMostOnceConsumer = CreateConsumer(
            OffsetCommitMode.Auto, OffsetStoreTiming.OnDelivery, Serializers.Ignore, Serializers.RawBytes);
        _constantStringConsumer = CreateConsumer(
            OffsetCommitMode.Auto, OffsetStoreTiming.AfterProcessing, Serializers.Ignore,
            new ConstantStringDeserializer());
        _stringValueConsumer = CreateConsumer(
            OffsetCommitMode.Auto, OffsetStoreTiming.AfterProcessing, Serializers.Ignore, Serializers.String);

        // Key deserializer matches the ConsumerBuilder default wrap exactly (same
        // constants), and promotion is driven to completion here so the measured
        // iterations start in the cached steady state the row's name claims — if the
        // 10,010-key set ever stops promoting, setup fails loudly instead of silently
        // measuring the observe regime.
        var stressShapeKeyDeserializer = new CachingStringDeserializer(
            Serializers.String,
            CachingStringDeserializer.DefaultKeyCacheMaxBytes,
            CachingStringDeserializer.DefaultKeyCacheMaxEntries);
        CachingDeserializerWarmup.PromoteOrThrow(
            stressShapeKeyDeserializer,
            new SerializationContext { Topic = Topic, Component = SerializationComponent.Key },
            keyPayloads);
        _stressShapeConsumer = CreateConsumer(
            OffsetCommitMode.Auto, OffsetStoreTiming.AfterProcessing, stressShapeKeyDeserializer,
            Serializers.String);
    }

    private static KafkaConsumer<TKey, TValue> CreateConsumer<TKey, TValue>(
        OffsetCommitMode commitMode,
        OffsetStoreTiming storeTiming,
        IDeserializer<TKey> keyDeserializer,
        IDeserializer<TValue> valueDeserializer,
        bool enableAutoOffsetStore = true)
    {
        var consumer = new KafkaConsumer<TKey, TValue>(
            new ConsumerOptions
            {
                BootstrapServers = ["localhost:9092"],
                OffsetCommitMode = commitMode,
                OffsetStoreTiming = storeTiming,
                EnableAutoOffsetStore = enableAutoOffsetStore,
                QueuedMinMessages = 1,
                FetchMaxWaitMs = 200,
            },
            keyDeserializer,
            valueDeserializer);
        BufferedConsumerHarness.InitializeForBufferedFastPath(consumer, Topic, Partition);
        return consumer;
    }

    [IterationSetup(Targets = [nameof(Raw_ManualCommit)])]
    public void RawManualIterationSetup() => ReseedPendingFetches(_rawManualConsumer);

    [IterationSetup(Targets = [nameof(Raw_AutoCommitAtLeastOnce)])]
    public void RawAtLeastOnceIterationSetup() => ReseedPendingFetches(_rawAtLeastOnceConsumer);

    [IterationSetup(Targets = [nameof(Raw_AutoCommitAtMostOnce)])]
    public void RawAtMostOnceIterationSetup() => ReseedPendingFetches(_rawAtMostOnceConsumer);

    [IterationSetup(Targets = [nameof(ConstantStringValue_AutoCommit)])]
    public void ConstantStringIterationSetup() => ReseedPendingFetches(_constantStringConsumer);

    [IterationSetup(Targets = [nameof(StringValue_AutoCommit)])]
    public void StringValueIterationSetup() => ReseedPendingFetches(_stringValueConsumer);

    [IterationSetup(Targets = [nameof(StressShape_AutoCommit)])]
    public void StressShapeIterationSetup() => ReseedPendingFetches(_stressShapeConsumer);

    // Empty on purpose: an IterationSetup forces the same engine configuration
    // (InvocationCount = 1, UnrollFactor = 1) as the drain rows, so the control's
    // overhead subtraction and per-op allocation accounting match the rows it is
    // subtracted from.
    [IterationSetup(Targets = [nameof(Utf8Decode_Control)])]
    public void Utf8DecodeControlIterationSetup()
    {
    }

    [IterationCleanup]
    public void IterationCleanup()
    {
        _iterationCancellation?.Dispose();
        _iterationCancellation = null;
    }

    [Benchmark(Baseline = true, OperationsPerInvoke = MessageCount)]
    public Task<int> Raw_ManualCommit() => DrainAsync(_rawManualConsumer);

    [Benchmark(OperationsPerInvoke = MessageCount)]
    public Task<int> Raw_AutoCommitAtLeastOnce() => DrainAsync(_rawAtLeastOnceConsumer);

    [Benchmark(OperationsPerInvoke = MessageCount)]
    public Task<int> Raw_AutoCommitAtMostOnce() => DrainAsync(_rawAtMostOnceConsumer);

    [Benchmark(OperationsPerInvoke = MessageCount)]
    public Task<int> ConstantStringValue_AutoCommit() => DrainAsync(_constantStringConsumer);

    [Benchmark(OperationsPerInvoke = MessageCount)]
    public Task<int> StringValue_AutoCommit() => DrainAsync(_stringValueConsumer);

    [Benchmark(OperationsPerInvoke = MessageCount)]
    public Task<int> StressShape_AutoCommit() => DrainAsync(_stressShapeConsumer);

    [Benchmark(OperationsPerInvoke = MessageCount)]
    public int Utf8Decode_Control()
    {
        var bytes = _valueBytes;
        var lengthSum = 0;
        for (var i = 0; i < MessageCount; i++)
            lengthSum += Encoding.UTF8.GetString(bytes).Length;
        return lengthSum;
    }

    private async Task<int> DrainAsync<TKey, TValue>(KafkaConsumer<TKey, TValue> consumer)
    {
        var count = 0;
        try
        {
            await foreach (var _ in consumer.ConsumeAsync(_iterationCancellation!.Token).ConfigureAwait(false))
            {
                if (++count == MessageCount)
                    _iterationCancellation.Cancel();
            }
        }
        catch (OperationCanceledException) when (_iterationCancellation!.IsCancellationRequested)
        {
            // ConsumeAsync polls indefinitely once buffered data is drained. The token ends
            // the invocation only after the pending-fetch cleanup measured above completes.
        }

        return count;
    }

    [GlobalCleanup]
    public async Task Cleanup()
    {
        await DisposeConsumerAsync(_rawManualConsumer);
        await DisposeConsumerAsync(_rawAtLeastOnceConsumer);
        await DisposeConsumerAsync(_rawAtMostOnceConsumer);
        await DisposeConsumerAsync(_constantStringConsumer);
        await DisposeConsumerAsync(_stringValueConsumer);
        await DisposeConsumerAsync(_stressShapeConsumer);
    }

    private static async Task DisposeConsumerAsync<TKey, TValue>(KafkaConsumer<TKey, TValue> consumer)
    {
        BufferedConsumerHarness.DrainPendingFetches(consumer);
        await consumer.DisposeAsync().ConfigureAwait(false);
    }

    private void ReseedPendingFetches<TKey, TValue>(KafkaConsumer<TKey, TValue> consumer)
    {
        _iterationCancellation = new CancellationTokenSource();
        BufferedConsumerHarness.ReseedPendingFetches(
            consumer, Topic, Partition, _batchRecords, BatchCount, RecordsPerBatch);
    }
}
