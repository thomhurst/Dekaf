using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace Dekaf.Producer;

internal enum BrokerBudgetProbeType
{
    MinimumRtt,
    Capacity
}

internal enum BrokerBudgetProbeOutcome
{
    Started,
    Succeeded,
    Failed
}

internal readonly record struct BrokerBudgetProbeEvent(
    BrokerBudgetProbeType ProbeType,
    BrokerBudgetProbeOutcome Outcome,
    DateTimeOffset OccurredAtUtc,
    long DurationMilliseconds,
    long BudgetBytes,
    long UnackedBytes);

/// <summary>
/// Bounds the bytes a single broker may hold unacknowledged (sealed but not yet acked)
/// so that producer queueing latency stays near <see cref="ProducerOptions.DeliveryLatencyTargetMs"/>
/// instead of growing to the full <see cref="ProducerOptions.BufferMemory"/> reservoir.
/// Under open-loop saturation, append-to-ack latency equals standing unacked bytes divided
/// by the broker's drain rate; capping the standing bytes to
/// <c>target × measured drain rate</c> caps the latency. The RTT safety horizon uses a
/// periodically refreshed minimum RTT or the loaded serving RTT, capped by the latency target
/// but never below one measured BDP so replicated service time still keeps the pipe full.
/// Raw round trips include the drain time of bytes the budget
/// itself admitted ahead of the request, so RTT samples are queue-corrected at intake (raw
/// RTT minus unacked-at-send over the measured max rate) before feeding the minimum and
/// serving estimates, and the serving EWMA is additionally clamped to a bounded multiple of
/// the minimum: an uncorrected floor of <c>1.5 × servingRtt</c> has loop gain above one —
/// each budget raise lengthens the RTT that justifies the next raise until the budget rides
/// its cap (the 3-broker acks=all latency ratchet, #2009).
/// <para>
/// Sustainable drain rate is measured over a delivery epoch. An app-limited send starts the
/// epoch; loaded sends retain its delivered-byte and first-send anchors across acknowledgements
/// for a rolling interval bounded at 250ms. The sample is cumulative delivered bytes over the
/// full epoch elapsed time, bounded below by the request sojourn and delivery clock. Samples
/// shorter than 100ms are ignored because host scheduling and burst quantization can dominate
/// them. A short truly app-limited sample may seed an empty estimator, but cannot replace an
/// established sustainable rate. A separate per-request rate remains available to the capacity
/// probe, which needs each probe admission's
/// immediate response. These axes prevent serialized send/ack cycles, hot polling, clustered
/// completions, and processing stalls from manufacturing inflated sustainable-rate samples.
/// </para>
/// <para>
/// Ownership contract: <see cref="Charge"/>/<see cref="Release"/>/<see cref="IsOverBudget"/>/
/// <see cref="IsOverBudgetAt"/>/<see cref="RecordAdmissionBlock"/>/
/// <see cref="ObserveWrittenUnackedBytes"/>/<see cref="SnapshotDelivery"/> are cross-thread
/// (producer appenders, terminal batch paths, and parallel connection sends).
/// <see cref="OnAcked"/>, <see cref="CompleteAckedPass"/>, and <see cref="SetCap"/> are
/// single-writer — only the owning broker's send loop calls them — so the remaining estimator
/// state needs no synchronization; the computed budget is published with a volatile write.
/// <see cref="SnapshotDelivery"/> serializes delivery-epoch publication with a CAS while the
/// most-recent-delivery timestamp remains an independent monotonic read. Skew between those
/// clocks only widens one sample marginally.
/// </para>
/// </summary>
internal sealed class BrokerUnackedByteBudget
{
    /// <summary>
    /// Per-send token minted by <see cref="SnapshotDelivery"/>: the current delivery clock,
    /// the loaded delivery epoch's delivered-byte and first-send anchors, the pre-write send
    /// timestamp, whether the broker pipe was empty, and whether the rate epoch was app-limited
    /// at send. Carried opaquely on the in-flight request and redeemed by <see cref="OnAcked"/>
    /// when the response arrives, so values captured together cannot drift apart at call sites.
    /// </summary>
    internal readonly record struct DeliverySnapshot(
        long DeliveredBytes,
        long DeliveredTimestamp,
        long DeliveryEpochDeliveredBytes,
        long DeliveryEpochFirstSendTimestamp,
        long SendTimestamp,
        bool AppLimited,
        bool RateAppLimited,
        long OldestBatchTimestamp,
        long UnackedBytesAtSend);

    /// <summary>
    /// Budget ceiling in batches per connection. Sized so a ~1 GB/s drain with ~30 ms ack
    /// round-trips keeps the pipe full at the 1 MB default batch size (~32 MB BDP). Shared
    /// with <see cref="BrokerSender"/>'s written-unacked pipeline ceiling so the two bounds
    /// stay consistent.
    /// </summary>
    internal const int CapBatchMultiplier = 32;

    private const int WindowBucketCount = 20;
    private const double WindowBucketSeconds = 0.100;
    // Retain demonstrated loaded capacity across transient GC/scheduler stalls. Recovery probes
    // provide the upward path, but a short dip must not become the admission ceiling within one
    // minute and create a self-reinforcing rate/budget decay loop.
    private const double LoadedRateHalfLifeSeconds = 600.0;
    private const double OccupancySafetyMultiplier = 1.5;
    private const double ProbeGrowthThreshold = 1.01;
    private const double DeliveryLatencyEwmaWeight = 0.125;
    private const double RequestSizeEwmaWeight = 0.125;
    private const double ServingRttEwmaWeight = 0.125;
    private const double MinimumLatencyBudgetScale = 0.10;
    private const double MinimumLatencyAdjustmentFactor = 0.75;
    private const double MaximumLatencyAdjustmentFactor = 1.05;
    private const double MaximumBudgetDecayPerSecond = 0.10;

    /// <summary>Minimum number of average encoded requests retained under proven demand.
    /// A byte BDP smaller than one or two request quanta serializes the acknowledgement clock,
    /// making its reduced delivery rate self-confirming. Four request quanta mirror BBR's
    /// minimum flight: enough to tolerate request and scheduler granularity without filling
    /// the delivery-latency target with queueing delay.</summary>
    private const int MinimumPipelineRequestQuanta = 4;

    /// <summary>
    /// The scale shrinks only while written-unacked occupancy demonstrates at least this
    /// fraction of the budget on the wire. Seal-to-send backlog with an underfilled wire
    /// means the send loop, not broker admission, is the bottleneck — shrinking admission
    /// there cannot reduce the latency, it only starves the sender below its capacity
    /// (the 1-broker collapse mode of #2008).
    /// </summary>
    private const double ShrinkOccupancyFraction = 0.5;

    /// <summary>
    /// Log2 histogram buckets for per-request diagnostics. Request sizes shift by
    /// <see cref="RequestSizeHistogramShift"/> (bucket 0 = under 512 B, bucket 15 = 8 MB and
    /// above); RTT buckets are unshifted microseconds (bucket 0 = under 2 µs, bucket 15 =
    /// 32 ms and above).
    /// </summary>
    private const int HistogramBucketCount = 16;
    internal const int AdmissionBlockHistogramBucketCount = 24;
    private const int MaxMinRttProbeDiagnosticEvents = 512;
    private const int MaxCapacityProbeDiagnosticEvents = 4_096;
    private const int RequestSizeHistogramShift = 8;

