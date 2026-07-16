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
    // The control run sustained its knee at roughly seven full requests. One extra quantum
    // absorbs response/scheduler granularity without allowing fragmented requests to redefine
    // the floor.
    private const int MinimumPipelineQuanta = 8;
    // Open partition batches also hold bounded lease slack, so start above the expected knee
    // and search downward. This avoids throughput-biased learning from a starved startup.
    private const int InitialRequestQuanta = 16;
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
    private readonly long[] _minimumRttBySizeTicks = new long[SizeBucketCount];
    private readonly long[] _minimumRttTimestamps = new long[SizeBucketCount];

    private long _capBytes;
    private long _windowBytes;
    private long _generation = 1;
    private BrokerWindowPhase _phase = BrokerWindowPhase.Steady;

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
        long initialRequestBytes)
    {
        _floorBytes = Math.Max(1, floorBytes);
        _capBytes = Math.Max(_floorBytes, capBytes);
        _targetDelayTicks = Math.Max(1, (long)(targetSeconds * Stopwatch.Frequency));
        _requestQuantumBytes = Math.Max(_floorBytes, initialRequestBytes);
        _windowBytes = Math.Clamp(
            SaturatingMultiply(_requestQuantumBytes, InitialRequestQuanta),
            MinimumWindowBytes,
            _capBytes);
    }

    internal long WindowBytes => _windowBytes;
    internal long Generation => _generation;
    internal BrokerWindowPhase Phase => _phase;
    internal long MinimumRttTicks => _minimumRttTicks;
    internal long MaximumGoodputBytesPerSecond => (long)_maximumGoodputBytesPerSecond;
    internal long ControlledDelayEwmaTicks => (long)_controlledDelayEwmaTicks;
    internal long RequestQuantumBytes => _requestQuantumBytes;
    internal double WindowScale => _capBytes == 0 ? 1 : (double)_windowBytes / _capBytes;
    internal long CapacityProbeSuccessCount { get; private set; }
    internal long CapacityProbeFailureCount { get; private set; }

    internal BrokerWindowDecision SetCap(long capBytes)
    {
        _capBytes = Math.Max(_floorBytes, capBytes);
        if (_windowBytes > _capBytes)
        {
            _windowBytes = _capBytes;
            _generation++;
        }
        if (_probeBaselineWindow > _capBytes)
            _probeBaselineWindow = _capBytes;
        if (_probeCandidateWindow > _capBytes)
            _probeCandidateWindow = _capBytes;

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
            _capBytes,
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
        var next = Math.Clamp(requestedBytes, minimum, _capBytes);
        if (next > minimum && _requestQuantumBytes > 1)
        {
            next = next / _requestQuantumBytes * _requestQuantumBytes;
            next = Math.Max(next, minimum);
        }

        return next;
    }

    private long MinimumWindowBytes => Math.Clamp(
        SaturatingMultiply(_requestQuantumBytes, MinimumPipelineQuanta),
        _floorBytes,
        _capBytes);

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
