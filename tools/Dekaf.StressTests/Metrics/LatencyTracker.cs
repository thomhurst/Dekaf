using System.Diagnostics;

namespace Dekaf.StressTests.Metrics;

/// <summary>
/// Thread-safe latency tracker using bucket-based histogram for percentile calculations.
/// Optimized for high-throughput scenarios with minimal allocation.
/// </summary>
internal sealed class LatencyTracker
{
    private readonly long[] _buckets;
    private readonly double _bucketWidth;
    private readonly double _maxValue;
    private long _count;
    private long _overflowCount;
    private double _minValue = double.MaxValue;
    private double _maxRecordedValue;
    private readonly object _minMaxLock = new();

    public LatencyTracker(double maxValueMs = 10000, int bucketCount = 10000)
    {
        _maxValue = maxValueMs;
        _buckets = new long[bucketCount];
        _bucketWidth = maxValueMs / bucketCount;
    }

    public void Record(double milliseconds)
    {
        Interlocked.Increment(ref _count);

        lock (_minMaxLock)
        {
            if (milliseconds < _minValue)
            {
                _minValue = milliseconds;
            }

            if (milliseconds > _maxRecordedValue)
            {
                _maxRecordedValue = milliseconds;
            }
        }

        if (milliseconds >= _maxValue)
        {
            Interlocked.Increment(ref _overflowCount);
            return;
        }

        var bucketIndex = (int)(milliseconds / _bucketWidth);
        if (bucketIndex >= _buckets.Length)
        {
            bucketIndex = _buckets.Length - 1;
        }

        Interlocked.Increment(ref _buckets[bucketIndex]);
    }

    public void RecordTicks(long ticks)
    {
        var milliseconds = ticks * 1000.0 / Stopwatch.Frequency;
        Record(milliseconds);
    }

    public double GetPercentile(double percentile)
    {
        var count = Interlocked.Read(ref _count);
        if (count == 0)
        {
            return 0;
        }

        var targetCount = (long)(count * percentile / 100.0);
        var cumulativeCount = 0L;

        for (var i = 0; i < _buckets.Length; i++)
        {
            cumulativeCount += Interlocked.Read(ref _buckets[i]);
            if (cumulativeCount >= targetCount)
            {
                return (i + 0.5) * _bucketWidth;
            }
        }

        return _maxRecordedValue;
    }

    public LatencySnapshot GetSnapshot()
    {
        double min, max;
        lock (_minMaxLock)
        {
            min = _minValue == double.MaxValue ? 0 : _minValue;
            max = _maxRecordedValue;
        }

        return new LatencySnapshot
        {
            Count = Interlocked.Read(ref _count),
            MinMs = min,
            MaxMs = max,
            P50Ms = GetPercentile(50),
            P95Ms = GetPercentile(95),
            P99Ms = GetPercentile(99),
            OverflowCount = Interlocked.Read(ref _overflowCount)
        };
    }
}

internal sealed class LatencySnapshot
{
    public required long Count { get; init; }
    public required double MinMs { get; init; }
    public required double MaxMs { get; init; }
    public required double P50Ms { get; init; }
    public required double P95Ms { get; init; }
    public required double P99Ms { get; init; }
    public required long OverflowCount { get; init; }
}
