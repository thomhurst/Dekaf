using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Dekaf.Consumer;

/// <summary>
/// Dynamically adjusts fetch sizes based on the ratio of processing time to fetch wait time.
/// When the consumer is keeping up (processing finishes before the next fetch returns),
/// fetch sizes grow to reduce round-trip overhead. When falling behind, fetch sizes shrink
/// to reduce memory pressure and allow the consumer to catch up.
/// </summary>
/// <remarks>
/// <para>This class is not thread-safe and should be called from a single loop (the prefetch loop).</para>
/// <para>The feedback signal is the ratio of processing time to fetch latency:</para>
/// <list type="bullet">
/// <item><description>Ratio &lt; 1.0: consumer processes faster than it fetches (keeping up) — grow fetch size</description></item>
/// <item><description>Ratio &gt; 1.0: consumer processes slower than it fetches (falling behind) — shrink fetch size</description></item>
/// </list>
/// <para>Growth is multiplicative (×1.5) and shrinkage is also multiplicative (×0.75),
/// providing asymmetric scaling: growing is cautious, shrinking is responsive.</para>
/// </remarks>
internal sealed class AdaptiveFetchSizer
{
    private readonly int _minPartitionFetchBytes;
    private readonly int _maxPartitionFetchBytes;
    private readonly int _minFetchMaxBytes;
    private readonly int _maxFetchMaxBytes;
    private readonly double _growthFactor;
    private readonly double _shrinkFactor;
    private readonly double _growThreshold;
    private readonly double _shrinkThreshold;
    private readonly int _stableWindowCount;

    private int _currentPartitionFetchBytes;
    private int _currentFetchMaxBytes;
    private int _consecutiveGrowSignals;
    private int _consecutiveShrinkSignals;
    private long _lastFetchStartTimestamp;
    private long _lastFetchEndTimestamp;
    private long _testTimeOffsetTicks;

    /// <summary>
    /// The current adaptive MaxPartitionFetchBytes value.
    /// </summary>
    internal int CurrentPartitionFetchBytes => _currentPartitionFetchBytes;

    /// <summary>
    /// The current adaptive FetchMaxBytes value.
    /// </summary>
    internal int CurrentFetchMaxBytes => _currentFetchMaxBytes;

    /// <summary>
    /// Number of consecutive grow signals observed. Exposed for testing.
    /// </summary>
    internal int ConsecutiveGrowSignals => _consecutiveGrowSignals;

    /// <summary>
    /// Number of consecutive shrink signals observed. Exposed for testing.
    /// </summary>
    internal int ConsecutiveShrinkSignals => _consecutiveShrinkSignals;

    public AdaptiveFetchSizer(AdaptiveFetchSizingOptions options)
    {
        _minPartitionFetchBytes = options.MinPartitionFetchBytes;
        _maxPartitionFetchBytes = options.MaxPartitionFetchBytes;
        _minFetchMaxBytes = options.MinFetchMaxBytes;
        _maxFetchMaxBytes = options.MaxFetchMaxBytes;
        _growthFactor = options.GrowthFactor;
        _shrinkFactor = options.ShrinkFactor;
        _growThreshold = options.GrowThreshold;
        _shrinkThreshold = options.ShrinkThreshold;
        _stableWindowCount = options.StableWindowCount;

        _currentPartitionFetchBytes = options.InitialPartitionFetchBytes;
        _currentFetchMaxBytes = options.InitialFetchMaxBytes;
    }

    private long GetTimestamp() => Stopwatch.GetTimestamp() + _testTimeOffsetTicks;

    /// <summary>
    /// Records the start of a fetch operation. Call before sending the fetch request.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void RecordFetchStart()
    {
        _lastFetchStartTimestamp = GetTimestamp();
    }

    /// <summary>
    /// Records the completion of a fetch operation. Call after the fetch response is received.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void RecordFetchEnd()
    {
        _lastFetchEndTimestamp = GetTimestamp();
    }

    /// <summary>
    /// Reports the processing duration for the most recent batch and evaluates
    /// whether fetch sizes should be adjusted. Call after the consumer finishes
    /// processing a batch of records.
    /// </summary>
    /// <param name="processingDuration">Time spent processing the fetched records.</param>
    public void ReportProcessingComplete(TimeSpan processingDuration)
    {
        if (_lastFetchStartTimestamp == 0 || _lastFetchEndTimestamp == 0)
            return;

        var fetchDuration = Stopwatch.GetElapsedTime(_lastFetchStartTimestamp, _lastFetchEndTimestamp);

        if (fetchDuration <= TimeSpan.Zero)
            return;

        var ratio = processingDuration / fetchDuration;
        EvaluateAndAdjust(ratio);
    }

