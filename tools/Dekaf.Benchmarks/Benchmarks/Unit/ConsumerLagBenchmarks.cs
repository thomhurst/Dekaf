using System.Reflection;
using BenchmarkDotNet.Attributes;
using Dekaf.Consumer;
using Dekaf.Protocol.Messages;

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
        var fetchBufferEpoch = (int)typeof(KafkaConsumer<byte[], byte[]>)
            .GetField("_fetchBufferEpoch", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(concrete)!;
        typeof(KafkaConsumer<byte[], byte[]>)
            .GetMethod("UpdateWatermarksFromFetchResponse", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(concrete, [_partition, new FetchResponsePartition
            {
                PartitionIndex = _partition.Partition,
                HighWatermark = 1_000,
                LogStartOffset = 0
            }, fetchBufferEpoch, 5, 0L]);
    }

    [Benchmark]
    public long? GetCachedCurrentLag() => _consumer.GetCurrentLag(_partition);

    [GlobalCleanup]
    public ValueTask Cleanup() => _consumer.DisposeAsync();
}
