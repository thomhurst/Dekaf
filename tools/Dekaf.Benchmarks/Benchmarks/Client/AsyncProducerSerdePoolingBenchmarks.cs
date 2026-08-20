using System.Buffers;
using System.Runtime.CompilerServices;
using System.Text;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using Dekaf.Benchmarks.Infrastructure;
using Dekaf.Serialization;
using DekafProducer = Dekaf.Producer;

namespace Dekaf.Benchmarks.Benchmarks.Client;

/// <summary>
/// Measures the FireAsync wrapper that necessarily suspends for asynchronous serialization.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RunStrategy.Throughput, launchCount: 1, warmupCount: 5, iterationCount: 10)]
public class AsyncProducerSerdePoolingBenchmarks
{
    private const string Topic = "bench-async-producer-serde";
    private const string YieldingTopic = "bench-yielding-producer-serde";
    private const int Operations = 100;

    private static readonly string[] Keys = BenchmarkData.CreateKeys(Operations);

    private KafkaTestEnvironment _kafka = null!;
    private DekafProducer.IKafkaProducer<string, string> _producer = null!;
    private DekafProducer.IKafkaProducer<string, string> _yieldingProducer = null!;
    private DekafProducer.ProducerMessage<string, string> _messageWithHeaders = null!;
    private string _value = null!;

    [GlobalSetup]
    public async Task Setup()
    {
        _kafka = await KafkaTestEnvironment.CreateAsync().ConfigureAwait(false);
        await Task.WhenAll(
            _kafka.CreateTopicAsync(Topic, 3),
            _kafka.CreateTopicAsync(YieldingTopic, 3)).ConfigureAwait(false);

        _value = new string('x', 100);
        _messageWithHeaders = new DekafProducer.ProducerMessage<string, string>
        {
            Topic = Topic,
            Key = Keys[0],
            Value = _value,
            Headers = new Headers(1).Add("caller", "value")
        };
        _producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(_kafka.BootstrapServers)
            .WithClientId("bench-async-producer-serde")
            .WithIdempotence(false)
            .WithAcks(DekafProducer.Acks.All)
            .WithLinger(TimeSpan.FromMilliseconds(5))
            .WithValueSerializer(new CompletedAsyncStringSerializer())
            .BuildAsync()
            .ConfigureAwait(false);

        _yieldingProducer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(_kafka.BootstrapServers)
            .WithClientId("bench-yielding-producer-serde")
            .WithIdempotence(false)
            .WithAcks(DekafProducer.Acks.All)
            .WithLinger(TimeSpan.FromMilliseconds(5))
            .WithValueSerializer(new YieldingAsyncStringSerializer())
            .BuildAsync()
            .ConfigureAwait(false);

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
            await _producer.FireAsync(Topic, Keys[i], _value)
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
        await _producer.DisposeAsync().ConfigureAwait(false);
        await _yieldingProducer.DisposeAsync().ConfigureAwait(false);
        await _kafka.DisposeAsync().ConfigureAwait(false);
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
