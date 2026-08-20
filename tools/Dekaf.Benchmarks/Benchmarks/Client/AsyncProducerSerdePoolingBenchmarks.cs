using System.Buffers;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using Dekaf.Benchmarks.Infrastructure;
using Dekaf.Metadata;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.Serialization;
using DekafProducer = Dekaf.Producer;

namespace Dekaf.Benchmarks.Benchmarks.Client;

/// <summary>
/// Measures mixed async and record-header serialization paths.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RunStrategy.Throughput, launchCount: 1, warmupCount: 5, iterationCount: 10)]
public class AsyncProducerSerdePoolingBenchmarks
{
    private const string Topic = "bench-async-producer-serde";
    private const string YieldingTopic = "bench-yielding-producer-serde";
    private const int Operations = 100;
    private const long FixtureCapacityBytes = 1L << 30;

    private static readonly string[] Keys = BenchmarkData.CreateKeys(Operations);

    private DekafProducer.KafkaProducer<string, string> _producer = null!;
    private DekafProducer.KafkaProducer<string, string> _yieldingProducer = null!;
    private DekafProducer.ObjectPool<Headers> _headerPool = null!;
    private Headers[] _rentedHeaders = null!;
    private CancellationTokenSource _drainerCts = null!;
    private Thread _drainerThread = null!;
    private DekafProducer.ProducerMessage<string, string> _messageWithoutHeaders = null!;
    private DekafProducer.ProducerMessage<string, string> _messageWithHeaders = null!;
    private string _value = null!;

    [GlobalSetup]
    public async Task Setup()
    {
        _value = new string('x', 100);
        _messageWithoutHeaders = new DekafProducer.ProducerMessage<string, string>
        {
            Topic = Topic,
            Key = Keys[0],
            Value = _value
        };
        _messageWithHeaders = new DekafProducer.ProducerMessage<string, string>
        {
            Topic = Topic,
            Key = Keys[0],
            Value = _value,
            Headers = new Headers(1).Add("caller", "value")
        };
        _producer = new DekafProducer.KafkaProducer<string, string>(
            CreateOptions("bench-async-producer-serde"),
            Serializers.String,
            new HeaderAddingStringSerializer(),
            asyncKeySerializer: new CompletedAsyncStringSerializer());
        _yieldingProducer = new DekafProducer.KafkaProducer<string, string>(
            CreateOptions("bench-yielding-producer-serde"),
            Serializers.String,
            Serializers.String,
            asyncValueSerializer: new YieldingAsyncStringSerializer());
        _headerPool = GetInstanceField<DekafProducer.ObjectPool<Headers>>(
            _producer,
            "_asyncSerializationHeadersPool");
        _rentedHeaders = new Headers[Operations];
        if (_headerPool.MaxPoolSize != Operations)
            throw new InvalidOperationException("Async serialization header pool capacity mismatch.");
        SaturateHeaderPool();

        await _producer.StopSenderLoopsForTestingAsync().ConfigureAwait(false);
        await _yieldingProducer.StopSenderLoopsForTestingAsync().ConfigureAwait(false);
        SeedMetadata(_producer, Topic);
        SeedMetadata(_yieldingProducer, YieldingTopic);
        SetInstanceField(_producer, "_initialized", true);
        SetInstanceField(_yieldingProducer, "_initialized", true);

        _drainerCts = new CancellationTokenSource();
        _drainerThread = new Thread(() => DrainLoop(_drainerCts.Token))
        {
            IsBackground = true,
            Name = "async-producer-serde-benchmark-drainer",
            Priority = ThreadPriority.Highest
        };
        _drainerThread.Start();

        for (var i = 0; i < 1_000; i++)
        {
            await _producer.ProduceAsync(Topic, Keys[i % Keys.Length], _value)
                .ConfigureAwait(false);
            await _yieldingProducer.ProduceAsync(YieldingTopic, Keys[i % Keys.Length], _value)
                .ConfigureAwait(false);
        }
    }

    [Benchmark(OperationsPerInvoke = Operations)]
    public async Task FireAsync_CompletedSerializer()
    {
        for (var i = 0; i < Operations; i++)
        {
            await _producer.FireAsync(_messageWithoutHeaders)
                .ConfigureAwait(false);
        }
    }

    [Benchmark(OperationsPerInvoke = Operations)]
    public async Task FireAsync_CompletedSerializerWithHeaders()
    {
        for (var i = 0; i < Operations; i++)
        {
            await _producer.FireAsync(_messageWithHeaders)
                .ConfigureAwait(false);
        }
    }