    /// <summary>
    /// Delivery-rate and written-occupancy maxima cover two seconds in 100ms buckets.
    /// This makes estimator memory independent of response-pass frequency while retaining
    /// enough history to ignore short scheduler and broker stalls.
    /// </summary>
    private static readonly long WindowBucketTicks = Math.Max(1, (long)(WindowBucketSeconds * Stopwatch.Frequency));
    // A momentarily empty pending-response count is normal between depth-one requests and
    // concurrent send waves. Treat it as genuine application idle only after one full fast
    // estimator window without a delivery.
    private static readonly long WindowDurationTicks = WindowBucketCount * WindowBucketTicks;
    private static readonly double LoadedRateDecayPerBucket =
        Math.Pow(0.5, WindowBucketSeconds / LoadedRateHalfLifeSeconds);
    private static readonly long LatencyControlIntervalTicks = Math.Max(1, Stopwatch.Frequency / 10);

    /// <summary>How long an observed base RTT remains valid before it is refreshed.</summary>
    private static readonly long MinRttWindowTicks = 10 * Stopwatch.Frequency;

    /// <summary>
    /// Minimum queue-drain interval used to refresh base RTT without standing queue
    /// delay. Long-base-RTT links probe for at least one observed round-trip.
    /// </summary>
    private static readonly long MinRttProbeDurationTicks = Stopwatch.Frequency / 20;

    /// <summary>
    /// Maximum queue-drain interval. An RTT outlier must not disable the retained
    /// RTT safety floor for its full (potentially multi-second) duration.
    /// </summary>
    private static readonly long MaxMinRttProbeDurationTicks = Stopwatch.Frequency / 4;

    /// <summary>Preferred multiple of the measured bandwidth-delay product (rate × RTT).
    /// The latency target caps this safety margin, but the horizon never drops below one
    /// base-RTT BDP; otherwise high-latency links underfill the pipe, lower the measured rate,
    /// and ratchet the budget down to the floor.</summary>
    private const double RttSafetyMultiplier = 1.5;
    private const double MinRttProbeSafetyMultiplier = 1.0;

    /// <summary>Minimum neutral safety horizon on low-latency links. Sub-millisecond
    /// RTT samples describe an empty pipe but do not leave enough byte admission depth to
    /// start a saturated sender's request pipeline. The request-quantum floor supplies the
    /// remaining granularity only under proven demand; minimum-RTT probes bypass both.</summary>
    private const double MinimumNormalHorizonSeconds = 0.001;

    /// <summary>Ceiling on the loaded serving RTT as a multiple of the queue-drained minimum
    /// RTT. The serving EWMA includes queueing delay the budget itself admitted, so letting
    /// it grow the RTT floor unboundedly is a positive-feedback loop with gain above one;
    /// clamping bounds the floor at <c>RttSafetyMultiplier × this × minRtt</c> (3 × BDP) —
    /// enough slack for replication service-time variance without the ratchet.</summary>
    private const double ServingRttClampMultiplier = 2.0;

    /// <summary>Lower bound on queue-corrected RTT samples as a fraction of the raw round
    /// trip, so an overestimated drain rate cannot collapse the corrected service estimate
    /// (and with it the RTT floor) toward zero.</summary>
    private const double MinCorrectedRttFraction = 0.25;

    /// <summary>Maximum upward movement accepted from one minimum-RTT refresh. A drain
    /// probe can remain partially queued across parallel connections; bounding one window
    /// prevents that transient from multiplying the BDP floor, while repeated refreshes
    /// still converge when the path's base RTT has genuinely increased.</summary>
    private const double MinRttRefreshGrowthFactor = 1.25;

    private const int ProbeIntervalRtts = 8;
    // A capacity probe needs one RTT for the enlarged budget to reach the wire,
    // then three wholly-probed RTTs to average before accepting or rejecting growth.
    private const int ProbeEvaluationRtts = 3;
    private const double ProbeBudgetMultiplier = 1.25;
    /// <summary>Only probe when standing demand fills at least this fraction of the gate;
    /// enlarging a more-open gate cannot reveal additional delivery capacity.</summary>
    private const double CapacityProbeDemandFraction = 0.5;
    /// <summary>A probe confirms capacity only when rate rises without the round trip
    /// inflating past this tolerance. Through a saturated broker, delivered rate never drops
    /// when queue is added, so a rate-only acceptance test converts every probe into budget
    /// growth; requiring a flat RTT distinguishes real headroom from added queueing.</summary>
    private const double ProbeRttGrowthTolerance = 1.25;
    private static readonly long MaxProbeIntervalTicks = Stopwatch.Frequency;
    // An empty pipe refilled within this gap is a serialized loaded cycle, not application idle.
    private static readonly long MaximumSerializedRateCycleGapTicks = Math.Max(1, Stopwatch.Frequency / 500);
    // Shorter loaded epochs are dominated by timer, scheduler, and burst quantization on common hosts.
    private static readonly long MinimumSustainableRateSampleTicks = Math.Max(
        1,
        Stopwatch.Frequency / 10);
    // Bound smoothing so a continuously loaded producer still tracks capacity changes promptly.
    private static readonly long MaximumLoadedDeliveryEpochTicks = Math.Max(1, Stopwatch.Frequency / 4);
    private const long NoDeliveryEpoch = -1;
    private const long DeliveryEpochUpdating = -2;

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
    private long _sendEpochDeliveredBytes = NoDeliveryEpoch;
    private long _sendEpochFirstTimestamp;

    // Single-writer state (owning send loop only; constructor runs before the loop starts).
    private readonly long _floorBytes;
    private readonly double _targetSeconds;
    private readonly long _probeResponseHorizonTicks;
    private long _capBytes;
    private readonly double[] _rateBucketMaxValues = new double[WindowBucketCount];
    private readonly double[] _rateBucketSums = new double[WindowBucketCount];
    private readonly int[] _rateBucketSampleCounts = new int[WindowBucketCount];
    private readonly long[] _occupancyBucketMaxValues = new long[WindowBucketCount];
    // Per-request diagnostics: log2 histograms of acked request sizes and RTTs. Incremented
    // only by the owning send loop; snapshot copies use per-element volatile reads, so
    // readers may observe slightly stale counts (acceptable for diagnostics).
    private readonly long[] _requestSizeLog2Histogram = new long[HistogramBucketCount];
    private readonly long[] _requestRttMicrosLog2Histogram = new long[HistogramBucketCount];
    private readonly long[]? _admissionBlockMicrosLog2Histogram;
    private readonly object? _probeDiagnosticsLock;
    private readonly Queue<BrokerBudgetProbeEvent>? _minRttProbeEvents;
    private readonly Queue<BrokerBudgetProbeEvent>? _capacityProbeEvents;
    private long _admissionBlockedSinceTimestamp;
    private long _currentWindowBucket = long.MinValue;
    private double _windowMaxRate;
    private double _windowRateSum;
    private int _windowRateSampleCount;
    private double _retainedLoadedMaxRate;
    private long _windowMaxOccupancyBytes;
    private double _minRttSeconds;
    private double _servingRttEwmaSeconds;
    private double _rawServingRttEwmaSeconds;
    private double _requestSizeEwmaBytes;
    // Capacity probes raise this request-depth floor only after proving that the larger
    // flight increases delivery rate without inflating RTT. It survives an individual
    // probe ending so normal admission does not fall back to the self-confirming four-
    // request floor after every successful rung. Sustained idle resets path evidence.
    private double _provenPipelineRequestQuanta = MinimumPipelineRequestQuanta;
    private bool _hasLoadedServingSample;
    private double _sealToAckEwmaSeconds;
    private double _minRttProbeMinimumSeconds;
    private bool _hasMinRttSample;
    private long _minRttTimestamp;
    // Written only by the send loop; acquire-read by appenders after _budgetBytes publishes
    // the matching precomputed post-probe budgets.
    private long _minRttProbeUntilTimestamp;
    private long _minRttProbeStartedTimestamp;
    private long _nextProbeTimestamp;
    private long _capacityProbeStartTimestamp;
    private long _capacityProbeDeadlineTimestamp;
    private long _capacityProbeEvaluationDeadlineTimestamp;
    private double _capacityProbeBaselineRate;
    private double _capacityProbeRateSum;
    private double _capacityProbeRttSumSeconds;
    private double _capacityProbePreProbeRttSeconds;
    private int _capacityProbeRateSampleCount;
    private bool _capacityProbeActive;
    private long _capacityProbeSuccessCount;
    private long _capacityProbeFailureCount;
    private double _deliveryLatencyEwmaSeconds;
    private double _latencyBudgetScale = 1.0;
    private bool _admissionPressureActive;
    private long _nextLatencyControlTimestamp;
    private long _lastLatencyControlAdmissionBlockEvents;
    private long _lastNormalBudgetBytes;
    private long _lastBudgetUpdateTimestamp;

