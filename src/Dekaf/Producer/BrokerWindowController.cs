using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace Dekaf.Producer;

/// <summary>
/// Operating mode of the broker congestion window. The controller has one writer: the
/// owning <see cref="BrokerSender"/> response loop.
/// </summary>
internal enum BrokerWindowPhase : byte
{
    Steady,
    ProbeUp,
    ProbeDown
}

internal readonly record struct BrokerWindowDecision(
    long WindowBytes,
    long Generation,
    BrokerWindowPhase Phase,
    BrokerBudgetProbeType? ProbeType = null,
    BrokerBudgetProbeOutcome? ProbeOutcome = null);

/// <summary>
/// Pure, allocation-free knee-seeking byte-window controller. Its quantum is the configured
/// batch capacity and never changes with fragmented request sizes. It searches for the
/// smallest whole-quantum window that preserves broker goodput.
/// When the latency ceiling is enabled, every window value is additionally bounded by
/// <c>delivery-latency target × windowed-maximum measured goodput</c>: standing unacked
/// bytes divided by drain rate is the queueing delay each delivery wears, so goodput probes
/// may only explore beneath the target's worth of measured drain. The same bound, fed by an
/// ack-clocked cumulative rate before the first control epoch completes, paces the
/// cold-start window open instead of releasing the optimistic initial size in one step.
/// </summary>
internal sealed class BrokerWindowController
{
    private enum CapacityProbeStage : byte
    {
        TreatmentBefore,
        Baseline,
        TreatmentAfter
    }

    private const int SizeBucketCount = 16;
    private const int SizeBucketShift = 8;
    // Two quanta keep the acknowledgement clock pipelined (one request on the wire while the
    // next seals) without encoding any topology's knee into the floor. The knee itself is
    // discovered by paired probes underneath the latency ceiling: an eight-quantum floor
    // matched a one-broker drain rate but forced ~3x the delivery-latency target of standing
    // queue per broker on three-broker topologies (#2109). Applies only while the latency
    // ceiling is armed — probing below the old floor is safe only with the ceiling's
    // rate-derived bound above it.
    private const int MinimumPipelineQuanta = 2;
    // Ceiling-disabled controllers (explicit cap override, Acks.None) keep the original
    // eight-quantum floor: for them the down-probe goodput guard is the only other
    // protection against walking a fast pipe below its knee, so their semantics are
    // deliberately unchanged.
    private const int UnceiledMinimumPipelineQuanta = 8;
    // Open partition batches also hold bounded lease slack, so start above the expected knee
    // and search downward. This avoids throughput-biased learning from a starved startup.
    // With the latency ceiling enabled the optimistic start is additionally bounded by
    // measured drain rate, so it cannot flood a cold broker.
    private const int InitialRequestQuanta = 16;
    // The latency ceiling tracks the maximum epoch goodput over this many completed control
    // epochs (~30s of loaded time). A windowed maximum ignores transient dips without letting
    // a stale one-time peak size the window forever.
    private const int GoodputRingEpochs = 60;
    // Treatments are deliberately sparse and settled: frequent sub-second window changes
    // caused self-inflicted admission stalls that looked like congestion. Each experiment is
    // a B-A-B sandwich (3s candidate, 6s baseline, 3s candidate after a 1s washout at each
    // transition), which cancels a linear runner trend instead of comparing one temporal
    // slice with one 1.5-second vote.
    private const int ProbeIntervalEpochs = 60;
    private const int ProbeSettleEpochs = 2;
    private const int OuterProbeEvaluationEpochs = 6;
    private const int BaselineProbeEvaluationEpochs = 12;
    // A treatment must not remain active forever if coalescing briefly mixes generations.
    // Four unqualified epochs bound the experiment to roughly two extra seconds.
    private const int MaximumUnqualifiedProbeEpochs = 4;
    private const double UpProbeMultiplier = 1.125;
    private const double DownProbeMultiplier = 0.875;
    private const double UpProbeGoodputThreshold = 1.03;
    private const double DownProbeGoodputThreshold = 0.99;
    private const double DelayGrowthTolerance = 1.25;
    private const double EwmaWeight = 0.125;

