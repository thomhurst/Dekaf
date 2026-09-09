using System.Diagnostics;

// Fixed-width latency buckets grouped by completion time. Preallocate and touch
// every page before workload warmup; snapshots run after measurement has stopped.
internal sealed class BlockHistogram
{
    public const int BlockSeconds = 10;
    public const int BucketWidthUs = 10;
    public const int BucketCount = 500_000;
    private readonly long[][] _buckets;
    private readonly long[] _latencyOverflow;
    private readonly int _capacitySeconds;
    private long _outsideCapacityCount;

    public BlockHistogram(int capacitySeconds)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacitySeconds);
        _capacitySeconds = capacitySeconds;
        var count = (capacitySeconds - 1) / BlockSeconds + 1;
        _buckets = new long[count][];
        _latencyOverflow = new long[count];
        foreach (ref var buckets in _buckets.AsSpan())
        {
            buckets = new long[BucketCount];
            // Volatile writes prevent removal of stores to already-zero pages.
            for (var index = 0; index < buckets.Length; index += 512)
                Volatile.Write(ref buckets[index], 0);
        }
    }

    public void RecordTicks(long latencyTicks, long completedAfterStartTicks)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(latencyTicks);
        ArgumentOutOfRangeException.ThrowIfNegative(completedAfterStartTicks);
        var second = completedAfterStartTicks / Stopwatch.Frequency;
        if (second >= _capacitySeconds)
        {
            Interlocked.Increment(ref _outsideCapacityCount);
            return;
        }
        var block = (int)(second / BlockSeconds);
        var microseconds = latencyTicks * 1_000_000.0 / Stopwatch.Frequency;
        if (microseconds >= (long)BucketCount * BucketWidthUs)
        {
            Interlocked.Increment(ref _latencyOverflow[block]);
            return;
        }
        Interlocked.Increment(ref _buckets[block][(int)(microseconds / BucketWidthUs)]);
    }

    // Snapshot only after drain for exact data. Failure snapshots remain explicitly
    // incomplete and can race late completions, just like the global histogram.
    public BlockHistogramSnapshot GetSnapshot()
    {
        var blocks = new HistogramBlock[_buckets.Length];
        for (var block = 0; block < blocks.Length; block++)
        {
            var occupied = new List<LatencyBucket>();
            var overflow = Volatile.Read(ref _latencyOverflow[block]);
            var total = overflow;
            for (var index = 0; index < BucketCount; index++)
            {
                var count = Volatile.Read(ref _buckets[block][index]);
                if (count == 0)
                    continue;
                occupied.Add(new LatencyBucket(index, count));
                total += count;
            }
            blocks[block] = new HistogramBlock(block * BlockSeconds, total, overflow, occupied.ToArray());
        }
        return new BlockHistogramSnapshot(BlockSeconds, BucketWidthUs, BucketCount,
            _capacitySeconds, Volatile.Read(ref _outsideCapacityCount), blocks);
    }
}

internal readonly record struct LatencyBucket(int Index, long Count);
internal sealed record HistogramBlock(int StartSecond, long Count, long LatencyOverflowCount, LatencyBucket[] Buckets);
internal sealed record BlockHistogramSnapshot(int BlockSeconds, int BucketWidthUs, int BucketCount,
    int CapacitySeconds, long OutsideCapacityCount, HistogramBlock[] Blocks)
{
    public bool MatchesIntervalCounts(IntervalLatencySnapshot intervals)
    {
        if (OutsideCapacityCount != 0 || intervals.OutsideCapacity.Count != 0
            || intervals.IntervalSeconds != 1 || intervals.Intervals.Length != CapacitySeconds)
            return false;
        foreach (var block in Blocks)
        {
            long count = 0;
            for (var second = block.StartSecond; second < Math.Min(block.StartSecond + BlockSeconds, CapacitySeconds); second++)
                count += intervals.Intervals[second].Count;
            if (count != block.Count)
                return false;
        }
        return true;
    }
}
