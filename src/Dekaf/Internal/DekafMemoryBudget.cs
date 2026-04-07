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
    private const ulong FallbackBudgetBytes = 320UL * 1024 * 1024; // 256 MiB producer + 64 MiB consumer

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
    /// Override the budget with an explicit byte count.
    /// Must be called before building any Dekaf instances.
    /// </summary>
    public static void SetBudget(ulong bytes)
    {
        ArgumentOutOfRangeException.ThrowIfZero(bytes);
        lock (_lock)
        {
            _explicitBudget = bytes;
            RebalanceUnlocked();
        }
    }

    /// <summary>
    /// Override the budget as a percentage of available system memory.
    /// </summary>
    public static void SetBudget(double percentOfAvailable)
    {
        if (percentOfAvailable is <= 0.0 or > 1.0)
            throw new ArgumentOutOfRangeException(nameof(percentOfAvailable),
                "Must be in the range (0.0, 1.0].");

        lock (_lock)
        {
            _explicitBudget = null;
            _percentOfAvailable = percentOfAvailable;
            RebalanceUnlocked();
        }
    }

    internal static void RegisterProducer(IBudgetedInstance instance)
    {
        lock (_lock)
        {
            _producers.Add(instance);
            RebalanceUnlocked();
        }
    }

    internal static void UnregisterProducer(IBudgetedInstance instance)
    {
        lock (_lock)
        {
            if (_producers.Remove(instance))
                RebalanceUnlocked();
        }
    }

    internal static void RegisterConsumer(IBudgetedInstance instance)
    {
        lock (_lock)
        {
            _consumers.Add(instance);
            RebalanceUnlocked();
        }
    }

    internal static void UnregisterConsumer(IBudgetedInstance instance)
    {
        lock (_lock)
        {
            if (_consumers.Remove(instance))
                RebalanceUnlocked();
        }
    }

    /// <summary>
    /// Reserve a fixed byte count against the budget for a manually-configured
    /// instance. This memory is removed from the auto-budget pool.
    /// </summary>
    internal static void ReserveExplicit(ulong bytes)
    {
        lock (_lock)
        {
            _explicitlyReserved += bytes;
            RebalanceUnlocked();
        }
    }

    internal static void ReleaseExplicit(ulong bytes)
    {
        lock (_lock)
        {
            _explicitlyReserved = _explicitlyReserved >= bytes
                ? _explicitlyReserved - bytes
                : 0;
            RebalanceUnlocked();
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

    private static void RebalanceUnlocked()
    {
        var producerLimit = ComputePerProducerLimitUnlocked();
        var consumerLimit = ComputePerConsumerLimitUnlocked();

        foreach (var p in _producers)
            p.OnBudgetChanged(producerLimit);
        foreach (var c in _consumers)
            c.OnBudgetChanged(consumerLimit);
    }
}
