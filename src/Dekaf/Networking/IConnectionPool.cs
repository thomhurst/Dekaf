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
    /// Gets a connection to a broker by ID and specific index within the connection group.
    /// Used by partition-affined multi-connection mode to pin partitions to specific connections.
    /// </summary>
    ValueTask<IKafkaConnection> GetConnectionByIndexAsync(int brokerId, int index, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a connection to a broker by host and port.
    /// </summary>
    ValueTask<IKafkaConnection> GetConnectionAsync(string host, int port, CancellationToken cancellationToken = default);

    /// <summary>
    /// Registers a broker's connection information.
    /// </summary>
    void RegisterBroker(int brokerId, string host, int port);

    /// <summary>
    /// Scales the connection group for a broker to the specified count.
    /// If the broker already has at least <paramref name="newCount"/> connections, this is a no-op.
    /// New connections are created in parallel and appended to the existing group.
    /// To reduce the connection count with caller-managed draining before disposal,
    /// use <see cref="ShrinkConnectionGroupAsync"/> instead.
    /// </summary>
    /// <param name="brokerId">The broker to scale connections for.</param>
    /// <param name="newCount">The desired number of connections.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The actual connection count after scaling.</returns>
    ValueTask<int> ScaleConnectionGroupAsync(int brokerId, int newCount, CancellationToken cancellationToken = default);

    /// <summary>
    /// Shrinks the connection group for a broker by removing one connection (the last in the group)
    /// and returning it to the caller for draining. Unlike <see cref="ScaleConnectionGroupAsync"/>,
    /// which creates connections and manages their lifecycle internally, this method transfers
    /// ownership of the removed connection to the caller, who is responsible for waiting until
    /// in-flight requests complete before disposing it.
    /// If the broker already has at most <paramref name="newCount"/> connections, this is a no-op.
    /// </summary>
    /// <param name="brokerId">The broker to shrink connections for.</param>
    /// <param name="newCount">The desired number of connections (must be >= 1).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The removed connection for draining, or null if no shrink was needed.</returns>
    ValueTask<IKafkaConnection?> ShrinkConnectionGroupAsync(int brokerId, int newCount, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a connection from the pool.
    /// </summary>
    ValueTask RemoveConnectionAsync(int brokerId);

    /// <summary>
    /// Closes all connections.
    /// </summary>
    ValueTask CloseAllAsync();
}
