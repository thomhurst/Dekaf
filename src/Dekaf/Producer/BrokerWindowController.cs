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
    Startup,
    Steady,
    ProbeUp,
    ProbeDown,
    ProbeRtt,
    Recovery
}

internal readonly record struct BrokerWindowDecision(
    long WindowBytes,
    long Generation,
    BrokerWindowPhase Phase,
    BrokerBudgetProbeType? ProbeType = null,
    BrokerBudgetProbeOutcome? ProbeOutcome = null);

/// <summary>
/// Pure, allocation-free knee-seeking byte-window controller. It searches for the smallest
/// logical-byte window that preserves broker goodput, instead of deriving a window from a
/// noisy instantaneous rate estimate. A lower window is retained when it preserves at least
/// 99% of goodput; a higher window is retained only when goodput grows without controllable
/// delay inflation.
/// </summary>
internal sealed class BrokerWindowController
{
    private const int SizeBucketCount = 16;
    private const int SizeBucketShift = 8;
    private const int InitialRequestQuanta = 4;
    private const int ProbeIntervalEpochs = 8;
    private const double UpProbeMultiplier = 1.125;
    private const double DownProbeMultiplier = 0.875;
    private const double UpProbeGoodputThreshold = 1.01;
    private const double DownProbeGoodputThreshold = 0.99;
    private const double StartupGoodputThreshold = 1.02;
    private const double DelayGrowthTolerance = 1.25;
    private const double RecoveryDelayThreshold = 1.25;
    private const double EwmaWeight = 0.125;

    private static readonly long ControlIntervalTicks = Math.Max(1, Stopwatch.Frequency / 10);
    private static readonly long MinimumRttRefreshTicks = 10 * Stopwatch.Frequency;

    private readonly long _floorBytes;
    private readonly long _targetDelayTicks;
    private readonly long[] _minimumRttBySizeTicks = new long[SizeBucketCount];
    private readonly long[] _minimumRttTimestamps = new long[SizeBucketCount];

    private long _capBytes;
    private long _windowBytes;
    private long _generation = 1;
    private BrokerWindowPhase _phase = BrokerWindowPhase.Startup;

    private long _epochStartedAt;
    private long _epochBytes;
    private long _epochCurrentGenerationBytes;
    private double _epochCurrentGenerationDelayByteTicks;
    private long _epochCurrentGenerationMaximumDelayTicks;
    private bool _epochLoaded;

    private double _requestSizeEwmaBytes;
    private double _controlledDelayEwmaTicks;
    private double _goodputEwmaBytesPerSecond;
    private double _maximumGoodputBytesPerSecond;
    private long _minimumRttTicks;
    private long _lastAdmissionBlockEvents;

    private double _startupReferenceGoodput;
    private int _startupNoGainEpochs;
    private int _epochsUntilProbe = ProbeIntervalEpochs;
    private bool _probeDownNext = true;
    private long _probeBaselineWindow;
    private double _probeBaselineGoodput;
    private double _probeBaselineDelayTicks;
    private long _nextRttProbeTimestamp;

    internal BrokerWindowController(
        double targetSeconds,
        long floorBytes,
        long capBytes,
        long initialRequestBytes)
    {
        _floorBytes = Math.Max(1, floorBytes);
        _capBytes = Math.Max(_floorBytes, capBytes);
        _targetDelayTicks = Math.Max(1, (long)(targetSeconds * Stopwatch.Frequency));
        _requestSizeEwmaBytes = Math.Max(_floorBytes, initialRequestBytes);
        _windowBytes = Math.Clamp(
            SaturatingMultiply((long)Math.Ceiling(_requestSizeEwmaBytes), InitialRequestQuanta),
            _floorBytes,
            _capBytes);
    }

    internal long WindowBytes => _windowBytes;
    internal long Generation => _generation;
    internal BrokerWindowPhase Phase => _phase;
    internal long MinimumRttTicks => _minimumRttTicks;
    internal long MaximumGoodputBytesPerSecond => (long)_maximumGoodputBytesPerSecond;
    internal long ControlledDelayEwmaTicks => (long)_controlledDelayEwmaTicks;
    internal double WindowScale => _capBytes == 0 ? 1 : (double)_windowBytes / _capBytes;
    internal long CapacityProbeSuccessCount { get; private set; }
    internal long CapacityProbeFailureCount { get; private set; }

