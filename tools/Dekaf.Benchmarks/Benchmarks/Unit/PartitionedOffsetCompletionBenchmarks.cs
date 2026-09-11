using BenchmarkDotNet.Attributes;
using Dekaf.Consumer;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>Measures complete delivery/acknowledgement cycles, including sparse Kafka offsets.</summary>
[MemoryDiagnoser]
public class PartitionedOffsetCompletionBenchmarks
{
    private PartitionLane<string, string> _lane = null!;
    private ConsumeResult<string, string>[] _batch = null!;
    private long _offset;

    [Params(64, 1024)]
    public int Records { get; set; }

    [Params(1, 2)]
    public int OffsetStep { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _lane = new PartitionLane<string, string>(new TopicPartition("completion", 0), 32,
            static (_, _) => default, static _ => { }, static (_, _) => { });
        _batch = new ConsumeResult<string, string>[Records];
    }

    // One operation is a complete batch. The stable completion owner is an amortized
    // batch allocation; the separate fragmentation cases measure per-message completion.
    [Benchmark]
    public TopicPartitionOffset? Ordered()
    {
        var batch = _lane.CreateCompletionBatch(Records);
        for (var index = 0; index < Records; index++)
        {
            _lane.TryEnqueue(CreateRecord(), batch);
            _lane.TryReadMessage(out var message);
            _lane.MarkProcessed(message);
        }
        _lane.EndBatch(batch, Records);
        return _lane.GetCommitOffset();
    }

    [Benchmark]
    public TopicPartitionOffset? ReverseBatch()
    {
        var batch = _lane.CreateCompletionBatch(Records);
        for (var index = 0; index < _batch.Length; index++)
        {
            _lane.TryEnqueue(CreateRecord(), batch);
            _lane.TryReadMessage(out _batch[index]);
        }
        _lane.EndBatch(batch, Records);
        for (var index = _batch.Length - 1; index >= 0; index--)
            _lane.MarkProcessed(_batch[index]);
        return _lane.GetCommitOffset();
    }

    private ConsumeResult<string, string> CreateRecord()
    {
        _offset += OffsetStep;
        return new ConsumeResult<string, string>("completion", 0, _offset,
            "key", "value", null, 0, TimestampType.CreateTime, 1);
    }
}
