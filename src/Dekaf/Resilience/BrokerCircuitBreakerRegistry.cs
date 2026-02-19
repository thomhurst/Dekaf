using System.Collections.Concurrent;

namespace Dekaf.Resilience;

/// <summary>
/// Manages per-broker circuit breaker instances using a shared configuration.
/// Thread-safe for concurrent access from producer and consumer threads.
/// </summary>
internal sealed class BrokerCircuitBreakerRegistry
{
    private readonly CircuitBreakerOptions _options;
    private readonly ConcurrentDictionary<int, ICircuitBreaker> _circuitBreakers = new();

    /// <summary>
    /// Creates a new registry with the specified options applied to all circuit breakers.
    /// </summary>
    /// <param name="options">The shared circuit breaker options.</param>
    public BrokerCircuitBreakerRegistry(CircuitBreakerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();
        _options = options;
    }

    /// <summary>
    /// Gets or creates a circuit breaker for the specified broker.
    /// </summary>
    /// <param name="brokerId">The broker ID.</param>
    /// <returns>The circuit breaker instance for the broker.</returns>
    public ICircuitBreaker GetOrCreate(int brokerId)
    {
        return _circuitBreakers.GetOrAdd(brokerId, static (_, opts) => new CircuitBreaker(opts), _options);
    }

    /// <summary>
    /// Gets the circuit breaker for the specified broker, if one exists.
    /// </summary>
    /// <param name="brokerId">The broker ID.</param>
    /// <param name="circuitBreaker">The circuit breaker, if found.</param>
    /// <returns><c>true</c> if a circuit breaker exists for the broker; otherwise, <c>false</c>.</returns>
    public bool TryGet(int brokerId, out ICircuitBreaker? circuitBreaker)
    {
        var found = _circuitBreakers.TryGetValue(brokerId, out var cb);
        circuitBreaker = cb;
        return found;
    }

    /// <summary>
    /// Gets a snapshot of all broker circuit breaker states.
    /// </summary>
    /// <returns>A dictionary mapping broker IDs to their current circuit breaker state.</returns>
    public IReadOnlyDictionary<int, CircuitBreakerState> GetAllStates()
    {
        var states = new Dictionary<int, CircuitBreakerState>();
        foreach (var kvp in _circuitBreakers)
        {
            states[kvp.Key] = kvp.Value.State;
        }
        return states;
    }

    /// <summary>
    /// Resets all circuit breakers to the closed state.
    /// </summary>
    public void ResetAll()
    {
        foreach (var kvp in _circuitBreakers)
        {
            kvp.Value.Reset();
        }
    }
}
