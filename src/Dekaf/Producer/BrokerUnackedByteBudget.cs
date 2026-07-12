using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Dekaf.Producer;

/// <summary>
/// Bounds the bytes a single broker may hold unacknowledged (sealed but not yet acked)
/// so that producer queueing latency stays near <see cref="ProducerOptions.DeliveryLatencyTargetMs"/>
/// instead of growing to the full <see cref="ProducerOptions.BufferMemory"/> reservoir.
/// Under open-loop saturation, append-to-ack latency equals standing unacked bytes divided
/// by the broker's drain rate; capping the standing bytes to
/// <c>target × measured drain rate</c> caps the latency. The RTT safety floor uses a
/// periodically refreshed minimum RTT so standing queue delay cannot inflate its horizon.
/// <para>
/// Ownership contract: <see cref="Charge"/>/<see cref="Release"/>/<see cref="IsOverBudget"/>/
/// <see cref="IsOverBudgetAt"/>/<see cref="RecordAdmissionBlock"/> are cross-thread (producer
/// appenders and terminal batch paths). <see cref="OnAcked"/> and <see cref="SetCap"/> are
/// single-writer — only the owning
/// broker's send loop calls them — so the estimator state needs no synchronization; the computed
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

    /// <summary>Number of per-request delivery-rate samples retained by the maximum filter.</summary>
    private const int RateWindowSamples = 10;

    /// <summary>How long an observed base RTT remains valid before it is refreshed.</summary>
    private static readonly long MinRttWindowTicks = 10 * Stopwatch.Frequency;

    /// <summary>
    /// Minimum target-only drain interval used to refresh base RTT without standing
    /// queue delay. Long-base-RTT links probe for at least one observed round-trip.
    /// </summary>
    private static readonly long MinRttProbeDurationTicks = Stopwatch.Frequency / 20;

    /// <summary>
    /// Maximum target-only drain interval. An RTT outlier must not disable the retained
    /// RTT safety floor for its full (potentially multi-second) duration.
    /// </summary>
    private static readonly long MaxMinRttProbeDurationTicks = Stopwatch.Frequency / 4;

    /// <summary>Budget never drops below this multiple of the measured bandwidth-delay
    /// product (rate × RTT). Without this guard a target below the broker RTT would shrink
    /// the budget below BDP, underfill the pipe, lower the measured rate, and ratchet the
    /// budget down to the floor — collapsing throughput on high-latency links.</summary>
    private const double RttSafetyMultiplier = 1.5;

    private const int ProbeIntervalRtts = 8;
    private const double ProbeBudgetMultiplier = 1.25;

    // Cross-thread state.
    private long _unackedBytes;
    private long _budgetBytes;
    private long _budgetAfterMinRttProbeBytes;
    private long _probeBudgetAfterMinRttProbeBytes;
    private long _admissionBlockEvents;

    // Single-writer state (owning send loop only; constructor runs before the loop starts).
    private readonly long _floorBytes;
    private readonly double _targetSeconds;
    private long _capBytes;
    // Monotonically-decreasing deque for the last RateWindowSamples delivery-rate samples.
    // Each sample enters and leaves once, keeping acknowledgement processing O(1) amortized.
    private readonly double[] _rateMaxValues = new double[RateWindowSamples];
    private readonly long[] _rateMaxSequences = new long[RateWindowSamples];
    private int _rateMaxHead;
    private int _rateMaxTail;
    private int _rateMaxCount;
    private long _rateSampleSequence;
    private double _minRttSeconds;
    private double _minRttProbeMinimumSeconds;
    private bool _hasMinRttSample;
    private long _minRttTimestamp;
    // Written only by the send loop; acquire-read by appenders after _budgetBytes publishes
    // the matching precomputed post-probe budgets.
    private long _minRttProbeUntilTimestamp;
    private long _nextProbeTimestamp;
    private long _probeUntilTimestamp;

    public BrokerUnackedByteBudget(double targetSeconds, long floorBytes, long initialCapBytes)
    {
        _targetSeconds = targetSeconds;
        _floorBytes = Math.Max(1, floorBytes);
        _capBytes = Math.Max(_floorBytes, initialCapBytes);
        Volatile.Write(ref _budgetBytes, _capBytes);
        Volatile.Write(ref _budgetAfterMinRttProbeBytes, _capBytes);
        Volatile.Write(ref _probeBudgetAfterMinRttProbeBytes, _capBytes);
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
    public bool IsOverBudgetAt(long nowTicks)
    {
        var budget = Volatile.Read(ref _budgetBytes);
        var minRttProbeUntilTimestamp = Volatile.Read(ref _minRttProbeUntilTimestamp);
        if (minRttProbeUntilTimestamp != 0 && nowTicks >= minRttProbeUntilTimestamp)
        {
            var capacityProbeUntilTimestamp = Volatile.Read(ref _probeUntilTimestamp);
            budget = capacityProbeUntilTimestamp != 0 && nowTicks < capacityProbeUntilTimestamp
                ? Volatile.Read(ref _probeBudgetAfterMinRttProbeBytes)
                : Volatile.Read(ref _budgetAfterMinRttProbeBytes);
        }

        return Volatile.Read(ref _unackedBytes) >= budget;
    }

    /// <summary>
    /// Returns the delay until an active minimum-RTT probe changes the admission budget,
    /// or <see cref="Timeout.Infinite"/> when no time-based recheck is needed.
    /// </summary>
    internal int GetAdmissionRecheckDelayMilliseconds(long nowTicks)
    {
        var probeUntilTimestamp = Volatile.Read(ref _minRttProbeUntilTimestamp);
        if (probeUntilTimestamp == 0 || nowTicks >= probeUntilTimestamp)
            return Timeout.Infinite;

        var remainingTicks = probeUntilTimestamp - nowTicks;
        return Math.Max(1, (int)Math.Min(
            int.MaxValue,
            Math.Ceiling((double)remainingTicks * 1000 / Stopwatch.Frequency)));
    }

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
    public void SetCap(long capBytes, long nowTicks)
    {
        _capBytes = Math.Max(_floorBytes, capBytes);
        CompleteExpiredMinRttProbe(nowTicks);
        if (_probeUntilTimestamp != 0 && nowTicks >= _probeUntilTimestamp)
            _probeUntilTimestamp = 0;
        RecomputeBudget();
    }

    /// <summary>
    /// Feeds successfully acknowledged requests from one response pass into the drain
    /// estimate, then republishes the budget. Called only from the owning send loop after
    /// a response pass completes successfully-acked requests. O(1), allocation-free.
    /// </summary>
    public void OnAcked(long ackedBytes, long rttTicks, long nowTicks)
    {
        if (ackedBytes <= 0 || rttTicks <= 0)
            return;

        var rttSeconds = (double)rttTicks / Stopwatch.Frequency;
        UpdateMinRtt(rttSeconds, nowTicks);

        AddRateSample(ackedBytes / rttSeconds);

        if (_probeUntilTimestamp != 0 && nowTicks >= _probeUntilTimestamp)
            _probeUntilTimestamp = 0;

        var probeIntervalTicks = ProbeIntervalRtts * rttTicks;
        if (_nextProbeTimestamp == 0)
            _nextProbeTimestamp = nowTicks + probeIntervalTicks;

        if (_probeUntilTimestamp == 0 && nowTicks >= _nextProbeTimestamp)
        {
            if (nowTicks - _nextProbeTimestamp < probeIntervalTicks)
                _probeUntilTimestamp = nowTicks + rttTicks;

            // A schedule missed by a full interval represents producer idle time, not an
            // active-pipeline probe opportunity. Re-arm instead of probing on resume.
            _nextProbeTimestamp = nowTicks + probeIntervalTicks;
        }

        RecomputeBudget();
    }

    private void UpdateMinRtt(double rttSeconds, long nowTicks)
    {
        if (!_hasMinRttSample)
        {
            _minRttSeconds = rttSeconds;
            _minRttTimestamp = nowTicks;
            _hasMinRttSample = true;
            StartMinRttProbe(nowTicks, rttSeconds);
            return;
        }

        if (_minRttProbeUntilTimestamp != 0)
        {
            if (nowTicks >= _minRttProbeUntilTimestamp)
            {
                CompleteMinRttProbe(nowTicks);
            }
            else
            {
                _minRttProbeMinimumSeconds = Math.Min(_minRttProbeMinimumSeconds, rttSeconds);
                return;
            }
        }

        if (rttSeconds < _minRttSeconds)
        {
            _minRttSeconds = rttSeconds;
            _minRttTimestamp = nowTicks;
            return;
        }

        if (nowTicks - _minRttTimestamp >= MinRttWindowTicks)
            StartMinRttProbe(nowTicks, rttSeconds);
    }

    private void CompleteExpiredMinRttProbe(long nowTicks)
    {
        if (_minRttProbeUntilTimestamp != 0 && nowTicks >= _minRttProbeUntilTimestamp)
            CompleteMinRttProbe(nowTicks);
    }

    private void CompleteMinRttProbe(long nowTicks)
    {
        if (_minRttProbeMinimumSeconds != double.MaxValue)
            _minRttSeconds = _minRttProbeMinimumSeconds;

        // If the probe expired without an ack, retain the prior minimum and re-arm its
        // freshness window. An idle gap is not a base-RTT measurement.
        _minRttTimestamp = nowTicks;
        _minRttProbeUntilTimestamp = 0;
    }

    private void StartMinRttProbe(long nowTicks, double observedRttSeconds)
    {
        _minRttProbeMinimumSeconds = double.MaxValue;
        var observedRttTicks = (long)(observedRttSeconds * Stopwatch.Frequency);
        var probeDurationTicks = Math.Clamp(
            observedRttTicks,
            MinRttProbeDurationTicks,
            MaxMinRttProbeDurationTicks);
        _minRttProbeUntilTimestamp = nowTicks + probeDurationTicks;
    }

    private void AddRateSample(double bytesPerSecond)
    {
        var sequence = ++_rateSampleSequence;
        var oldestSequence = sequence - RateWindowSamples;

        if (_rateMaxCount > 0 && _rateMaxSequences[_rateMaxHead] <= oldestSequence)
        {
            _rateMaxHead = (_rateMaxHead + 1) % RateWindowSamples;
            _rateMaxCount--;
        }

        while (_rateMaxCount > 0)
        {
            var last = (_rateMaxTail + RateWindowSamples - 1) % RateWindowSamples;
            if (_rateMaxValues[last] > bytesPerSecond)
                break;

            _rateMaxTail = last;
            _rateMaxCount--;
        }

        _rateMaxValues[_rateMaxTail] = bytesPerSecond;
        _rateMaxSequences[_rateMaxTail] = sequence;
        _rateMaxTail = (_rateMaxTail + 1) % RateWindowSamples;
        _rateMaxCount++;
    }

    private void RecomputeBudget()
    {
        var minRttProbeActive = _minRttProbeUntilTimestamp != 0;
        var budget = ComputeBudget(
            minRttProbeActive,
            _probeUntilTimestamp != 0,
            _minRttSeconds);
        if (minRttProbeActive)
        {
            var postProbeMinRttSeconds = _minRttProbeMinimumSeconds != double.MaxValue
                ? _minRttProbeMinimumSeconds
                : _minRttSeconds;
            Volatile.Write(ref _budgetAfterMinRttProbeBytes,
                ComputeBudget(
                    minRttProbeActive: false,
                    capacityProbeActive: false,
                    minRttSeconds: postProbeMinRttSeconds));
            Volatile.Write(ref _probeBudgetAfterMinRttProbeBytes,
                ComputeBudget(
                    minRttProbeActive: false,
                    capacityProbeActive: true,
                    minRttSeconds: postProbeMinRttSeconds));
        }
        else
        {
            Volatile.Write(ref _budgetAfterMinRttProbeBytes, budget);
            Volatile.Write(ref _probeBudgetAfterMinRttProbeBytes, budget);
        }

        Volatile.Write(ref _budgetBytes, budget);
    }

    private long ComputeBudget(
        bool minRttProbeActive,
        bool capacityProbeActive,
        double minRttSeconds)
    {
        long budget;
        if (_rateMaxCount == 0)
        {
            // Cold start: no drain estimate yet — keep today's semantics (cap) so short-lived
            // producers and tests never see admission throttling.
            budget = _capBytes;
        }
        else
        {
            var horizonSeconds = _hasMinRttSample && !minRttProbeActive
                ? Math.Max(_targetSeconds, RttSafetyMultiplier * minRttSeconds)
                : _targetSeconds;
            var estimatedBytes = _rateMaxValues[_rateMaxHead] * horizonSeconds;
            if (capacityProbeActive && !minRttProbeActive)
                estimatedBytes *= ProbeBudgetMultiplier;

            budget = (long)estimatedBytes;
            budget = Math.Clamp(budget, _floorBytes, _capBytes);
        }

        return budget;
    }
}
