using System.Collections.Concurrent;
using BenchmarkDotNet.Attributes;
using Dekaf.Benchmarks.Infrastructure;
using Dekaf.Consumer;
using Dekaf.Protocol.Records;
using Dekaf.Serialization;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>
/// One batch stream over PartitionCount partitions, each with one queued fetch and one pending
/// partition EOF. It delivers every fetch, then every EOF. This covers the EOF drain that holds
/// an EOF back while its partition still has queued records. On this no-skip path the fetch
/// queue is empty when the drain starts, so the check must add no per-EOF scan. SkipHalf is the
/// K-of-N skip case: the stream skips the first half of the partitions without enumerating
/// them. Each skip releases its partition (one buffer sweep per batch loop, not per skip, and
/// no allocation), and the remaining EOFs are held behind their records. The unit is a fixed number of MoveNext calls, which
/// keeps it bounded on revisions that re-yield a skipped batch instead of releasing it.
/// </summary>
[MemoryDiagnoser]
public class ConsumerBatchEofDrainBenchmarks
{
    private const string Topic = "batch-eof-drain";
    private const int RecordsPerFetch = 16;
    private KafkaConsumer<Ignore, ReadOnlyMemory<byte>> _consumer = null!;
    private ConcurrentQueue<(TopicPartition Partition, long Offset)> _eofEvents = null!;
    private TopicPartition[] _partitions = null!;
    private Record[][] _records = null!;

    [Params(1, 64)]
    public int PartitionCount { get; set; }

    [Params(false, true)]
    public bool Raw { get; set; }

    [Params(false, true)]
    public bool SkipHalf { get; set; }

    private int Skipped => SkipHalf ? PartitionCount / 2 : 0;

    private long ExpectedBatches => Skipped + 2L * (PartitionCount - Skipped);

    [GlobalSetup]
    public async Task Setup()
    {
        _records = [new Record[RecordsPerFetch]];
        for (var i = 0; i < RecordsPerFetch; i++)
            _records[0][i] = new Record { OffsetDelta = i, IsKeyNull = true, Value = new byte[16] };
        _consumer = new KafkaConsumer<Ignore, ReadOnlyMemory<byte>>(new ConsumerOptions
        {
            BootstrapServers = ["localhost:9092"], OffsetCommitMode = OffsetCommitMode.Manual,
            QueuedMinMessages = 1, MaxPollRecords = RecordsPerFetch
        }, Serializers.Ignore, Serializers.RawBytes);
        BufferedConsumerHarness.InitializeForBufferedFastPath(_consumer, Topic, 0);
        _partitions = new TopicPartition[PartitionCount];
        for (var i = 0; i < PartitionCount; i++)
            _partitions[i] = new TopicPartition(Topic, i);
        _consumer.Assign(_partitions);
        foreach (var partition in _partitions)
            BufferedConsumerHarness.GetFetchPositions(_consumer)[partition] = 0;
        BufferedConsumerHarness.SetPrivateField(
            _consumer,
            "_lastManualAssignmentEnsureVersion",
            BufferedConsumerHarness.GetPrivateField(_consumer, "_assignmentEnsureVersion"));
        _eofEvents = (ConcurrentQueue<(TopicPartition Partition, long Offset)>)
            BufferedConsumerHarness.GetPrivateField(_consumer, "_pendingEofEvents")!;

        if (await Drain() != ExpectedBatches)
            throw new InvalidOperationException("Without skips, every fetch and every EOF must be delivered once.");
    }

    [Benchmark]
    public ValueTask<long> Drain() => Raw ? RawStream() : TypedStream();

    private void Seed()
    {
        BufferedConsumerHarness.DrainPendingFetches(_consumer);
        _eofEvents.Clear();
        for (var i = 0; i < PartitionCount; i++)
        {
            BufferedConsumerHarness.AppendPendingFetch(
                _consumer, Topic, i, _records, 1, RecordsPerFetch, baseOffset: 0);
            _eofEvents.Enqueue((_partitions[i], RecordsPerFetch));
        }
    }

    private async ValueTask<long> TypedStream()
    {
        Seed();
        long batches = 0;
        long eofs = 0;
        await foreach (var batch in _consumer.ConsumeBatchAsync())
        {
            if (batch.IsPartitionEof)
            {
                eofs++;
            }
            else if (batch.Partition >= Skipped)
            {
                foreach (var _ in batch) { }
            }
            if (++batches == ExpectedBatches)
                break;
        }
        // Each stream is a fixed number of MoveNext calls, so it stays bounded on revisions
        // that re-yield a skipped batch. Delivery order across skips is the unit tests' job;
        // only the no-skip invariant, which every revision shares, is checked here.
        return SkipHalf || eofs == PartitionCount ? batches : -1;
    }

    private async ValueTask<long> RawStream()
    {
        Seed();
        long batches = 0;
        long eofs = 0;
        await foreach (var batch in _consumer.ConsumeRawBatchAsync())
        {
            if (batch.IsPartitionEof)
            {
                eofs++;
            }
            else if (batch.Partition >= Skipped)
            {
                foreach (var _ in batch) { }
            }
            if (++batches == ExpectedBatches)
                break;
        }
        // Each stream is a fixed number of MoveNext calls, so it stays bounded on revisions
        // that re-yield a skipped batch. Delivery order across skips is the unit tests' job;
        // only the no-skip invariant, which every revision shares, is checked here.
        return SkipHalf || eofs == PartitionCount ? batches : -1;
    }

    [GlobalCleanup]
    public async Task Cleanup()
    {
        BufferedConsumerHarness.DrainPendingFetches(_consumer);
        _eofEvents.Clear();
        await _consumer.DisposeAsync();
    }
}
