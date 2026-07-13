using System.Diagnostics;

namespace Dekaf.Producer;

/// <summary>
/// Suppresses the send loop's wave-coalesce spin for workloads where it never pays off.
/// The spin waits up to a full quiet window per wave hoping a batch for another partition
/// arrives; on workloads whose requests are effectively single-partition (large batches,
/// few partitions per broker) that wait is pure CPU burn on every wave. After
/// <see cref="FruitlessSpinSuppressThreshold"/> consecutive spins that coalesced nothing,
/// the gate closes and only re-opens for a probe after a short time interval. Time-based
/// recovery lets a low-rate workload probe on its next wave instead of remaining suppressed
/// for an arbitrarily long fixed number of sends.
/// </summary>
internal struct WaveCoalesceGate
{
    /// <summary>
    /// Consecutive fruitless spins tolerated before suppression. Bounds the worst-case
    /// wasted spin time per suppression cycle to this many quiet windows.
    /// </summary>
    internal const int FruitlessSpinSuppressThreshold = 8;

    /// <summary>
    /// Minimum time between probe spins while suppressed. Keeps steady-state spin cost bounded
    /// under high request rates while allowing the first wave after a low-rate idle period to
    /// detect a workload shift immediately.
    /// </summary>
    internal static readonly long SuppressedReprobeIntervalTicks = Math.Max(1, Stopwatch.Frequency / 20);

    private int _consecutiveFruitlessSpins;
    private long _nextProbeTimestamp;

    private readonly bool IsSuppressed => _consecutiveFruitlessSpins >= FruitlessSpinSuppressThreshold;

    /// <summary>Whether the next wave may enter the coalesce spin.</summary>
    public readonly bool ShouldSpin(long nowTicks)
        => !IsSuppressed || nowTicks >= _nextProbeTimestamp;

    /// <summary>
    /// Records the outcome of a spin permitted by <see cref="ShouldSpin"/>.
    /// A spin that coalesced at least one additional batch re-opens the gate.
    /// </summary>
    public void OnSpinCompleted(bool coalescedAdditionalBatch, long nowTicks)
    {
        if (coalescedAdditionalBatch)
        {
            _consecutiveFruitlessSpins = 0;
            _nextProbeTimestamp = 0;
        }
        else
        {
            if (!IsSuppressed)
                _consecutiveFruitlessSpins++;

            if (IsSuppressed)
                _nextProbeTimestamp = nowTicks + SuppressedReprobeIntervalTicks;
        }
    }
}
