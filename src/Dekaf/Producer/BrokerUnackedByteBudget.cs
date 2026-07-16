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
/// Per-broker logical-byte congestion window and admission gate.
/// <para>
/// A partition batch acquires fixed-size byte leases and records consume the credit locally.
/// Sealing refunds unused credit and transfers the exact logical batch bytes through
/// acknowledgement. Compressed bytes remain a separate socket-pipeline concern in
/// <see cref="BrokerSender"/>.
/// </para>
/// <para>
/// The batch gate is multi-writer and amortizes compare/exchange reservations across many
/// records. The adaptive controller is single-writer: only the owning broker response loop
/// calls <see cref="OnAcked"/>,
/// <see cref="CompleteAckedPass"/>, and <see cref="SetCap"/>. It publishes one byte limit and
/// generation for appenders to read.
/// </para>
/// </summary>
internal sealed class BrokerUnackedByteBudget
{
    internal readonly record struct DeliverySnapshot(
        long SendTimestamp,
        bool AppLimited,
        long OldestBatchTimestamp,
        long UnackedBytesAtSend,
        long AdmissionGeneration);

    internal const int CapBatchMultiplier = 32;
    internal const int AdmissionBlockHistogramBucketCount = 24;

    private const int HistogramBucketCount = 16;
    private const int RequestSizeHistogramShift = 8;
    private const int MaxProbeDiagnosticEvents = 4_096;

    private readonly BrokerWindowController _controller;
    private readonly long[] _requestSizeLog2Histogram = new long[HistogramBucketCount];
    private readonly long[] _requestRttMicrosLog2Histogram = new long[HistogramBucketCount];
    private readonly long[]? _admissionBlockMicrosLog2Histogram;
    private readonly object? _probeDiagnosticsLock;
    private readonly Queue<BrokerBudgetProbeEvent>? _probeEvents;

    private long _unackedBytes;
    private long _budgetBytes;
    private long _generation;
    private long _admissionBlockEvents;
    private long _admissionBlockedSinceTimestamp;
    private long _pendingOutstandingPeakBytes;
    private long _minimumRttMicros;
    private long _maxRateBytesPerSecond;
    private long _deliveryLatencyEwmaMicros;
    private long _capacityProbeSuccessCount;
    private long _capacityProbeFailureCount;
    private long _accountingUnderflowCount;
    private long _pipelineBatchCount;
    private long _admissionFlushClaim;
    private long _nextAdmissionFlushClaim;
    private long _probeStartedTimestamp;
    private double _latencyBudgetScale = 1;

    public BrokerUnackedByteBudget(
        double targetSeconds,
        long floorBytes,
        long initialCapBytes,
        double lingerSeconds = 0,
        bool enableDiagnostics = false,
        long initialRequestBytes = 0)
    {
        _ = lingerSeconds;
        var floor = Math.Max(1, floorBytes);
        var cap = Math.Max(floor, initialCapBytes);
        if (initialRequestBytes <= 0)
            initialRequestBytes = Math.Clamp(64 * 1024, floor, cap);

        _controller = new BrokerWindowController(
            targetSeconds,
            floor,
            cap,
            initialRequestBytes);
        Publish(_controller.WindowBytes, _controller.Generation);

        if (enableDiagnostics)
        {
            _admissionBlockMicrosLog2Histogram = new long[AdmissionBlockHistogramBucketCount];
            _probeDiagnosticsLock = new object();
            _probeEvents = new Queue<BrokerBudgetProbeEvent>();
        }
    }

    internal static long ComputeCap(int batchSize, int connectionCount) =>
        (long)Math.Max(1, batchSize) * CapBatchMultiplier * Math.Max(1, connectionCount);

