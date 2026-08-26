using System.Reflection;
using BenchmarkDotNet.Attributes;
using Dekaf.Consumer;
using Dekaf.Protocol.Messages;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>Guards the allocation cost of updating consumer watermarks from fetch responses.</summary>
[MemoryDiagnoser(displayGenColumns: false)]
[ShortRunJob]
public class ConsumerFetchWatermarkBenchmarks
{
    private KafkaConsumer<byte[], byte[]> _consumer = null!;
    private TopicPartition _partition;
    private FetchResponsePartition _response = null!;
    private UpdateLagEndOffset _updateLagEndOffset = null!;
    private UpdateWatermarks _updateWatermarks = null!;
    private int _fetchBufferEpoch;

    [GlobalSetup]
    public void Setup()
    {
        _consumer = (KafkaConsumer<byte[], byte[]>)Kafka.CreateConsumer<byte[], byte[]>()
            .WithBootstrapServers("localhost:9092")
            .Build();
        _partition = new TopicPartition("lag-benchmark", 0);
        _consumer.IncrementalAssign(
            [new TopicPartitionOffset(_partition.Topic, _partition.Partition, 0)]);
        _response = new FetchResponsePartition
        {
            PartitionIndex = 0,
            HighWatermark = 1_000,
            LastStableOffset = 900,
            LogStartOffset = 0
        };
        _updateWatermarks = typeof(KafkaConsumer<byte[], byte[]>)
            .GetMethod("UpdateWatermarksFromFetchResponse", BindingFlags.Instance | BindingFlags.NonPublic)!
            .CreateDelegate<UpdateWatermarks>();
        _updateLagEndOffset = typeof(KafkaConsumer<byte[], byte[]>)
            .GetMethod("UpdateCachedLagEndOffset", BindingFlags.Instance | BindingFlags.NonPublic)!
            .CreateDelegate<UpdateLagEndOffset>();
        _fetchBufferEpoch = (int)typeof(KafkaConsumer<byte[], byte[]>)
            .GetField("_fetchBufferEpoch", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(_consumer)!;

        _updateWatermarks(_consumer, _partition, _response, _fetchBufferEpoch, 5, 0);
    }

    [Benchmark]
    public void UpdateFromFetchResponse() =>
        _updateWatermarks(_consumer, _partition, _response, _fetchBufferEpoch, 5, 0);

    [Benchmark]
    public void UpdateDivergentFromFetchResponse()
    {
        _updateLagEndOffset(_consumer, _partition, 1_100, -1, 2);
        _updateWatermarks(_consumer, _partition, _response, _fetchBufferEpoch, -1, 1);
    }

    [GlobalCleanup]
    public ValueTask Cleanup() => _consumer.DisposeAsync();

    private delegate void UpdateWatermarks(
        KafkaConsumer<byte[], byte[]> consumer,
        TopicPartition partition,
        FetchResponsePartition response,
        int fetchBufferEpoch,
        int leaderEpoch,
        long watermarkUpdateSequence);

    private delegate void UpdateLagEndOffset(
        KafkaConsumer<byte[], byte[]> consumer,
        TopicPartition partition,
        long lagEndOffset,
        int leaderEpoch,
        long watermarkUpdateSequence);
}
