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
    // caused self-inflicted admission stalls that looked like congestion.
    private const int ProbeIntervalEpochs = 60;
    private const int ProbeEvaluationEpochs = 3;
    // A treatment must not remain active forever if coalescing briefly mixes generations.
    // Four unqualified epochs bound the experiment to roughly two extra seconds.
    private const int MaximumUnqualifiedProbeEpochs = 4;
    private const double UpProbeMultiplier = 1.125;
    private const double DownProbeMultiplier = 0.875;
    private const double UpProbeGoodputThreshold = 1.01;
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
    private double _goodputEwmaBytesPerSecond;
    private double _maximumGoodputBytesPerSecond;
    private long _minimumRttTicks;
    private long _lastAdmissionBlockEvents;

    private int _epochsUntilProbe = ProbeIntervalEpochs;
    private bool _probeDownNext = true;
    private long _probeBaselineWindow;
    private double _probeBaselineGoodput;
    private double _probeBaselineDelayTicks;
    private int _probeEvaluationCount;
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
        {
            _goodputEwmaBytesPerSecond = UpdateEwma(_goodputEwmaBytesPerSecond, goodput);
            _maximumGoodputBytesPerSecond = Math.Max(_maximumGoodputBytesPerSecond, goodput);
        }

        var generationQualified = _epochCurrentGenerationBytes > 0
            && _epochCurrentGenerationBytes >= _epochBytes / 2 + _epochBytes % 2;
        var controlledDelayTicks = generationQualified
            ? _epochCurrentGenerationDelayByteTicks / _epochCurrentGenerationBytes
            : _controlledDelayEwmaTicks;
        var admissionPressure = admissionBlockEvents != _lastAdmissionBlockEvents;
        // Occupancy is a demand signal, not a floor. A floor would make the lower-window
        // treatment impossible to test; the 99% goodput guard below reverts a harmful probe
        // after three qualified epochs instead.
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
            BrokerWindowPhase.Steady => UpdateSteady(goodput, controlledDelayTicks, demand),
            BrokerWindowPhase.ProbeUp or BrokerWindowPhase.ProbeDown =>
                EvaluateCapacityProbe(goodput, controlledDelayTicks),
            _ => CurrentDecision()
        };
    }

    private BrokerWindowDecision UpdateSteady(
        double goodput,
        double controlledDelayTicks,
        bool demand)
    {
        if (!demand)
            return CurrentDecision();

        if (--_epochsUntilProbe > 0)
            return CurrentDecision();

        if (_probeDownNext && _windowBytes > MinimumWindowBytes)
            return StartDownProbe(goodput, controlledDelayTicks);

        var requestedWindow = Math.Min(
            _capBytes,
            Math.Max(
                _windowBytes + _requestQuantumBytes,
                (long)Math.Ceiling(_windowBytes * UpProbeMultiplier)));
        return StartCapacityProbe(
            BrokerWindowPhase.ProbeUp,
            requestedWindow,
            goodput,
            controlledDelayTicks);
    }

    private BrokerWindowDecision StartDownProbe(
        double goodput,
        double controlledDelayTicks)
    {
        var requestedWindow = Math.Max(
            MinimumWindowBytes,
            (long)Math.Floor(_windowBytes * DownProbeMultiplier));
        return StartCapacityProbe(
            BrokerWindowPhase.ProbeDown,
            requestedWindow,
            goodput,
            controlledDelayTicks);
    }

    private BrokerWindowDecision StartCapacityProbe(
        BrokerWindowPhase phase,
        long requestedWindow,
        double goodput,
        double controlledDelayTicks)
    {
        _probeBaselineWindow = _windowBytes;
        _probeBaselineGoodput = Math.Max(goodput, _goodputEwmaBytesPerSecond);
        _probeBaselineDelayTicks = controlledDelayTicks;
        _phase = phase;
        _unqualifiedProbeEpochs = 0;
        ResetProbeEvaluation();
        SetWindow(requestedWindow);
        if (_windowBytes == _probeBaselineWindow)
        {
            _phase = BrokerWindowPhase.Steady;
            _probeDownNext = phase == BrokerWindowPhase.ProbeUp;
            _epochsUntilProbe = ProbeIntervalEpochs;
            return CurrentDecision();
        }

        return CurrentDecision(
            BrokerBudgetProbeType.Capacity,
            BrokerBudgetProbeOutcome.Started);
    }

    private BrokerWindowDecision EvaluateCapacityProbe(
        double goodput,
        double controlledDelayTicks)
    {
        if (!TryCompleteProbeEvaluation(
            goodput,
            controlledDelayTicks,
            out var averageGoodput,
            out var averageControlledDelayTicks))
        {
            return CurrentDecision();
        }

        var probingUp = _phase == BrokerWindowPhase.ProbeUp;
        var delayLimit = Math.Max(
            _targetDelayTicks,
            _probeBaselineDelayTicks * DelayGrowthTolerance);
        var goodputThreshold = probingUp
            ? UpProbeGoodputThreshold
            : DownProbeGoodputThreshold;
        // Both treatments must stay within the soft delay envelope. A lower window is useful
        // only when it preserves goodput without replacing queueing with extra request delay.
        var succeeded = averageGoodput >= _probeBaselineGoodput * goodputThreshold
            && averageControlledDelayTicks <= delayLimit;
        return CompleteCapacityProbe(succeeded, averageGoodput);
    }

    private BrokerWindowDecision AbortUnqualifiedProbe() =>
        CompleteCapacityProbe(succeeded: false, averageGoodput: 0);

    private BrokerWindowDecision CompleteCapacityProbe(
        bool succeeded,
        double averageGoodput)
    {
        var probingUp = _phase == BrokerWindowPhase.ProbeUp;
        if (succeeded)
        {
            CapacityProbeSuccessCount++;
            _goodputEwmaBytesPerSecond = averageGoodput;
        }
        else
        {
            CapacityProbeFailureCount++;
            SetWindow(_probeBaselineWindow);
        }

        _phase = BrokerWindowPhase.Steady;
        _probeDownNext = probingUp;
        _epochsUntilProbe = ProbeIntervalEpochs;
        _unqualifiedProbeEpochs = 0;
        ResetProbeEvaluation();
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

    private bool TryCompleteProbeEvaluation(
        double goodput,
        double controlledDelayTicks,
        out double averageGoodput,
        out double averageControlledDelayTicks)
    {
        _probeEvaluationCount++;
        _probeGoodputTotal += goodput;
        _probeDelayTotalTicks += controlledDelayTicks;
        if (_probeEvaluationCount < ProbeEvaluationEpochs)
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
        var minimum = MinimumWindowBytes;
        var next = Math.Clamp(requestedBytes, minimum, _capBytes);
        if (next > minimum && _requestQuantumBytes > 1)
        {
            next = next / _requestQuantumBytes * _requestQuantumBytes;
            next = Math.Max(next, minimum);
        }

        if (next == _windowBytes)
            return;

        _windowBytes = next;
        _generation++;
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
