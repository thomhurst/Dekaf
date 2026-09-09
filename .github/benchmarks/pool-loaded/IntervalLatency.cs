using System.Diagnostics;

// Exact extrema by completion time. These are not independent latency samples or
// confidence bounds for the population maximum. Global histograms remain separate.
internal sealed class IntervalLatency
{
    private readonly Interval[] _intervals;
    private readonly Interval _outsideCapacity = new();

    public IntervalLatency(int capacitySeconds)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacitySeconds);
        _intervals = new Interval[capacitySeconds];
        for (var index = 0; index < _intervals.Length; index++)
            _intervals[index] = new Interval();
    }

    public void RecordTicks(long latencyTicks, long completedAfterStartTicks)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(latencyTicks);
        ArgumentOutOfRangeException.ThrowIfNegative(completedAfterStartTicks);
        var index = completedAfterStartTicks / Stopwatch.Frequency;
        var interval = index < _intervals.Length ? _intervals[(int)index] : _outsideCapacity;
        interval.Record(latencyTicks);
    }

    // Read after successful drain for an exact result. Failure snapshots can race
    // late completions and must remain labeled as incomplete diagnostics.
    public IntervalLatencySnapshot GetSnapshot()
    {
        var intervals = new IntervalExtrema[_intervals.Length];
        for (var index = 0; index < intervals.Length; index++)
            intervals[index] = _intervals[index].GetSnapshot();
        return new IntervalLatencySnapshot(1, Stopwatch.Frequency, intervals, _outsideCapacity.GetSnapshot());
    }

    private sealed class Interval
    {
        private long _count;
        private long _minTicks = long.MaxValue;
        private long _maxTicks;

        public void Record(long ticks)
        {
            long previous;
            do
            {
                previous = Volatile.Read(ref _minTicks);
                if (ticks >= previous)
                    break;
            } while (Interlocked.CompareExchange(ref _minTicks, ticks, previous) != previous);
            do
            {
                previous = Volatile.Read(ref _maxTicks);
                if (ticks <= previous)
                    break;
            } while (Interlocked.CompareExchange(ref _maxTicks, ticks, previous) != previous);
            Interlocked.Increment(ref _count);
        }

        public IntervalExtrema GetSnapshot()
        {
            var count = Volatile.Read(ref _count);
            return new IntervalExtrema(count,
                count == 0 ? 0 : Volatile.Read(ref _minTicks), Volatile.Read(ref _maxTicks));
        }
    }
}

internal sealed record IntervalExtrema(long Count, long MinTicks, long MaxTicks);
internal sealed record IntervalLatencySnapshot(int IntervalSeconds, long TicksPerSecond,
    IntervalExtrema[] Intervals, IntervalExtrema OutsideCapacity);
