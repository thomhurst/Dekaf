using BenchmarkDotNet.Attributes;
using Dekaf.Consumer;

namespace Dekaf.Benchmarks.Benchmarks.Unit;

/// <summary>Measures a complete fragmented delivery/completion cycle after warmup.</summary>
[MemoryDiagnoser]
public class PartitionedOffsetFragmentationBenchmarks
{
    // Ordered/reverse and smaller batches are covered by PartitionedOffsetCompletionBenchmarks.
    // This case exceeds four reservation chunks and retains 2049 disjoint completed ranges.
    private const int RecordCount = 4098;
    private PartitionLane<string, string> _lane = null!;
    private ConsumeResult<string, string>[] _records = null!;
    private long _offset;

    [GlobalSetup]
    public void Setup()
    {
        _lane = new(new TopicPartition("fragmentation", 0), 32,
            static (_, _) => default, static _ => { }, static (_, _) => { });
        _records = new ConsumeResult<string, string>[RecordCount];
    }

    // One operation includes reservation, delivery, fragmented completion and final drain.
    // Report allocations per batch; the stable completion owner is an amortized batch cost.
    [Benchmark]
    public TopicPartitionOffset? AlternatingCompletion()
    {
        var previousCheckpoint = _lane.GetCommitOffset()?.Offset;
        var batch = _lane.CreateCompletionBatch(RecordCount);
        for (var index = 0; index < _records.Length; index++)
        {
            _offset += 2;
            var record = new ConsumeResult<string, string>("fragmentation", 0, _offset,
                "key", "value", null, 0, TimestampType.CreateTime, index);
            if (!_lane.TryEnqueue(record, batch) || !_lane.TryReadMessage(out _records[index]))
                throw new InvalidOperationException("Unable to prepare the completion window.");
        }
        _lane.EndBatch(batch, RecordCount);

        for (var index = 1; index < _records.Length; index += 2)
            _lane.MarkProcessed(_records[index]);
        if (_lane.GetCommitOffset()?.Offset != previousCheckpoint)
            throw new InvalidOperationException("Completion advanced past an unfinished predecessor.");
        for (var index = 0; index < _records.Length; index += 2)
            _lane.MarkProcessed(_records[index]);

        var checkpoint = _lane.GetCommitOffset();
        if (checkpoint?.Offset != _records[^1].Offset + 1)
            throw new InvalidOperationException("The completed window did not advance its checkpoint.");
        if (_records[0].ProcessingBatch!.Nodes is not null)
            throw new InvalidOperationException("The completed window retained its reserved nodes.");
        return checkpoint;
    }
}
