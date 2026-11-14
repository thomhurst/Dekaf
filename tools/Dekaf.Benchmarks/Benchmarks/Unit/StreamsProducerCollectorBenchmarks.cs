using System.Reflection;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using Dekaf.Metadata;
using Dekaf.Producer;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.Serialization;
using Dekaf.Streams.Internal.Runtime;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>
/// Broker-free benchmark for a Dekaf.Streams task's output send: the record collector hands each
/// record to a real producer with an explicit partition, timestamp and delivery handler, on cached
/// metadata, and a drainer thread keeps the accumulator from filling.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RunStrategy.Throughput, launchCount: 1, warmupCount: 3, iterationCount: 3)]
public class StreamsProducerCollectorBenchmarks
{
    private const string Topic = "streams-collector";
    private const int RecordsPerInvoke = 100;
    private const long FixtureCapacityBytes = 1L << 30;

    private KafkaProducer<ReadOnlyMemory<byte>?, ReadOnlyMemory<byte>?> _producer = null!;
    private ProducerCollector _collector = null!;
    private RecordAccumulator _accumulator = null!;
    private CancellationTokenSource _drainerCts = null!;
    private Thread _drainerThread = null!;
    private readonly byte[] _key = new byte[8];
    private readonly byte[] _value = new byte[100];

    [GlobalSetup]
    public async Task Setup()
    {
        _producer = new KafkaProducer<ReadOnlyMemory<byte>?, ReadOnlyMemory<byte>?>(
            new ProducerOptions
            {
                BootstrapServers = ["localhost:9092"],
                ClientId = "streams-collector",
                BufferMemory = (ulong)FixtureCapacityBytes,
                BatchSize = 1_048_576,
                LingerMs = 1_000,
                RequestTimeoutMs = 500,
                DeliveryTimeoutMs = 1_000,
                CloseTimeoutMs = 1_000,
                EnableIdempotence = false,
                // Keep admission non-blocking so this measures front-end CPU, not drainer scheduling.
                UnackedByteBudgetCapOverride = FixtureCapacityBytes,
            },
            NullableBytesSerde.Instance,
            NullableBytesSerde.Instance);

        await _producer.StopSenderLoopsForTestingAsync().ConfigureAwait(false);
        SeedMetadata(_producer);
        SetInstanceField(_producer, "_initialized", true);

        _accumulator = _producer.RecordAccumulator;
        if (_accumulator.GetBrokerUnackedBudget(brokerId: 0) is { } budget)
            SetInstanceField(budget, "_budgetBytes", FixtureCapacityBytes);

        _collector = new ProducerCollector(_producer);
        _drainerCts = new CancellationTokenSource();
        _drainerThread = new Thread(() => DrainLoop(_drainerCts.Token))
        {
            IsBackground = true,
            Name = "streams-collector-drainer",
            Priority = ThreadPriority.Highest,
        };
        _drainerThread.Start();

        SendBatch();
    }

    [GlobalCleanup]
    public async Task Cleanup()
    {
        _drainerCts.Cancel();
        _drainerThread.Join();
        _drainerCts.Dispose();
        _collector.Dispose();
        await _producer.DisposeAsync().ConfigureAwait(false);
    }

    [Benchmark(OperationsPerInvoke = RecordsPerInvoke)]
    public void SendBatch()
    {
        for (var i = 0; i < RecordsPerInvoke; i++)
        {
            _collector.Send(Topic, i & 3, _key, false, _value, false, timestamp: 1_750_000_000_000 + i, headers: null);
        }

        if (_collector.HasPending || _collector.Failure is not null)
        {
            throw new InvalidOperationException(
                $"Collector send did not complete synchronously or failed: {_collector.Failure?.Message}. " +
                $"BufferedBytes={_accumulator.BufferedBytes}.");
        }

        // Appends copy the record into the batch, so the arena is free once the sends return.
        _collector.Reset();
    }

    /// <summary>
    /// Recycles only published batches: the deque-polling <c>TryDrainBatch</c> can take a batch
    /// before the sealing thread's last touch (<c>StartPreSerialization</c>) and return it to the
    /// pool under it.
    /// </summary>
    private void DrainLoop(CancellationToken cancellationToken)
    {
        var spinner = new SpinWait();
        while (!cancellationToken.IsCancellationRequested)
        {
            if (_accumulator.TryDrainPublishedBatch(out var batch))
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

    private static void SeedMetadata(object producer)
    {
        var partitions = new PartitionMetadata[4];
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
            ClusterId = "streams-collector",
            ControllerId = 0,
            Topics = [new TopicMetadata { ErrorCode = ErrorCode.None, Name = Topic, Partitions = partitions }],
        });
        typeof(MetadataManager)
            .GetMethod(
                "UpdateMetadataClusterId",
                BindingFlags.NonPublic | BindingFlags.Instance,
                binder: null,
                [typeof(string)],
                modifiers: null)!
            .Invoke(metadataManager, ["streams-collector"]);
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
}