    [Benchmark(OperationsPerInvoke = Operations)]
    public async Task FireAsync_YieldingSerializer()
    {
        for (var i = 0; i < Operations; i++)
        {
            await _yieldingProducer.FireAsync(YieldingTopic, Keys[i], _value)
                .ConfigureAwait(false);
        }
    }

    [GlobalCleanup]
    public async Task Cleanup()
    {
        _drainerCts.Cancel();
        _drainerThread.Join();
        _drainerCts.Dispose();
        await _producer.DisposeAsync().ConfigureAwait(false);
        await _yieldingProducer.DisposeAsync().ConfigureAwait(false);
    }

    [Benchmark(OperationsPerInvoke = Operations)]
    public void HeaderPool_SaturatedSupportedConcurrency() => SaturateHeaderPool();

    private static DekafProducer.ProducerOptions CreateOptions(string clientId) => new()
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
        ValueTaskSourcePoolSize = Operations,
        UnackedByteBudgetCapOverride = FixtureCapacityBytes
    };

    private void SaturateHeaderPool()
    {
        for (var i = 0; i < _rentedHeaders.Length; i++)
            _rentedHeaders[i] = _headerPool.Rent();

        if (_headerPool.Misses != 0)
            throw new InvalidOperationException("Async serialization header pool missed at supported concurrency.");

        for (var i = 0; i < _rentedHeaders.Length; i++)
        {
            _headerPool.Return(_rentedHeaders[i]);
            _rentedHeaders[i] = null!;
        }
    }

    private void DrainLoop(CancellationToken cancellationToken)
    {
        var spinner = new SpinWait();
        long offset = 0;
        while (!cancellationToken.IsCancellationRequested)
        {
            var drained = DrainOne(_producer.RecordAccumulator, ref offset)
                | DrainOne(_yieldingProducer.RecordAccumulator, ref offset);
            if (drained)
                spinner.Reset();
            else
                spinner.SpinOnce();
        }
    }

    private static bool DrainOne(DekafProducer.RecordAccumulator accumulator, ref long offset)
    {
        if (!accumulator.TryDrainBatch(out var batch))
            return false;

        batch.CompleteSend(offset++, DateTimeOffset.UtcNow);
        accumulator.ReleaseBatchMemory(batch);
        accumulator.OnBatchExitsPipeline(batch);
        accumulator.ReturnReadyBatch(batch);
        return true;
    }

    private static void SeedMetadata(
        DekafProducer.KafkaProducer<string, string> producer,
        string topic)
    {
        var metadataManager = GetInstanceField<MetadataManager>(producer, "_metadataManager");
        metadataManager.Metadata.Update(new MetadataResponse
        {
            Brokers =
            [
                new BrokerMetadata { NodeId = 0, Host = "localhost", Port = 9092 }
            ],
            ClusterId = "async-producer-serde-benchmark",
            ControllerId = 0,
            Topics =
            [
                new TopicMetadata
                {
                    ErrorCode = ErrorCode.None,
                    Name = topic,
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

    private sealed class CompletedAsyncStringSerializer : IAsyncSerializer<string>
    {
        public ValueTask SerializeAsync(
            string value,
            IBufferWriter<byte> destination,
            SerializationContext context,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var byteCount = Encoding.UTF8.GetByteCount(value);
            var span = destination.GetSpan(byteCount);
            var written = Encoding.UTF8.GetBytes(value, span);
            destination.Advance(written);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class HeaderAddingStringSerializer : ISerializer<string>, IRecordHeaderSerializer
    {
        private static readonly byte[] HeaderValue = [1];

        public bool ProducesRecordHeaders => true;

        public void Serialize<TWriter>(string value, ref TWriter destination, SerializationContext context)
            where TWriter : IBufferWriter<byte>
#if NET10_0_OR_GREATER
            , allows ref struct
#endif
        {
            context.Headers!.Add("identity", HeaderValue);
            Serializers.String.Serialize(value, ref destination, context);
        }
    }

    private sealed class YieldingAsyncStringSerializer : IAsyncSerializer<string>
    {
        [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
        public async ValueTask SerializeAsync(
            string value,
            IBufferWriter<byte> destination,
            SerializationContext context,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Yield();
            var byteCount = Encoding.UTF8.GetByteCount(value);
            var span = destination.GetSpan(byteCount);
            var written = Encoding.UTF8.GetBytes(value, span);
            destination.Advance(written);
        }
    }
}
