namespace Dekaf.Internal;

internal sealed class ClientMemoryBudget : IDekafMemoryBudget
{
    private const double DefaultPercentOfAvailable = 0.40;
    private const double ProducerShareWhenBoth = 0.75;
    private const double ConsumerShareWhenBoth = 0.25;
    private const ulong ProducerFloorBytes = 32UL * 1024 * 1024;
    private const ulong ConsumerFloorBytes = 16UL * 1024 * 1024;
    private const ulong FallbackBudgetBytes = 320UL * 1024 * 1024;

    private readonly object _lock = new();
    private readonly ulong? _explicitBudget;
    private readonly double _percentOfAvailable;
    private ulong _explicitlyReserved;
    private readonly List<IBudgetedInstance> _producers = [];
    private readonly List<IBudgetedInstance> _consumers = [];

    public ClientMemoryBudget(ulong? explicitBudget = null, double percentOfAvailable = DefaultPercentOfAvailable)
    {
        if (explicitBudget is 0)
            throw new ArgumentOutOfRangeException(nameof(explicitBudget));
        if (percentOfAvailable is <= 0.0 or > 1.0)
            throw new ArgumentOutOfRangeException(nameof(percentOfAvailable),
                "Must be in the range (0.0, 1.0].");

        _explicitBudget = explicitBudget;
        _percentOfAvailable = percentOfAvailable;
    }

    public ulong PreviewProducerLimit()
    {
        lock (_lock)
        {
            var producerCount = _producers.Count + 1;
            var auto = AutoBudgetUnlocked();
            var share = _consumers.Count == 0 ? auto : (ulong)(auto * ProducerShareWhenBoth);
            var perInstance = share / (ulong)producerCount / (ulong)DekafMemoryBudget.ProducerOverheadDivisor;
            return Math.Max(perInstance, ProducerFloorBytes);
        }
    }

    public ulong PreviewConsumerLimit()
    {
        lock (_lock)
        {
            var consumerCount = _consumers.Count + 1;
            var auto = AutoBudgetUnlocked();
            var share = _producers.Count == 0 ? auto : (ulong)(auto * ConsumerShareWhenBoth);
            return Math.Max(share / (ulong)consumerCount, ConsumerFloorBytes);
        }
    }

    public void RegisterProducer(IBudgetedInstance instance)
    {
        RebalanceSnapshot snapshot;
        lock (_lock)
        {
            _producers.Add(instance);
            snapshot = SnapshotRebalanceUnlocked();
        }
        snapshot.Dispatch();
    }

    public void UnregisterProducer(IBudgetedInstance instance)
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
            ReportBookkeepingError(ex);
        }
    }

    public void RegisterConsumer(IBudgetedInstance instance)
    {
        RebalanceSnapshot snapshot;
        lock (_lock)
        {
            _consumers.Add(instance);
            snapshot = SnapshotRebalanceUnlocked();
        }
        snapshot.Dispatch();
    }

    public void UnregisterConsumer(IBudgetedInstance instance)
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
            ReportBookkeepingError(ex);
        }
    }

    public void ReserveExplicit(ulong bytes)
    {
        RebalanceSnapshot snapshot;
        lock (_lock)
        {
            _explicitlyReserved += bytes;
            snapshot = SnapshotRebalanceUnlocked();
        }
        snapshot.Dispatch();
    }

    public void ReleaseExplicit(ulong bytes)
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
            ReportBookkeepingError(ex);
        }
    }

    private ulong ComputeTotalBudgetUnlocked()
    {
        if (_explicitBudget.HasValue)
            return _explicitBudget.Value;

        var available = (ulong)GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
        if (available == 0)
            return FallbackBudgetBytes;

        return (ulong)(available * _percentOfAvailable);
    }

    private ulong AutoBudgetUnlocked()
    {
        var total = ComputeTotalBudgetUnlocked();
        return total > _explicitlyReserved ? total - _explicitlyReserved : 0;
    }

    private ulong ComputePerProducerLimitUnlocked()
    {
        if (_producers.Count == 0)
            return 0;

        var auto = AutoBudgetUnlocked();
        var share = _consumers.Count == 0 ? auto : (ulong)(auto * ProducerShareWhenBoth);
        var perInstance = share / (ulong)_producers.Count / (ulong)DekafMemoryBudget.ProducerOverheadDivisor;
        return Math.Max(perInstance, ProducerFloorBytes);
    }

    private ulong ComputePerConsumerLimitUnlocked()
    {
        if (_consumers.Count == 0)
            return 0;

        var auto = AutoBudgetUnlocked();
        var share = _producers.Count == 0 ? auto : (ulong)(auto * ConsumerShareWhenBoth);
        var perInstance = share / (ulong)_consumers.Count;
        return Math.Max(perInstance, ConsumerFloorBytes);
    }

    private RebalanceSnapshot SnapshotRebalanceUnlocked()
    {
        return new RebalanceSnapshot
        {
            Producers = _producers.ToArray(),
            Consumers = _consumers.ToArray(),
            ProducerLimit = ComputePerProducerLimitUnlocked(),
            ConsumerLimit = ComputePerConsumerLimitUnlocked(),
        };
    }

    private readonly struct RebalanceSnapshot
    {
        public required IBudgetedInstance[] Producers { get; init; }
        public required IBudgetedInstance[] Consumers { get; init; }
        public required ulong ProducerLimit { get; init; }
        public required ulong ConsumerLimit { get; init; }

        public void Dispatch()
        {
            foreach (var p in Producers)
            {
                try { p.OnBudgetChanged(ProducerLimit); }
                catch (Exception ex) { ReportBookkeepingError(ex); }
            }
            foreach (var c in Consumers)
            {
                try { c.OnBudgetChanged(ConsumerLimit); }
                catch (Exception ex) { ReportBookkeepingError(ex); }
            }
        }
    }

    private static void ReportBookkeepingError(Exception exception)
    {
        try { DekafMemoryBudget.OnBookkeepingError?.Invoke(exception); } catch { }
    }
}