    internal BrokerWindowDecision SetCap(long capBytes)
    {
        _capBytes = Math.Max(_floorBytes, capBytes);
        SetWindow(_windowBytes);
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

        _requestSizeEwmaBytes = UpdateEwma(_requestSizeEwmaBytes, logicalBytes);
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
            _epochCurrentGenerationMaximumDelayTicks = Math.Max(
                _epochCurrentGenerationMaximumDelayTicks,
                controlledDelayTicks);
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
            _nextRttProbeTimestamp = nowTicks + MinimumRttRefreshTicks;
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
            ? Math.Max(
                _controlledDelayEwmaTicks,
                _epochCurrentGenerationDelayByteTicks / _epochCurrentGenerationBytes)
            : _controlledDelayEwmaTicks;
        var maximumDelayTicks = generationQualified
            ? _epochCurrentGenerationMaximumDelayTicks
            : (long)controlledDelayTicks;
        var admissionPressure = admissionBlockEvents != _lastAdmissionBlockEvents;
        var occupancyPressure = observedOutstandingBytes
            >= _windowBytes - _windowBytes / 4;
        var demand = admissionPressure || occupancyPressure || _epochLoaded;

        _lastAdmissionBlockEvents = admissionBlockEvents;
        ResetEpoch(nowTicks);

        if (!generationQualified || goodput <= 0)
            return CurrentDecision();

        if (_phase is not BrokerWindowPhase.ProbeRtt
            && demand
            && maximumDelayTicks > _targetDelayTicks * RecoveryDelayThreshold)
        {
            return EnterRecovery(controlledDelayTicks);
        }

        return _phase switch
        {
            BrokerWindowPhase.Startup => UpdateStartup(goodput, controlledDelayTicks, demand),
            BrokerWindowPhase.Steady => UpdateSteady(goodput, controlledDelayTicks, demand, nowTicks),
            BrokerWindowPhase.ProbeUp => EvaluateProbeUp(goodput, controlledDelayTicks),
            BrokerWindowPhase.ProbeDown => EvaluateProbeDown(goodput),
            BrokerWindowPhase.ProbeRtt => CompleteRttProbe(goodput, controlledDelayTicks, nowTicks),
            BrokerWindowPhase.Recovery => UpdateRecovery(goodput, controlledDelayTicks, demand),
            _ => CurrentDecision()
        };
    }

    private BrokerWindowDecision UpdateStartup(
        double goodput,
        double controlledDelayTicks,
        bool demand)
    {
        if (!demand || _windowBytes >= _capBytes)
            return EnterSteady(goodput, controlledDelayTicks);

        if (_startupReferenceGoodput == 0)
        {
            _startupReferenceGoodput = goodput;
            GrowStartupWindow();
            return CurrentDecision();
        }

        if (goodput >= _startupReferenceGoodput * StartupGoodputThreshold)
        {
            _startupReferenceGoodput = goodput;
            _startupNoGainEpochs = 0;
            GrowStartupWindow();
            return CurrentDecision();
        }

        if (++_startupNoGainEpochs < 2)
        {
            SetWindow(Math.Max(
                _windowBytes + CurrentRequestBytes,
                (long)Math.Ceiling(_windowBytes * UpProbeMultiplier)));
            return CurrentDecision();
        }

        return EnterSteady(Math.Max(goodput, _startupReferenceGoodput), controlledDelayTicks);
    }

    private BrokerWindowDecision UpdateSteady(
        double goodput,
        double controlledDelayTicks,
        bool demand,
        long nowTicks)
    {
        _goodputEwmaBytesPerSecond = UpdateEwma(_goodputEwmaBytesPerSecond, goodput);
        if (!demand)
            return CurrentDecision();

        if (nowTicks >= _nextRttProbeTimestamp)
            return StartRttProbe(nowTicks);

        if (--_epochsUntilProbe > 0)
            return CurrentDecision();

        _probeBaselineWindow = _windowBytes;
        _probeBaselineGoodput = Math.Max(goodput, _goodputEwmaBytesPerSecond);
        _probeBaselineDelayTicks = controlledDelayTicks;

        if (_probeDownNext && _windowBytes > MinimumWindowBytes)
        {
            _phase = BrokerWindowPhase.ProbeDown;
            SetWindow(Math.Max(
                MinimumWindowBytes,
                (long)Math.Floor(_windowBytes * DownProbeMultiplier)));
            return CurrentDecision(
                BrokerBudgetProbeType.Capacity,
                BrokerBudgetProbeOutcome.Started);
        }

        _phase = BrokerWindowPhase.ProbeUp;
        SetWindow(Math.Min(
            _capBytes,
            Math.Max(
                _windowBytes + CurrentRequestBytes,
                (long)Math.Ceiling(_windowBytes * UpProbeMultiplier))));
        if (_windowBytes == _probeBaselineWindow)
        {
            _phase = BrokerWindowPhase.Steady;
            _probeDownNext = true;
            _epochsUntilProbe = ProbeIntervalEpochs;
            return CurrentDecision();
        }

        return CurrentDecision(
            BrokerBudgetProbeType.Capacity,
            BrokerBudgetProbeOutcome.Started);
    }

    private BrokerWindowDecision EvaluateProbeUp(double goodput, double controlledDelayTicks)
    {
        var delayLimit = Math.Max(
            _targetDelayTicks,
            _probeBaselineDelayTicks * DelayGrowthTolerance);
        var succeeded = goodput >= _probeBaselineGoodput * UpProbeGoodputThreshold
            && controlledDelayTicks <= delayLimit;
        if (succeeded)
        {
            CapacityProbeSuccessCount++;
            _goodputEwmaBytesPerSecond = goodput;
        }
        else
        {
            CapacityProbeFailureCount++;
            SetWindow(_probeBaselineWindow);
        }

        _phase = BrokerWindowPhase.Steady;
        _probeDownNext = true;
        _epochsUntilProbe = ProbeIntervalEpochs;
        return CurrentDecision(
            BrokerBudgetProbeType.Capacity,
            succeeded ? BrokerBudgetProbeOutcome.Succeeded : BrokerBudgetProbeOutcome.Failed);
    }