    public BrokerUnackedByteBudget(
        double targetSeconds,
        long floorBytes,
        long initialCapBytes,
        double lingerSeconds = 0,
        bool enableDiagnostics = false)
    {
        _targetSeconds = targetSeconds;
        _probeResponseHorizonTicks = Math.Max(
            1,
            (long)(Math.Max(0, targetSeconds + lingerSeconds) * Stopwatch.Frequency));
        _floorBytes = Math.Max(1, floorBytes);
        _capBytes = Math.Max(_floorBytes, initialCapBytes);
        _lastNormalBudgetBytes = _capBytes;
        Volatile.Write(ref _budgetBytes, _capBytes);
        Volatile.Write(ref _budgetAfterMinRttProbeBytes, _capBytes);
        Volatile.Write(ref _probeBudgetAfterMinRttProbeBytes, _capBytes);
        if (enableDiagnostics)
        {
            _admissionBlockMicrosLog2Histogram = new long[AdmissionBlockHistogramBucketCount];
            _probeDiagnosticsLock = new object();
            _minRttProbeEvents = new Queue<BrokerBudgetProbeEvent>();
            _capacityProbeEvents = new Queue<BrokerBudgetProbeEvent>();
        }
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

    internal long CapacityProbeSuccessCount => Volatile.Read(ref _capacityProbeSuccessCount);

    internal long CapacityProbeFailureCount => Volatile.Read(ref _capacityProbeFailureCount);

    internal double ProvenPipelineRequestQuanta =>
        Volatile.Read(ref _provenPipelineRequestQuanta);

    /// <summary>EWMA of controllable seal-to-send queue latency. The diagnostic property
    /// retains its original name for compatibility.</summary>
    internal long DeliveryLatencyEwmaMicros =>
        (long)(Volatile.Read(ref _deliveryLatencyEwmaSeconds) * 1_000_000);

    internal double LatencyBudgetScale => Volatile.Read(ref _latencyBudgetScale);

    /// <summary>
    /// Cross-thread: parallel connection sends mint the per-request delivery token immediately
    /// before the socket write (so <paramref name="sendTimestamp"/> can never postdate the
    /// response's arrival), feeding it back to <see cref="OnAcked"/> on the response. Delivery
    /// epoch changes at rate-app-limited boundaries are CAS-published so parallel sends cannot
    /// regress the delivered-byte marker; loaded and immediately post-delivery sends reuse the
    /// published epoch. The independent last-delivery timestamp may only conservatively widen
    /// a sample.
    /// </summary>
    /// <param name="sendTimestamp">Stopwatch timestamp taken just before the socket write.</param>
    /// <param name="appLimited">True when the broker pipe was empty at send time.</param>
    /// <param name="oldestBatchTimestamp">Seal timestamp of the oldest batch in the request,
    /// or zero when delivery-age feedback is unavailable.</param>
    internal DeliverySnapshot SnapshotDelivery(
        long sendTimestamp,
        bool appLimited,
        long oldestBatchTimestamp = 0)
    {
        var deliveredTimestamp = Volatile.Read(ref _lastDeliveredTimestamp);
        var deliveryGapTicks = sendTimestamp - deliveredTimestamp;
        // An empty pipe sent immediately after an ack is a serialized loaded rate cycle, not
        // idle application time. Negative gaps can arise from concurrent delivery publication
        // and cannot prove that ordering, so they remain rate-app-limited.
        var continuesSerializedRateCycle = appLimited
            && deliveredTimestamp != 0
            && deliveryGapTicks >= 0
            && deliveryGapTicks < MaximumSerializedRateCycleGapTicks;
        var rateAppLimited = appLimited && !continuesSerializedRateCycle;
        var deliveryEpochFirstSendTimestamp = CaptureDeliveryEpoch(
            sendTimestamp,
            rateAppLimited,
            out var deliveryEpochDeliveredBytes,
            out var deliveredBytes);
        return new DeliverySnapshot(
            deliveredBytes,
            deliveredTimestamp,
            deliveryEpochDeliveredBytes,
            deliveryEpochFirstSendTimestamp,
            sendTimestamp,
            appLimited,
            rateAppLimited,
            oldestBatchTimestamp,
            Volatile.Read(ref _unackedBytes));
    }

    private long CaptureDeliveryEpoch(
        long sendTimestamp,
        bool rateAppLimited,
        out long deliveryEpochDeliveredBytes,
        out long deliveredBytes)
    {
        var spinner = new SpinWait();
        while (true)
        {
            deliveredBytes = Volatile.Read(ref _totalDeliveredBytes);
            var epochDeliveredBytes = Volatile.Read(ref _sendEpochDeliveredBytes);
            if (epochDeliveredBytes == DeliveryEpochUpdating)
            {
                spinner.SpinOnce();
                continue;
            }

            // The timestamp and delivered marker form one publication. Re-read the marker
            // after the timestamp so a reader spanning another publisher's two writes retries
            // instead of pairing an old byte anchor with a new time anchor.
            var epochFirstSendTimestamp = Volatile.Read(ref _sendEpochFirstTimestamp);
            if (Volatile.Read(ref _sendEpochDeliveredBytes) != epochDeliveredBytes)
            {
                spinner.SpinOnce();
                continue;
            }

            if (epochDeliveredBytes == deliveredBytes)
            {
                deliveryEpochDeliveredBytes = epochDeliveredBytes;
                return epochFirstSendTimestamp;
            }

            // A loaded pipe keeps a bounded rolling epoch across acknowledgements. Reset at
            // rate-app-limited boundaries or after 250ms: the lower bound prevents serialized
            // requestBytes / ownRTT spikes; the upper bound preserves capacity adaptation.
            if (!rateAppLimited
                && epochDeliveredBytes != NoDeliveryEpoch
                && sendTimestamp - epochFirstSendTimestamp < MaximumLoadedDeliveryEpochTicks)
            {
                deliveryEpochDeliveredBytes = epochDeliveredBytes;
                return epochFirstSendTimestamp;
            }

            if (Interlocked.CompareExchange(
                    ref _sendEpochDeliveredBytes,
                    DeliveryEpochUpdating,
                    epochDeliveredBytes) != epochDeliveredBytes)
            {
                spinner.SpinOnce();
                continue;
            }

            return PublishDeliveryEpoch(
                sendTimestamp,
                epochDeliveredBytes,
                out deliveryEpochDeliveredBytes,
                out deliveredBytes);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private long PublishDeliveryEpoch(
        long sendTimestamp,
        long previousEpochDeliveredBytes,
        out long deliveryEpochDeliveredBytes,
        out long deliveredBytes)
    {
        // The loop-top delivery read can predate a newer epoch value observed by the
        // successful CAS. Re-read after winning so publication never regresses that value.
        // A redundant publisher must also preserve the first send timestamp already chosen
        // for the epoch; moving it forward would narrow the send-axis delivery interval.
        deliveredBytes = Volatile.Read(ref _totalDeliveredBytes);
        if (deliveredBytes != previousEpochDeliveredBytes)
            Volatile.Write(ref _sendEpochFirstTimestamp, sendTimestamp);

        Volatile.Write(ref _sendEpochDeliveredBytes, deliveredBytes);
        deliveryEpochDeliveredBytes = deliveredBytes;
        return Volatile.Read(ref _sendEpochFirstTimestamp);
    }

    /// <summary>Diagnostics: log2 histogram of acked request payload sizes (see
    /// <see cref="HistogramBucketCount"/> for bucket semantics).</summary>
    internal long[] CopyRequestSizeHistogram() => CopyHistogram(_requestSizeLog2Histogram);

    /// <summary>Diagnostics: log2 histogram of acked request round-trip times in microseconds.</summary>
    internal long[] CopyRequestRttMicrosHistogram() => CopyHistogram(_requestRttMicrosLog2Histogram);

    internal long[] CopyAdmissionBlockMicrosHistogram() =>
        _admissionBlockMicrosLog2Histogram is { } histogram
            ? CopyHistogram(histogram)
            : [];

    internal BrokerBudgetProbeEvent[] CopyProbeEvents()
    {
        if (_minRttProbeEvents is null)
            return [];

        lock (_probeDiagnosticsLock!)
            return [.. _minRttProbeEvents, .. _capacityProbeEvents!];
    }

    internal void ResetDiagnostics(long nowTicks = 0)
    {
        if (_admissionBlockMicrosLog2Histogram is { } histogram)
        {
            for (var i = 0; i < histogram.Length; i++)
                Interlocked.Exchange(ref histogram[i], 0);

            if (nowTicks == 0)
                nowTicks = Stopwatch.GetTimestamp();
            while (true)
            {
                var startedAt = Volatile.Read(ref _admissionBlockedSinceTimestamp);
                if (startedAt == 0
                    || Interlocked.CompareExchange(
                        ref _admissionBlockedSinceTimestamp,
                        nowTicks,
                        startedAt) == startedAt)
                {
                    break;
                }
            }
        }

        if (_minRttProbeEvents is not null)
        {
            lock (_probeDiagnosticsLock!)
            {
                _minRttProbeEvents.Clear();
                _capacityProbeEvents!.Clear();
            }
        }
    }

    internal long GetCurrentAdmissionBlockDurationMicros(long nowTicks)
    {
        var startedAt = Volatile.Read(ref _admissionBlockedSinceTimestamp);
        return startedAt == 0 || nowTicks <= startedAt
            ? 0
            : (long)((nowTicks - startedAt) * 1_000_000.0 / Stopwatch.Frequency);
    }

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
    public void RecordAdmissionBlock(long nowTicks = 0)
    {
        Interlocked.Increment(ref _admissionBlockEvents);
        if (_admissionBlockMicrosLog2Histogram is null)
            return;

        if (nowTicks == 0)
            nowTicks = Stopwatch.GetTimestamp();
        _ = Interlocked.CompareExchange(ref _admissionBlockedSinceTimestamp, nowTicks, 0);
    }

    private void RecordAdmissionAvailable(long nowTicks)
    {
        var histogram = _admissionBlockMicrosLog2Histogram;
        if (histogram is null)
            return;

        var startedAt = Interlocked.Exchange(ref _admissionBlockedSinceTimestamp, 0);
        if (startedAt == 0 || nowTicks <= startedAt)
            return;

        var durationMicros = (ulong)Math.Max(
            1,
            (long)((nowTicks - startedAt) * 1_000_000.0 / Stopwatch.Frequency));
        var bucket = Math.Min(
            BitOperations.Log2(durationMicros),
            AdmissionBlockHistogramBucketCount - 1);
        Interlocked.Increment(ref histogram[bucket]);
    }

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
            DeactivateCapacityProbe(nowTicks);
        RecomputeBudget(nowTicks);
        if (!IsOverBudgetAt(nowTicks))
            RecordAdmissionAvailable(nowTicks);
    }

    /// <summary>
    /// Feeds one successfully acknowledged request into the drain estimate. Called only from
    /// the owning send loop, once per acked request. O(1), allocation-free. The budget itself
    /// is republished once per response pass by <see cref="CompleteAckedPass"/>, which owns
    /// the per-pass-vs-per-request rationale.
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
        var deliveryIdleTicks = sendSnapshot.SendTimestamp - sendSnapshot.DeliveredTimestamp;
        var sustainedIdle = sendSnapshot.AppLimited
            && sendSnapshot.DeliveredTimestamp != 0
            && deliveryIdleTicks >= WindowDurationTicks;
        var loadedServingSample = !sendSnapshot.AppLimited
            || (sendSnapshot.DeliveredTimestamp != 0 && !sustainedIdle);
        var retainLoadedRateSample = !sendSnapshot.AppLimited;
        if (sustainedIdle)
        {
            _hasLoadedServingSample = false;
            Volatile.Write(
                ref _provenPipelineRequestQuanta,
                MinimumPipelineRequestQuanta);
            _lastNormalBudgetBytes = _capBytes;
            _lastBudgetUpdateTimestamp = nowTicks;
        }
        else if (loadedServingSample)
            _hasLoadedServingSample = true;
        // The deadline ack may establish the first loaded serving estimate (depth-one traffic),
        // but must not drag an existing estimate toward the queue-drained probe RTT.
        var minRttProbeActive = _minRttProbeUntilTimestamp != 0
            && (nowTicks < _minRttProbeUntilTimestamp
                || (nowTicks == _minRttProbeUntilTimestamp && _servingRttEwmaSeconds > 0));
        // Queue-corrected service RTT: the raw round trip includes the time this request
        // spent behind bytes the budget itself admitted (unacked at send, drained at the
        // measured max rate). Feeding raw RTTs into the minimum and serving estimates lets
        // self-queueing masquerade as service time and re-inflate the RTT floor those
        // estimates size (#2009). The windowed-max rate under-corrects (biased-safe), and a
        // fraction guard bounds outlier over-correction.
        var serviceRttSeconds = CorrectRttForSelfQueueing(
            rttSeconds,
            sendSnapshot.UnackedBytesAtSend,
            ackedBytes);
        UpdateMinRtt(
            serviceRttSeconds,
            rttSeconds,
            nowTicks,
            naturalMinRttSample: sendSnapshot.AppLimited && !sustainedIdle);
        if (loadedServingSample && !minRttProbeActive)
        {
            _servingRttEwmaSeconds = UpdateEwma(
                _servingRttEwmaSeconds,
                serviceRttSeconds,
                ServingRttEwmaWeight);
            // Raw (uncorrected) companion estimate for the capacity probe: probe samples are
            // raw round trips, so their acceptance baseline must live in the same domain —
            // a corrected baseline under standing load would reject every probe.
            _rawServingRttEwmaSeconds = UpdateEwma(
                _rawServingRttEwmaSeconds,
                rttSeconds,
                ServingRttEwmaWeight);
        }
        Volatile.Write(ref _minimumRttMicros, (long)(_minRttSeconds * 1_000_000));
        UpdateDeliveryLatency(
            sendSnapshot.OldestBatchTimestamp,
            sendSnapshot.SendTimestamp,
            nowTicks);

        _requestSizeEwmaBytes = UpdateEwma(
            _requestSizeEwmaBytes,
            ackedBytes,
            RequestSizeEwmaWeight);
        RecordRequestHistograms(ackedBytes, rttSeconds);

        // Keep two delivery-rate axes. The request axis attributes immediate delivery credit
        // to one capacity probe. The epoch axis estimates sustainable rate from all delivery
        // since the pipe last became loaded, including the first send's RTT; loaded sends keep
        // that anchor across intervening acknowledgements. Thus a serialized request cannot
        // claim requestBytes / ownRTT as broker capacity on every fast response.
        var totalDelivered = _totalDeliveredBytes + ackedBytes;
        Volatile.Write(ref _totalDeliveredBytes, totalDelivered);
        var deliveredDelta = totalDelivered - sendSnapshot.DeliveredBytes;
        var deliveryEpochDelta = totalDelivered - sendSnapshot.DeliveryEpochDeliveredBytes;

        var requestIntervalTicks = rttTicks;
        if (!sendSnapshot.AppLimited && sendSnapshot.DeliveredTimestamp != 0)
        {
            requestIntervalTicks = Math.Max(
                requestIntervalTicks,
                nowTicks - sendSnapshot.DeliveredTimestamp);
        }

        var deliveryEpochIntervalTicks = requestIntervalTicks;
        if (sendSnapshot.DeliveryEpochFirstSendTimestamp > 0)
        {
            deliveryEpochIntervalTicks = Math.Max(
                deliveryEpochIntervalTicks,
                nowTicks - sendSnapshot.DeliveryEpochFirstSendTimestamp);
        }
        Volatile.Write(ref _lastDeliveredTimestamp, nowTicks);

        AdvanceWindow(nowTicks);
        var requestBytesPerSecond = deliveredDelta * (double)Stopwatch.Frequency / requestIntervalTicks;
        var deliveryEpochBytesPerSecond = deliveryEpochDelta * (double)Stopwatch.Frequency
            / deliveryEpochIntervalTicks;
        var bootstrapsEmptyEstimator = sendSnapshot.RateAppLimited
            && GetEffectiveMaxRate() == 0;
        if (deliveryEpochBytesPerSecond > 0
            && (deliveryEpochIntervalTicks >= MinimumSustainableRateSampleTicks
                || bootstrapsEmptyEstimator))
        {
            AddRateSample(deliveryEpochBytesPerSecond, retainLoadedRateSample, sustainedIdle);
            Volatile.Write(ref _maxRateBytesPerSecond, (long)GetEffectiveMaxRate());
        }

        UpdateCapacityProbe(
            requestBytesPerSecond,
            sendSnapshot.OldestBatchTimestamp != 0
                ? sendSnapshot.OldestBatchTimestamp
                : sendSnapshot.SendTimestamp,
            rttTicks,
            rttSeconds,
            nowTicks);
    }

    /// <summary>
    /// Drains the cross-thread written-occupancy peak into the sample window and republishes
    /// the budget. Called by the owning send loop once per response pass, after every
    /// <see cref="OnAcked"/> for that pass. The interlocked peak drain and the budget
    /// recompute are the estimator's expensive steps; a pass at the in-flight ceiling drains
    /// many requests, and the admission gate only reads the budget between passes, so
    /// per-request publishing multiplied that cost for no admission-precision gain.
    /// </summary>
    public void CompleteAckedPass(long nowTicks)
    {
        AdvanceWindow(nowTicks);
        var writtenUnackedPeak = Interlocked.Exchange(ref _pendingWrittenUnackedPeakBytes, 0);
        if (writtenUnackedPeak > 0)
            AddOccupancySample(writtenUnackedPeak);

        RecomputeBudget(nowTicks);
        if (!IsOverBudgetAt(nowTicks))
            RecordAdmissionAvailable(nowTicks);
    }

    private void UpdateDeliveryLatency(
        long oldestBatchTimestamp,
        long sendTimestamp,
        long nowTicks)
    {
        if (oldestBatchTimestamp <= 0 || oldestBatchTimestamp >= sendTimestamp)
            return;

        var sampleSeconds = (double)(sendTimestamp - oldestBatchTimestamp) / Stopwatch.Frequency;
        var latencyEstimate = UpdateEwma(
            _deliveryLatencyEwmaSeconds,
            sampleSeconds,
            DeliveryLatencyEwmaWeight);
        Volatile.Write(ref _deliveryLatencyEwmaSeconds, latencyEstimate);

        // Measured seal-to-ack is the delivery latency users experience from the moment a
        // batch is eligible to send. The queue-drained base RTT is subtracted before the
        // control decision: broker round-trip time cannot be reduced by shrinking admission,
        // and charging it to the controller is what turned the target into an equilibrium
        // setpoint that suppressed fan-out (#1988). What remains — sender backlog plus
        // broker-side queueing — is exactly the delay the budget controls (#2008, #2009).
        var sealToAckSeconds = (double)(nowTicks - oldestBatchTimestamp) / Stopwatch.Frequency;
        _sealToAckEwmaSeconds = UpdateEwma(
            _sealToAckEwmaSeconds,
            sealToAckSeconds,
            DeliveryLatencyEwmaWeight);

        if (_nextLatencyControlTimestamp == 0)
        {
            UpdateAdmissionPressure();
            _nextLatencyControlTimestamp = nowTicks + LatencyControlIntervalTicks;
            return;
        }

        if (nowTicks < _nextLatencyControlTimestamp)
            return;

        UpdateAdmissionPressure();

        var baseRttSeconds = _hasMinRttSample ? _minRttSeconds : 0;
        var brokerQueueSeconds = _sealToAckEwmaSeconds - baseRttSeconds;
        var controlLatencySeconds = Math.Max(latencyEstimate, brokerQueueSeconds);
        var targetRatio = _targetSeconds / controlLatencySeconds;
        double adjustment;
        if (targetRatio < 1)
        {
            // Broker-side queueing always responds to admission — shrink acts on it
            // unconditionally. When the seal-to-send backlog dominates instead, shrink only
            // if the wire is demonstrably carrying the budget: backlog with an underfilled
            // wire means the send loop, not admission, is the bottleneck, and shrinking
            // there starves the sender below its capacity without reducing the backlog
            // (the 1-broker collapse mode of #2008).
            var senderBacklogDominates = latencyEstimate >= brokerQueueSeconds;
            var shrinkActionable = !senderBacklogDominates
                || _windowMaxOccupancyBytes
                    >= ShrinkOccupancyFraction * _lastNormalBudgetBytes;
            adjustment = shrinkActionable
                ? Math.Max(MinimumLatencyAdjustmentFactor, targetRatio)
                : 1.0;
        }
        else if (_latencyBudgetScale < 1.0)
        {
            // A transient queue can legitimately derate the budget, but once measured
            // latency is back below target the controller must return to its neutral scale.
            // Requiring another admission block here makes the derating self-sealing: the
            // smaller budget removes the pressure event that would restore it. Recovery
            // without pressure stops at the neutral 1.0 scale.
            adjustment = Math.Min(
                MaximumLatencyAdjustmentFactor,
                1.0 / _latencyBudgetScale);
        }
        else
        {
            adjustment = 1.0;
        }

        Volatile.Write(ref _latencyBudgetScale, Math.Clamp(
            _latencyBudgetScale * adjustment,
            MinimumLatencyBudgetScale,
            1.0));
        _nextLatencyControlTimestamp = nowTicks + LatencyControlIntervalTicks;
    }

    private void UpdateAdmissionPressure()
    {
        var admissionBlockEvents = AdmissionBlockEvents;
        _admissionPressureActive = admissionBlockEvents > _lastLatencyControlAdmissionBlockEvents;
        _lastLatencyControlAdmissionBlockEvents = admissionBlockEvents;
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

    /// <summary>Removes the drain time of bytes already unacked at send from a measured
    /// round trip, leaving an estimate of the broker's service time. The correction is
    /// bounded below by <see cref="MinCorrectedRttFraction"/> of the raw round trip so a
    /// rate-estimate outlier cannot collapse the RTT floor the result feeds.</summary>
    private double CorrectRttForSelfQueueing(double rttSeconds, long unackedBytesAtSend, long ackedBytes)
    {
        var effectiveMaxRate = GetEffectiveMaxRate();
        var queueAheadBytes = unackedBytesAtSend - ackedBytes;
        if (effectiveMaxRate <= 0 || queueAheadBytes <= 0)
            return rttSeconds;

        return Math.Max(
            rttSeconds * MinCorrectedRttFraction,
            rttSeconds - queueAheadBytes / effectiveMaxRate);
    }

    private void UpdateMinRtt(
        double serviceRttSeconds,
        double rawRttSeconds,
        long nowTicks,
        bool naturalMinRttSample)
    {
        if (!_hasMinRttSample)
        {
            _minRttSeconds = serviceRttSeconds;
            _minRttTimestamp = nowTicks;
            _hasMinRttSample = true;
            StartMinRttProbe(nowTicks, rawRttSeconds);
            return;
        }

        if (naturalMinRttSample
            && serviceRttSeconds <= _minRttSeconds * ProbeRttGrowthTolerance)
        {
            if (_minRttProbeUntilTimestamp != 0)
            {
                _minRttProbeMinimumSeconds = Math.Min(
                    _minRttProbeMinimumSeconds,
                    serviceRttSeconds);
                CompleteMinRttProbe(nowTicks);
            }
            else
            {
                _minRttSeconds = Math.Min(_minRttSeconds, serviceRttSeconds);
                _minRttTimestamp = nowTicks;
            }
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
                _minRttProbeMinimumSeconds = Math.Min(_minRttProbeMinimumSeconds, serviceRttSeconds);
                return;
            }
        }

        if (serviceRttSeconds < _minRttSeconds)
        {
            _minRttSeconds = serviceRttSeconds;
            _minRttTimestamp = nowTicks;
            return;
        }

        // Probe duration is sized from the raw round trip: draining the standing queue
        // takes real wall-clock time even when the corrected service estimate is small.
        if (nowTicks - _minRttTimestamp >= MinRttWindowTicks)
            StartMinRttProbe(nowTicks, rawRttSeconds);
    }

    private void CompleteExpiredMinRttProbe(long nowTicks)
    {
        if (_minRttProbeUntilTimestamp != 0 && nowTicks >= _minRttProbeUntilTimestamp)
            CompleteMinRttProbe(nowTicks);
    }

    private void CompleteMinRttProbe(long nowTicks)
    {
        var succeeded = _minRttProbeMinimumSeconds != double.MaxValue;
        if (succeeded)
        {
            _minRttSeconds = Math.Min(
                _minRttProbeMinimumSeconds,
                _minRttSeconds * MinRttRefreshGrowthFactor);
        }

        // If the probe expired without an ack, retain the prior minimum and re-arm its
        // freshness window. An idle gap is not a base-RTT measurement.
        _minRttTimestamp = nowTicks;
        _minRttProbeUntilTimestamp = 0;
        RecordProbeEvent(
            BrokerBudgetProbeType.MinimumRtt,
            succeeded ? BrokerBudgetProbeOutcome.Succeeded : BrokerBudgetProbeOutcome.Failed,
            nowTicks,
            _minRttProbeStartedTimestamp);
        _minRttProbeStartedTimestamp = 0;
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
        _minRttProbeStartedTimestamp = nowTicks;
        RecordProbeEvent(
            BrokerBudgetProbeType.MinimumRtt,
            BrokerBudgetProbeOutcome.Started,
            nowTicks,
            nowTicks);
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
            Array.Clear(_rateBucketSums);
            Array.Clear(_rateBucketSampleCounts);
            Array.Clear(_occupancyBucketMaxValues);
            _windowMaxRate = 0;
            _windowRateSum = 0;
            _windowRateSampleCount = 0;
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
                _windowRateSum -= _rateBucketSums[slot];
                _windowRateSampleCount -= _rateBucketSampleCounts[slot];
                _rateBucketMaxValues[slot] = 0;
                _rateBucketSums[slot] = 0;
                _rateBucketSampleCounts[slot] = 0;
                _occupancyBucketMaxValues[slot] = 0;
            }

            if (recomputeRate)
                _windowMaxRate = Max(_rateBucketMaxValues);
            if (recomputeOccupancy)
                _windowMaxOccupancyBytes = Max(_occupancyBucketMaxValues);
        }