    private static readonly long ControlIntervalTicks = Math.Max(1, Stopwatch.Frequency / 2);
    private static readonly long MinimumRttRefreshTicks = 60 * Stopwatch.Frequency;

    private readonly long _floorBytes;
    private readonly long _requestQuantumBytes;
    private readonly long _targetDelayTicks;
    private readonly bool _latencyCeilingEnabled;
    private readonly long[] _minimumRttBySizeTicks = new long[SizeBucketCount];
    private readonly long[] _minimumRttTimestamps = new long[SizeBucketCount];
    private readonly double[] _epochGoodputRing = new double[GoodputRingEpochs];

    private long _capBytes;
    private long _windowBytes;
    private long _generation = 1;
    private BrokerWindowPhase _phase = BrokerWindowPhase.Steady;
    private long _latencyCeilingBytes = long.MaxValue;
    private int _epochGoodputIndex;
    private bool _coldStartRampComplete;
    private long _rampStartTimestamp;
    private long _rampAckedBytes;

    private long _epochStartedAt;
    private long _epochBytes;
    private long _epochCurrentGenerationBytes;
    private double _epochCurrentGenerationDelayByteTicks;
    private bool _epochLoaded;

    private double _controlledDelayEwmaTicks;
    private double _maximumGoodputBytesPerSecond;
    private long _minimumRttTicks;
    private long _lastAdmissionBlockEvents;

    private int _epochsUntilProbe = ProbeIntervalEpochs;
    private bool _probeDownNext = true;
    private long _probeBaselineWindow;
    private long _probeCandidateWindow;
    private CapacityProbeStage _probeStage;
    private double _probeTreatmentBeforeGoodput;
    private double _probeTreatmentBeforeDelayTicks;
    private double _probeBaselineGoodput;
    private double _probeBaselineDelayTicks;
    private int _probeEvaluationCount;
    private int _probeSettleEpochsRemaining;
    private int _unqualifiedProbeEpochs;
    private double _probeGoodputTotal;
    private double _probeDelayTotalTicks;

    internal BrokerWindowController(
        double targetSeconds,
        long floorBytes,
        long capBytes,
        long initialRequestBytes,
        bool latencyCeilingEnabled = false)
    {
        _floorBytes = Math.Max(1, floorBytes);
        _capBytes = Math.Max(_floorBytes, capBytes);
        _targetDelayTicks = Math.Max(1, (long)(targetSeconds * Stopwatch.Frequency));
        _requestQuantumBytes = Math.Max(_floorBytes, initialRequestBytes);
        _latencyCeilingEnabled = latencyCeilingEnabled;
        // Ceiling-enabled controllers start pessimistic: the window opens as the cold-start
        // ramp measures the broker's actual drain rate, so the first acknowledgement cannot
        // release an unlearned multi-quantum burst into a cold broker (#2109).
        if (latencyCeilingEnabled)
            _latencyCeilingBytes = MinimumWindowBytes;
        _windowBytes = Math.Clamp(
            SaturatingMultiply(_requestQuantumBytes, InitialRequestQuanta),
            MinimumWindowBytes,
            EffectiveCapBytes);
    }

    internal long WindowBytes => _windowBytes;
    internal long Generation => _generation;
    internal BrokerWindowPhase Phase => _phase;
    internal long MinimumRttTicks => _minimumRttTicks;
    internal long MaximumGoodputBytesPerSecond => (long)_maximumGoodputBytesPerSecond;
    internal long ControlledDelayEwmaTicks => (long)_controlledDelayEwmaTicks;
    internal long RequestQuantumBytes => _requestQuantumBytes;
    internal long LatencyCeilingBytes => _latencyCeilingEnabled ? _latencyCeilingBytes : 0;
    internal double WindowScale => _capBytes == 0 ? 1 : (double)_windowBytes / _capBytes;
    internal long CapacityProbeSuccessCount { get; private set; }
    internal long CapacityProbeFailureCount { get; private set; }

