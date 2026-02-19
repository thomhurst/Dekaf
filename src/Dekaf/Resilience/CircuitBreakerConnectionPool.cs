using Dekaf.Networking;

namespace Dekaf.Resilience;

/// <summary>
/// A connection pool decorator that integrates circuit breaker protection per broker.
/// Checks the circuit breaker state before providing a connection, and wraps the
/// returned connection to automatically record successes and failures.
/// </summary>
internal sealed class CircuitBreakerConnectionPool : IConnectionPool
{
    private readonly IConnectionPool _inner;
    private readonly BrokerCircuitBreakerRegistry _registry;

    public CircuitBreakerConnectionPool(IConnectionPool inner, BrokerCircuitBreakerRegistry registry)
    {
        _inner = inner;
        _registry = registry;
    }

    /// <summary>
    /// Gets the circuit breaker registry for statistics reporting.
    /// </summary>
    internal BrokerCircuitBreakerRegistry Registry => _registry;

    public async ValueTask<IKafkaConnection> GetConnectionAsync(int brokerId, CancellationToken cancellationToken = default)
    {
        var circuitBreaker = _registry.GetOrCreate(brokerId);

        if (!circuitBreaker.IsAllowingRequests)
        {
            throw new CircuitBreakerOpenException(brokerId);
        }

        var connection = await _inner.GetConnectionAsync(brokerId, cancellationToken).ConfigureAwait(false);
        return new CircuitBreakerConnection(connection, circuitBreaker);
    }

    public async ValueTask<IKafkaConnection> GetConnectionAsync(string host, int port, CancellationToken cancellationToken = default)
    {
        // For bootstrap connections (no broker ID yet), bypass circuit breaker
        return await _inner.GetConnectionAsync(host, port, cancellationToken).ConfigureAwait(false);
    }

    public void RegisterBroker(int brokerId, string host, int port)
    {
        _inner.RegisterBroker(brokerId, host, port);
    }

    public async ValueTask RemoveConnectionAsync(int brokerId)
    {
        await _inner.RemoveConnectionAsync(brokerId).ConfigureAwait(false);
    }

    public async ValueTask CloseAllAsync()
    {
        await _inner.CloseAllAsync().ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        await _inner.DisposeAsync().ConfigureAwait(false);
    }
}
