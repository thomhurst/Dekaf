using BenchmarkDotNet.Attributes;
using Dekaf;
using Dekaf.Consumer;
using Dekaf.Protocol;

[MemoryDiagnoser]
public class CompletionTrackingBench
{
    [Params(128, 4096)]
    public int RecordCount { get; set; }

    [Params(false, true)]
    public bool Fragmented { get; set; }

    private PartitionLane<int, int> _lane = null!;
    private ConsumeResult<int, int>[] _delivered = null!;
    private long _nextOffset;

    [GlobalSetup]
    public void Setup()
    {
        _lane = new(new TopicPartition("completion", 0), RecordCount,
            static (_, _) => default, static _ => { }, static (_, error) => throw error);
        _delivered = new ConsumeResult<int, int>[RecordCount];
    }

    // One operation is a complete published batch, including its reservation.
    // Report amortized per-batch storage separately from per-record allocations.
    [Benchmark]
    public long PublishAndCompleteBatch()
    {
#if ABA_CANDIDATE
        var owner = _lane.CreateCompletionBatch(RecordCount);
#endif
        for (var index = 0; index < RecordCount; index++)
        {
            var record = new ConsumeResult<int, int>("completion", 0, _nextOffset + index,
                0, 0, null, 0, TimestampType.CreateTime, 7);
#if ABA_CANDIDATE
            var written = _lane.TryEnqueue(record, owner);
#else
            var written = _lane.TryEnqueue(record);
#endif
            if (!written || !_lane.TryReadMessage(out _delivered[index]))
                throw new InvalidOperationException("Completion fixture lost a published record");
        }
#if ABA_CANDIDATE
        _lane.EndBatch(owner, RecordCount);
#endif
        // Establish the first frontier equally on both implementations. Contiguous
        // offsets keep the old implementation correct while stressing fragmentation.
        _lane.MarkProcessed(_delivered[0]);
        if (Fragmented)
        {
            for (var index = 1; index < RecordCount; index += 2)
                _lane.MarkProcessed(_delivered[index]);
            for (var index = 2; index < RecordCount; index += 2)
                _lane.MarkProcessed(_delivered[index]);
        }
        else
        {
            for (var index = 1; index < RecordCount; index++)
                _lane.MarkProcessed(_delivered[index]);
        }
        _nextOffset += RecordCount;
        var checkpoint = _lane.GetCommitOffset();
        if (checkpoint is not { LeaderEpoch: 7 } || checkpoint.Value.Offset != _nextOffset)
            throw new InvalidOperationException("Completion fixture advanced an incorrect checkpoint");
        return _nextOffset;
    }
}