    internal BrokerWindowDecision SetCap(long capBytes)
    {
        _capBytes = Math.Max(_floorBytes, capBytes);
        ApplyEffectiveCap();
        return CurrentDecision();
    }

    /// <summary>
    /// Adds one successful acknowledgement. Bytes are logical, uncompressed record bytes;
    /// encoded wire bytes are deliberately excluded from congestion-window accounting.
    /// </summary>
    internal void RecordAcknowledgement(
        long logicalBytes,
        long rttTicks,
        long sealToSendTicks,
        bool appLimited,
        long admissionGeneration,
        long nowTicks)
    {
        if (logicalBytes <= 0 || rttTicks <= 0)
            return;

        if (_epochStartedAt == 0)
            _epochStartedAt = nowTicks;

        if (_latencyCeilingEnabled && !_coldStartRampComplete)
            AdvanceColdStartRamp(logicalBytes, rttTicks, nowTicks);

        UpdateMinimumRtt(logicalBytes, rttTicks, nowTicks);

        var controlledDelayTicks = Math.Max(0, sealToSendTicks)
            + Math.Max(0, rttTicks - GetMinimumRtt(logicalBytes));
        _controlledDelayEwmaTicks = UpdateEwma(
            _controlledDelayEwmaTicks,
            controlledDelayTicks);

        _epochBytes = SaturatingAdd(_epochBytes, logicalBytes);
        _epochLoaded |= !appLimited;
        if (admissionGeneration == _generation)
        {
            _epochCurrentGenerationBytes = SaturatingAdd(
                _epochCurrentGenerationBytes,
                logicalBytes);
            _epochCurrentGenerationDelayByteTicks += logicalBytes * (double)controlledDelayTicks;
        }
    }

    internal BrokerWindowDecision CompleteInterval(
        long admissionBlockEvents,
        long observedOutstandingBytes,
        long nowTicks)
    {
        if (_epochStartedAt == 0)
        {
            _epochStartedAt = nowTicks;
            _lastAdmissionBlockEvents = admissionBlockEvents;
            return CurrentDecision();
        }

        var elapsedTicks = nowTicks - _epochStartedAt;
        if (elapsedTicks < ControlIntervalTicks)
            return CurrentDecision();

        var goodput = _epochBytes * (double)Stopwatch.Frequency / elapsedTicks;
        if (goodput > 0)
            _maximumGoodputBytesPerSecond = Math.Max(_maximumGoodputBytesPerSecond, goodput);

        var generationQualified = _epochCurrentGenerationBytes > 0
            && _epochCurrentGenerationBytes >= _epochBytes / 2 + _epochBytes % 2;
        var controlledDelayTicks = generationQualified
            ? _epochCurrentGenerationDelayByteTicks / _epochCurrentGenerationBytes
            : _controlledDelayEwmaTicks;
        var admissionPressure = admissionBlockEvents != _lastAdmissionBlockEvents;
        // Occupancy is a demand signal, not a floor. A floor would make the lower-window
        // treatment impossible to test; the 99% goodput guard below reverts a harmful probe
        // after the paired experiment instead.
        var occupancyPressure = observedOutstandingBytes
            >= _windowBytes - _windowBytes / 4;
        var demand = admissionPressure || occupancyPressure || _epochLoaded;

        _lastAdmissionBlockEvents = admissionBlockEvents;
        ResetEpoch(nowTicks);

        if (goodput <= 0)
            return CurrentDecision();

        if (_latencyCeilingEnabled)
            CompleteLatencyCeilingEpoch(goodput);

        if (!generationQualified)
        {
            if (_phase == BrokerWindowPhase.Steady
                || ++_unqualifiedProbeEpochs < MaximumUnqualifiedProbeEpochs)
            {
                return CurrentDecision();
            }

            return AbortUnqualifiedProbe();
        }

        _unqualifiedProbeEpochs = 0;

        return _phase switch
        {
            BrokerWindowPhase.Steady => UpdateSteady(demand),
            BrokerWindowPhase.ProbeUp or BrokerWindowPhase.ProbeDown =>
                EvaluateCapacityProbe(goodput, controlledDelayTicks),
            _ => CurrentDecision()
        };
    }

