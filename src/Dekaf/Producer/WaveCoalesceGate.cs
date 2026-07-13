namespace Dekaf.Producer;

/// <summary>
/// Suppresses the send loop's wave-coalesce spin for workloads where it never pays off.
/// The spin waits up to a full quiet window per wave hoping a batch for another partition
/// arrives; on workloads whose requests are effectively single-partition (large batches,
/// few partitions per broker) that wait is pure CPU burn on every wave. After
/// <see cref="FruitlessSpinSuppressThreshold"/> consecutive spins that coalesced nothing,
/// the gate closes and only re-opens for a single probe spin every
/// <see cref="SuppressedReprobeSendInterval"/> sends, so a workload shift back toward
/// coalescible waves is still detected quickly.
/// </summary>
internal struct WaveCoalesceGate
{
    /// <summary>
    /// Consecutive fruitless spins tolerated before suppression. Bounds the worst-case
    /// wasted spin time per suppression cycle to this many quiet windows.
    /// </summary>
    internal const int FruitlessSpinSuppressThreshold = 8;

    /// <summary>
    /// Sends between probe spins while suppressed. Keeps steady-state waste to one quiet
    /// window per this many waves (&lt;0.1% of a core at millisecond wave cadence) while
    /// re-detecting coalescible workloads within one interval.
    /// </summary>
    internal const int SuppressedReprobeSendInterval = 64;

    private int _consecutiveFruitlessSpins;
    private int _sendsSinceSuppressed;

    private readonly bool IsSuppressed => _consecutiveFruitlessSpins >= FruitlessSpinSuppressThreshold;

    /// <summary>Whether the next wave may enter the coalesce spin.</summary>
    public readonly bool ShouldSpin()
        => !IsSuppressed || _sendsSinceSuppressed >= SuppressedReprobeSendInterval;

    /// <summary>
    /// Records the outcome of a spin permitted by <see cref="ShouldSpin"/>.
    /// A spin that coalesced at least one additional batch re-opens the gate.
    /// </summary>
    public void OnSpinCompleted(bool coalescedAdditionalBatch)
    {
        if (coalescedAdditionalBatch)
        {
            _consecutiveFruitlessSpins = 0;
        }
        else if (!IsSuppressed)
        {
            _consecutiveFruitlessSpins++;
        }

        _sendsSinceSuppressed = 0;
    }

    /// <summary>Advances the probe schedule while suppressed. Call once per completed send pass.</summary>
    public void OnSent()
    {
        if (IsSuppressed)
            _sendsSinceSuppressed++;
    }
}