        if (_retainedLoadedMaxRate > _windowMaxRate)
        {
            var decay = elapsed == 1
                ? LoadedRateDecayPerBucket
                : Math.Pow(LoadedRateDecayPerBucket, elapsed);
            _retainedLoadedMaxRate = _windowMaxRate
                + (_retainedLoadedMaxRate - _windowMaxRate) * decay;
        }

        _currentWindowBucket = bucket;
    }

    private void AddRateSample(double bytesPerSecond, bool loadedSample, bool sustainedIdle)
    {
        var slot = (int)(_currentWindowBucket % WindowBucketCount);
        _rateBucketMaxValues[slot] = Math.Max(_rateBucketMaxValues[slot], bytesPerSecond);
        _rateBucketSums[slot] += bytesPerSecond;
        _rateBucketSampleCounts[slot]++;
        _windowMaxRate = Math.Max(_windowMaxRate, bytesPerSecond);
        _windowRateSum += bytesPerSecond;
        _windowRateSampleCount++;
        if (sustainedIdle)
            _retainedLoadedMaxRate = 0;
        else if (loadedSample)
            _retainedLoadedMaxRate = Math.Max(_retainedLoadedMaxRate, bytesPerSecond);
    }

    private double GetEffectiveMaxRate() => Math.Max(_windowMaxRate, _retainedLoadedMaxRate);

    private double GetWindowAverageRate() => _windowRateSampleCount > 0
        ? _windowRateSum / _windowRateSampleCount
        : 0;

    private void AddOccupancySample(long bytes)
    {
        var slot = (int)(_currentWindowBucket % WindowBucketCount);
        _occupancyBucketMaxValues[slot] = Math.Max(_occupancyBucketMaxValues[slot], bytes);
        _windowMaxOccupancyBytes = Math.Max(_windowMaxOccupancyBytes, bytes);
    }

    private void UpdateCapacityProbe(
        double bytesPerSecond,
        long admissionTimestamp,
        long rttTicks,
        double rttSeconds,
        long nowTicks)
    {
        var probeIntervalTicks = GetProbeIntervalTicks(rttTicks);
        var evaluationWindowTicks = Math.Max(
            ProbeEvaluationRtts * rttTicks,
            _probeResponseHorizonTicks);
        var probeDurationTicks = Math.Max(
            probeIntervalTicks,
            rttTicks + evaluationWindowTicks);
        if (_nextProbeTimestamp == 0)
            _nextProbeTimestamp = nowTicks + probeIntervalTicks;

        if (_capacityProbeActive)
        {
            if (nowTicks >= _capacityProbeDeadlineTimestamp)
            {
                DeactivateCapacityProbe(nowTicks);
                _nextProbeTimestamp = nowTicks + probeIntervalTicks;
                return;
            }

            // A response can contribute only when every batch in its request was admitted
            // after the larger budget was published. Send time is too late: a request sent
            // during the probe can still contain batches sealed under the old budget.
            if (admissionTimestamp < _capacityProbeStartTimestamp)
                return;

            _capacityProbeRateSum += bytesPerSecond;
            _capacityProbeRttSumSeconds += rttSeconds;
            _capacityProbeRateSampleCount++;
            if (_capacityProbeEvaluationDeadlineTimestamp == 0)
            {
                _capacityProbeEvaluationDeadlineTimestamp = nowTicks + evaluationWindowTicks;
                Volatile.Write(
                    ref _capacityProbeDeadlineTimestamp,
                    Math.Max(_capacityProbeDeadlineTimestamp, nowTicks + evaluationWindowTicks));
                return;
            }

            if (nowTicks < _capacityProbeEvaluationDeadlineTimestamp)
                return;

            // Real headroom shows as rate-up with a flat round trip. A saturated broker also
            // shows rate-not-down when the probe adds queue, so a rate-only test would accept
            // every probe under load and ratchet the budget into standing latency. The rate
            // gate stays primary; the RTT guard (raw-domain baseline vs raw probe samples,
            // <= 0 only when reflection-seeded in tests) catches the queue-dominated regime
            // where round trips inflate well past the tolerance.
            var averageRate = _capacityProbeRateSum / _capacityProbeRateSampleCount;
            var averageRttSeconds = _capacityProbeRttSumSeconds / _capacityProbeRateSampleCount;
            var rttHeld = _capacityProbePreProbeRttSeconds <= 0
                || averageRttSeconds <= _capacityProbePreProbeRttSeconds * ProbeRttGrowthTolerance;
            if (rttHeld && averageRate > _capacityProbeBaselineRate * ProbeGrowthThreshold)
            {
                // Compare like with like across rungs. A single clustered-response peak is not
                // a sustainable baseline and would make the next average-rate rung unwinnable.
                _capacityProbeBaselineRate = averageRate;
                _capacityProbePreProbeRttSeconds = averageRttSeconds;
                Volatile.Write(
                    ref _provenPipelineRequestQuanta,
                    Math.Min(
                        GetMaximumPipelineRequestQuanta(),
                        _provenPipelineRequestQuanta * ProbeBudgetMultiplier));
                RecordProbeEvent(
                    BrokerBudgetProbeType.Capacity,
                    BrokerBudgetProbeOutcome.Succeeded,
                    nowTicks,
                    _capacityProbeStartTimestamp);
                _capacityProbeStartTimestamp = nowTicks;
                ResetCapacityProbeSamples();
                Interlocked.Increment(ref _capacityProbeSuccessCount);
                Volatile.Write(ref _capacityProbeDeadlineTimestamp, nowTicks + probeDurationTicks);
            }
            else
            {
                DeactivateCapacityProbe(nowTicks);
                _nextProbeTimestamp = nowTicks + probeIntervalTicks;
            }

            return;
        }

        if (nowTicks < _nextProbeTimestamp)
            return;

        if (nowTicks - _nextProbeTimestamp < probeIntervalTicks
            && HasCapacityProbeDemand())
        {
            _capacityProbeStartTimestamp = nowTicks;
            _capacityProbeBaselineRate = GetWindowAverageRate();
            // Raw-domain baseline to match the raw probe samples; the queue-corrected
            // serving EWMA sits below loaded round trips and would fail every probe.
            _capacityProbePreProbeRttSeconds = _rawServingRttEwmaSeconds > 0
                ? _rawServingRttEwmaSeconds
                : rttSeconds;
            ResetCapacityProbeSamples();
            Volatile.Write(ref _capacityProbeDeadlineTimestamp, nowTicks + probeDurationTicks);
            Volatile.Write(ref _capacityProbeActive, true);
            RecordProbeEvent(
                BrokerBudgetProbeType.Capacity,
                BrokerBudgetProbeOutcome.Started,
                nowTicks,
                nowTicks);
        }

        // A schedule missed by a full interval represents producer idle time, not an
        // active-pipeline probe opportunity. Re-arm instead of probing on resume.
        _nextProbeTimestamp = nowTicks + probeIntervalTicks;
    }

    private bool HasCapacityProbeDemand()
    {
        var budgetBytes = Volatile.Read(ref _budgetBytes);
        var minimumDemandBytes = Math.Max(
            1,
            (long)(budgetBytes * CapacityProbeDemandFraction));
        return Volatile.Read(ref _unackedBytes) >= minimumDemandBytes;
    }

    private static long GetProbeIntervalTicks(long rttTicks)
        => rttTicks >= MaxProbeIntervalTicks / ProbeIntervalRtts
            ? MaxProbeIntervalTicks
            : ProbeIntervalRtts * rttTicks;

    private void ResetCapacityProbeSamples()
    {
        _capacityProbeRateSum = 0;
        _capacityProbeRttSumSeconds = 0;
        _capacityProbeRateSampleCount = 0;
        _capacityProbeEvaluationDeadlineTimestamp = 0;
    }

    private double GetMaximumPipelineRequestQuanta()
        => _requestSizeEwmaBytes > 0
            ? Math.Max(MinimumPipelineRequestQuanta, _capBytes / _requestSizeEwmaBytes)
            : MinimumPipelineRequestQuanta;

    private void DeactivateCapacityProbe(long nowTicks)
    {
        if (_capacityProbeActive)
        {
            Interlocked.Increment(ref _capacityProbeFailureCount);
            RecordProbeEvent(
                BrokerBudgetProbeType.Capacity,
                BrokerBudgetProbeOutcome.Failed,
                nowTicks,
                _capacityProbeStartTimestamp);
        }

        ResetCapacityProbeSamples();
        Volatile.Write(ref _capacityProbeActive, false);
        Volatile.Write(ref _capacityProbeDeadlineTimestamp, 0);
    }

    private void RecordProbeEvent(
        BrokerBudgetProbeType probeType,
        BrokerBudgetProbeOutcome outcome,
        long nowTicks,
        long startedAtTicks)
    {
        if (_minRttProbeEvents is null)
            return;

        var durationMilliseconds = startedAtTicks > 0 && nowTicks > startedAtTicks
            ? (long)((nowTicks - startedAtTicks) * 1_000.0 / Stopwatch.Frequency)
            : 0;
        var diagnostic = new BrokerBudgetProbeEvent(
            probeType,
            outcome,
            DateTimeOffset.UtcNow,
            durationMilliseconds,
            BudgetBytes,
            UnackedBytes);
        lock (_probeDiagnosticsLock!)
        {
            var events = probeType == BrokerBudgetProbeType.MinimumRtt
                ? _minRttProbeEvents
                : _capacityProbeEvents!;
            var capacity = probeType == BrokerBudgetProbeType.MinimumRtt
                ? MaxMinRttProbeDiagnosticEvents
                : MaxCapacityProbeDiagnosticEvents;
            if (events.Count >= capacity)
                _ = events.Dequeue();
            events.Enqueue(diagnostic);
        }
    }

    private static double Max(double[] values)
    {
        var maximum = 0.0;
        for (var i = 0; i < values.Length; i++)
            maximum = Math.Max(maximum, values[i]);

        return maximum;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double UpdateEwma(double current, double sample, double weight)
        => current == 0 ? sample : current + weight * (sample - current);

    private static long Max(long[] values)
    {
        var maximum = 0L;
        for (var i = 0; i < values.Length; i++)
            maximum = Math.Max(maximum, values[i]);

        return maximum;
    }

    private void RecomputeBudget(long nowTicks)
    {
        var minRttProbeActive = _minRttProbeUntilTimestamp != 0;
        var admissionPressureActive = _admissionPressureActive;
        var postProbeMinRttSeconds = _minRttSeconds;
        if (minRttProbeActive && _minRttProbeMinimumSeconds != double.MaxValue)
            postProbeMinRttSeconds = _minRttProbeMinimumSeconds;
        var computedNormalBudget = ComputeBudget(
            minRttProbeActive: false,
            capacityProbeActive: false,
            admissionPressureActive,
            postProbeMinRttSeconds);
        var normalBudget = ApplyNormalBudgetDecayHysteresis(computedNormalBudget, nowTicks);
        var probeBudget = ComputeBudget(
            minRttProbeActive: false,
            capacityProbeActive: true,
            admissionPressureActive,
            postProbeMinRttSeconds);
        probeBudget = Math.Max(normalBudget, probeBudget);
        Volatile.Write(ref _budgetAfterMinRttProbeBytes, normalBudget);
        Volatile.Write(ref _probeBudgetAfterMinRttProbeBytes, probeBudget);

        long budget;
        if (minRttProbeActive)
        {
            budget = ComputeBudget(
                minRttProbeActive: true,
                capacityProbeActive: false,
                admissionPressureActive,
                _minRttSeconds);
        }
        else
        {
            budget = _capacityProbeActive ? probeBudget : normalBudget;
        }

        Volatile.Write(ref _budgetBytes, budget);
    }

    private long ApplyNormalBudgetDecayHysteresis(long computedBudget, long nowTicks)
    {
        // Smooth every downward change. Capping decay at 10%/s prevents the short
        // estimator window (including the first cold-start sample) from draining the
        // budget faster than a loaded pipeline can demonstrate recovery.
        var elapsedSeconds = _lastBudgetUpdateTimestamp == 0
            ? 0
            : Math.Max(0, (double)(nowTicks - _lastBudgetUpdateTimestamp) / Stopwatch.Frequency);
        var budget = BoundBudgetDecay(
            _lastNormalBudgetBytes,
            computedBudget,
            elapsedSeconds);
        budget = Math.Min(budget, _capBytes);
        _lastNormalBudgetBytes = budget;
        _lastBudgetUpdateTimestamp = nowTicks;
        return budget;
    }

    internal static long BoundBudgetDecay(
        long previousBudget,
        long computedBudget,
        double elapsedSeconds)
    {
        if (computedBudget >= previousBudget)
            return computedBudget;

        var decayFraction = Math.Min(
            1.0,
            MaximumBudgetDecayPerSecond * Math.Max(0, elapsedSeconds));
        var minimumBudget = (long)Math.Ceiling(previousBudget * (1.0 - decayFraction));
        return Math.Max(computedBudget, minimumBudget);
    }

    private long ComputeBudget(
        bool minRttProbeActive,
        bool capacityProbeActive,
        bool admissionPressureActive,
        double minRttSeconds)
    {
        long budget;
        double rateBudgetBytes = 0;
        var effectiveMaxRate = GetEffectiveMaxRate();
        if (effectiveMaxRate == 0)
        {
            // Cold start: no drain estimate yet — keep today's semantics (cap) so short-lived
            // producers and tests never see admission throttling.
            budget = _capBytes;
        }
        else
        {
            var latencyGovernedTargetSeconds = _targetSeconds * _latencyBudgetScale;
            // The serving EWMA and the minimum RTT are queue-corrected at sample intake
            // (see OnAcked), so neither re-inflates from queueing this budget admitted;
            // the clamp below is a residual guard for correction error.
            var loadAwareRttSeconds = _hasLoadedServingSample
                ? Math.Max(
                    minRttSeconds,
                    Math.Min(_servingRttEwmaSeconds, ServingRttClampMultiplier * minRttSeconds))
                : minRttSeconds;
            var rttSafetyHorizonSeconds = minRttProbeActive
                ? MinRttProbeSafetyMultiplier * minRttSeconds
                : Math.Max(
                    RttSafetyMultiplier * loadAwareRttSeconds,
                    MinimumNormalHorizonSeconds);
            var latencyCapSeconds = Math.Max(latencyGovernedTargetSeconds, minRttSeconds);
            var horizonSeconds = _hasMinRttSample
                ? Math.Min(rttSafetyHorizonSeconds, latencyCapSeconds)
                : latencyGovernedTargetSeconds;

            var hasProvenAdditionalDepth =
                _provenPipelineRequestQuanta > MinimumPipelineRequestQuanta;
            if (!minRttProbeActive
                && _requestSizeEwmaBytes > 0
                && (admissionPressureActive || hasProvenAdditionalDepth))
            {
                // Admission is charged in batch/request-sized quanta, so a sub-request BDP
                // cannot expose additional capacity even when the byte-rate estimator is
                // accurate. Recent blocked demand enables a small acknowledgement pipeline;
                // the latency cap bounds it. General capacity discovery remains the job of
                // the periodic 25% probes below — this floor only crosses coarse request
                // granularity that a smaller byte probe cannot expose. Successful capacity
                // probes persist higher proven quanta here; the latency cap still supplies
                // the downward bound when that deeper flight starts queueing.
                var requestPipelineHorizonSeconds =
                    _provenPipelineRequestQuanta * _requestSizeEwmaBytes / effectiveMaxRate;
                horizonSeconds = Math.Max(
                    horizonSeconds,
                    Math.Min(requestPipelineHorizonSeconds, latencyCapSeconds));
            }

            rateBudgetBytes = effectiveMaxRate * horizonSeconds;
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
                occupancyFloor = Math.Min(occupancyFloor, (long)rateBudgetBytes);
            }

            budget = Math.Max(budget, occupancyFloor);
        }

        budget = Math.Clamp(budget, _floorBytes, _capBytes);

        return budget;
    }
}
