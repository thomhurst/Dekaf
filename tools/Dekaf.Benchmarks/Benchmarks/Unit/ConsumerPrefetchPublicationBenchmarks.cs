using System.Collections.Concurrent;
using System.Reflection;
using BenchmarkDotNet.Attributes;
using Dekaf.Benchmarks.Infrastructure;
using Dekaf.Consumer;
using Dekaf.Protocol.Records;
using Dekaf.Serialization;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>Publishes and drains two pooled responses, including replica-routing overlap.</summary>
[MemoryDiagnoser]
public class ConsumerPrefetchPublicationBenchmarks
{
    private const string Topic = "prefetch-publication";
    private static readonly TopicPartition Partition = new(Topic, 0);
    private readonly RecordBatch[] _firstBatches = new RecordBatch[1];
    private readonly RecordBatch[] _secondBatches = new RecordBatch[1];
    private readonly PendingFetchData[] _pending = new PendingFetchData[2];
    private KafkaConsumer<byte[], byte[]> _consumer = null!;
    private MpscFetchBuffer _buffer = null!;
    private Record[] _records = null!;
    private ConcurrentDictionary<TopicPartition, long> _positions = null!;
    private Func<IReadOnlyList<PendingFetchData>, int, CancellationToken, ValueTask> _write = null!;
    private Action<PendingFetchData, bool> _trackBytes = null!;
    private int _epoch;

    [Params(1, 1024)]
    public int RecordsPerResponse { get; set; }

    [Params(0, 1, 50, 100)]
    public int OverlapPercent { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _consumer = new KafkaConsumer<byte[], byte[]>(new ConsumerOptions
        {
            BootstrapServers = ["localhost:9092"],
            EnableAutoOffsetStore = false
        }, Serializers.ByteArray, Serializers.ByteArray);
        _consumer.IncrementalAssign([new TopicPartitionOffset(Topic, 0, 0)]);
        _positions = BufferedConsumerHarness.GetFetchPositions(_consumer);
        _buffer = (MpscFetchBuffer)BufferedConsumerHarness.GetPrivateField(_consumer, "_prefetchBuffer")!;
        _epoch = (int)BufferedConsumerHarness.GetPrivateField(_consumer, "_fetchBufferEpoch")!;
        var type = _consumer.GetType();
        _write = type.GetMethod("WritePrefetchedItemsAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
            .CreateDelegate<Func<IReadOnlyList<PendingFetchData>, int, CancellationToken, ValueTask>>(_consumer);
        _trackBytes = type.GetMethod("TrackPrefetchedBytes", BindingFlags.Instance | BindingFlags.NonPublic)!
            .CreateDelegate<Action<PendingFetchData, bool>>(_consumer);
        _records = new Record[RecordsPerResponse];
        for (var i = 0; i < _records.Length; i++)
            _records[i] = new Record { OffsetDelta = i, Key = "key"u8.ToArray(), Value = "value"u8.ToArray() };

        // Both revisions use this fixture. The baseline exposes duplicates; the
        // corrected publisher trims them. Declare both contracts explicitly.
        var trimsOverlap = typeof(PendingFetchData).GetMethod("RaiseStartOffset",
            BindingFlags.Instance | BindingFlags.NonPublic) is not null;
        var expected = 2 * RecordsPerResponse - (trimsOverlap ? RecordsPerResponse * OverlapPercent / 100 : 0);
        if (PublishAndDrain() != expected)
            throw new InvalidOperationException("Prefetch publication fixture produced an unexpected record count.");
    }

    [Benchmark]
    public int PublishAndDrain()
    {
        _positions[Partition] = 0;
        _firstBatches[0] = CreateBatch(0);
        _secondBatches[0] = CreateBatch(RecordsPerResponse - RecordsPerResponse * OverlapPercent / 100);
        _pending[0] = PendingFetchData.Create(Topic, 0, _firstBatches, skipRecordsBelowOffset: 0);
        _pending[1] = PendingFetchData.Create(Topic, 0, _secondBatches, skipRecordsBelowOffset: 0);
        var write = _write(_pending, _epoch, CancellationToken.None);
        if (!write.IsCompletedSuccessfully)
            throw new InvalidOperationException("The empty prefetch buffer must accept both responses synchronously.");
        write.GetAwaiter().GetResult();
        var count = 0;
        while (_buffer.TryRead(out var pending))
        {
            _trackBytes(pending, true);
            while (pending.MoveNext())
                count++;
            pending.Dispose();
        }
        return count;
    }

    private RecordBatch CreateBatch(long baseOffset)
    {
        var batch = RecordBatch.RentFromPool();
        batch.BaseOffset = baseOffset;
        batch.LastOffsetDelta = RecordsPerResponse - 1;
        batch.Records = _records;
        return batch;
    }

    [GlobalCleanup]
    public ValueTask Cleanup() => _consumer.DisposeAsync();
}
