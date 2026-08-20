using System.Buffers;
using System.Reflection;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using Dekaf.Benchmarks.Infrastructure;
using Dekaf.Metadata;
using Dekaf.Producer;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.Serialization;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>
/// Broker-free benchmark for keyed FireAsync with a user partitioner.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RunStrategy.Throughput, launchCount: 1, warmupCount: 3, iterationCount: 5)]
public class CustomPartitionerFireHotPathBenchmarks
{
    private const string Topic = "custom-partitioner-fire-hot-path";
    private const long FixtureCapacityBytes = 1L << 30;
    private static readonly string[] Keys = BenchmarkData.CreateKeys(10_000);

    private KafkaProducer<string, string> _producer = null!;
    private KafkaProducer<string, string> _recursiveProducer = null!;
    private RecordAccumulator _accumulator = null!;
    private RecordAccumulator _recursiveAccumulator = null!;
    private CancellationTokenSource _drainerCts = null!;
    private Thread _drainerThread = null!;
    private ProducerMessage<string, string>[] _messages = null!;
    private ProducerMessage<string, string>[] _recursiveMessages = null!;

    [GlobalSetup]
    public async Task Setup()
    {
        _producer = await CreateProducerAsync(
                "custom-partitioner-fire-hot-path",
                FirstPartitioner.Instance)
            .ConfigureAwait(false);
        _accumulator = _producer.RecordAccumulator;

        var value = new string('x', 1_000);
        _messages = new ProducerMessage<string, string>[100];
        for (var index = 0; index < _messages.Length; index++)
        {
            _messages[index] = new ProducerMessage<string, string>
            {
                Topic = Topic,
                Key = Keys[index],
                Value = value,
            };
        }

        var recursivePartitioner = new RecursivePartitioner();
        _recursiveProducer = await CreateProducerAsync(
                "recursive-custom-partitioner-fire-hot-path",
                recursivePartitioner)
            .ConfigureAwait(false);
        _recursiveAccumulator = _recursiveProducer.RecordAccumulator;

        _recursiveMessages = new ProducerMessage<string, string>[100];
        for (var index = 0; index < _recursiveMessages.Length; index++)
        {
            _recursiveMessages[index] = new ProducerMessage<string, string>
            {
                Topic = Topic,
                Key = Keys[index + _messages.Length],
                Value = value,
            };
        }

        recursivePartitioner.Initialize(
            _recursiveProducer,
            new ProducerMessage<string, string>
            {
                Topic = Topic,
                Key = "recursive-inner-1",
                Value = value,
            },
            new ProducerMessage<string, string>
            {
                Topic = Topic,
                Key = "recursive-inner-2",
                Value = value,
            });

        _drainerCts = new CancellationTokenSource();
        _drainerThread = new Thread(() => DrainLoop(_drainerCts.Token))
        {
            IsBackground = true,
            Name = "custom-partitioner-fire-hot-path-drainer",
            Priority = ThreadPriority.Highest,
        };
        _drainerThread.Start();
        FireBatch();
        FireRecursiveBatch();
    }

    private static async Task<KafkaProducer<string, string>> CreateProducerAsync(
        string clientId,
        IPartitioner partitioner)
    {
        var producer = new KafkaProducer<string, string>(
            new ProducerOptions
            {
                BootstrapServers = ["localhost:9092"],
                ClientId = clientId,
                BufferMemory = (ulong)FixtureCapacityBytes,
                BatchSize = 1_048_576,
                LingerMs = 1_000,
                RequestTimeoutMs = 500,
                DeliveryTimeoutMs = 1_000,
                CloseTimeoutMs = 1_000,
                EnableIdempotence = false,
                UnackedByteBudgetCapOverride = FixtureCapacityBytes,
                CustomPartitioner = partitioner,
            },
            Serializers.String,
            RecordHeaderStringSerializer.Instance);

        await producer.StopSenderLoopsForTestingAsync().ConfigureAwait(false);
        SeedMetadata(producer);
        SetInstanceField(producer, "_initialized", true);
        if (producer.RecordAccumulator.GetBrokerUnackedBudget(brokerId: 0) is { } budget)
            SetInstanceField(budget, "_budgetBytes", FixtureCapacityBytes);

        return producer;
    }

    [GlobalCleanup]
    public async Task Cleanup()
    {
        _drainerCts.Cancel();
        _drainerThread.Join();
        _drainerCts.Dispose();
        await _producer.DisposeAsync().ConfigureAwait(false);
        await _recursiveProducer.DisposeAsync().ConfigureAwait(false);
    }

    [Benchmark(OperationsPerInvoke = 100)]
    public void FireBatch()
    {
        for (var index = 0; index < _messages.Length; index++)
        {
            var result = _producer.FireAsync(_messages[index]);
            if (!result.IsCompletedSuccessfully)
            {
                throw new InvalidOperationException(
                    $"Producer front-end became asynchronous; BufferedBytes={_accumulator.BufferedBytes}, " +
                    $"PendingAppends={_accumulator.PendingAppendCountForTest}.");
            }
        }
    }

