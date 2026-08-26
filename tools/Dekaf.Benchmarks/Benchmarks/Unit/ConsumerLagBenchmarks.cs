using System.Collections.Concurrent;
using System.Reflection;
using BenchmarkDotNet.Attributes;
using Dekaf.Consumer;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>Guards the allocation-free cached consumer-lag query.</summary>
[MemoryDiagnoser(displayGenColumns: false)]
[ShortRunJob]
public class ConsumerLagBenchmarks
{
    private IKafkaConsumer<byte[], byte[]> _consumer = null!;
    private TopicPartition _partition;

    [GlobalSetup]
    public void Setup()
    {
        _partition = new TopicPartition("lag-benchmark", 0);
        _consumer = Kafka.CreateConsumer<byte[], byte[]>()
            .WithBootstrapServers("localhost:9092")
            .Build();
        _consumer.IncrementalAssign([
            new TopicPartitionOffset(_partition.Topic, _partition.Partition, 250)
        ]);

        var concrete = (KafkaConsumer<byte[], byte[]>)_consumer;
        var watermarks = (ConcurrentDictionary<TopicPartition, WatermarkOffsets>)(
            typeof(KafkaConsumer<byte[], byte[]>)
                .GetField("_watermarks", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.GetValue(concrete)
            ?? throw new InvalidOperationException("_watermarks field not found"));
        watermarks[_partition] = new WatermarkOffsets(0, 1_000);
        var lagEndOffsets = (ConcurrentDictionary<TopicPartition, long>)(
            typeof(KafkaConsumer<byte[], byte[]>)
                .GetField("_lagEndOffsets", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.GetValue(concrete)
            ?? throw new InvalidOperationException("_lagEndOffsets field not found"));
        lagEndOffsets[_partition] = 1_000;
    }

    [Benchmark]
    public long? GetCachedCurrentLag() => _consumer.GetCurrentLag(_partition);

    [GlobalCleanup]
    public ValueTask Cleanup() => _consumer.DisposeAsync();
}
