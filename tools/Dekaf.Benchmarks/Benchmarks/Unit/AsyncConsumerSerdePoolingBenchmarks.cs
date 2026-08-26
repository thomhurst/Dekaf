using System.Runtime.CompilerServices;
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
/// Measures the buffered ConsumeOneAsync path with an asynchronous value deserializer,
/// including a forced-suspension case that exposes state-machine allocations.
/// </summary>
[MemoryDiagnoser]
[Config(typeof(AsyncConsumerSerdeJobConfig))]
public class AsyncConsumerSerdePoolingBenchmarks
{
    private const int RecordsPerBatch = 1_000;
    private const int BatchCount = 200;
    private const int PollsPerIteration = BatchCount * RecordsPerBatch;
    private const string Topic = "consume-one-async-serde";
    private const int Partition = 0;
    private static readonly TimeSpan PollTimeout = TimeSpan.FromSeconds(10);

    private sealed class AsyncConsumerSerdeJobConfig : ManualConfig
    {
        public AsyncConsumerSerdeJobConfig()
        {
            AddJob(Job.Default
                .WithStrategy(RunStrategy.Throughput)
                .WithLaunchCount(1)
                .WithWarmupCount(3)
                .WithIterationCount(10)
                .WithInvocationCount(1)
                .WithUnrollFactor(1));
        }
    }

    private Record[][] _batchRecords = null!;
    private Record[][] _headerBatchRecords = null!;
    private KafkaConsumer<ReadOnlyMemory<byte>, ReadOnlyMemory<byte>> _completedConsumer = null!;
    private KafkaConsumer<ReadOnlyMemory<byte>, ReadOnlyMemory<byte>> _yieldingConsumer = null!;
    private KafkaConsumer<ReadOnlyMemory<byte>, ReadOnlyMemory<byte>> _completedKeyHeaderValueConsumer = null!;
    private KafkaConsumer<ReadOnlyMemory<byte>, ReadOnlyMemory<byte>> _yieldingKeyHeaderValueConsumer = null!;

    [GlobalSetup]
    public void Setup()
    {
        var value = new byte[100];
        _batchRecords = new Record[10][];
        _headerBatchRecords = new Record[10][];
        for (var b = 0; b < _batchRecords.Length; b++)
        {
            var records = new Record[RecordsPerBatch];
            var headerRecords = new Record[RecordsPerBatch];
            for (var i = 0; i < records.Length; i++)
            {
                records[i] = new Record
                {
                    OffsetDelta = i,
                    TimestampDelta = i,
                    Key = value,
                    Value = value
                };
                headerRecords[i] = new Record
                {
                    OffsetDelta = i,
                    TimestampDelta = i,
                    Key = value,
                    Value = value,
                    Headers = [new Header("record-id", value)]
                };
            }

            _batchRecords[b] = records;
            _headerBatchRecords[b] = headerRecords;
        }

        _completedConsumer = CreateConsumer(new CompletedDeserializer());
        _yieldingConsumer = CreateConsumer(new YieldingDeserializer());
        _completedKeyHeaderValueConsumer = CreateConsumer(
            new CompletedDeserializer(),
            new HeaderDeserializer());
        _yieldingKeyHeaderValueConsumer = CreateConsumer(
            new YieldingDeserializer(),
            new HeaderDeserializer());
    }

    [IterationSetup(Target = nameof(Completed))]
    public void CompletedSetup() => Reseed(_completedConsumer);

    [IterationSetup(Target = nameof(Yielding))]
    public void YieldingSetup() => Reseed(_yieldingConsumer);

    [IterationSetup(Target = nameof(CompletedKeySyncHeaderValue))]
    public void CompletedKeySyncHeaderValueSetup() =>
        Reseed(_completedKeyHeaderValueConsumer, _headerBatchRecords);

    [IterationSetup(Target = nameof(YieldingKeySyncHeaderValue))]
    public void YieldingKeySyncHeaderValueSetup() =>
        Reseed(_yieldingKeyHeaderValueConsumer, _headerBatchRecords);

    [Benchmark(OperationsPerInvoke = PollsPerIteration)]
    public ValueTask Completed() => ConsumeAllAsync(_completedConsumer);

    [Benchmark(OperationsPerInvoke = PollsPerIteration)]
    public ValueTask Yielding() => ConsumeAllAsync(_yieldingConsumer);

    [Benchmark(OperationsPerInvoke = PollsPerIteration)]
    public ValueTask CompletedKeySyncHeaderValue() =>
        ConsumeAllAsync(_completedKeyHeaderValueConsumer);

    [Benchmark(OperationsPerInvoke = PollsPerIteration)]
    public ValueTask YieldingKeySyncHeaderValue() =>
        ConsumeAllAsync(_yieldingKeyHeaderValueConsumer);

