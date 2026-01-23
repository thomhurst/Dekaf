namespace Dekaf.Statistics;

/// <summary>
/// Statistics for a single Kafka broker connection.
/// </summary>
public sealed class BrokerStatistics
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
    /// The current connection state.
    /// </summary>
    public required BrokerConnectionState State { get; init; }

    /// <summary>
    /// Number of requests sent to this broker.
    /// </summary>
    public long RequestsSent { get; init; }

    /// <summary>
    /// Number of responses received from this broker.
    /// </summary>
    public long ResponsesReceived { get; init; }

    /// <summary>
    /// Number of bytes sent to this broker.
    /// </summary>
    public long BytesSent { get; init; }

    /// <summary>
    /// Number of bytes received from this broker.
    /// </summary>
    public long BytesReceived { get; init; }

    /// <summary>
    /// Number of requests currently in-flight to this broker.
    /// </summary>
    public int RequestsInFlight { get; init; }

    /// <summary>
    /// Average request latency in milliseconds.
    /// </summary>
    public double AvgRequestLatencyMs { get; init; }
}

/// <summary>
/// Connection state for a broker.
/// </summary>
public enum BrokerConnectionState
{
    /// <summary>
    /// Not connected to the broker.
    /// </summary>
    Disconnected,

    /// <summary>
    /// Currently connecting to the broker.
    /// </summary>
    Connecting,

    /// <summary>
    /// Connected and ready for requests.
    /// </summary>
    Connected,

    /// <summary>
    /// Connection is closing.
    /// </summary>
    Closing
}
