namespace Dekaf.Internal;

/// <summary>
/// Implemented by Dekaf instances that participate in the global memory budget.
/// </summary>
internal interface IBudgetedInstance
{
    /// <summary>
    /// Called when the budget computes a new per-instance limit. Implementations
    /// must update their internal limit atomically.
    /// </summary>
    void OnBudgetChanged(ulong newLimit);
}

/// <summary>
/// Process-global memory budget for Dekaf producers and consumers.
/// Defaults to 40% of available system memory (respects container cgroup limits).
/// </summary>
public static class DekafMemoryBudget
{
    private const double DefaultPercentOfAvailable = 0.40;
    private const double ProducerShareWhenBoth = 0.75;
    private const double ConsumerShareWhenBoth = 0.25;
    private const ulong ProducerFloorBytes = 32UL * 1024 * 1024;
    private const ulong ConsumerFloorBytes = 16UL * 1024 * 1024;
    private const ulong FallbackBudgetBytes = 320UL * 1024 * 1024;

    private static readonly object _lock = new();
    private static ulong? _explicitBudget;
    private static double _percentOfAvailable = DefaultPercentOfAvailable;
    private static ulong _explicitlyReserved;

    private static readonly List<IBudgetedInstance> _producers = new();
    private static readonly List<IBudgetedInstance> _consumers = new();

    /// <summary>
    /// The total memory budget in bytes available to all Dekaf instances in the process.
    /// </summary>
    public static ulong TotalBudget
    {
        get { lock (_lock) { return ComputeTotalBudgetUnlocked(); } }
    }

    /// <summary>
    /// Diagnostics hook invoked when budget bookkeeping throws during disposal.
    /// Bookkeeping failures are swallowed so that disposal cannot fail, but callers
    /// wiring a logger here can observe otherwise-silent regressions. Invoked on the
    /// disposing thread; handlers must not throw.
    /// </summary>
    public static Action<Exception>? OnBookkeepingError { get; set; }

    /// <summary>
    /// Override the budget with an explicit byte count.
    /// Must be called before building any Dekaf instances.
    /// </summary>
    public static void SetBudget(ulong bytes)
    {
        ArgumentOutOfRangeException.ThrowIfZero(bytes);
        RebalanceSnapshot snapshot;
        lock (_lock)
        {
            _explicitBudget = bytes;
            snapshot = SnapshotRebalanceUnlocked();
        }
        snapshot.Dispatch();
    }

    /// <summary>
    /// Override the budget as a percentage of available system memory.
    /// </summary>
    public static void SetBudget(double percentOfAvailable)
    {
        if (percentOfAvailable is <= 0.0 or > 1.0)
            throw new ArgumentOutOfRangeException(nameof(percentOfAvailable),
                "Must be in the range (0.0, 1.0].");

        RebalanceSnapshot snapshot;
        lock (_lock)
        {
            _explicitBudget = null;
            _percentOfAvailable = percentOfAvailable;
            snapshot = SnapshotRebalanceUnlocked();
        }
        snapshot.Dispatch();
    }

    internal static void RegisterProducer(IBudgetedInstance instance)
    {
        RebalanceSnapshot snapshot;
        lock (_lock)
        {
            _producers.Add(instance);
            snapshot = SnapshotRebalanceUnlocked();
        }
        snapshot.Dispatch();
    }

    internal static void UnregisterProducer(IBudgetedInstance instance)
    {
        try
        {
            RebalanceSnapshot snapshot;
            lock (_lock)
            {
                if (!_producers.Remove(instance))
                    return;
                snapshot = SnapshotRebalanceUnlocked();
            }
            snapshot.Dispatch();
        }
        catch (Exception ex)
        {
            // Budget bookkeeping must never break disposal. Route through the
            // diagnostics hook so callers can still observe unexpected regressions.
            try { OnBookkeepingError?.Invoke(ex); } catch { /* handler must not throw */ }
        }
    }

    internal static void RegisterConsumer(IBudgetedInstance instance)
    {
        RebalanceSnapshot snapshot;
        lock (_lock)
        {
            _consumers.Add(instance);
            snapshot = SnapshotRebalanceUnlocked();
        }
        snapshot.Dispatch();
    }

    internal static void UnregisterConsumer(IBudgetedInstance instance)
    {
        try
        {
            RebalanceSnapshot snapshot;
            lock (_lock)
            {
                if (!_consumers.Remove(instance))
                    return;
                snapshot = SnapshotRebalanceUnlocked();
            }
            snapshot.Dispatch();
        }
        catch (Exception ex)
        {
            // Budget bookkeeping must never break disposal. Route through the
            // diagnostics hook so callers can still observe unexpected regressions.
            try { OnBookkeepingError?.Invoke(ex); } catch { /* handler must not throw */ }
        }
    }

