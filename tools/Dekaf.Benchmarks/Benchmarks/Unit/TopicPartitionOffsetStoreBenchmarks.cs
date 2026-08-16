using BenchmarkDotNet.Attributes;
using Dekaf.Consumer;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>
/// Guards the public single-offset staging path used after each manually processed message.
/// Setup pre-populates dictionary keys so the measurement covers steady-state updates.
/// </summary>
[MemoryDiagnoser]
[OperationsPerSecond]
[ShortRunJob]
public class TopicPartitionOffsetStoreBenchmarks
{
    private IKafkaConsumer<byte[], byte[]> _consumer = null!;
    private TopicPartitionOffset _offset;

    [GlobalSetup]
    public void Setup()
    {
        _consumer = Kafka.CreateConsumer<byte[], byte[]>()
            .WithBootstrapServers("localhost:9092")
            .Build();
        _offset = new TopicPartitionOffset("offset-store-benchmark", 0, 42, leaderEpoch: 3);
        _consumer.StoreOffset(_offset);
    }

    [GlobalCleanup]
    public ValueTask Cleanup() => _consumer.DisposeAsync();

    [Benchmark]
    public void StoreOffset() => _consumer.StoreOffset(_offset);
}
