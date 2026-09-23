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
/// Broker-free benchmark for the awaited <see cref="KafkaProducer{TKey,TValue}.ProduceAsync(ProducerMessage{TKey,TValue},CancellationToken)"/>
/// synchronous fast path: admission, metadata cache, serialization, partitioning and the span
/// append into the accumulator with a completion source. A background drainer completes and
/// recycles published batches (as a sender would on success), so each produce's pooled completion
/// resolves and is returned; nothing is set up per iteration. Uses only APIs that exist on main so
/// the gate compares both revisions.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RunStrategy.Throughput, launchCount: 1, warmupCount: 3, iterationCount: 5)]
public class ProducerProduceAsyncHotPathBenchmarks
{
    private const string Topic = "producer-produce-hot-path";
    private const int ProducesPerInvoke = 100;
    private const long FixtureCapacityBytes = 1L << 30;
    private static readonly string[] Keys = BenchmarkData.CreateKeys(ProducesPerInvoke);

    private readonly ValueTask<RecordMetadata>[] _pending = new ValueTask<RecordMetadata>[ProducesPerInvoke];
    private KafkaProducer<string, string> _producer = null!;
    private RecordAccumulator _accumulator = null!;
    private CancellationTokenSource _drainerCts = null!;
    private Thread _drainerThread = null!;
    private ProducerMessage<string, string>[] _messages = null!;

    [Params(1, 12)]
    public int PartitionCount { get; set; }

    [GlobalSetup]
    public async Task Setup()
    {
        _producer = new KafkaProducer<string, string>(
            new ProducerOptions
            {
                BootstrapServers = ["localhost:9092"],
                ClientId = "producer-produce-hot-path",
                BufferMemory = (ulong)FixtureCapacityBytes,
                BatchSize = 1_048_576,
                LingerMs = 0,
                RequestTimeoutMs = 500,
                DeliveryTimeoutMs = 1_000,
                CloseTimeoutMs = 1_000,
                EnableIdempotence = false,
                DeliveryLatencyTargetMs = 0,
                UnackedByteBudgetCapOverride = FixtureCapacityBytes,
            },
            Serializers.String,
            Serializers.String);

        await _producer.StopSenderLoopsForTestingAsync().ConfigureAwait(false);
        SeedMetadata(_producer, PartitionCount);
        SetInstanceField(_producer, "_initialized", true);
        _accumulator = _producer.RecordAccumulator;

        var value = new string('x', 100);
        _messages = new ProducerMessage<string, string>[ProducesPerInvoke];
        for (var i = 0; i < _messages.Length; i++)
        {
            _messages[i] = new ProducerMessage<string, string>
            {
                Topic = Topic,
                Key = Keys[i],
                Value = value
            };
        }

        _drainerCts = new CancellationTokenSource();
        _drainerThread = new Thread(() => DrainLoop(_drainerCts.Token))
        {
            IsBackground = true,
            Name = "producer-produce-hot-path-drainer",
            Priority = ThreadPriority.Highest,
        };
        _drainerThread.Start();

        for (var i = 0; i < 20; i++)
            ProduceBatch();
    }

    [GlobalCleanup]
    public async Task Cleanup()
    {
        _drainerCts.Cancel();
        _drainerThread.Join();
        _drainerCts.Dispose();
        await _producer.DisposeAsync().ConfigureAwait(false);
    }

    [Benchmark(OperationsPerInvoke = ProducesPerInvoke)]
    public void ProduceBatch()
    {
        for (var i = 0; i < ProducesPerInvoke; i++)
            _pending[i] = _producer.ProduceAsync(_messages[i]);

        // The drainer completes each batch; no continuation is registered, so completing the
        // pooled sources schedules nothing. GetResult returns each source to its pool.
        var spinner = new SpinWait();
        for (var i = 0; i < ProducesPerInvoke; i++)
        {
            var pending = _pending[i];
            while (!pending.IsCompleted)
                spinner.SpinOnce();

            pending.GetAwaiter().GetResult();
            _pending[i] = default;
        }
    }

    private void DrainLoop(CancellationToken cancellationToken)
    {
        var spinner = new SpinWait();
        var timestamp = DateTimeOffset.UnixEpoch;
        while (!cancellationToken.IsCancellationRequested)
        {
            if (_accumulator.TryDrainPublishedBatch(out var batch))
            {
                _accumulator.MarkBatchDeliveryComplete(batch);
                batch.CompleteSend(0, timestamp);
                _accumulator.OnBatchExitsPipeline(batch);
                _accumulator.ReleaseMemory(batch.DataSize);
                _accumulator.ReturnReadyBatch(batch);
                spinner.Reset();
            }
            else
            {
                // The linger loop is stopped with the senders; seal the expired (LingerMs = 0)
                // batches here so awaited produces complete.
                var expire = _accumulator.ExpireLingerAsync(cancellationToken);
                if (!expire.IsCompletedSuccessfully)
                    expire.AsTask().GetAwaiter().GetResult();
                spinner.SpinOnce();
            }
        }
    }

    private static void SeedMetadata(KafkaProducer<string, string> producer, int partitionCount)
    {
        var partitions = new PartitionMetadata[partitionCount];
        for (var partition = 0; partition < partitionCount; partition++)
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
            Brokers =
            [
                new BrokerMetadata { NodeId = 0, Host = "localhost", Port = 9092 },
            ],
            ClusterId = "producer-produce-hot-path",
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
            .Invoke(metadataManager, ["producer-produce-hot-path"]);
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
