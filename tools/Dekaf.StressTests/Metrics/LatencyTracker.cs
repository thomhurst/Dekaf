using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Dekaf.StressTests.Metrics;

/// <summary>
/// Thread-safe latency tracker using a microsecond-resolution histogram for percentile calculations.
/// Pre-allocates all buckets up front so recording is allocation-free.
/// Default configuration: 10μs bucket width, covering 0–5,000ms (500,000 buckets ≈ 4MB).
/// </summary>
internal sealed class LatencyTracker
{
    private readonly long[] _buckets;
    private readonly double _bucketWidthUs;
    private readonly double _maxValueUs;
    private long _count;
    private long _overflowCount;
    private long _minTicks = long.MaxValue;
    private long _maxTicks;

    /// <param name="maxValueMs">Upper bound of the histogram in milliseconds. Values above this are counted as overflow.</param>
    /// <param name="bucketWidthUs">Width of each bucket in microseconds. Smaller = higher resolution but more memory.</param>
    public LatencyTracker(double maxValueMs = 5000, double bucketWidthUs = 10)
    {
        _maxValueUs = maxValueMs * 1000.0;
        _bucketWidthUs = bucketWidthUs;
        _buckets = new long[(int)(_maxValueUs / bucketWidthUs)];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void RecordTicks(long ticks)
    {
        Interlocked.Increment(ref _count);
        UpdateMinMax(ticks);

        var microseconds = ticks * 1_000_000.0 / Stopwatch.Frequency;

        if (microseconds >= _maxValueUs)
        {
            Interlocked.Increment(ref _overflowCount);
            return;
        }

        var bucketIndex = (int)(microseconds / _bucketWidthUs);
        if ((uint)bucketIndex >= (uint)_buckets.Length)
        {
            bucketIndex = _buckets.Length - 1;
        }

        Interlocked.Increment(ref _buckets[bucketIndex]);
    }

    public void Record(double milliseconds)
    {
        var ticks = (long)(milliseconds / 1000.0 * Stopwatch.Frequency);
        RecordTicks(ticks);
    }

    private void UpdateMinMax(long ticks)
    {
        long currentMin;
        do
        {
            currentMin = Interlocked.Read(ref _minTicks);
            if (ticks >= currentMin)
                break;
        } while (Interlocked.CompareExchange(ref _minTicks, ticks, currentMin) != currentMin);

        long currentMax;
        do
        {
            currentMax = Interlocked.Read(ref _maxTicks);
            if (ticks <= currentMax)
                break;
        } while (Interlocked.CompareExchange(ref _maxTicks, ticks, currentMax) != currentMax);
    }

    private double GetPercentileUs(double percentile)
    {
        var count = Interlocked.Read(ref _count);
        if (count == 0)
            return 0;

        var targetCount = (long)(count * percentile / 100.0);
        var cumulativeCount = 0L;

        for (var i = 0; i < _buckets.Length; i++)
        {
            cumulativeCount += Interlocked.Read(ref _buckets[i]);
            if (cumulativeCount >= targetCount)
            {
                return (i + 0.5) * _bucketWidthUs;
            }
        }

        return TicksToUs(Interlocked.Read(ref _maxTicks));
    }

    public LatencySnapshot GetSnapshot()
    {
        var minTicks = Interlocked.Read(ref _minTicks);
        var maxTicks = Interlocked.Read(ref _maxTicks);

        return new LatencySnapshot
        {
            Count = Interlocked.Read(ref _count),
            MinUs = minTicks == long.MaxValue ? 0 : TicksToUs(minTicks),
            MaxUs = TicksToUs(maxTicks),
            P50Us = GetPercentileUs(50),
            P95Us = GetPercentileUs(95),
            P99Us = GetPercentileUs(99),
            OverflowCount = Interlocked.Read(ref _overflowCount)
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double TicksToUs(long ticks) => ticks * 1_000_000.0 / Stopwatch.Frequency;
}

internal sealed class LatencySnapshot
{
    public required long Count { get; init; }
    public required double MinUs { get; init; }
    public required double MaxUs { get; init; }
    public required double P50Us { get; init; }
    public required double P95Us { get; init; }
    public required double P99Us { get; init; }
    public required long OverflowCount { get; init; }

    // Convenience properties for backward compatibility with JSON serialization
    public double MinMs => MinUs / 1000.0;
    public double MaxMs => MaxUs / 1000.0;
    public double P50Ms => P50Us / 1000.0;
    public double P95Ms => P95Us / 1000.0;
    public double P99Ms => P99Us / 1000.0;
}
