namespace Dekaf.Networking;

/// <summary>
/// Interface for connection pooling.
/// </summary>
public interface IConnectionPool : IAsyncDisposable
{
    /// <summary>
    /// Gets a connection to a broker by ID.
    /// </summary>
    ValueTask<IKafkaConnection> GetConnectionAsync(int brokerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a connection to a broker by host and port.
    /// </summary>
    ValueTask<IKafkaConnection> GetConnectionAsync(string host, int port, CancellationToken cancellationToken = default);

    /// <summary>
    /// Registers a broker's connection information.
    /// </summary>
    void RegisterBroker(int brokerId, string host, int port);

    /// <summary>
    /// Removes a connection from the pool.
    /// </summary>
    ValueTask RemoveConnectionAsync(int brokerId);

    /// <summary>
    /// Closes all connections.
    /// </summary>
    ValueTask CloseAllAsync();
}
