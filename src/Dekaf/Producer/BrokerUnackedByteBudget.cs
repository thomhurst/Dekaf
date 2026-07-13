using System.Diagnostics;
using System.Numerics;
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
/// Drain rate is measured per acknowledged request, BBR-style: each request snapshots the
/// cumulative delivered-bytes counter at send time, and its rate sample is the delivered
/// delta over the larger of the request's own sojourn and the interval since the delivery
/// clock at send (sojourn only for app-limited sends, where the pipe was empty and the
/// preceding interval is idle time). The denominator is therefore never smaller than one
/// real round trip, so response-pass timing (hot polling, clustered completions,
/// processing stalls) cannot manufacture inflated samples.
/// </para>
/// <para>
/// Ownership contract: <see cref="Charge"/>/<see cref="Release"/>/<see cref="IsOverBudget"/>/
/// <see cref="IsOverBudgetAt"/>/<see cref="RecordAdmissionBlock"/>/
/// <see cref="ObserveWrittenUnackedBytes"/>/<see cref="SnapshotDelivery"/> are cross-thread
/// (producer appenders, terminal batch paths, and parallel connection sends).
/// <see cref="OnAcked"/> and <see cref="SetCap"/> are single-writer — only the owning broker's
/// send loop calls them — so the remaining estimator state needs no synchronization; the
/// computed budget is published with a volatile write. <see cref="SnapshotDelivery"/> performs
/// two independent volatile reads racing the single writer; both fields are monotonic, so a
/// torn pair only skews one rate sample marginally and the windowed max filters it.
/// </para>
/// </summary>
internal sealed class BrokerUnackedByteBudget
{
    /// <summary>
    /// Per-send token minted by <see cref="SnapshotDelivery"/>: the delivery clock (cumulative
    /// delivered bytes and the timestamp of the most recent delivery), the pre-write send
    /// timestamp, and whether the broker pipe was empty at send. Carried opaquely on the
    /// in-flight request and redeemed by <see cref="OnAcked"/> when the response arrives, so
    /// the values that must be captured together cannot drift apart at call sites.
    /// </summary>
    internal readonly record struct DeliverySnapshot(
        long DeliveredBytes,
        long DeliveredTimestamp,
        long SendTimestamp,
        bool AppLimited,
        long OldestBatchTimestamp);

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
    private const double DeliveryLatencyEwmaWeight = 0.125;
    private const double MinimumLatencyBudgetScale = 0.10;
    private const double MinimumLatencyAdjustmentFactor = 0.75;
    private const double MaximumLatencyAdjustmentFactor = 1.05;

    /// <summary>
    /// Bounds the occupancy floor to this multiple of the rate-derived budget once a drain
    /// estimate exists. Occupancy is itself admission-bounded by the budget, so an uncapped
    /// <c>1.5 × occupancy</c> floor is a positive feedback loop that ratchets the budget to
    /// its cap. With the bound, the iteration's fixed point is this multiple of the rate
    /// budget (1.5 × target = 15 ms standing latency at the 10 ms default) instead of the cap.
    /// Genuine wire demand beyond the estimate recovers via capacity probes within 8 RTTs.
    /// </summary>
    private const double OccupancyFloorHeadroom = 1.5;

    /// <summary>
    /// Log2 histogram buckets for per-request diagnostics. Request sizes shift by
    /// <see cref="RequestSizeHistogramShift"/> (bucket 0 = under 512 B, bucket 15 = 8 MB and
    /// above); RTT buckets are unshifted microseconds (bucket 0 = under 2 µs, bucket 15 =
    /// 32 ms and above).
    /// </summary>
    private const int HistogramBucketCount = 16;
    private const int RequestSizeHistogramShift = 8;