    /// <summary>
    /// Evaluates the processing-to-fetch ratio and adjusts fetch sizes if thresholds
    /// have been sustained for the required window count.
    /// </summary>
    /// <param name="processingToFetchRatio">
    /// Ratio of processing time to fetch time.
    /// Values &lt; <see cref="_growThreshold"/> indicate the consumer is keeping up.
    /// Values &gt; <see cref="_shrinkThreshold"/> indicate the consumer is falling behind.
    /// </param>
    internal void EvaluateAndAdjust(double processingToFetchRatio)
    {
        switch (processingToFetchRatio)
        {
            case var r when r < _growThreshold:
                _consecutiveGrowSignals++;
                _consecutiveShrinkSignals = 0;

                if (_consecutiveGrowSignals >= _stableWindowCount)
                {
                    Grow();
                    _consecutiveGrowSignals = 0;
                }
                break;

            case var r when r > _shrinkThreshold:
                _consecutiveShrinkSignals++;
                _consecutiveGrowSignals = 0;

                if (_consecutiveShrinkSignals >= _stableWindowCount)
                {
                    Shrink();
                    _consecutiveShrinkSignals = 0;
                }
                break;

            default:
                // In the stable zone — reset both counters
                _consecutiveGrowSignals = 0;
                _consecutiveShrinkSignals = 0;
                break;
        }
    }

    private void Grow()
    {
        _currentPartitionFetchBytes = Math.Min(
            (int)Math.Min((long)(_currentPartitionFetchBytes * _growthFactor), int.MaxValue),
            _maxPartitionFetchBytes);

        _currentFetchMaxBytes = Math.Min(
            (int)Math.Min((long)(_currentFetchMaxBytes * _growthFactor), int.MaxValue),
            _maxFetchMaxBytes);
    }

    private void Shrink()
    {
        _currentPartitionFetchBytes = Math.Max(
            (int)(_currentPartitionFetchBytes * _shrinkFactor),
            _minPartitionFetchBytes);

        _currentFetchMaxBytes = Math.Max(
            (int)(_currentFetchMaxBytes * _shrinkFactor),
            _minFetchMaxBytes);
    }

    /// <summary>
    /// Advances the internal test clock by the specified duration.
    /// For testing only.
    /// </summary>
    internal void TestAdvanceTime(TimeSpan duration)
        => _testTimeOffsetTicks += (long)(duration.TotalSeconds * Stopwatch.Frequency);
}

/// <summary>
/// Configuration options for adaptive fetch sizing.
/// </summary>
public sealed class AdaptiveFetchSizingOptions
{
    /// <summary>
    /// Minimum MaxPartitionFetchBytes. Fetch size will not shrink below this.
    /// Default is 1 MB (1048576 bytes).
    /// </summary>
    public int MinPartitionFetchBytes { get; init; } = 1048576;

    /// <summary>
    /// Maximum MaxPartitionFetchBytes. Fetch size will not grow above this.
    /// Default is 16 MB (16777216 bytes).
    /// </summary>
    public int MaxPartitionFetchBytes { get; init; } = 16 * 1024 * 1024;

    /// <summary>
    /// Initial MaxPartitionFetchBytes to start with before adaptation begins.
    /// Default is 1 MB (1048576 bytes), matching the Kafka default.
    /// </summary>
    public int InitialPartitionFetchBytes { get; init; } = 1048576;

    /// <summary>
    /// Minimum FetchMaxBytes. Total fetch response size will not shrink below this.
    /// Default is 1 MB (1048576 bytes).
    /// </summary>
    public int MinFetchMaxBytes { get; init; } = 1048576;

    /// <summary>
    /// Maximum FetchMaxBytes. Total fetch response size will not grow above this.
    /// Default is 100 MB (104857600 bytes).
    /// </summary>
    public int MaxFetchMaxBytes { get; init; } = 100 * 1024 * 1024;

    /// <summary>
    /// Initial FetchMaxBytes to start with before adaptation begins.
    /// Default is 52428800 (50 MB), matching the Kafka default.
    /// </summary>
    public int InitialFetchMaxBytes { get; init; } = 52428800;

    /// <summary>
    /// Multiplicative factor for growing fetch sizes. Applied when the consumer is keeping up.
    /// Default is 1.5 (50% growth per step).
    /// </summary>
    public double GrowthFactor { get; init; } = 1.5;

    /// <summary>
    /// Multiplicative factor for shrinking fetch sizes. Applied when the consumer is falling behind.
    /// Default is 0.75 (25% reduction per step).
    /// </summary>
    public double ShrinkFactor { get; init; } = 0.75;

    /// <summary>
    /// Processing-to-fetch ratio below which the consumer is considered "keeping up"
    /// and fetch sizes should grow. Default is 0.5 (processing takes less than half the fetch time).
    /// </summary>
    public double GrowThreshold { get; init; } = 0.5;

    /// <summary>
    /// Processing-to-fetch ratio above which the consumer is considered "falling behind"
    /// and fetch sizes should shrink. Default is 2.0 (processing takes more than twice the fetch time).
    /// </summary>
    public double ShrinkThreshold { get; init; } = 2.0;

    /// <summary>
    /// Number of consecutive signals in the same direction required before adjusting fetch sizes.
    /// Prevents oscillation from transient spikes. Default is 3.
    /// </summary>
    public int StableWindowCount { get; init; } = 3;
}
