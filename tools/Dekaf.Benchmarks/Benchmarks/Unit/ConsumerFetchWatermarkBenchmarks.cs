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
    private FetchResponsePartition _response = null!;
    private UpdateWatermarks _updateWatermarks = null!;

    [GlobalSetup]
    public void Setup()
    {
        _consumer = (KafkaConsumer<byte[], byte[]>)Kafka.CreateConsumer<byte[], byte[]>()
            .WithBootstrapServers("localhost:9092")
            .Build();
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

        _updateWatermarks(_consumer, "lag-benchmark", _response);
    }

    [Benchmark]
    public void UpdateFromFetchResponse() =>
        _updateWatermarks(_consumer, "lag-benchmark", _response);

    [GlobalCleanup]
    public ValueTask Cleanup() => _consumer.DisposeAsync();

    private delegate void UpdateWatermarks(
        KafkaConsumer<byte[], byte[]> consumer,
        string topic,
        FetchResponsePartition response);
}