    private BrokerWindowDecision UpdateSteady(bool demand)
    {
        if (!demand)
            return CurrentDecision();

        if (--_epochsUntilProbe > 0)
            return CurrentDecision();

        if (_probeDownNext && _windowBytes > MinimumWindowBytes)
            return StartDownProbe();

        var requestedWindow = Math.Min(
            EffectiveCapBytes,
            Math.Max(
                _windowBytes + _requestQuantumBytes,
                (long)Math.Ceiling(_windowBytes * UpProbeMultiplier)));
        return StartCapacityProbe(
            BrokerWindowPhase.ProbeUp,
            requestedWindow);
    }

    private BrokerWindowDecision StartDownProbe()
    {
        var requestedWindow = Math.Max(
            MinimumWindowBytes,
            (long)Math.Floor(_windowBytes * DownProbeMultiplier));
        return StartCapacityProbe(
            BrokerWindowPhase.ProbeDown,
            requestedWindow);
    }

    private BrokerWindowDecision StartCapacityProbe(
        BrokerWindowPhase phase,
        long requestedWindow)
    {
        _probeBaselineWindow = _windowBytes;
        _probeCandidateWindow = NormalizeWindow(requestedWindow);
        if (_probeCandidateWindow == _probeBaselineWindow)
        {
            _probeDownNext = phase == BrokerWindowPhase.ProbeUp;
            _epochsUntilProbe = ProbeIntervalEpochs;
            return CurrentDecision();
        }

        _phase = phase;
        _probeStage = CapacityProbeStage.TreatmentBefore;
        _unqualifiedProbeEpochs = 0;
        ResetProbeExperiment();
        _probeSettleEpochsRemaining = ProbeSettleEpochs;
        SetWindow(_probeCandidateWindow);

        return CurrentDecision(
            BrokerBudgetProbeType.Capacity,
            BrokerBudgetProbeOutcome.Started);
    }

    private BrokerWindowDecision EvaluateCapacityProbe(
        double goodput,
        double controlledDelayTicks)
    {
        if (_probeSettleEpochsRemaining > 0)
        {
            _probeSettleEpochsRemaining--;
            return CurrentDecision();
        }

        var requiredEpochs = _probeStage == CapacityProbeStage.Baseline
            ? BaselineProbeEvaluationEpochs
            : OuterProbeEvaluationEpochs;
        if (!TryCompleteProbeEvaluation(
                goodput,
                controlledDelayTicks,
                requiredEpochs,
                out var averageGoodput,
                out var averageControlledDelayTicks))
        {
            return CurrentDecision();
        }

        if (_probeStage == CapacityProbeStage.TreatmentBefore)
        {
            _probeTreatmentBeforeGoodput = averageGoodput;
            _probeTreatmentBeforeDelayTicks = averageControlledDelayTicks;
            _probeStage = CapacityProbeStage.Baseline;
            _probeSettleEpochsRemaining = ProbeSettleEpochs;
            SetWindow(_probeBaselineWindow);
            return CurrentDecision();
        }

        if (_probeStage == CapacityProbeStage.Baseline)
        {
            _probeBaselineGoodput = averageGoodput;
            _probeBaselineDelayTicks = averageControlledDelayTicks;
            _probeStage = CapacityProbeStage.TreatmentAfter;
            _probeSettleEpochsRemaining = ProbeSettleEpochs;
            SetWindow(_probeCandidateWindow);
            return CurrentDecision();
        }

        var treatmentGoodput = (_probeTreatmentBeforeGoodput + averageGoodput) / 2;
        var treatmentDelayTicks =
            (_probeTreatmentBeforeDelayTicks + averageControlledDelayTicks) / 2;
        var probingUp = _phase == BrokerWindowPhase.ProbeUp;
        var delayLimit = Math.Max(
            _targetDelayTicks,
            _probeBaselineDelayTicks * DelayGrowthTolerance);
        var goodputThreshold = probingUp
            ? UpProbeGoodputThreshold
            : DownProbeGoodputThreshold;
        // Seal-to-send plus network queueing omits pre-seal admission and batch-fill time, so
        // it cannot veto a lower window. It remains a conservative guard against growing a
        // window that raises the delay component the controller can observe.
        var succeeded = treatmentGoodput >= _probeBaselineGoodput * goodputThreshold
            && (!probingUp || treatmentDelayTicks <= delayLimit);
        return CompleteCapacityProbe(succeeded);
    }

