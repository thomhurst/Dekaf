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
    private RecordAccumulator _accumulator = null!;
    private CancellationTokenSource _drainerCts = null!;
    private Thread _drainerThread = null!;
    private ProducerMessage<string, string>[] _messages = null!;

    [GlobalSetup]
    public async Task Setup()
    {
        _producer = new KafkaProducer<string, string>(
            new ProducerOptions
            {
                BootstrapServers = ["localhost:9092"],
                ClientId = "custom-partitioner-fire-hot-path",
                BufferMemory = (ulong)FixtureCapacityBytes,
                BatchSize = 1_048_576,
                LingerMs = 1_000,
                RequestTimeoutMs = 500,
                DeliveryTimeoutMs = 1_000,
                CloseTimeoutMs = 1_000,
                EnableIdempotence = false,
                UnackedByteBudgetCapOverride = FixtureCapacityBytes,
                CustomPartitioner = FirstPartitioner.Instance,
            },
            Serializers.String,
            Serializers.String);

        await _producer.StopSenderLoopsForTestingAsync().ConfigureAwait(false);
        SeedMetadata(_producer);
        SetInstanceField(_producer, "_initialized", true);

        _accumulator = _producer.RecordAccumulator;
        if (_accumulator.GetBrokerUnackedBudget(brokerId: 0) is { } budget)
            SetInstanceField(budget, "_budgetBytes", FixtureCapacityBytes);

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

        _drainerCts = new CancellationTokenSource();
        _drainerThread = new Thread(() => DrainLoop(_drainerCts.Token))
        {
            IsBackground = true,
            Name = "custom-partitioner-fire-hot-path-drainer",
            Priority = ThreadPriority.Highest,
        };
        _drainerThread.Start();
        FireBatch();
    }

    [GlobalCleanup]
    public async Task Cleanup()
    {
        _drainerCts.Cancel();
        _drainerThread.Join();
        _drainerCts.Dispose();
        await _producer.DisposeAsync().ConfigureAwait(false);
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

    private void DrainLoop(CancellationToken cancellationToken)
    {
        var spinner = new SpinWait();
        while (!cancellationToken.IsCancellationRequested)
        {
            if (_accumulator.TryDrainBatch(out var batch))
            {
                _accumulator.OnBatchExitsPipeline(batch);
                _accumulator.ReleaseMemory(batch.DataSize);
                _accumulator.ReturnReadyBatch(batch);
                spinner.Reset();
            }
            else
            {
                spinner.SpinOnce();
            }
        }
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
}
