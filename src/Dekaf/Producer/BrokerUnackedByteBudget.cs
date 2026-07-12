using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Dekaf.Producer;

/// <summary>
/// Bounds the bytes a single broker may hold unacknowledged (sealed but not yet acked)
/// so that producer queueing latency stays near <see cref="ProducerOptions.DeliveryLatencyTargetMs"/>
/// instead of growing to the full <see cref="ProducerOptions.BufferMemory"/> reservoir.
/// Under open-loop saturation, append-to-ack latency equals standing unacked bytes divided
/// by the broker's drain rate; capping the standing bytes to
/// <c>target × measured drain rate</c> caps the latency.
/// <para>
/// Ownership contract: <see cref="Charge"/>/<see cref="Release"/>/<see cref="IsOverBudget"/>/
/// <see cref="RecordAdmissionBlock"/> are cross-thread (producer appenders and terminal batch
/// paths). <see cref="OnAcked"/> and <see cref="SetCap"/> are single-writer — only the owning
/// broker's send loop calls them — so the EWMA state needs no synchronization; the computed
/// budget is published with a volatile write.
/// </para>
/// </summary>
internal sealed class BrokerUnackedByteBudget
{
    /// <summary>
    /// Budget ceiling in batches per connection. Sized so a ~1 GB/s drain with ~30 ms ack
    /// round-trips keeps the pipe full at the 1 MB default batch size (~32 MB BDP). Shared
    /// with <see cref="BrokerSender"/>'s written-unacked pipeline ceiling so the two bounds
    /// stay consistent.
    /// </summary>
    internal const int CapBatchMultiplier = 32;

    /// <summary>EWMA time constant for the acked-bytes rate. An active sample spanning dt
    /// seconds gets weight min(1, dt/τ), so history older than ~τ decays away. A gap longer
    /// than τ with no remaining unacked bytes is treated as idle and re-anchors the window.</summary>
    private const double RateEwmaTauSeconds = 1.0;

    /// <summary>Fixed smoothing factor for the per-request ack round-trip EWMA.</summary>
    private const double RttEwmaAlpha = 0.25;

    /// <summary>Budget never drops below this multiple of the measured bandwidth-delay
    /// product (rate × RTT). Without this guard a target below the broker RTT would shrink
    /// the budget below BDP, underfill the pipe, lower the measured rate, and ratchet the
    /// budget down to the floor — collapsing throughput on high-latency links.</summary>
    private const double RttSafetyMultiplier = 1.5;

    /// <summary>Rate samples shorter than this are carried into the next sample so a burst
    /// of sub-millisecond ack passes cannot produce wildly noisy instantaneous rates.</summary>
    private const double MinRateSampleSeconds = 0.001;

    // Cross-thread state.
    private long _unackedBytes;
    private long _budgetBytes;
    private long _admissionBlockEvents;

    // Single-writer state (owning send loop only; constructor runs before the loop starts).
    private readonly long _floorBytes;
    private readonly double _targetSeconds;
    private long _capBytes;
    private double _ewmaBytesPerSecond;
    private double _ewmaRttSeconds;
    private long _pendingAckedBytes;
    private long _lastRateSampleTimestamp;
    private bool _hasRateSample;
    private bool _hasRttSample;

    public BrokerUnackedByteBudget(double targetSeconds, long floorBytes, long initialCapBytes)
    {
        _targetSeconds = targetSeconds;
        _floorBytes = Math.Max(1, floorBytes);
        _capBytes = Math.Max(_floorBytes, initialCapBytes);
        Volatile.Write(ref _budgetBytes, _capBytes);
    }

    /// <summary>
    /// Budget ceiling for a broker: the same formula as the written-unacked pipeline ceiling
    /// (<c>BrokerSender.GetInFlightByteBudget</c>), which delegates here so the admission
    /// ceiling and the pipeline ceiling can never drift apart.
    /// </summary>
    internal static long ComputeCap(int batchSize, int connectionCount)
        => (long)Math.Max(1, batchSize) * CapBatchMultiplier * Math.Max(1, connectionCount);

    /// <summary>Current standing unacked bytes charged to this broker.</summary>
    internal long UnackedBytes => Volatile.Read(ref _unackedBytes);

