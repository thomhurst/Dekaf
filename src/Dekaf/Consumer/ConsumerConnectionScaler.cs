using System.Diagnostics;

namespace Dekaf.Consumer;

/// <summary>
/// Monitors prefetch pipeline utilization and triggers connection scaling.
/// Scale-up: when all pipeline slots are saturated for >5 seconds.
/// Scale-down: when utilization is below 30% for >120 seconds.
/// Cooldown: 5 seconds between any scale operations.
/// </summary>
internal sealed class ConsumerConnectionScaler
{
    private const double ScaleDownUtilizationThreshold = 0.3;
    private const long ScaleUpSustainedTicks = 5 * TimeSpan.TicksPerSecond;
    private const long ScaleDownSustainedTicks = 120 * TimeSpan.TicksPerSecond;
    private const long CooldownTicks = 5 * TimeSpan.TicksPerSecond;

    private readonly int _initialConnectionCount;
    private readonly int _maxConnectionCount;
    private readonly Func<CancellationToken, ValueTask> _scaleUpAsync;
    private readonly Func<CancellationToken, ValueTask> _scaleDownAsync;
    private readonly int _pipelineDepth;

    private int _currentConnectionCount;
    private long _saturationStartTimestamp;
    private long _lowUtilizationStartTimestamp;
    private long _lastScaleTimestamp;
    private long _testTimeOffsetTicks; // For deterministic testing

    public int CurrentConnectionCount => _currentConnectionCount;

    public ConsumerConnectionScaler(
        int initialConnectionCount,
        int maxConnectionCount,
        Func<CancellationToken, ValueTask> scaleUpAsync,
        Func<CancellationToken, ValueTask> scaleDownAsync,
        int pipelineDepth)
    {
        _initialConnectionCount = initialConnectionCount;
        _maxConnectionCount = maxConnectionCount;
        _scaleUpAsync = scaleUpAsync;
        _scaleDownAsync = scaleDownAsync;
        _pipelineDepth = pipelineDepth;
        _currentConnectionCount = initialConnectionCount;
    }

    private long GetTimestamp() => Stopwatch.GetTimestamp() + _testTimeOffsetTicks;

    private static long ElapsedTicks(long startTimestamp, long endTimestamp)
        => (long)((endTimestamp - startTimestamp) * ((double)TimeSpan.TicksPerSecond / Stopwatch.Frequency));

    /// <summary>
    /// Reports current pipeline utilization. Call this each time a fetch completes or is dispatched.
    /// </summary>
    public void ReportPipelineUtilization(int inFlightCount, int pipelineDepth)
    {
        var now = GetTimestamp();
        var isSaturated = inFlightCount >= pipelineDepth;
        var utilization = pipelineDepth > 0 ? (double)inFlightCount / pipelineDepth : 0;

        if (isSaturated)
        {
            if (_saturationStartTimestamp == 0)
                _saturationStartTimestamp = now;
        }
        else
        {
            _saturationStartTimestamp = 0;
        }

        if (utilization < ScaleDownUtilizationThreshold)
        {
            if (_lowUtilizationStartTimestamp == 0)
                _lowUtilizationStartTimestamp = now;
        }
        else
        {
            _lowUtilizationStartTimestamp = 0;
        }
    }

    /// <summary>
    /// Evaluates scaling conditions and triggers scale-up or scale-down if thresholds are met.
    /// </summary>
    public void MaybeScale()
    {
        var now = GetTimestamp();

        // Check cooldown
        if (_lastScaleTimestamp != 0 && ElapsedTicks(_lastScaleTimestamp, now) < CooldownTicks)
            return;

        // Scale-up check
        if (_saturationStartTimestamp != 0
            && _currentConnectionCount < _maxConnectionCount
            && ElapsedTicks(_saturationStartTimestamp, now) >= ScaleUpSustainedTicks)
        {
            _currentConnectionCount++;
            _saturationStartTimestamp = 0;
            _lastScaleTimestamp = now;
            _ = _scaleUpAsync(CancellationToken.None);
            return;
        }

        // Scale-down check
        if (_lowUtilizationStartTimestamp != 0
            && _currentConnectionCount > _initialConnectionCount
            && ElapsedTicks(_lowUtilizationStartTimestamp, now) >= ScaleDownSustainedTicks)
        {
            _currentConnectionCount--;
            _lowUtilizationStartTimestamp = 0;
            _lastScaleTimestamp = now;
            _ = _scaleDownAsync(CancellationToken.None);
        }
    }

    // Test helpers
    internal void TestAdvanceTime(TimeSpan duration)
        => _testTimeOffsetTicks += (long)(duration.TotalSeconds * Stopwatch.Frequency);

    internal void TestSetConnectionCount(int count)
        => _currentConnectionCount = count;
}