    private BrokerWindowDecision EvaluateProbeDown(double goodput)
    {
        var succeeded = goodput >= _probeBaselineGoodput * DownProbeGoodputThreshold;
        if (succeeded)
        {
            CapacityProbeSuccessCount++;
            _goodputEwmaBytesPerSecond = goodput;
        }
        else
        {
            CapacityProbeFailureCount++;
            SetWindow(_probeBaselineWindow);
        }

        _phase = BrokerWindowPhase.Steady;
        _probeDownNext = false;
        _epochsUntilProbe = ProbeIntervalEpochs;
        return CurrentDecision(
            BrokerBudgetProbeType.Capacity,
            succeeded ? BrokerBudgetProbeOutcome.Succeeded : BrokerBudgetProbeOutcome.Failed);
    }

    private BrokerWindowDecision StartRttProbe(long nowTicks)
    {
        _probeBaselineWindow = _windowBytes;
        _probeBaselineGoodput = _goodputEwmaBytesPerSecond;
        _probeBaselineDelayTicks = _controlledDelayEwmaTicks;
        _phase = BrokerWindowPhase.ProbeRtt;
        Array.Clear(_minimumRttBySizeTicks);
        Array.Clear(_minimumRttTimestamps);
        _minimumRttTicks = 0;
        SetWindow(MinimumWindowBytes);
        _nextRttProbeTimestamp = nowTicks + MinimumRttRefreshTicks;
        return CurrentDecision(
            BrokerBudgetProbeType.MinimumRtt,
            BrokerBudgetProbeOutcome.Started);
    }

    private BrokerWindowDecision CompleteRttProbe(
        double goodput,
        double controlledDelayTicks,
        long nowTicks)
    {
        SetWindow(_probeBaselineWindow);
        _goodputEwmaBytesPerSecond = Math.Max(_goodputEwmaBytesPerSecond, goodput);
        _controlledDelayEwmaTicks = controlledDelayTicks;
        _phase = BrokerWindowPhase.Steady;
        _epochsUntilProbe = ProbeIntervalEpochs;
        _nextRttProbeTimestamp = nowTicks + MinimumRttRefreshTicks;
        return CurrentDecision(
            BrokerBudgetProbeType.MinimumRtt,
            BrokerBudgetProbeOutcome.Succeeded);
    }

    private BrokerWindowDecision EnterRecovery(double controlledDelayTicks)
    {
        _phase = BrokerWindowPhase.Recovery;
        var factor = Math.Clamp(
            _targetDelayTicks / Math.Max(controlledDelayTicks, 1),
            0.5,
            0.9);
        SetWindow((long)Math.Floor(_windowBytes * factor));
        return CurrentDecision();
    }

    private BrokerWindowDecision UpdateRecovery(
        double goodput,
        double controlledDelayTicks,
        bool demand)
    {
        if (controlledDelayTicks <= _targetDelayTicks * 0.8)
            return EnterSteady(goodput, controlledDelayTicks);

        if (demand && controlledDelayTicks > _targetDelayTicks)
        {
            var factor = Math.Clamp(
                _targetDelayTicks / Math.Max(controlledDelayTicks, 1),
                0.65,
                0.9);
            SetWindow((long)Math.Floor(_windowBytes * factor));
        }

        return CurrentDecision();
    }

    private BrokerWindowDecision EnterSteady(double goodput, double controlledDelayTicks)
    {
        _phase = BrokerWindowPhase.Steady;
        _goodputEwmaBytesPerSecond = goodput;
        _controlledDelayEwmaTicks = controlledDelayTicks;
        _epochsUntilProbe = ProbeIntervalEpochs;
        _probeDownNext = true;
        return CurrentDecision();
    }

    private void GrowStartupWindow()
    {
        SetWindow(Math.Max(
            _windowBytes + CurrentRequestBytes,
            SaturatingMultiply(_windowBytes, 2)));
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
        var next = Math.Clamp(requestedBytes, MinimumWindowBytes, _capBytes);
        if (next == _windowBytes)
            return;

        _windowBytes = next;
        _generation++;
    }

    private long MinimumWindowBytes => Math.Clamp(CurrentRequestBytes, _floorBytes, _capBytes);
    private long CurrentRequestBytes => Math.Max(_floorBytes, (long)Math.Ceiling(_requestSizeEwmaBytes));

    private void ResetEpoch(long nowTicks)
    {
        _epochStartedAt = nowTicks;
        _epochBytes = 0;
        _epochCurrentGenerationBytes = 0;
        _epochCurrentGenerationDelayByteTicks = 0;
        _epochCurrentGenerationMaximumDelayTicks = 0;
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