    private BrokerWindowDecision AbortUnqualifiedProbe() =>
        CompleteCapacityProbe(succeeded: false);

    private BrokerWindowDecision CompleteCapacityProbe(bool succeeded)
    {
        var probingUp = _phase == BrokerWindowPhase.ProbeUp;
        if (succeeded)
        {
            CapacityProbeSuccessCount++;
        }
        else
        {
            CapacityProbeFailureCount++;
            SetWindow(_probeBaselineWindow);
        }

        _phase = BrokerWindowPhase.Steady;
        // Continue walking in a direction while it wins. A failed treatment reverses the
        // next probe, converging on the knee instead of oscillating unconditionally.
        _probeDownNext = probingUp ? !succeeded : succeeded;
        _epochsUntilProbe = ProbeIntervalEpochs;
        _unqualifiedProbeEpochs = 0;
        ResetProbeExperiment();
        return CurrentDecision(
            BrokerBudgetProbeType.Capacity,
            succeeded ? BrokerBudgetProbeOutcome.Succeeded : BrokerBudgetProbeOutcome.Failed);
    }

    private void ResetProbeEvaluation()
    {
        _probeEvaluationCount = 0;
        _probeGoodputTotal = 0;
        _probeDelayTotalTicks = 0;
    }

    private void ResetProbeExperiment()
    {
        _probeTreatmentBeforeGoodput = 0;
        _probeTreatmentBeforeDelayTicks = 0;
        _probeBaselineGoodput = 0;
        _probeBaselineDelayTicks = 0;
        _probeSettleEpochsRemaining = 0;
        ResetProbeEvaluation();
    }

    private bool TryCompleteProbeEvaluation(
        double goodput,
        double controlledDelayTicks,
        int requiredEpochs,
        out double averageGoodput,
        out double averageControlledDelayTicks)
    {
        _probeEvaluationCount++;
        _probeGoodputTotal += goodput;
        _probeDelayTotalTicks += controlledDelayTicks;
        if (_probeEvaluationCount < requiredEpochs)
        {
            averageGoodput = 0;
            averageControlledDelayTicks = 0;
            return false;
        }

        averageGoodput = _probeGoodputTotal / _probeEvaluationCount;
        averageControlledDelayTicks = _probeDelayTotalTicks / _probeEvaluationCount;
        ResetProbeEvaluation();
        return true;
    }

