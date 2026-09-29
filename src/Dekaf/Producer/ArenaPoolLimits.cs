namespace Dekaf.Producer;

/// <summary>
/// Limits one producer asks of the process-wide arena pool.
/// </summary>
/// <param name="PoolSize">Slots the pool starts with for this producer.</param>
/// <param name="MissRatchetLimit">Largest size sustained misses may grow the pool to.</param>
/// <param name="RetainedBytes">Idle arena bytes the pool may keep; 0 means no byte limit.</param>
internal readonly record struct ArenaPoolLimit(int PoolSize, int MissRatchetLimit, long RetainedBytes);

/// <summary>
/// Tracks the arena pool limits of live producers. The effective limit is the largest request
/// among registered producers, so it falls again when the producer that raised it is disposed.
/// </summary>
/// <remarks>
/// Registration runs once per producer construction and disposal, never on the append path.
/// A producer that is never disposed keeps its registration, just as it keeps its connections.
/// </remarks>
internal sealed class ArenaPoolLimits
{
    private readonly Lock _lock = new();
    private readonly List<Registration> _registrations = [];
    private readonly ArenaPoolLimit _drained;
    private ArenaPoolLimit _current;

    /// <param name="idle">Effective limit before any producer registers.</param>
    /// <param name="drained">Effective limit once every registered producer has unregistered.</param>
    public ArenaPoolLimits(ArenaPoolLimit idle, ArenaPoolLimit drained)
    {
        _drained = drained;
        _current = idle;
    }

    /// <summary>
    /// Effective limit: the largest request among live registrations, the idle limit before the
    /// first registration, or the drained limit after the last unregistration.
    /// </summary>
    public ArenaPoolLimit Current
    {
        get { lock (_lock) { return _current; } }
    }

    /// <summary>Adds a producer's request to the effective limit.</summary>
    public Registration Register(ArenaPoolLimit limit)
    {
        var registration = new Registration(limit);
        lock (_lock)
        {
            _registrations.Add(registration);
            _current = ComputeUnlocked();
        }

        return registration;
    }

    /// <summary>
    /// Removes a producer's request from the effective limit.
    /// Returns false when the registration was already removed.
    /// </summary>
    public bool Unregister(Registration registration)
    {
        lock (_lock)
        {
            if (!_registrations.Remove(registration))
                return false;

            _current = ComputeUnlocked();
            return true;
        }
    }

    private ArenaPoolLimit ComputeUnlocked()
    {
        if (_registrations.Count == 0)
            return _drained;

        var poolSize = 0;
        var missRatchetLimit = 0;
        var retainedBytes = 0L;
        foreach (var registration in _registrations)
        {
            poolSize = Math.Max(poolSize, registration.Limit.PoolSize);
            missRatchetLimit = Math.Max(missRatchetLimit, registration.Limit.MissRatchetLimit);
            retainedBytes = Math.Max(retainedBytes, registration.Limit.RetainedBytes);
        }

        return new ArenaPoolLimit(poolSize, missRatchetLimit, retainedBytes);
    }

    /// <summary>One producer's request, removed by <see cref="Unregister"/>.</summary>
    internal sealed class Registration(ArenaPoolLimit limit)
    {
        public ArenaPoolLimit Limit { get; } = limit;
    }
}