    /// <summary>
    /// Delivery-rate and written-occupancy maxima cover two seconds in 100ms buckets.
    /// This makes estimator memory independent of response-pass frequency while retaining
    /// enough history to ignore short scheduler and broker stalls.
    /// </summary>
    private static readonly long WindowBucketTicks = Math.Max(1, (long)(WindowBucketSeconds * Stopwatch.Frequency));
    private static readonly long LatencyControlIntervalTicks = Math.Max(1, Stopwatch.Frequency / 10);

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
    // BBR delivery clock: cumulative acked bytes ("delivered") and the timestamp of the most
    // recent delivery ("delivered_mstamp"). Written only by OnAcked (single writer); snapshot
    // via volatile reads by parallel connection sends at send time.
    private long _totalDeliveredBytes;
    private long _lastDeliveredTimestamp;

    // Single-writer state (owning send loop only; constructor runs before the loop starts).
    private readonly long _floorBytes;
    private readonly double _targetSeconds;
    private long _capBytes;
    private readonly double[] _rateBucketMaxValues = new double[WindowBucketCount];
    private readonly long[] _occupancyBucketMaxValues = new long[WindowBucketCount];
    // Per-request diagnostics: log2 histograms of acked request sizes and RTTs. Incremented
    // only by the owning send loop; snapshot copies use per-element volatile reads, so
    // readers may observe slightly stale counts (acceptable for diagnostics).
    private readonly long[] _requestSizeLog2Histogram = new long[HistogramBucketCount];
    private readonly long[] _requestRttMicrosLog2Histogram = new long[HistogramBucketCount];
    private long _currentWindowBucket = long.MinValue;
    private double _windowMaxRate;
    private long _windowMaxOccupancyBytes;
    private double _minRttSeconds;
    private double _minRttProbeMinimumSeconds;
    private bool _hasMinRttSample;
    private long _minRttTimestamp;
    // Written only by the send loop; acquire-read by appenders after _budgetBytes publishes
    // the matching precomputed post-probe budgets.
    private long _minRttProbeUntilTimestamp;
    private long _nextProbeTimestamp;
    private long _capacityProbeEvaluationTimestamp;
    private long _capacityProbeDeadlineTimestamp;
    private double _capacityProbeBaselineRate;
    private bool _capacityProbeActive;
    private double _deliveryLatencyEwmaSeconds;
    private double _latencyBudgetScale = 1.0;
    private long _nextLatencyControlTimestamp;

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

    internal long DeliveryLatencyEwmaMicros =>
        (long)(Volatile.Read(ref _deliveryLatencyEwmaSeconds) * 1_000_000);

    internal double LatencyBudgetScale => Volatile.Read(ref _latencyBudgetScale);

    /// <summary>
    /// Cross-thread: parallel connection sends mint the per-request delivery token immediately
    /// before the socket write (so <paramref name="sendTimestamp"/> can never postdate the
    /// response's arrival), feeding it back to <see cref="OnAcked"/> on the response. Two
    /// independent volatile reads — tearing is benign because both fields are monotonic and a
    /// skewed pair only shifts one rate sample marginally.
    /// </summary>
    /// <param name="sendTimestamp">Stopwatch timestamp taken just before the socket write.</param>
    /// <param name="appLimited">True when the broker pipe was empty at send time.</param>
    /// <param name="oldestBatchTimestamp">Seal timestamp of the oldest batch in the request,
    /// or zero when delivery-age feedback is unavailable.</param>
    internal DeliverySnapshot SnapshotDelivery(
        long sendTimestamp,
        bool appLimited,
        long oldestBatchTimestamp = 0)
        => new(
            Volatile.Read(ref _totalDeliveredBytes),
            Volatile.Read(ref _lastDeliveredTimestamp),
            sendTimestamp,
            appLimited,
            oldestBatchTimestamp);

    /// <summary>Diagnostics: log2 histogram of acked request payload sizes (see
    /// <see cref="HistogramBucketCount"/> for bucket semantics).</summary>
    internal long[] CopyRequestSizeHistogram() => CopyHistogram(_requestSizeLog2Histogram);

    /// <summary>Diagnostics: log2 histogram of acked request round-trip times in microseconds.</summary>
    internal long[] CopyRequestRttMicrosHistogram() => CopyHistogram(_requestRttMicrosLog2Histogram);

