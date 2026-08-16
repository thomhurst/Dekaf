using BenchmarkDotNet.Attributes;
using Dekaf.Consumer;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>
/// Compares allocation-free batch offset staging with the equivalent explicit single-offset loop.
/// Setup pre-populates every dictionary key so measurements cover steady-state updates only.
/// </summary>
[MemoryDiagnoser]
[ShortRunJob]
public class OffsetStoreBenchmarks
{
    private IKafkaConsumer<byte[], byte[]> _consumer = null!;
    private TopicPartitionOffset[] _offsets = null!;
    private IReadOnlyList<TopicPartitionOffset> _offsetList = null!;
    private StructOffsetList _structOffsets;

    [Params(1, 8, 64)]
    public int PartitionCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _consumer = Kafka.CreateConsumer<byte[], byte[]>()
            .WithBootstrapServers("localhost:9092")
            .Build();
        _offsets = new TopicPartitionOffset[PartitionCount];
        for (var partition = 0; partition < _offsets.Length; partition++)
            _offsets[partition] = new TopicPartitionOffset("offset-store-benchmark", partition, 42, leaderEpoch: 3);

        _offsetList = _offsets.ToList();
        _structOffsets = new StructOffsetList(_offsets);
        _consumer.StoreOffsets(_offsets);
    }

    [GlobalCleanup]
    public ValueTask Cleanup() => _consumer.DisposeAsync();

    [Benchmark(Baseline = true)]
    public void RepeatedSingle()
    {
        for (var index = 0; index < _offsets.Length; index++)
            _consumer.StoreOffset(_offsets[index]);
    }

    [Benchmark]
    public void SpanBatch() => _consumer.StoreOffsets(_offsets.AsSpan());

    [Benchmark]
    public void ArrayBatch() => _consumer.StoreOffsets(_offsets);

    [Benchmark]
    public void ListBatch() => _consumer.StoreOffsets(_offsetList);

    [Benchmark]
    public void StructListBatch() => _consumer.StoreOffsets(_structOffsets);

    private readonly struct StructOffsetList(TopicPartitionOffset[] offsets) : IReadOnlyList<TopicPartitionOffset>
    {
        public int Count => offsets.Length;

        public TopicPartitionOffset this[int index] => offsets[index];

        public IEnumerator<TopicPartitionOffset> GetEnumerator() =>
            ((IEnumerable<TopicPartitionOffset>)offsets).GetEnumerator();

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => offsets.GetEnumerator();
    }
}

/// <summary>
/// Protects the common per-message manual-store path from validation overhead intended for
/// caller-created <see cref="TopicPartitionOffset"/> values.
/// </summary>
[MemoryDiagnoser]
[ShortRunJob]
public class ConsumeResultOffsetStoreBenchmarks
{
    private IKafkaConsumer<byte[], byte[]> _consumer = null!;
    private ConsumeResult<byte[], byte[]> _result;

    [GlobalSetup]
    public void Setup()
    {
        _consumer = Kafka.CreateConsumer<byte[], byte[]>()
            .WithBootstrapServers("localhost:9092")
            .Build();
        _result = new ConsumeResult<byte[], byte[]>(
            topic: "offset-store-benchmark",
            partition: 0,
            offset: 41,
            keyData: ReadOnlyMemory<byte>.Empty,
            isKeyNull: true,
            valueData: ReadOnlyMemory<byte>.Empty,
            isValueNull: true,
            headers: null,
            timestampMs: 0,
            timestampType: TimestampType.NotAvailable,
            leaderEpoch: 3,
            keyDeserializer: null,
            valueDeserializer: null);
        _consumer.StoreOffset(_result);
    }

    [GlobalCleanup]
    public ValueTask Cleanup() => _consumer.DisposeAsync();

    [Benchmark]
    public void StoreOffset() => _consumer.StoreOffset(_result);
}
