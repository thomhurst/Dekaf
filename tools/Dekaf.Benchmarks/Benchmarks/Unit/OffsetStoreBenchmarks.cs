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
/// caller-created <see cref="TopicPartitionOffset"/> values. The fetched-record cases run on a
/// group-managed (subscribed) consumer whose partition is owned and synchronized, as after a
/// rebalance, without a broker: <see cref="StoreFetchedOffset"/> takes the ownership fast path
/// (the record's fetch generation and the coordinator's assignment version, three volatile
/// reads), and <see cref="StoreConstructedOffsetGroupManaged"/> the ownership lookup a result
/// without a fetch needs.
/// </summary>
[MemoryDiagnoser]
[ShortRunJob]
public class ConsumeResultOffsetStoreBenchmarks
{
    private const string Topic = "offset-store-benchmark";
    private IKafkaConsumer<byte[], byte[]> _consumer = null!;
    private KafkaConsumer<byte[], byte[]> _groupConsumer = null!;
    private ConsumeResult<byte[], byte[]> _result;
    private ConsumeResult<byte[], byte[]> _fetchedResult;
    private PendingFetchData _fetch = null!;

    [GlobalSetup]
    public void Setup()
    {
        _consumer = Kafka.CreateConsumer<byte[], byte[]>()
            .WithBootstrapServers("localhost:9092")
            .Build();
        _result = new ConsumeResult<byte[], byte[]>(
            topic: Topic,
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

        _groupConsumer = (KafkaConsumer<byte[], byte[]>)Kafka.CreateConsumer<byte[], byte[]>()
            .WithBootstrapServers("localhost:9092")
            .WithGroupId("offset-store-benchmark")
            .WithOffsetCommitMode(OffsetCommitMode.Manual)
            .WithAutoOffsetStore(false)
            .Build();
        _groupConsumer.Subscribe(Topic);
        OwnSynchronizedPartition(_groupConsumer, new TopicPartition(Topic, 0));

        // A record delivered from a fetch created after its partition's ownership began.
        // Created as the prefetch loop creates it: tagged with the partition's ownership start.
        var ownershipStarts = typeof(KafkaConsumer<byte[], byte[]>)
            .GetField("_ownershipStartGenerations", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?
            .GetValue(_groupConsumer) as System.Collections.Concurrent.ConcurrentDictionary<TopicPartition, long>;
        // Reflection keeps the fixture compiling against revisions without ownership tags.
        _fetch = PendingFetchData.Create(Topic, 0, Array.Empty<Dekaf.Protocol.Records.RecordBatch>());
        typeof(PendingFetchData)
            .GetField("_ownershipStart", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?
            .SetValue(_fetch, ownershipStarts?.GetValueOrDefault(new TopicPartition(Topic, 0)) ?? 0L);
        _fetchedResult = new ConsumeResult<byte[], byte[]>(
            Topic,
            0,
            41,
            ReadOnlyMemory<byte>.Empty,
            isKeyNull: true,
            ReadOnlyMemory<byte>.Empty,
            isValueNull: true,
            pooledHeaders: null,
            pooledHeaderCount: 0,
            headerOwner: _fetch,
            timestampMs: 0,
            TimestampType.NotAvailable,
            leaderEpoch: 3,
            keyDeserializer: null,
            valueDeserializer: null);
        _groupConsumer.StoreOffset(_fetchedResult);
        _groupConsumer.StoreOffset(_result);
    }

    /// <summary>
    /// The state assignment sync leaves for an owned partition: the coordinator's assignment holds
    /// it, the consumer published and initialized it, and the sync is current.
    /// </summary>
    private static void OwnSynchronizedPartition(KafkaConsumer<byte[], byte[]> consumer, TopicPartition partition)
    {
        const System.Reflection.BindingFlags Instance =
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var consumerType = typeof(KafkaConsumer<byte[], byte[]>);
        var coordinator = (ConsumerCoordinator)consumerType.GetField("_coordinator", Instance)!.GetValue(consumer)!;
        typeof(ConsumerCoordinator).GetField("_assignedPartitions", Instance)!
            .SetValue(coordinator, new HashSet<TopicPartition> { partition });
        ((HashSet<TopicPartition>)consumerType.GetField("_assignment", Instance)!.GetValue(consumer)!).Add(partition);
        consumerType.GetMethod("PublishAssignmentSnapshot", Instance)!.Invoke(consumer, null);
        consumerType.GetMethod("SetFetchPosition", Instance)!.Invoke(consumer, [partition, 0L]);
        consumerType.GetField("_lastCoordinatorAssignmentVersion", Instance)!
            .SetValue(consumer, coordinator.AssignmentVersion);
    }

    [GlobalCleanup]
    public async ValueTask Cleanup()
    {
        _fetch.Dispose();
        await _groupConsumer.DisposeAsync().ConfigureAwait(false);
        await _consumer.DisposeAsync().ConfigureAwait(false);
    }

    [Benchmark]
    public void StoreOffset() => _consumer.StoreOffset(_result);

    [Benchmark]
    public void StoreFetchedOffset() => _groupConsumer.StoreOffset(_fetchedResult);

    [Benchmark]
    public void StoreConstructedOffsetGroupManaged() => _groupConsumer.StoreOffset(_result);
}