    private static long[] CopyHistogram(long[] source)
    {
        var copy = new long[source.Length];
        for (var i = 0; i < source.Length; i++)
            copy[i] = Volatile.Read(ref source[i]);

        return copy;
    }

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
    /// Feeds one successfully acknowledged request into the drain estimate, then republishes
    /// the budget. Called only from the owning send loop, once per acked request. O(1),
    /// allocation-free.
    /// </summary>
    /// <param name="ackedBytes">Encoded payload bytes of the acknowledged request.</param>
    /// <param name="sendSnapshot">Token minted via <see cref="SnapshotDelivery"/> just before
    /// the request's socket write, so <c>nowTicks - SendTimestamp</c> is never smaller than
    /// the true round trip. For app-limited sends the interval since the last delivery is
    /// idle time, not drain time, and the sample falls back to the request's own sojourn.</param>
    /// <param name="nowTicks">Response observation timestamp.</param>
    public void OnAcked(long ackedBytes, DeliverySnapshot sendSnapshot, long nowTicks)
    {
        var rttTicks = nowTicks - sendSnapshot.SendTimestamp;
        if (ackedBytes <= 0 || rttTicks <= 0)
            return;

        var rttSeconds = (double)rttTicks / Stopwatch.Frequency;
        UpdateMinRtt(rttSeconds, nowTicks);
        Volatile.Write(ref _minimumRttMicros, (long)(_minRttSeconds * 1_000_000));
        UpdateDeliveryLatency(sendSnapshot.OldestBatchTimestamp, nowTicks);

        RecordRequestHistograms(ackedBytes, rttSeconds);

        // BBR delivery-rate sample: delivered-counter delta over the larger of the request's
        // own sojourn and the ack-axis interval (time since the delivery clock at send). The
        // sojourn bounds the denominator below by one real round trip, so clustered response
        // passes cannot inflate the sample; the ack axis spreads a processing-stall backlog
        // over the wall time the deliveries actually took.
        var totalDelivered = _totalDeliveredBytes + ackedBytes;
        Volatile.Write(ref _totalDeliveredBytes, totalDelivered);
        var deliveredDelta = totalDelivered - sendSnapshot.DeliveredBytes;

        var intervalTicks = rttTicks;
        if (!sendSnapshot.AppLimited && sendSnapshot.DeliveredTimestamp != 0)
            intervalTicks = Math.Max(intervalTicks, nowTicks - sendSnapshot.DeliveredTimestamp);
        Volatile.Write(ref _lastDeliveredTimestamp, nowTicks);

        AdvanceWindow(nowTicks);
        var bytesPerSecond = deliveredDelta * (double)Stopwatch.Frequency / intervalTicks;
        if (bytesPerSecond > 0)
        {
            AddRateSample(bytesPerSecond);
            Volatile.Write(ref _maxRateBytesPerSecond, (long)_windowMaxRate);
        }

        var writtenUnackedPeak = Interlocked.Exchange(ref _pendingWrittenUnackedPeakBytes, 0);
        if (writtenUnackedPeak > 0)
            AddOccupancySample(writtenUnackedPeak);

        UpdateCapacityProbe(bytesPerSecond, sendSnapshot.SendTimestamp, rttTicks, nowTicks);

        RecomputeBudget();
    }

