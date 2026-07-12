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
/// <see cref="IsOverBudgetAt"/>/<see cref="RecordAdmissionBlock"/>/
/// <see cref="ObserveWrittenUnackedBytes"/> are cross-thread (producer appenders, terminal batch
/// paths, and parallel connection sends). <see cref="OnAcked"/> and <see cref="SetCap"/> are
/// single-writer — only the owning broker's send loop calls them — so the remaining estimator
/// state needs no synchronization; the computed budget is published with a volatile write.
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

    private const int WindowBucketCount = 20;
    private const double WindowBucketSeconds = 0.100;
    private const double OccupancySafetyMultiplier = 1.5;
    private const double ProbeGrowthThreshold = 1.01;

    /// <summary>
    /// Delivery-rate and written-occupancy maxima cover two seconds in 100ms buckets.
    /// This makes estimator memory independent of response-pass frequency while retaining
    /// enough history to ignore short scheduler and broker stalls.
    /// </summary>
    private static readonly long WindowBucketTicks = Math.Max(1, (long)(WindowBucketSeconds * Stopwatch.Frequency));

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
    private long _minimumRttMicros;
    private long _maxRateBytesPerSecond;
    private long _pendingWrittenUnackedPeakBytes;

    // Single-writer state (owning send loop only; constructor runs before the loop starts).
    private readonly long _floorBytes;
    private readonly double _targetSeconds;
    private long _capBytes;
    private readonly double[] _rateBucketMaxValues = new double[WindowBucketCount];
    private readonly long[] _occupancyBucketMaxValues = new long[WindowBucketCount];
    private long _currentWindowBucket = long.MinValue;
    private double _windowMaxRate;
    private long _windowMaxOccupancyBytes;
    private long _lastAckTimestamp;
    private double _minRttSeconds;
    private double _minRttProbeMinimumSeconds;
    private bool _hasMinRttSample;
    private long _minRttTimestamp;
    // Written only by the send loop; acquire-read by appenders after _budgetBytes publishes
    // the matching precomputed post-probe budgets.
    private long _minRttProbeUntilTimestamp;
    private long _nextProbeTimestamp;
    private long _capacityProbeStartTimestamp;
    private long _capacityProbeDeadlineTimestamp;
    private double _capacityProbeBaselineRate;
    private bool _capacityProbeActive;

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

    internal long MinimumRttMicros => Volatile.Read(ref _minimumRttMicros);

    internal long MaxRateBytesPerSecond => Volatile.Read(ref _maxRateBytesPerSecond);

    // Volatile.Read (a plain acquire load) rather than Interlocked.Read: this runs per
    // message from every appender thread, and a locked read would take the cache line
    // exclusive on every call, turning a read-mostly gate into a contention point.
    // During a capacity probe, use the lower normal budget as the fast threshold. The
    // deadline-aware gate then admits against the larger probe budget while it is current,
    // but expired probes cannot bypass that gate merely because _budgetBytes is stale.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsOverBudget()
        => Volatile.Read(ref _unackedBytes) >= Math.Min(
            Volatile.Read(ref _budgetBytes),
            Volatile.Read(ref _budgetAfterMinRttProbeBytes));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsOverBudgetAt(long nowTicks)
    {
        var budget = Volatile.Read(ref _budgetBytes);
        var capacityProbeActive = Volatile.Read(ref _capacityProbeActive);
        var capacityProbeDeadline = Volatile.Read(ref _capacityProbeDeadlineTimestamp);
        var capacityProbeCurrent = capacityProbeActive
            && capacityProbeDeadline != 0
            && nowTicks < capacityProbeDeadline;
        var minRttProbeUntilTimestamp = Volatile.Read(ref _minRttProbeUntilTimestamp);
        if (minRttProbeUntilTimestamp != 0 && nowTicks >= minRttProbeUntilTimestamp)
        {
            budget = capacityProbeCurrent
                ? Volatile.Read(ref _probeBudgetAfterMinRttProbeBytes)
                : Volatile.Read(ref _budgetAfterMinRttProbeBytes);
        }
        else if (minRttProbeUntilTimestamp == 0
            && capacityProbeActive
            && !capacityProbeCurrent)
        {
            budget = Volatile.Read(ref _budgetAfterMinRttProbeBytes);
        }

        return Volatile.Read(ref _unackedBytes) >= budget;
    }

    /// <summary>
    /// Returns the delay until an active minimum-RTT probe changes the admission budget,
    /// or <see cref="Timeout.Infinite"/> when no time-based recheck is needed.
    /// </summary>
    internal int GetAdmissionRecheckDelayMilliseconds(long nowTicks)
    {
        var recheckTimestamp = long.MaxValue;
        var minRttProbeUntilTimestamp = Volatile.Read(ref _minRttProbeUntilTimestamp);
        if (minRttProbeUntilTimestamp > nowTicks)
            recheckTimestamp = minRttProbeUntilTimestamp;

        if (Volatile.Read(ref _capacityProbeActive))
        {
            var capacityProbeDeadline = Volatile.Read(ref _capacityProbeDeadlineTimestamp);
            if (capacityProbeDeadline > nowTicks)
                recheckTimestamp = Math.Min(recheckTimestamp, capacityProbeDeadline);
        }

        if (recheckTimestamp == long.MaxValue)
            return Timeout.Infinite;

        var remainingTicks = recheckTimestamp - nowTicks;
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
    /// Records broker bytes written and awaiting responses. Parallel connection sends may
    /// call this concurrently; the owning send loop consumes the peak on its next ack pass.
    /// </summary>
    public void ObserveWrittenUnackedBytes(long bytes)
    {
        var observed = Volatile.Read(ref _pendingWrittenUnackedPeakBytes);
        while (bytes > observed)
        {
            var prior = Interlocked.CompareExchange(ref _pendingWrittenUnackedPeakBytes, bytes, observed);
            if (prior == observed)
                return;

            observed = prior;
        }
    }

    /// <summary>
    /// Updates the ceiling (routing-width-dependent) and republishes the budget.
    /// Called by the owning send loop at construction and on connection scale events.
    /// </summary>
    public void SetCap(long capBytes, long nowTicks)
    {
        _capBytes = Math.Max(_floorBytes, capBytes);
        CompleteExpiredMinRttProbe(nowTicks);
        if (_capacityProbeActive && nowTicks >= _capacityProbeDeadlineTimestamp)
            DeactivateCapacityProbe();
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
        Volatile.Write(ref _minimumRttMicros, (long)(_minRttSeconds * 1_000_000));

        var requestStartTimestamp = nowTicks - rttTicks;
        var rateIntervalStartTimestamp = _lastAckTimestamp == 0
            ? requestStartTimestamp
            : Math.Max(_lastAckTimestamp, requestStartTimestamp);
        _lastAckTimestamp = nowTicks;

        AdvanceWindow(nowTicks);
        var intervalTicks = nowTicks - rateIntervalStartTimestamp;
        var bytesPerSecond = intervalTicks > 0
            ? ackedBytes * (double)Stopwatch.Frequency / intervalTicks
            : 0;
        if (bytesPerSecond > 0)
        {
            AddRateSample(bytesPerSecond);
            Volatile.Write(ref _maxRateBytesPerSecond, (long)_windowMaxRate);
        }

        var writtenUnackedPeak = Interlocked.Exchange(ref _pendingWrittenUnackedPeakBytes, 0);
        if (writtenUnackedPeak > 0)
            AddOccupancySample(writtenUnackedPeak);

        UpdateCapacityProbe(bytesPerSecond, requestStartTimestamp, rttTicks, nowTicks);

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

    private void AdvanceWindow(long nowTicks)
    {
        var bucket = nowTicks / WindowBucketTicks;
        if (_currentWindowBucket == long.MinValue)
        {
            _currentWindowBucket = bucket;
            return;
        }

        if (bucket <= _currentWindowBucket)
            return;

        var elapsed = bucket - _currentWindowBucket;
        if (elapsed >= WindowBucketCount)
        {
            Array.Clear(_rateBucketMaxValues);
            Array.Clear(_occupancyBucketMaxValues);
            _windowMaxRate = 0;
            _windowMaxOccupancyBytes = 0;
        }
        else
        {
            var recomputeRate = false;
            var recomputeOccupancy = false;
            for (var i = 1L; i <= elapsed; i++)
            {
                var slot = (int)((_currentWindowBucket + i) % WindowBucketCount);
                recomputeRate |= _windowMaxRate > 0 && _rateBucketMaxValues[slot] >= _windowMaxRate;
                recomputeOccupancy |= _windowMaxOccupancyBytes > 0
                    && _occupancyBucketMaxValues[slot] >= _windowMaxOccupancyBytes;
                _rateBucketMaxValues[slot] = 0;
                _occupancyBucketMaxValues[slot] = 0;
            }

            if (recomputeRate)
                _windowMaxRate = Max(_rateBucketMaxValues);
            if (recomputeOccupancy)
                _windowMaxOccupancyBytes = Max(_occupancyBucketMaxValues);
        }

        _currentWindowBucket = bucket;
    }

    private void AddRateSample(double bytesPerSecond)
    {
        var slot = (int)(_currentWindowBucket % WindowBucketCount);
        _rateBucketMaxValues[slot] = Math.Max(_rateBucketMaxValues[slot], bytesPerSecond);
        _windowMaxRate = Math.Max(_windowMaxRate, bytesPerSecond);
    }

    private void AddOccupancySample(long bytes)
    {
        var slot = (int)(_currentWindowBucket % WindowBucketCount);
        _occupancyBucketMaxValues[slot] = Math.Max(_occupancyBucketMaxValues[slot], bytes);
        _windowMaxOccupancyBytes = Math.Max(_windowMaxOccupancyBytes, bytes);
    }

    private void UpdateCapacityProbe(
        double bytesPerSecond,
        long requestStartTimestamp,
        long rttTicks,
        long nowTicks)
    {
        var probeIntervalTicks = ProbeIntervalRtts * rttTicks;
        if (_nextProbeTimestamp == 0)
            _nextProbeTimestamp = nowTicks + probeIntervalTicks;

        if (_capacityProbeActive)
        {
            if (nowTicks >= _capacityProbeDeadlineTimestamp)
            {
                DeactivateCapacityProbe();
                _nextProbeTimestamp = nowTicks + probeIntervalTicks;
                return;
            }

            // A response can confirm the probe only when its oldest request was created
            // after the larger budget was published. If observed rate grows, retain the
            // probe and require another wholly-probed sample at the new level.
            if (requestStartTimestamp < _capacityProbeStartTimestamp)
                return;

            if (bytesPerSecond > _capacityProbeBaselineRate * ProbeGrowthThreshold)
            {
                _capacityProbeBaselineRate = bytesPerSecond;
                _capacityProbeStartTimestamp = nowTicks;
                Volatile.Write(ref _capacityProbeDeadlineTimestamp, nowTicks + probeIntervalTicks);
            }
            else
            {
                DeactivateCapacityProbe();
                _nextProbeTimestamp = nowTicks + probeIntervalTicks;
            }

            return;
        }

        if (nowTicks < _nextProbeTimestamp)
            return;

        if (nowTicks - _nextProbeTimestamp < probeIntervalTicks)
        {
            _capacityProbeStartTimestamp = nowTicks;
            _capacityProbeBaselineRate = _windowMaxRate;
            Volatile.Write(ref _capacityProbeDeadlineTimestamp, nowTicks + probeIntervalTicks);
            Volatile.Write(ref _capacityProbeActive, true);
        }

        // A schedule missed by a full interval represents producer idle time, not an
        // active-pipeline probe opportunity. Re-arm instead of probing on resume.
        _nextProbeTimestamp = nowTicks + probeIntervalTicks;
    }

    private void DeactivateCapacityProbe()
    {
        Volatile.Write(ref _capacityProbeActive, false);
        Volatile.Write(ref _capacityProbeDeadlineTimestamp, 0);
    }

    private static double Max(double[] values)
    {
        var maximum = 0.0;
        for (var i = 0; i < values.Length; i++)
            maximum = Math.Max(maximum, values[i]);

        return maximum;
    }

    private static long Max(long[] values)
    {
        var maximum = 0L;
        for (var i = 0; i < values.Length; i++)
            maximum = Math.Max(maximum, values[i]);

        return maximum;
    }

    private void RecomputeBudget()
    {
        var minRttProbeActive = _minRttProbeUntilTimestamp != 0;
        var postProbeMinRttSeconds = _minRttSeconds;
        if (minRttProbeActive && _minRttProbeMinimumSeconds != double.MaxValue)
            postProbeMinRttSeconds = _minRttProbeMinimumSeconds;
        var normalBudget = ComputeBudget(
            minRttProbeActive: false,
            capacityProbeActive: false,
            postProbeMinRttSeconds);
        var probeBudget = ComputeBudget(
            minRttProbeActive: false,
            capacityProbeActive: true,
            postProbeMinRttSeconds);
        Volatile.Write(ref _budgetAfterMinRttProbeBytes, normalBudget);
        Volatile.Write(ref _probeBudgetAfterMinRttProbeBytes, probeBudget);

        long budget;
        if (minRttProbeActive)
        {
            budget = ComputeBudget(
                minRttProbeActive: true,
                capacityProbeActive: false,
                _minRttSeconds);
        }
        else
        {
            budget = _capacityProbeActive ? probeBudget : normalBudget;
        }

        Volatile.Write(ref _budgetBytes, budget);
    }

    private long ComputeBudget(
        bool minRttProbeActive,
        bool capacityProbeActive,
        double minRttSeconds)
    {
        long budget;
        if (_windowMaxRate == 0)
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
            var estimatedBytes = _windowMaxRate * horizonSeconds;
            if (capacityProbeActive && !minRttProbeActive)
                estimatedBytes *= ProbeBudgetMultiplier;

            budget = (long)estimatedBytes;
        }

        if (!minRttProbeActive)
        {
            var occupancyFloor = (long)(_windowMaxOccupancyBytes * OccupancySafetyMultiplier);
            budget = Math.Max(budget, occupancyFloor);
        }

        budget = Math.Clamp(budget, _floorBytes, _capBytes);

        return budget;
    }
}