    /// <summary>Currently published budget in bytes.</summary>
    internal long BudgetBytes => Volatile.Read(ref _budgetBytes);

    /// <summary>Total admission-gate blocks observed; consumed as a delta by adaptive
    /// connection scaling, which cannot see buffer pressure while the gate holds the
    /// accumulator near-empty.</summary>
    internal long AdmissionBlockEvents => Volatile.Read(ref _admissionBlockEvents);

    // Volatile.Read (a plain acquire load) rather than Interlocked.Read: this runs per
    // message from every appender thread, and a locked read would take the cache line
    // exclusive on every call, turning a read-mostly gate into a contention point.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsOverBudget()
        => Volatile.Read(ref _unackedBytes) >= Volatile.Read(ref _budgetBytes);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Charge(long bytes)
        => Interlocked.Add(ref _unackedBytes, bytes);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Release(long bytes)
    {
        var remaining = Interlocked.Add(ref _unackedBytes, -bytes);
        Debug.Assert(remaining >= 0, "Unacked byte accounting went negative — release without matching charge.");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void RecordAdmissionBlock()
        => Interlocked.Increment(ref _admissionBlockEvents);

    /// <summary>
    /// Updates the ceiling (routing-width-dependent) and republishes the budget.
    /// Called by the owning send loop at construction and on connection scale events.
    /// </summary>
    public void SetCap(long capBytes)
    {
        _capBytes = Math.Max(_floorBytes, capBytes);
        RecomputeBudget();
    }

    /// <summary>
    /// Feeds acked bytes and the latest request round-trip into the drain estimate, then
    /// republishes the budget. Called only from the owning send loop after a response
    /// pass completes successfully-acked requests. O(1), allocation-free.
    /// </summary>
    public void OnAcked(long ackedBytes, long rttTicks, long nowTicks)
    {
        _pendingAckedBytes += ackedBytes;

        if (rttTicks > 0)
        {
            var rttSeconds = (double)rttTicks / Stopwatch.Frequency;
            _ewmaRttSeconds = _hasRttSample
                ? _ewmaRttSeconds + (RttEwmaAlpha * (rttSeconds - _ewmaRttSeconds))
                : rttSeconds;
            _hasRttSample = true;
        }

        if (_lastRateSampleTimestamp == 0)
        {
            // First observation anchors the sampling window; no rate can be derived yet.
            _lastRateSampleTimestamp = nowTicks;
            return;
        }

        var elapsedSeconds = (double)(nowTicks - _lastRateSampleTimestamp) / Stopwatch.Frequency;
        if (elapsedSeconds > RateEwmaTauSeconds && UnackedBytes == 0)
        {
            // No backlog survived the gap, so elapsed time is producer idle time rather than
            // broker drain time. Preserve the established rate and start a fresh window with
            // this first resumed ack; folding the idle gap into the sample would collapse the
            // budget to the floor and unnecessarily throttle the resumed burst.
            _lastRateSampleTimestamp = nowTicks;
            return;
        }

        if (elapsedSeconds < MinRateSampleSeconds)
            return;

        var sampleBytesPerSecond = _pendingAckedBytes / elapsedSeconds;
        _pendingAckedBytes = 0;
        _lastRateSampleTimestamp = nowTicks;

        var alpha = Math.Min(1.0, elapsedSeconds / RateEwmaTauSeconds);
        _ewmaBytesPerSecond = _hasRateSample
            ? _ewmaBytesPerSecond + (alpha * (sampleBytesPerSecond - _ewmaBytesPerSecond))
            : sampleBytesPerSecond;
        _hasRateSample = true;

        RecomputeBudget();
    }

    private void RecomputeBudget()
    {
        long budget;
        if (!_hasRateSample)
        {
            // Cold start: no drain estimate yet — keep today's semantics (cap) so short-lived
            // producers and tests never see admission throttling.
            budget = _capBytes;
        }
        else
        {
            var horizonSeconds = _hasRttSample
                ? Math.Max(_targetSeconds, RttSafetyMultiplier * _ewmaRttSeconds)
                : _targetSeconds;
            budget = (long)(_ewmaBytesPerSecond * horizonSeconds);
            budget = Math.Clamp(budget, _floorBytes, _capBytes);
        }

        Volatile.Write(ref _budgetBytes, budget);
    }
}