    private void UpdateMinimumRtt(long logicalBytes, long rttTicks, long nowTicks)
    {
        var bucket = GetSizeBucket(logicalBytes);
        var minimum = _minimumRttBySizeTicks[bucket];
        var expired = nowTicks - _minimumRttTimestamps[bucket] >= MinimumRttRefreshTicks;
        if (minimum == 0 || rttTicks < minimum || expired)
        {
            _minimumRttBySizeTicks[bucket] = rttTicks;
            _minimumRttTimestamps[bucket] = nowTicks;
        }

        _minimumRttTicks = GetGlobalMinimumRtt();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private long GetMinimumRtt(long logicalBytes)
    {
        var bucketMinimum = _minimumRttBySizeTicks[GetSizeBucket(logicalBytes)];
        return bucketMinimum != 0 ? bucketMinimum : _minimumRttTicks;
    }

    private long GetGlobalMinimumRtt()
    {
        var minimum = long.MaxValue;
        for (var i = 0; i < _minimumRttBySizeTicks.Length; i++)
        {
            var value = _minimumRttBySizeTicks[i];
            if (value > 0)
                minimum = Math.Min(minimum, value);
        }

        return minimum == long.MaxValue ? 0 : minimum;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetSizeBucket(long bytes) => Math.Clamp(
        BitOperations.Log2((ulong)bytes | 1) - SizeBucketShift,
        0,
        SizeBucketCount - 1);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SetWindow(long requestedBytes)
    {
        var next = NormalizeWindow(requestedBytes);
        if (next == _windowBytes)
            return;

        _windowBytes = next;
        _generation++;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private long NormalizeWindow(long requestedBytes)
    {
        var minimum = MinimumWindowBytes;
        var next = Math.Clamp(requestedBytes, minimum, EffectiveCapBytes);
        if (next > minimum && _requestQuantumBytes > 1)
        {
            next = next / _requestQuantumBytes * _requestQuantumBytes;
            next = Math.Max(next, minimum);
        }

        return next;
    }

    /// <summary>Quantum floor for the window. Every latency-ceiling write clamps the ceiling
    /// to at least this value, so the effective cap never undercuts it.</summary>
    private long MinimumWindowBytes => Math.Clamp(
        SaturatingMultiply(
            _requestQuantumBytes,
            _latencyCeilingEnabled ? MinimumPipelineQuanta : UnceiledMinimumPipelineQuanta),
        _floorBytes,
        _capBytes);

    /// <summary>Cap actually available to the window: the routing-width byte ceiling bounded
    /// by the latency ceiling (delivery-latency target × windowed-maximum measured goodput).
    /// Under open-loop saturation, standing unacked bytes divided by drain rate is the
    /// queueing delay every delivery wears, so no goodput-probed window may hold more than
    /// the target's worth of measured drain. Goodput cannot be inflated by self-queueing
    /// (unlike an RTT horizon), so this bound has no positive-feedback ratchet.</summary>
    private long EffectiveCapBytes => Math.Min(_capBytes, _latencyCeilingBytes);

    /// <summary>Clamps the window and any in-flight probe bookkeeping to the current
    /// effective cap after the cap or the latency ceiling moves.</summary>
    private void ApplyEffectiveCap()
    {
        var effectiveCap = EffectiveCapBytes;
        if (_windowBytes > effectiveCap)
        {
            _windowBytes = effectiveCap;
            _generation++;
        }
        if (_probeBaselineWindow > effectiveCap)
            _probeBaselineWindow = effectiveCap;
        if (_probeCandidateWindow > effectiveCap)
            _probeCandidateWindow = effectiveCap;
    }

    /// <summary>
    /// Keeps the pre-acknowledgement window large enough for one request wave at the current
    /// routing width. Connection scale-up before the first acknowledgement grows the wave
    /// beyond the pessimistic two-quantum start; the wave is still an unmeasured burst the
    /// cold-start gate itself bounds, so admitting it does not reintroduce the flood.
    /// No-op once any acknowledgement has been measured — the ramp owns the ceiling then.
    /// </summary>
    internal BrokerWindowDecision NoteColdStartWave(long waveBytes)
    {
        // _rampAckedBytes == 0 also implies the ramp has not completed: completing requires
        // a control epoch with goodput, which requires an acknowledgement the ramp counted.
        if (_latencyCeilingEnabled && _rampAckedBytes == 0)
            TryRaiseColdStartCeiling(Math.Min(waveBytes, _capBytes));

        return CurrentDecision();
    }

    /// <summary>
    /// Cold-start ramp: before the first completed control epoch, the ceiling grows with the
    /// cumulative acknowledged drain rate and the window follows it toward the optimistic
    /// initial size. Ack-clocked and rate-proportional — a fast broker opens the window
    /// within a few round trips, a cold broker admits only what it has demonstrably drained.
    /// Single-writer (owning send loop), so plain field updates suffice.
    /// </summary>
    private void AdvanceColdStartRamp(long logicalBytes, long rttTicks, long nowTicks)
    {
        if (_rampStartTimestamp == 0)
            _rampStartTimestamp = nowTicks - rttTicks;
        _rampAckedBytes = SaturatingAdd(_rampAckedBytes, logicalBytes);
        var elapsedTicks = Math.Max(1, nowTicks - _rampStartTimestamp);
        var rateBytesPerSecond = _rampAckedBytes * (double)Stopwatch.Frequency / elapsedTicks;
        TryRaiseColdStartCeiling(ComputeCeilingBytes(rateBytesPerSecond));
    }

    /// <summary>Raises the cold-start ceiling and re-seeks the optimistic initial window
    /// underneath it. Raise-only: pre-epoch estimates may only open the window further.</summary>
    private void TryRaiseColdStartCeiling(long ceilingBytes)
    {
        if (ceilingBytes <= _latencyCeilingBytes)
            return;

        _latencyCeilingBytes = ceilingBytes;
        SetWindow(SaturatingMultiply(_requestQuantumBytes, InitialRequestQuanta));
    }

    /// <summary>Feeds one completed epoch's goodput into the windowed-maximum ring and
    /// re-derives the latency ceiling. Ends the cold-start ramp: epoch goodput is measured
    /// over a full control interval and supersedes the ramp's cumulative estimate.
    /// A window sitting on the ceiling follows it back up when measured goodput recovers —
    /// the raise carries the same rate-times-target justification as the clamp, so recovery
    /// from a transient dip does not wait for the probe schedule. Windows the probe walk
    /// placed below the ceiling are deliberate and stay put.</summary>
    private void CompleteLatencyCeilingEpoch(double goodputBytesPerSecond)
    {
        _coldStartRampComplete = true;
        _epochGoodputRing[_epochGoodputIndex] = goodputBytesPerSecond;
        _epochGoodputIndex = (_epochGoodputIndex + 1) % GoodputRingEpochs;

        var windowedMax = 0.0;
        for (var i = 0; i < _epochGoodputRing.Length; i++)
            windowedMax = Math.Max(windowedMax, _epochGoodputRing[i]);

        var previousEffectiveCap = EffectiveCapBytes;
        _latencyCeilingBytes = ComputeCeilingBytes(windowedMax);
        if (_phase == BrokerWindowPhase.Steady
            && _windowBytes >= previousEffectiveCap
            && EffectiveCapBytes > previousEffectiveCap)
        {
            SetWindow(EffectiveCapBytes);
        }
        else
        {
            ApplyEffectiveCap();
        }
    }

    private long ComputeCeilingBytes(double rateBytesPerSecond)
    {
        var targetBytes = rateBytesPerSecond * _targetDelayTicks / Stopwatch.Frequency;
        return (long)Math.Clamp(targetBytes, MinimumWindowBytes, _capBytes);
    }

    private void ResetEpoch(long nowTicks)
    {
        _epochStartedAt = nowTicks;
        _epochBytes = 0;
        _epochCurrentGenerationBytes = 0;
        _epochCurrentGenerationDelayByteTicks = 0;
        _epochLoaded = false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private BrokerWindowDecision CurrentDecision(
        BrokerBudgetProbeType? probeType = null,
        BrokerBudgetProbeOutcome? probeOutcome = null) =>
        new(_windowBytes, _generation, _phase, probeType, probeOutcome);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double UpdateEwma(double current, double sample) =>
        current == 0 ? sample : current + EwmaWeight * (sample - current);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static long SaturatingAdd(long current, long value) =>
        value > long.MaxValue - current ? long.MaxValue : current + value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static long SaturatingMultiply(long value, int multiplier) =>
        value > long.MaxValue / multiplier ? long.MaxValue : value * multiplier;
}
