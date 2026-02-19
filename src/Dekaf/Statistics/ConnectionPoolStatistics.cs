namespace Dekaf.Statistics;

/// <summary>
/// Statistics snapshot for the connection pool.
/// </summary>
public sealed class ConnectionPoolStatistics
{
    /// <summary>
    /// Total number of active (connected) connections across all brokers.
    /// </summary>
    public int ActiveConnections { get; init; }

    /// <summary>
    /// Total number of idle connections (connected but no pending requests) across all brokers.
    /// </summary>
    public int IdleConnections { get; init; }

    /// <summary>
    /// Total number of connections created over the lifetime of the pool.
    /// </summary>
    public long TotalConnectionsCreated { get; init; }

    /// <summary>
    /// Total number of connections closed over the lifetime of the pool.
    /// </summary>
    public long TotalConnectionsClosed { get; init; }

    /// <summary>
    /// Total number of connection errors across all brokers.
    /// </summary>
    public long TotalErrors { get; init; }

    /// <summary>
    /// Statistics per broker.
    /// </summary>
    public IReadOnlyDictionary<int, BrokerConnectionStatistics> Brokers { get; init; } =
        new Dictionary<int, BrokerConnectionStatistics>();
}