    internal long UnackedBytes => Volatile.Read(ref _unackedBytes);
    internal long BudgetBytes => Volatile.Read(ref _budgetBytes);
    internal long CurrentGeneration => Volatile.Read(ref _generation);
    internal BrokerWindowPhase Phase => _controller.Phase;
    internal long AdmissionBlockEvents => Volatile.Read(ref _admissionBlockEvents);
    internal long MinimumRttMicros => Volatile.Read(ref _minimumRttMicros);
    internal long MaxRateBytesPerSecond => Volatile.Read(ref _maxRateBytesPerSecond);
    internal long CapacityProbeSuccessCount => Volatile.Read(ref _capacityProbeSuccessCount);
    internal long CapacityProbeFailureCount => Volatile.Read(ref _capacityProbeFailureCount);
    internal long AccountingUnderflowCount => Volatile.Read(ref _accountingUnderflowCount);
    internal long PipelineBatchCount => Volatile.Read(ref _pipelineBatchCount);
    internal long DeliveryLatencyEwmaMicros => Volatile.Read(ref _deliveryLatencyEwmaMicros);
    internal double LatencyBudgetScale => Volatile.Read(ref _latencyBudgetScale);

    /// <summary>
    /// Atomically reserves logical bytes. One reservation larger than the current window may
    /// exceed it alongside at most one window of existing traffic. Once admitted, its own
    /// charge keeps every later reservation blocked until enough bytes are acknowledged. This
    /// bounded overshoot prevents a valid large record from starving behind continuous traffic.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryReserve(long bytes, out long generation)
    {
        if (bytes <= 0)
        {
            generation = CurrentGeneration;
            return true;
        }

        while (true)
        {
            var generationBefore = Volatile.Read(ref _generation);
            var used = Volatile.Read(ref _unackedBytes);
            var limit = Volatile.Read(ref _budgetBytes);
            var oversized = bytes > limit;
            if (oversized ? used > limit : used > limit - bytes)
            {
                generation = 0;
                return false;
            }

            if (bytes > long.MaxValue - used)
            {
                generation = 0;
                return false;
            }

            if (Interlocked.CompareExchange(ref _unackedBytes, used + bytes, used) != used)
                continue;

            var generationAfter = Volatile.Read(ref _generation);
            generation = generationBefore == generationAfter ? generationAfter : 0;
            return true;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsOverBudget() =>
        Volatile.Read(ref _unackedBytes) >= Volatile.Read(ref _budgetBytes);

    /// <summary>
    /// Unconditional ownership transfer used only when a live batch is rerouted. New appends
    /// must use <see cref="TryReserve"/>; already-admitted data cannot be rejected mid-flight.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Charge(long bytes)
    {
        if (bytes > 0)
            Interlocked.Add(ref _unackedBytes, bytes);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Release(long bytes)
    {
        if (bytes <= 0)
            return;

        var remaining = Interlocked.Add(ref _unackedBytes, -bytes);
        if (remaining < 0)
        {
            Interlocked.Increment(ref _accountingUnderflowCount);
            remaining = ClampNegativeAccountingToZero(remaining);
        }

        if (_admissionBlockMicrosLog2Histogram is not null
            && remaining < Volatile.Read(ref _budgetBytes))
        {
            RecordAdmissionAvailable(Stopwatch.GetTimestamp());
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private long ClampNegativeAccountingToZero(long observed)
    {
        while (observed < 0)
        {
            var prior = Interlocked.CompareExchange(ref _unackedBytes, 0, observed);
            if (prior == observed)
                return 0;
            observed = prior;
        }

        return observed;
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

    /// <summary>
    /// Claims the right to force one partial batch into the broker pipeline. A claim is only
    /// needed when all charged bytes are held by open batches; an existing pipeline batch can
    /// already acknowledge bytes and wake blocked appenders without fragmenting every partition.
    /// </summary>
    internal bool TryClaimAdmissionFlush(out long claim)
    {
        claim = 0;
        if (Volatile.Read(ref _pipelineBatchCount) != 0)
            return false;

        var requestedClaim = Interlocked.Increment(ref _nextAdmissionFlushClaim);
        if (requestedClaim == 0)
            requestedClaim = Interlocked.Increment(ref _nextAdmissionFlushClaim);

        if (Interlocked.CompareExchange(ref _admissionFlushClaim, requestedClaim, 0) != 0)
            return false;

        // Close the race with a batch entering the pipeline after the first count read.
        if (Volatile.Read(ref _pipelineBatchCount) == 0)
        {
            claim = requestedClaim;
            return true;
        }

        ReleaseAdmissionFlushClaim(requestedClaim);
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool IsAdmissionFlushClaimActive(long claim) =>
        claim != 0
        && Volatile.Read(ref _admissionFlushClaim) == claim
        && Volatile.Read(ref _pipelineBatchCount) == 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void ReleaseAdmissionFlushClaim(long claim)
    {
        if (claim != 0)
            _ = Interlocked.CompareExchange(ref _admissionFlushClaim, 0, claim);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void RecordPipelineBatchEntered()
    {
        Interlocked.Increment(ref _pipelineBatchCount);
        // Pipeline progress supersedes a forced-flush claim. Its owner verifies the claim
        // before sealing and will keep the partial batch open when this cancellation wins.
        Interlocked.Exchange(ref _admissionFlushClaim, 0);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void RecordPipelineBatchExited()
    {
        var remaining = Interlocked.Decrement(ref _pipelineBatchCount);
        Debug.Assert(remaining >= 0, "Broker pipeline batch count went negative.");
    }

    public void ObserveWrittenUnackedBytes(long bytes)
    {
        var observed = Volatile.Read(ref _pendingOutstandingPeakBytes);
        while (bytes > observed)
        {
            var prior = Interlocked.CompareExchange(
                ref _pendingOutstandingPeakBytes,
                bytes,
                observed);
            if (prior == observed)
                return;

            observed = prior;
        }
    }

    public void SetCap(long capBytes, long nowTicks)
    {
        _ = nowTicks;
        var decision = _controller.SetCap(capBytes);
        Publish(decision.WindowBytes, decision.Generation);
    }

    internal DeliverySnapshot SnapshotDelivery(
        long sendTimestamp,
        bool appLimited,
        long oldestBatchTimestamp = 0,
        long admissionGeneration = 0) =>
        new(
            sendTimestamp,
            appLimited,
            oldestBatchTimestamp,
            Volatile.Read(ref _unackedBytes),
            admissionGeneration);

    /// <summary>
    /// Records one fully successful request using logical bytes. Controller publication is
    /// batched by <see cref="CompleteAckedPass"/> to keep acknowledgement cost constant.
    /// </summary>
    public void OnAcked(long ackedBytes, DeliverySnapshot sendSnapshot, long nowTicks)
    {
        var rttTicks = nowTicks - sendSnapshot.SendTimestamp;
        if (ackedBytes <= 0 || rttTicks <= 0)
            return;

        var sealToSendTicks = sendSnapshot.OldestBatchTimestamp > 0
            && sendSnapshot.OldestBatchTimestamp < sendSnapshot.SendTimestamp
                ? sendSnapshot.SendTimestamp - sendSnapshot.OldestBatchTimestamp
                : 0;
        _controller.RecordAcknowledgement(
            ackedBytes,
            rttTicks,
            sealToSendTicks,
            sendSnapshot.AppLimited,
            sendSnapshot.AdmissionGeneration,
            nowTicks);
        RecordRequestHistograms(ackedBytes, rttTicks);
    }

    public void CompleteAckedPass(long nowTicks)
    {
        var outstandingPeak = Math.Max(
            Interlocked.Exchange(ref _pendingOutstandingPeakBytes, 0),
            Volatile.Read(ref _unackedBytes));
        var decision = _controller.CompleteInterval(
            AdmissionBlockEvents,
            outstandingPeak,
            nowTicks);
        PublishDecision(decision, nowTicks);
        if (!IsOverBudget())
            RecordAdmissionAvailable(nowTicks);
    }

    internal long[] CopyRequestSizeHistogram() => CopyHistogram(_requestSizeLog2Histogram);
    internal long[] CopyRequestRttMicrosHistogram() => CopyHistogram(_requestRttMicrosLog2Histogram);

    internal long[] CopyAdmissionBlockMicrosHistogram() =>
        _admissionBlockMicrosLog2Histogram is { } histogram
            ? CopyHistogram(histogram)
            : [];

    internal BrokerBudgetProbeEvent[] CopyProbeEvents()
    {
        if (_probeEvents is null)
            return [];

        lock (_probeDiagnosticsLock!)
            return [.. _probeEvents];
    }

    internal void ResetDiagnostics(long nowTicks = 0)
    {
        ClearHistogram(_requestSizeLog2Histogram);
        ClearHistogram(_requestRttMicrosLog2Histogram);
        if (_admissionBlockMicrosLog2Histogram is { } histogram)
            ClearHistogram(histogram);

        if (_probeEvents is not null)
        {
            lock (_probeDiagnosticsLock!)
                _probeEvents.Clear();
        }

        if (nowTicks == 0)
            nowTicks = Stopwatch.GetTimestamp();
        if (IsOverBudget())
            Volatile.Write(ref _admissionBlockedSinceTimestamp, nowTicks);
        else
            Volatile.Write(ref _admissionBlockedSinceTimestamp, 0);
    }

    internal long GetCurrentAdmissionBlockDurationMicros(long nowTicks)
    {
        var startedAt = Volatile.Read(ref _admissionBlockedSinceTimestamp);
        return startedAt == 0 || nowTicks <= startedAt
            ? 0
            : (long)((nowTicks - startedAt) * 1_000_000.0 / Stopwatch.Frequency);
    }

    private void PublishDecision(BrokerWindowDecision decision, long nowTicks)
    {
        Publish(decision.WindowBytes, decision.Generation);
        Volatile.Write(
            ref _minimumRttMicros,
            _controller.MinimumRttTicks * 1_000_000 / Stopwatch.Frequency);
        Volatile.Write(ref _maxRateBytesPerSecond, _controller.MaximumGoodputBytesPerSecond);
        Volatile.Write(
            ref _deliveryLatencyEwmaMicros,
            _controller.ControlledDelayEwmaTicks * 1_000_000 / Stopwatch.Frequency);
        Volatile.Write(ref _latencyBudgetScale, _controller.WindowScale);
        Volatile.Write(ref _capacityProbeSuccessCount, _controller.CapacityProbeSuccessCount);
        Volatile.Write(ref _capacityProbeFailureCount, _controller.CapacityProbeFailureCount);

        if (decision.ProbeType is { } probeType && decision.ProbeOutcome is { } outcome)
            RecordProbeEvent(probeType, outcome, nowTicks);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Publish(long budgetBytes, long generation)
    {
        Volatile.Write(ref _budgetBytes, budgetBytes);
        Volatile.Write(ref _generation, generation);
    }

    private void RecordProbeEvent(
        BrokerBudgetProbeType probeType,
        BrokerBudgetProbeOutcome outcome,
        long nowTicks)
    {
        if (_probeEvents is null)
            return;

        long durationMilliseconds = 0;
        if (outcome == BrokerBudgetProbeOutcome.Started)
        {
            _probeStartedTimestamp = nowTicks;
        }
        else if (_probeStartedTimestamp > 0 && nowTicks > _probeStartedTimestamp)
        {
            durationMilliseconds = (long)((nowTicks - _probeStartedTimestamp)
                * 1_000.0 / Stopwatch.Frequency);
            _probeStartedTimestamp = 0;
        }

        var probeEvent = new BrokerBudgetProbeEvent(
            probeType,
            outcome,
            DateTimeOffset.UtcNow,
            durationMilliseconds,
            BudgetBytes,
            UnackedBytes);
        lock (_probeDiagnosticsLock!)
        {
            if (_probeEvents.Count >= MaxProbeDiagnosticEvents)
                _ = _probeEvents.Dequeue();
            _probeEvents.Enqueue(probeEvent);
        }
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void RecordRequestHistograms(long ackedBytes, long rttTicks)
    {
        var sizeBucket = Math.Clamp(
            BitOperations.Log2((ulong)ackedBytes | 1) - RequestSizeHistogramShift,
            0,
            HistogramBucketCount - 1);
        _requestSizeLog2Histogram[sizeBucket]++;

        var rttMicros = (ulong)Math.Max(
            1,
            rttTicks * 1_000_000.0 / Stopwatch.Frequency);
        var rttBucket = Math.Min(
            BitOperations.Log2(rttMicros),
            HistogramBucketCount - 1);
        _requestRttMicrosLog2Histogram[rttBucket]++;
    }

    private static long[] CopyHistogram(long[] source)
    {
        var copy = new long[source.Length];
        for (var i = 0; i < source.Length; i++)
            copy[i] = Volatile.Read(ref source[i]);
        return copy;
    }

    private static void ClearHistogram(long[] histogram)
    {
        for (var i = 0; i < histogram.Length; i++)
            Interlocked.Exchange(ref histogram[i], 0);
    }
}
