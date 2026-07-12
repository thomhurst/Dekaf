using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace Dekaf.StressTests.Metrics;

/// <summary>
/// Thread-safe latency tracker using a microsecond-resolution histogram for percentile calculations.
/// Pre-allocates all buckets up front so recording is allocation-free.
/// Default configuration: 10μs bucket width, covering 0–5,000ms (500,000 buckets ≈ 4MB).
/// </summary>
internal sealed class LatencyTracker
{
    private const int MaxOutlierSamples = 256;
    private static readonly long OutlierThresholdTicks = Stopwatch.Frequency;

    private readonly long[] _buckets;
    private readonly object _outlierLock = new();
    private readonly LatencyOutlierSample[] _outlierSamples = new LatencyOutlierSample[MaxOutlierSamples];
    private readonly double _bucketWidthUs;
    private readonly double _maxValueUs;
    private long _count;
    private long _overflowCount;
    private long _minTicks = long.MaxValue;
    private long _maxTicks;
    private long _outlierCount;

    /// <param name="maxValueMs">Upper bound of the histogram in milliseconds. Values above this are counted as overflow.</param>
    /// <param name="bucketWidthUs">Width of each bucket in microseconds. Smaller = higher resolution but more memory.</param>
    public LatencyTracker(double maxValueMs = 5000, double bucketWidthUs = 10)
    {
        _maxValueUs = maxValueMs * 1000.0;
        _bucketWidthUs = bucketWidthUs;
        _buckets = new long[(int)(_maxValueUs / bucketWidthUs)];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void RecordTicks(long ticks, long? messageIndex = null)
    {
        Interlocked.Increment(ref _count);
        UpdateMinMax(ticks);

        if (ticks >= OutlierThresholdTicks)
        {
            RecordOutlier(ticks, messageIndex);
        }

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

    private void RecordOutlier(long ticks, long? messageIndex)
    {
        lock (_outlierLock)
        {
            var slot = _outlierCount++;
            if ((ulong)slot >= MaxOutlierSamples)
            {
                return;
            }

            var completedAt = DateTimeOffset.UtcNow;
            var latency = Stopwatch.GetElapsedTime(0, ticks);
            _outlierSamples[slot] = new LatencyOutlierSample
            {
                MessageIndex = messageIndex,
                StartedAtUtc = completedAt - latency,
                CompletedAtUtc = completedAt,
                LatencyUs = latency.TotalMicroseconds
            };
        }
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

    /// <summary>
    /// Computes multiple percentiles in a single pass over the histogram buckets.
    /// </summary>
    private (double P50, double P95, double P99) GetPercentilesUs()
    {
        var count = Interlocked.Read(ref _count);
        if (count == 0)
            return (0, 0, 0);

        var targetP50 = Math.Max(1L, (long)(count * 50 / 100.0));
        var targetP95 = Math.Max(1L, (long)(count * 95 / 100.0));
        var targetP99 = Math.Max(1L, (long)(count * 99 / 100.0));

        double p50 = 0, p95 = 0, p99 = 0;
        var foundP50 = false;
        var foundP95 = false;
        var foundP99 = false;
        var cumulativeCount = 0L;

        for (var i = 0; i < _buckets.Length; i++)
        {
            cumulativeCount += Interlocked.Read(ref _buckets[i]);
            var bucketMidpoint = (i + 0.5) * _bucketWidthUs;

            if (!foundP50 && cumulativeCount >= targetP50)
            {
                p50 = bucketMidpoint;
                foundP50 = true;
            }

            if (!foundP95 && cumulativeCount >= targetP95)
            {
                p95 = bucketMidpoint;
                foundP95 = true;
            }

            if (!foundP99 && cumulativeCount >= targetP99)
            {
                p99 = bucketMidpoint;
                foundP99 = true;
                break; // All percentiles found
            }
        }

        var maxUs = TicksToUs(Interlocked.Read(ref _maxTicks));
        if (!foundP50) p50 = maxUs;
        if (!foundP95) p95 = maxUs;
        if (!foundP99) p99 = maxUs;

        return (p50, p95, p99);
    }

    public LatencySnapshot GetSnapshot()
    {
        var minTicks = Interlocked.Read(ref _minTicks);
        var maxTicks = Interlocked.Read(ref _maxTicks);
        var (p50, p95, p99) = GetPercentilesUs();

        List<LatencyOutlierSample> outlierSamples;
        long droppedOutlierSamples;
        lock (_outlierLock)
        {
            var capturedOutlierCount = (int)Math.Min(_outlierCount, MaxOutlierSamples);
            outlierSamples = new List<LatencyOutlierSample>(capturedOutlierCount);
            for (var index = 0; index < capturedOutlierCount; index++)
            {
                outlierSamples.Add(_outlierSamples[index]);
            }

            droppedOutlierSamples = Math.Max(0, _outlierCount - MaxOutlierSamples);
        }

        return new LatencySnapshot
        {
            Count = Interlocked.Read(ref _count),
            MinUs = minTicks == long.MaxValue ? 0 : TicksToUs(minTicks),
            MaxUs = TicksToUs(maxTicks),
            P50Us = p50,
            P95Us = p95,
            P99Us = p99,
            OverflowCount = Interlocked.Read(ref _overflowCount),
            OutlierSamples = outlierSamples,
            DroppedOutlierSamples = droppedOutlierSamples
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
    public List<LatencyOutlierSample> OutlierSamples { get; init; } = [];
    public long DroppedOutlierSamples { get; init; }

    // Convenience properties for backward compatibility (excluded from JSON output)
    [JsonIgnore] public double MinMs => MinUs / 1000.0;
    [JsonIgnore] public double MaxMs => MaxUs / 1000.0;
    [JsonIgnore] public double P50Ms => P50Us / 1000.0;
    [JsonIgnore] public double P95Ms => P95Us / 1000.0;
    [JsonIgnore] public double P99Ms => P99Us / 1000.0;
}

internal readonly record struct LatencyOutlierSample
{
    public long? MessageIndex { get; init; }
    public required DateTimeOffset StartedAtUtc { get; init; }
    public required DateTimeOffset CompletedAtUtc { get; init; }
    public required double LatencyUs { get; init; }
}
