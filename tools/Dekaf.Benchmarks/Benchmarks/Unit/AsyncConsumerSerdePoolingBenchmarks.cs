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
                .WithInvocationCount(PollsPerIteration)
                .WithUnrollFactor(1));
        }
    }

    private Record[][] _batchRecords = null!;
    private KafkaConsumer<ReadOnlyMemory<byte>, ReadOnlyMemory<byte>> _completedConsumer = null!;
    private KafkaConsumer<ReadOnlyMemory<byte>, ReadOnlyMemory<byte>> _yieldingConsumer = null!;

    [GlobalSetup]
    public void Setup()
    {
        var value = new byte[100];
        _batchRecords = new Record[10][];
        for (var b = 0; b < _batchRecords.Length; b++)
        {
            var records = new Record[RecordsPerBatch];
            for (var i = 0; i < records.Length; i++)
            {
                records[i] = new Record
                {
                    OffsetDelta = i,
                    TimestampDelta = i,
                    Key = value,
                    Value = value
                };
            }

            _batchRecords[b] = records;
        }

        _completedConsumer = CreateConsumer(new CompletedDeserializer());
        _yieldingConsumer = CreateConsumer(new YieldingDeserializer());
    }

    [IterationSetup(Target = nameof(Completed))]
    public void CompletedSetup() => Reseed(_completedConsumer);

    [IterationSetup(Target = nameof(Yielding))]
    public void YieldingSetup() => Reseed(_yieldingConsumer);

    [Benchmark]
    public ValueTask<ConsumeResult<ReadOnlyMemory<byte>, ReadOnlyMemory<byte>>?> Completed()
        => _completedConsumer.ConsumeOneAsync(PollTimeout);

    [Benchmark]
    public ValueTask<ConsumeResult<ReadOnlyMemory<byte>, ReadOnlyMemory<byte>>?> Yielding()
        => _yieldingConsumer.ConsumeOneAsync(PollTimeout);

    [GlobalCleanup]
    public void Cleanup()
    {
        BufferedConsumerHarness.DrainPendingFetches(_completedConsumer);
        BufferedConsumerHarness.DrainPendingFetches(_yieldingConsumer);
        _completedConsumer.DisposeAsync().AsTask().GetAwaiter().GetResult();
        _yieldingConsumer.DisposeAsync().AsTask().GetAwaiter().GetResult();
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

    private void Reseed(KafkaConsumer<ReadOnlyMemory<byte>, ReadOnlyMemory<byte>> consumer)
        => BufferedConsumerHarness.ReseedPendingFetches(
            consumer, Topic, Partition, _batchRecords, BatchCount, RecordsPerBatch);

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
}
