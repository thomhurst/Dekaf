namespace Dekaf.Statistics;

/// <summary>
/// Connection statistics for a single Kafka broker in the connection pool.
/// </summary>
public sealed class BrokerConnectionStatistics
{
    /// <summary>
    /// The broker node ID.
    /// </summary>
    public required int NodeId { get; init; }

    /// <summary>
    /// The broker host.
    /// </summary>
    public required string Host { get; init; }

    /// <summary>
    /// The broker port.
    /// </summary>
    public required int Port { get; init; }

    /// <summary>
    /// Number of active connections to this broker.
    /// </summary>
    public int ConnectionCount { get; init; }

    /// <summary>
    /// Total number of connections created to this broker over the lifetime of the pool.
    /// </summary>
    public long TotalConnectionsCreated { get; init; }

    /// <summary>
    /// Total number of connections closed to this broker over the lifetime of the pool.
    /// </summary>
    public long TotalConnectionsClosed { get; init; }

    /// <summary>
    /// Number of requests currently pending (in-flight) to this broker.
    /// </summary>
    public int PendingRequests { get; init; }

    /// <summary>
    /// Total number of requests sent to this broker.
    /// </summary>
    public long TotalRequestsSent { get; init; }

    /// <summary>
    /// Total number of responses received from this broker.
    /// </summary>
    public long TotalResponsesReceived { get; init; }

    /// <summary>
    /// Total number of errors encountered on connections to this broker.
    /// </summary>
    public long TotalErrors { get; init; }
}