    [GlobalCleanup]
    public void Cleanup()
    {
        BufferedConsumerHarness.DrainPendingFetches(_completedConsumer);
        BufferedConsumerHarness.DrainPendingFetches(_yieldingConsumer);
        BufferedConsumerHarness.DrainPendingFetches(_completedKeyHeaderValueConsumer);
        BufferedConsumerHarness.DrainPendingFetches(_yieldingKeyHeaderValueConsumer);
        _completedConsumer.DisposeAsync().AsTask().GetAwaiter().GetResult();
        _yieldingConsumer.DisposeAsync().AsTask().GetAwaiter().GetResult();
        _completedKeyHeaderValueConsumer.DisposeAsync().AsTask().GetAwaiter().GetResult();
        _yieldingKeyHeaderValueConsumer.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    // BenchmarkDotNet synchronously waits for an async operation between invocations. Running a
    // full consumer loop per invocation keeps successive polls on the async continuation thread,
    // matching application usage and measuring steady-state allocations per consumed record.
#if NET
    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
#endif
    private static async ValueTask ConsumeAllAsync(
        KafkaConsumer<ReadOnlyMemory<byte>, ReadOnlyMemory<byte>> consumer)
    {
        for (var i = 0; i < PollsPerIteration; i++)
        {
            if (await consumer.ConsumeOneAsync(PollTimeout).ConfigureAwait(false) is null)
                throw new InvalidOperationException("Buffered benchmark record was unavailable.");
        }
    }

    private static KafkaConsumer<ReadOnlyMemory<byte>, ReadOnlyMemory<byte>> CreateConsumer(
        IAsyncDeserializer<ReadOnlyMemory<byte>> asyncValueDeserializer)
    {
        var consumer = new KafkaConsumer<ReadOnlyMemory<byte>, ReadOnlyMemory<byte>>(
            new ConsumerOptions
            {
                BootstrapServers = ["localhost:9092"],
                OffsetCommitMode = OffsetCommitMode.Manual,
                QueuedMinMessages = 1
            },
            Serializers.RawBytes,
            Serializers.RawBytes,
            asyncValueDeserializer: asyncValueDeserializer);
        BufferedConsumerHarness.InitializeForBufferedFastPath(consumer, Topic, Partition);
        return consumer;
    }

    private static KafkaConsumer<ReadOnlyMemory<byte>, ReadOnlyMemory<byte>> CreateConsumer(
        IAsyncDeserializer<ReadOnlyMemory<byte>> asyncKeyDeserializer,
        IDeserializer<ReadOnlyMemory<byte>> valueDeserializer)
    {
        var consumer = new KafkaConsumer<ReadOnlyMemory<byte>, ReadOnlyMemory<byte>>(
            new ConsumerOptions
            {
                BootstrapServers = ["localhost:9092"],
                OffsetCommitMode = OffsetCommitMode.Manual,
                QueuedMinMessages = 1
            },
            Serializers.RawBytes,
            valueDeserializer,
            asyncKeyDeserializer: asyncKeyDeserializer);
        BufferedConsumerHarness.InitializeForBufferedFastPath(consumer, Topic, Partition);
        return consumer;
    }

    private void Reseed(KafkaConsumer<ReadOnlyMemory<byte>, ReadOnlyMemory<byte>> consumer)
        => Reseed(consumer, _batchRecords);

    private static void Reseed(
        KafkaConsumer<ReadOnlyMemory<byte>, ReadOnlyMemory<byte>> consumer,
        Record[][] records)
        => BufferedConsumerHarness.ReseedPendingFetches(
            consumer, Topic, Partition, records, BatchCount, RecordsPerBatch);

    private sealed class CompletedDeserializer : IAsyncDeserializer<ReadOnlyMemory<byte>>
    {
        public ValueTask<ReadOnlyMemory<byte>> DeserializeAsync(
            ReadOnlyMemory<byte> data,
            SerializationContext context,
            CancellationToken cancellationToken = default)
            => ValueTask.FromResult(data);
    }

    private sealed class YieldingDeserializer : IAsyncDeserializer<ReadOnlyMemory<byte>>
    {
        [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
        public async ValueTask<ReadOnlyMemory<byte>> DeserializeAsync(
            ReadOnlyMemory<byte> data,
            SerializationContext context,
            CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            return data;
        }
    }

    private sealed class HeaderDeserializer :
        IDeserializer<ReadOnlyMemory<byte>>,
        IRecordHeaderDeserializer<ReadOnlyMemory<byte>>,
        IRecordHeaderRoutingProvider
    {
        private const string HeaderName = "record-id";

        public ReadOnlyMemory<byte> Deserialize(
            ReadOnlyMemory<byte> data,
            SerializationContext context) =>
            data;

        public ReadOnlyMemory<byte> Deserialize(
            ReadOnlyMemory<byte> data,
            SerializationContext context,
            in RecordHeaderRoutingLookup headers) =>
            headers.TryGetLast(HeaderName, out _) ? data : ReadOnlyMemory<byte>.Empty;

        public void CollectHeaderNames(List<string> names) => names.Add(HeaderName);
    }
}