    /// <summary>
    /// Reserve a fixed byte count against the budget for a manually-configured
    /// instance. This memory is removed from the auto-budget pool.
    /// </summary>
    internal static void ReserveExplicit(ulong bytes)
    {
        RebalanceSnapshot snapshot;
        lock (_lock)
        {
            _explicitlyReserved += bytes;
            snapshot = SnapshotRebalanceUnlocked();
        }
        snapshot.Dispatch();
    }

    internal static void ReleaseExplicit(ulong bytes)
    {
        try
        {
            RebalanceSnapshot snapshot;
            lock (_lock)
            {
                _explicitlyReserved = _explicitlyReserved >= bytes
                    ? _explicitlyReserved - bytes
                    : 0;
                snapshot = SnapshotRebalanceUnlocked();
            }
            snapshot.Dispatch();
        }
        catch (Exception ex)
        {
            // Budget bookkeeping must never break disposal. Route through the
            // diagnostics hook so callers can still observe unexpected regressions.
            try { OnBookkeepingError?.Invoke(ex); } catch { /* handler must not throw */ }
        }
    }

    /// <summary>
    /// Test hook: reset budget state to defaults.
    /// </summary>
    internal static void ResetForTesting()
    {
        lock (_lock)
        {
            _explicitBudget = null;
            _percentOfAvailable = DefaultPercentOfAvailable;
            _explicitlyReserved = 0;
            _producers.Clear();
            _consumers.Clear();
        }
    }

    private static ulong ComputeTotalBudgetUnlocked()
    {
        if (_explicitBudget.HasValue)
            return _explicitBudget.Value;

        var available = (ulong)GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
        if (available == 0)
            return FallbackBudgetBytes;

        return (ulong)(available * _percentOfAvailable);
    }

    private static ulong AutoBudgetUnlocked()
    {
        var total = ComputeTotalBudgetUnlocked();
        return total > _explicitlyReserved ? total - _explicitlyReserved : 0;
    }

    /// <summary>
    /// Computes the limit a newly-registered producer would receive.
    /// Used by builders to seed the initial RecordAccumulator buffer size before
    /// the producer is constructed and registered.
    /// </summary>
    internal static ulong PreviewProducerLimit()
    {
        lock (_lock)
        {
            var producerCount = _producers.Count + 1;
            var auto = AutoBudgetUnlocked();
            var share = _consumers.Count == 0 ? auto : (ulong)(auto * ProducerShareWhenBoth);
            return Math.Max(share / (ulong)producerCount, ProducerFloorBytes);
        }
    }

    /// <summary>
    /// Computes the limit a newly-registered consumer would receive.
    /// </summary>
    internal static ulong PreviewConsumerLimit()
    {
        lock (_lock)
        {
            var consumerCount = _consumers.Count + 1;
            var auto = AutoBudgetUnlocked();
            var share = _producers.Count == 0 ? auto : (ulong)(auto * ConsumerShareWhenBoth);
            return Math.Max(share / (ulong)consumerCount, ConsumerFloorBytes);
        }
    }

    private static ulong ComputePerProducerLimitUnlocked()
    {
        if (_producers.Count == 0)
            return 0;

        var auto = AutoBudgetUnlocked();
        var share = _consumers.Count == 0 ? auto : (ulong)(auto * ProducerShareWhenBoth);
        var perInstance = share / (ulong)_producers.Count;
        return Math.Max(perInstance, ProducerFloorBytes);
    }

    private static ulong ComputePerConsumerLimitUnlocked()
    {
        if (_consumers.Count == 0)
            return 0;

        var auto = AutoBudgetUnlocked();
        var share = _producers.Count == 0 ? auto : (ulong)(auto * ConsumerShareWhenBoth);
        var perInstance = share / (ulong)_consumers.Count;
        return Math.Max(perInstance, ConsumerFloorBytes);
    }

    /// <summary>
     /// Snapshot of the current per-instance limits and the instances to notify.
     /// Callers must invoke <see cref="Dispatch"/> AFTER releasing <see cref="_lock"/>
     /// so that budget callbacks never run with the global lock held.
     /// </summary>
    private readonly struct RebalanceSnapshot
    {
        public required IBudgetedInstance[] Producers { get; init; }
        public required IBudgetedInstance[] Consumers { get; init; }
        public required ulong ProducerLimit { get; init; }
        public required ulong ConsumerLimit { get; init; }

        public void Dispatch()
        {
            foreach (var p in Producers)
                p.OnBudgetChanged(ProducerLimit);
            foreach (var c in Consumers)
                c.OnBudgetChanged(ConsumerLimit);
        }
    }

    private static RebalanceSnapshot SnapshotRebalanceUnlocked()
    {
        return new RebalanceSnapshot
        {
            Producers = _producers.ToArray(),
            Consumers = _consumers.ToArray(),
            ProducerLimit = ComputePerProducerLimitUnlocked(),
            ConsumerLimit = ComputePerConsumerLimitUnlocked(),
        };
    }
}
