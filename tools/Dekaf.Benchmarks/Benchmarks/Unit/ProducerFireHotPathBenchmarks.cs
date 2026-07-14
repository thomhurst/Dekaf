using System.Reflection;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using Dekaf.Metadata;
using Dekaf.Producer;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.Serialization;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>
/// Broker-free benchmark for the complete keyed <see cref="KafkaProducer{TKey,TValue}.FireAsync(string,TKey,TValue)"/>
/// front-end: metadata cache, serialization, partitioning, timestamping, and accumulator append.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RunStrategy.Throughput, launchCount: 1, warmupCount: 3, iterationCount: 3)]
public class ProducerFireHotPathBenchmarks
{
    private const string Topic = "producer-fire-hot-path";
    private static readonly string[] Keys = CreateKeys();

    private KafkaProducer<string, string> _producer = null!;
    private RecordAccumulator _accumulator = null!;
    private CancellationTokenSource _drainerCts = null!;
    private Task _drainerTask = null!;
    private string _value = null!;

    [Params(1000)]
    public int MessageSize { get; set; }

    [Params(0, 10)]
    public int DeliveryLatencyTargetMs { get; set; }

    [GlobalSetup]
    public async Task Setup()
    {
        _producer = new KafkaProducer<string, string>(
            new ProducerOptions
            {
                BootstrapServers = ["localhost:9092"],
                ClientId = "producer-fire-hot-path",
                BufferMemory = 256UL * 1024 * 1024,
                BatchSize = 1_048_576,
                LingerMs = 1_000,
                RequestTimeoutMs = 500,
                DeliveryTimeoutMs = 1_000,
                CloseTimeoutMs = 1_000,
                EnableIdempotence = false,
                DeliveryLatencyTargetMs = DeliveryLatencyTargetMs,
                // Keep admission non-blocking so this measures front-end CPU, not drainer scheduling.
                UnackedByteBudgetCapOverride = 1L << 30,
            },
            Serializers.String,
            Serializers.String);

        await StopBackgroundLoopsAsync(_producer).ConfigureAwait(false);
        SeedMetadata(_producer);
        SetInstanceField(_producer, "_initialized", true);

        _accumulator = _producer.RecordAccumulator;
        _value = new string('x', MessageSize);
        _drainerCts = new CancellationTokenSource();
        _drainerTask = Task.Run(() => DrainLoop(_drainerCts.Token));

        FireBatch();
    }

    [GlobalCleanup]
    public async Task Cleanup()
    {
        await _drainerCts.CancelAsync().ConfigureAwait(false);
        try
        {
            await _drainerTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }

        await _producer.DisposeAsync().ConfigureAwait(false);
    }

    [Benchmark(OperationsPerInvoke = 100)]
    public void FireBatch()
    {
        for (var i = 0; i < 100; i++)
        {
            var result = _producer.FireAsync(Topic, Keys[i], _value);
            if (!result.IsCompletedSuccessfully)
                result.GetAwaiter().GetResult();
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

    private static string[] CreateKeys()
    {
        var keys = new string[10_000];
        for (var i = 0; i < keys.Length; i++)
            keys[i] = $"key-{i}";
        return keys;
    }

    private static void SeedMetadata(KafkaProducer<string, string> producer)
    {
        var metadataManager = GetInstanceField<MetadataManager>(producer, "_metadataManager");
        metadataManager.Metadata.Update(new MetadataResponse
        {
            Brokers =
            [
                new BrokerMetadata { NodeId = 0, Host = "localhost", Port = 9092 },
            ],
            ClusterId = "producer-fire-hot-path",
            ControllerId = 0,
            Topics =
            [
                new TopicMetadata
                {
                    ErrorCode = ErrorCode.None,
                    Name = Topic,
                    Partitions =
                    [
                        new PartitionMetadata
                        {
                            ErrorCode = ErrorCode.None,
                            PartitionIndex = 0,
                            LeaderId = 0,
                            ReplicaNodes = [0],
                            IsrNodes = [0],
                        },
                    ],
                },
            ],
        });
    }

    private static async Task StopBackgroundLoopsAsync(KafkaProducer<string, string> producer)
    {
        var cancellation = GetInstanceField<CancellationTokenSource>(producer, "_senderCts");
        var senderTask = GetInstanceField<Task>(producer, "_senderTask");
        var lingerTask = GetInstanceField<Task>(producer, "_lingerTask");

        await cancellation.CancelAsync().ConfigureAwait(false);
        await Task.WhenAll(senderTask, lingerTask).WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
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
