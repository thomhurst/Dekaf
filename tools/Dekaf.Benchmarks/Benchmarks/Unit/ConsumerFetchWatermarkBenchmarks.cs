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
    private UpdateWatermarks _updateWatermarks = null!;
    private int _assignmentVersion;

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
        _assignmentVersion = (int)typeof(KafkaConsumer<byte[], byte[]>)
            .GetField("_assignmentEnsureVersion", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(_consumer)!;

        _updateWatermarks(_consumer, _partition, _response, _assignmentVersion);
    }

    [Benchmark]
    public void UpdateFromFetchResponse() =>
        _updateWatermarks(_consumer, _partition, _response, _assignmentVersion);

    [GlobalCleanup]
    public ValueTask Cleanup() => _consumer.DisposeAsync();

    private delegate void UpdateWatermarks(
        KafkaConsumer<byte[], byte[]> consumer,
        TopicPartition partition,
        FetchResponsePartition response,
        int assignmentVersion);
}