    [Benchmark(OperationsPerInvoke = 300)]
    public void FireRecursiveBatch()
    {
        for (var index = 0; index < _recursiveMessages.Length; index++)
        {
            var result = _recursiveProducer.FireAsync(_recursiveMessages[index]);
            if (!result.IsCompletedSuccessfully)
            {
                throw new InvalidOperationException(
                    $"Recursive producer front-end became asynchronous; BufferedBytes={_recursiveAccumulator.BufferedBytes}, " +
                    $"PendingAppends={_recursiveAccumulator.PendingAppendCountForTest}.");
            }
        }
    }

    private void DrainLoop(CancellationToken cancellationToken)
    {
        var spinner = new SpinWait();
        while (!cancellationToken.IsCancellationRequested)
        {
            var drained = DrainOne(_accumulator);
            drained |= DrainOne(_recursiveAccumulator);
            if (drained)
                spinner.Reset();
            else
                spinner.SpinOnce();
        }
    }

    private static bool DrainOne(RecordAccumulator accumulator)
    {
        if (!accumulator.TryDrainBatch(out var batch))
            return false;

        accumulator.OnBatchExitsPipeline(batch);
        accumulator.ReleaseMemory(batch.DataSize);
        accumulator.ReturnReadyBatch(batch);
        return true;
    }

    private static void SeedMetadata(KafkaProducer<string, string> producer)
    {
        var partitions = new PartitionMetadata[12];
        for (var partition = 0; partition < partitions.Length; partition++)
        {
            partitions[partition] = new PartitionMetadata
            {
                ErrorCode = ErrorCode.None,
                PartitionIndex = partition,
                LeaderId = 0,
                ReplicaNodes = [0],
                IsrNodes = [0],
            };
        }

        var metadataManager = GetInstanceField<MetadataManager>(producer, "_metadataManager");
        metadataManager.Metadata.Update(new MetadataResponse
        {
            Brokers = [new BrokerMetadata { NodeId = 0, Host = "localhost", Port = 9092 }],
            ClusterId = "custom-partitioner-fire-hot-path",
            ControllerId = 0,
            Topics =
            [
                new TopicMetadata
                {
                    ErrorCode = ErrorCode.None,
                    Name = Topic,
                    Partitions = partitions,
                },
            ],
        });
        typeof(MetadataManager)
            .GetMethod(
                "UpdateMetadataClusterId",
                BindingFlags.NonPublic | BindingFlags.Instance,
                binder: null,
                [typeof(string)],
                modifiers: null)!
            .Invoke(metadataManager, ["custom-partitioner-fire-hot-path"]);
    }

    private static T GetInstanceField<T>(object target, string name)
    {
        const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        return (T)target.GetType().GetField(name, Flags)!.GetValue(target)!;
    }

    private static void SetInstanceField<T>(object target, string name, T value)
    {
        const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        target.GetType().GetField(name, Flags)!.SetValue(target, value);
    }

    private sealed class FirstPartitioner : IPartitioner
    {
        internal static readonly FirstPartitioner Instance = new();

        public int Partition(string topic, ReadOnlySpan<byte> key, bool keyIsNull, int partitionCount) => 0;
    }

    private sealed class RecursivePartitioner : IPartitioner
    {
        private readonly ProducerMessage<string, string>[] _nestedMessages = new ProducerMessage<string, string>[2];
        private KafkaProducer<string, string> _producer = null!;
        private int _depth;

        internal void Initialize(
            KafkaProducer<string, string> producer,
            ProducerMessage<string, string> firstNestedMessage,
            ProducerMessage<string, string> secondNestedMessage)
        {
            _producer = producer;
            _nestedMessages[0] = firstNestedMessage;
            _nestedMessages[1] = secondNestedMessage;
        }

        public int Partition(string topic, ReadOnlySpan<byte> key, bool keyIsNull, int partitionCount)
        {
            if (_depth == _nestedMessages.Length)
                return 0;

            var nestedMessage = _nestedMessages[_depth++];
            try
            {
                var result = _producer.FireAsync(nestedMessage);
                if (!result.IsCompletedSuccessfully)
                    throw new InvalidOperationException("Expected recursive FireAsync to complete synchronously.");
            }
            finally
            {
                _depth--;
            }

            return 0;
        }
    }

    private sealed class RecordHeaderStringSerializer : ISerializer<string>, IRecordHeaderSerializer
    {
        internal static readonly RecordHeaderStringSerializer Instance = new();

        public bool ProducesRecordHeaders => true;

        public void Serialize<TWriter>(string value, ref TWriter destination, SerializationContext context)
            where TWriter : IBufferWriter<byte>
#if NET10_0_OR_GREATER
            , allows ref struct
#endif
            => Serializers.String.Serialize(value, ref destination, context);
    }
}