    private void UpdateDeliveryLatency(long oldestBatchTimestamp, long nowTicks)
    {
        if (oldestBatchTimestamp <= 0 || oldestBatchTimestamp >= nowTicks)
            return;

        var sampleSeconds = (double)(nowTicks - oldestBatchTimestamp) / Stopwatch.Frequency;
        var latencyEstimate = _deliveryLatencyEwmaSeconds == 0
            ? sampleSeconds
            : _deliveryLatencyEwmaSeconds
                + DeliveryLatencyEwmaWeight * (sampleSeconds - _deliveryLatencyEwmaSeconds);
        Volatile.Write(ref _deliveryLatencyEwmaSeconds, latencyEstimate);

        if (_nextLatencyControlTimestamp == 0)
        {
            _nextLatencyControlTimestamp = nowTicks + LatencyControlIntervalTicks;
            return;
        }

        if (nowTicks < _nextLatencyControlTimestamp)
            return;

        var targetRatio = _targetSeconds / latencyEstimate;
        var adjustment = targetRatio < 1
            ? Math.Max(MinimumLatencyAdjustmentFactor, targetRatio)
            : Math.Min(MaximumLatencyAdjustmentFactor, targetRatio);
        Volatile.Write(ref _latencyBudgetScale, Math.Clamp(
            _latencyBudgetScale * adjustment,
            MinimumLatencyBudgetScale,
            1.0));
        _nextLatencyControlTimestamp = nowTicks + LatencyControlIntervalTicks;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void RecordRequestHistograms(long ackedBytes, double rttSeconds)
    {
        var sizeBucket = Math.Clamp(
            BitOperations.Log2((ulong)ackedBytes | 1) - RequestSizeHistogramShift,
            0,
            HistogramBucketCount - 1);
        _requestSizeLog2Histogram[sizeBucket]++;

        var rttMicros = (ulong)(rttSeconds * 1_000_000);
        var rttBucket = Math.Min(BitOperations.Log2(rttMicros | 1), HistogramBucketCount - 1);
        _requestRttMicrosLog2Histogram[rttBucket]++;
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
        long sendTimestamp,
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

            // Admission released by the larger budget must traverse batch sealing,
            // serialization, and one network round trip before a response can measure the
            // probed pipe. A request sent earlier may have been queued under the normal
            // budget; treating it as proof of no gain closes every probe before it fills.
            if (sendTimestamp < _capacityProbeEvaluationTimestamp)
                return;

            if (bytesPerSecond > _capacityProbeBaselineRate * ProbeGrowthThreshold)
            {
                _capacityProbeBaselineRate = bytesPerSecond;
                _capacityProbeEvaluationTimestamp = nowTicks + rttTicks;
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
            _capacityProbeEvaluationTimestamp = nowTicks + rttTicks;
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
        _capacityProbeEvaluationTimestamp = 0;
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
        double rateBudgetBytes = 0;
        if (_windowMaxRate == 0)
        {
            // Cold start: no drain estimate yet — keep today's semantics (cap) so short-lived
            // producers and tests never see admission throttling.
            budget = _capBytes;
        }
        else
        {
            var latencyGovernedTargetSeconds = _targetSeconds * _latencyBudgetScale;
            var horizonSeconds = _hasMinRttSample && !minRttProbeActive
                ? Math.Max(latencyGovernedTargetSeconds, RttSafetyMultiplier * minRttSeconds)
                : latencyGovernedTargetSeconds;
            rateBudgetBytes = _windowMaxRate * horizonSeconds;
            var estimatedBytes = rateBudgetBytes;
            if (capacityProbeActive && !minRttProbeActive)
                estimatedBytes *= ProbeBudgetMultiplier;

            budget = (long)estimatedBytes;
        }

        if (!minRttProbeActive)
        {
            var occupancyFloor = (long)(_windowMaxOccupancyBytes * OccupancySafetyMultiplier);
            if (rateBudgetBytes > 0)
            {
                // The floor exists so a lagging estimator cannot strangle a pipe that is
                // demonstrably fuller (#1911). Once a drain estimate exists, genuine extra
                // demand shows up in the delivered-rate samples within one round trip, so the
                // occupancy proxy may exceed the rate budget by a bounded headroom only —
                // otherwise occupancy (itself admission-bounded by the budget) ratchets the
                // budget to its cap and the latency target is never enforced.
                occupancyFloor = Math.Min(occupancyFloor, (long)(OccupancyFloorHeadroom * rateBudgetBytes));
            }

            budget = Math.Max(budget, occupancyFloor);
        }

        budget = Math.Clamp(budget, _floorBytes, _capBytes);

        return budget;
    }
}
