using System.Buffers;
using System.Diagnostics;
using System.Reflection;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Jobs;
using Dekaf.Metadata;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.Producer;
using Dekaf.Serialization;

namespace Dekaf.Benchmarks.Benchmarks.Client;

[MemoryDiagnoser]
[Config(typeof(AllocationJobConfig))]
public class TransactionalProduceAllocationBenchmarks
{
    private const int MessagesPerIteration = 50_000;

    private sealed class AllocationJobConfig : ManualConfig
    {
        public AllocationJobConfig()
        {
            AddJob(Job.Default
                .WithStrategy(RunStrategy.Throughput)
                .WithLaunchCount(1)
                .WithWarmupCount(3)
                .WithIterationCount(10)
                .WithInvocationCount(MessagesPerIteration)
                .WithUnrollFactor(1));
        }
    }

    private IKafkaProducer<string, string> _producer = null!;
    private ITransaction<string, string> _transaction = null!;
    private ProducerMessage<string, string> _message = null!;
    private RecordAccumulator _accumulator = null!;
    private TopicPartition _topicPartition;
    private long _offset;
    private CancellationTokenSource _cancellation = null!;
    private ActivityListener? _activityListener;

    [Params(false, true)]
    public bool UsePreparedSerializer { get; set; }

    [Params(false, true)]
    public bool TracingEnabled { get; set; }

    [GlobalSetup]
    public async Task Setup()
    {
        _producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithTransactionalId("benchmark-transaction-allocation")
            .WithBufferMemory(ulong.MaxValue)
            .WithLinger(TimeSpan.Zero)
            .WithValueSerializer(UsePreparedSerializer ? PreparedStringSerializer.Instance : Serializers.String)
            .Build();
        if (TracingEnabled)
        {
            _activityListener = new ActivityListener
            {
                ShouldListenTo = static source => source.Name == Diagnostics.DekafDiagnostics.ActivitySourceName,
                Sample = static (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
            };
            ActivitySource.AddActivityListener(_activityListener);
        }
        var producer = (KafkaProducer<string, string>)_producer;
        await producer.StopSenderLoopsForTestingAsync();

        SeedMetadata(GetField<MetadataManager>(producer, "_metadataManager"));
        SetField(producer, "_initialized", true);
        SetField(producer, "_transactionState", TransactionState.Ready);
        _transaction = _producer.BeginTransaction();
        _accumulator = producer.RecordAccumulator;
        _topicPartition = new TopicPartition("benchmark-transaction-allocation", 0);
        // No explicit Partition: every benchmark routes through the partitioner
        // (single-partition topic) so the componentwise overload's partition
        // selection cost is not asymmetrically absent from the message paths.
        _message = new ProducerMessage<string, string>
        {
            Topic = "benchmark-transaction-allocation",
            Key = "key",
            Value = "value"
        };
        _cancellation = new CancellationTokenSource();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _activityListener?.Dispose();
        _cancellation.Dispose();
    }

    [Benchmark(Baseline = true)]
    public void ProducerProduceAsync() =>
        CompleteCycle(_producer.ProduceAsync(_message));

    [Benchmark]
    public void TransactionProduceAsync() =>
        CompleteCycle(_transaction.ProduceAsync(_message));

    [Benchmark]
    public void TransactionProduceAsyncComponentwise() =>
        CompleteCycle(_transaction.ProduceAsync("benchmark-transaction-allocation", "key", "value"));

    /// <summary>
    /// Same cycle with a live cancellable token so RegisterCancellation runs per
    /// produce (UnsafeRegister on modern TFMs); GetResult disposes the registration
    /// each cycle, so the steady state must stay allocation-free.
    /// </summary>
    [Benchmark]
    public void TransactionProduceAsyncComponentwiseCancellable() =>
        CompleteCycle(_transaction.ProduceAsync(
            "benchmark-transaction-allocation", "key", "value", _cancellation.Token));

    /// <summary>
    /// Completes the serial-awaited one-message-per-batch lifecycle the EOS stress lane
    /// runs (and the unit allocation gate mirrors): linger sweep seals the just-appended
    /// batch, the batch is drained and retired the way BrokerSender does, and the caller's
    /// awaited ValueTask is consumed so the pooled completion source returns to its pool.
    /// Without this, every invocation would accumulate pending records and rent a fresh
    /// source, measuring pool churn instead of the steady-state produce cycle.
    /// </summary>
    private void CompleteCycle(ValueTask<RecordMetadata> produce)
    {
        var seal = _accumulator.ExpireLingerAsync(CancellationToken.None);
        if (seal.IsCompletedSuccessfully)
            seal.GetAwaiter().GetResult();

        if (_accumulator.TryDrainBatch(_topicPartition, out var batch))
        {
            batch.CompleteSend(_offset++, DateTimeOffset.UtcNow);
            _accumulator.ReleaseBatchMemory(batch);
            _accumulator.OnBatchExitsPipeline(batch);
            _accumulator.ReturnReadyBatch(batch);
        }

        _ = produce.GetAwaiter().GetResult();
        // AwaitWithActivity completes on a worker in this synchronous harness. Reset the
        // benchmark thread's ambient span so later invocations do not become nested children.
        Activity.Current = null;
    }

    private static void SeedMetadata(MetadataManager metadataManager) =>
        metadataManager.Metadata.Update(new MetadataResponse
        {
            Brokers =
            [
                new BrokerMetadata { NodeId = 0, Host = "localhost", Port = 9092 }
            ],
            ClusterId = "benchmark-cluster",
            ControllerId = 0,
            Topics =
            [
                new TopicMetadata
                {
                    ErrorCode = ErrorCode.None,
                    Name = "benchmark-transaction-allocation",
                    Partitions =
                    [
                        new PartitionMetadata
                        {
                            ErrorCode = ErrorCode.None,
                            PartitionIndex = 0,
                            LeaderId = 0,
                            ReplicaNodes = [0],
                            IsrNodes = [0]
                        }
                    ]
                }
            ]
        });

    private static T GetField<T>(object target, string name) =>
        (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(target)!;

    private static void SetField<T>(object target, string name, T value) =>
        target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(target, value);

    private sealed class PreparedStringSerializer :
        ISerializer<string>,
        IAsyncSerializerPreparationAdmission<string>
    {
        internal static readonly PreparedStringSerializer Instance = new();
        private static readonly object PreparedSchema = new();

        public ValueTask PrepareAsync(
            string value,
            SerializationContext context,
            CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public ValueTask<SerializerPreparationAdmission> PrepareForSerializationAsync(
            string value,
            SerializationContext context,
            CancellationToken cancellationToken = default) =>
            new(new SerializerPreparationAdmission("benchmark", 1, PreparedSchema));

        public void Serialize<TWriter>(string value, ref TWriter destination, SerializationContext context)
            where TWriter : IBufferWriter<byte>
#if NET10_0_OR_GREATER
            , allows ref struct
#endif
            => Serializers.String.Serialize(value, ref destination, context);

        public void SerializePrepared<TWriter>(
            string value,
            ref TWriter destination,
            SerializationContext context,
            in SerializerPreparationAdmission admission)
            where TWriter : IBufferWriter<byte>
#if NET10_0_OR_GREATER
            , allows ref struct
#endif
            => Serializers.String.Serialize(value, ref destination, context);
    }
}
