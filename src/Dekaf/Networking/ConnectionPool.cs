using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Dekaf.Networking;

/// <summary>
/// Connection pool for managing connections to Kafka brokers.
/// Supports multiple connections per broker for parallel request handling.
/// </summary>
public sealed class ConnectionPool : IConnectionPool
{
    private readonly string? _clientId;
    private readonly ConnectionOptions _connectionOptions;
    private readonly ILoggerFactory? _loggerFactory;
    private readonly ILogger<ConnectionPool>? _logger;
    private readonly int _connectionsPerBroker;

    private readonly ConcurrentDictionary<int, BrokerInfo> _brokers = new();
    private readonly ConcurrentDictionary<EndpointKey, IKafkaConnection> _connectionsByEndpoint = new();
    private readonly ConcurrentDictionary<int, IKafkaConnection> _connectionsById = new();
    private readonly ConcurrentDictionary<EndpointKey, Lazy<ValueTask<IKafkaConnection>>> _connectionCreationTasks = new();

    // Multi-connection support: connection groups and round-robin index
    private readonly ConcurrentDictionary<int, IKafkaConnection[]> _connectionGroupsById = new();
    private readonly ConcurrentDictionary<EndpointKey, IKafkaConnection[]> _connectionGroupsByEndpoint = new();
    private readonly ConcurrentDictionary<(int BrokerId, int Index), Lazy<ValueTask<IKafkaConnection>>> _connectionGroupCreationTasks = new();
    private int _nextConnectionIndex;

    private readonly SemaphoreSlim _disposeLock = new(1, 1);
    private volatile bool _disposed;

    public ConnectionPool(
        string? clientId = null,
        ConnectionOptions? connectionOptions = null,
        ILoggerFactory? loggerFactory = null,
        int connectionsPerBroker = 1)
    {
        _clientId = clientId;
        _connectionOptions = connectionOptions ?? new ConnectionOptions();
        _loggerFactory = loggerFactory;
        _logger = loggerFactory?.CreateLogger<ConnectionPool>();
        _connectionsPerBroker = Math.Max(1, connectionsPerBroker);
    }

    public void RegisterBroker(int brokerId, string host, int port)
    {
        _brokers[brokerId] = new BrokerInfo(brokerId, host, port);
        _logger?.LogDebug("Registered broker {BrokerId} at {Host}:{Port}", brokerId, host, port);
    }

    public async ValueTask<IKafkaConnection> GetConnectionAsync(int brokerId, CancellationToken cancellationToken = default)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(ConnectionPool));

        // Multi-connection path: use connection groups with round-robin selection
        if (_connectionsPerBroker > 1)
        {
            return await GetConnectionFromGroupAsync(brokerId, cancellationToken).ConfigureAwait(false);
        }

        // Single-connection path (original behavior)
        // Try to get existing connection
        if (_connectionsById.TryGetValue(brokerId, out var existing) && existing.IsConnected)
        {
            return existing;
        }

        // Get broker info
        if (!_brokers.TryGetValue(brokerId, out var brokerInfo))
        {
            throw new InvalidOperationException($"Unknown broker ID: {brokerId}");
        }

        return await GetOrCreateConnectionAsync(brokerId, brokerInfo.Host, brokerInfo.Port, cancellationToken)
            .ConfigureAwait(false);
    }

    private async ValueTask<IKafkaConnection> GetConnectionFromGroupAsync(int brokerId, CancellationToken cancellationToken)
    {
        // Get broker info
        if (!_brokers.TryGetValue(brokerId, out var brokerInfo))
        {
            throw new InvalidOperationException($"Unknown broker ID: {brokerId}");
        }

        // Try to get existing connection group
        if (_connectionGroupsById.TryGetValue(brokerId, out var connections))
        {
            // Round-robin selection among available connections
            var index = (uint)Interlocked.Increment(ref _nextConnectionIndex) % (uint)connections.Length;
            var connection = connections[index];

            if (connection is not null && connection.IsConnected)
            {
                return connection;
            }

            // Connection at this index is invalid, try to replace it
            return await ReplaceConnectionInGroupAsync(brokerId, brokerInfo, (int)index, cancellationToken).ConfigureAwait(false);
        }

        // Need to create new connection group
        return await CreateConnectionGroupAsync(brokerId, brokerInfo, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<IKafkaConnection> CreateConnectionGroupAsync(int brokerId, BrokerInfo brokerInfo, CancellationToken cancellationToken)
    {
        // Create all connections for this broker in parallel
        var connections = new IKafkaConnection[_connectionsPerBroker];
        var tasks = new Task<IKafkaConnection>[_connectionsPerBroker];

        for (var i = 0; i < _connectionsPerBroker; i++)
        {
            var index = i;
            // Use GetOrAdd pattern for each connection to avoid duplicates
            var lazyTask = _connectionGroupCreationTasks.GetOrAdd(
                (brokerId, index),
                _ => new Lazy<ValueTask<IKafkaConnection>>(() =>
                    CreateConnectionForGroupAsync(brokerId, brokerInfo.Host, brokerInfo.Port, index, CancellationToken.None)));

            tasks[i] = lazyTask.Value.AsTask();
        }

        await Task.WhenAll(tasks).ConfigureAwait(false);

        for (var i = 0; i < _connectionsPerBroker; i++)
        {
            connections[i] = tasks[i].Result;
            _connectionGroupCreationTasks.TryRemove((brokerId, i), out _);
        }

        // Atomically set the connection group
        _connectionGroupsById[brokerId] = connections;
        _connectionGroupsByEndpoint[new EndpointKey(brokerInfo.Host, brokerInfo.Port)] = connections;

        _logger?.LogDebug("Created connection group with {Count} connections to broker {BrokerId}", _connectionsPerBroker, brokerId);

        // Return the first connection
        return connections[0];
    }

    private async ValueTask<IKafkaConnection> ReplaceConnectionInGroupAsync(int brokerId, BrokerInfo brokerInfo, int index, CancellationToken cancellationToken)
    {
        // Use GetOrAdd pattern to ensure only one replacement happens
        var lazyTask = _connectionGroupCreationTasks.GetOrAdd(
            (brokerId, index),
            _ => new Lazy<ValueTask<IKafkaConnection>>(() =>
                CreateConnectionForGroupAsync(brokerId, brokerInfo.Host, brokerInfo.Port, index, CancellationToken.None)));

        try
        {
            var newConnection = await lazyTask.Value.ConfigureAwait(false);

            // Update the connection in the group
            if (_connectionGroupsById.TryGetValue(brokerId, out var connections))
            {
                var oldConnection = Interlocked.Exchange(ref connections[index], newConnection);
                if (oldConnection is not null && oldConnection != newConnection)
                {
                    await oldConnection.DisposeAsync().ConfigureAwait(false);
                }
            }

            _connectionGroupCreationTasks.TryRemove((brokerId, index), out _);
            return newConnection;
        }
        catch
        {
            _connectionGroupCreationTasks.TryRemove((brokerId, index), out _);
            throw;
        }
    }

    private async ValueTask<IKafkaConnection> CreateConnectionForGroupAsync(int brokerId, string host, int port, int index, CancellationToken cancellationToken)
    {
        var connection = new KafkaConnection(
            brokerId,
            host,
            port,
            _clientId,
            _connectionOptions,
            _loggerFactory?.CreateLogger<KafkaConnection>());

        await connection.ConnectAsync(cancellationToken).ConfigureAwait(false);

        _logger?.LogDebug("Created connection {Index} to broker {BrokerId} at {Host}:{Port}", index, brokerId, host, port);

        return connection;
    }

    public async ValueTask<IKafkaConnection> GetConnectionAsync(string host, int port, CancellationToken cancellationToken = default)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(ConnectionPool));

        var endpoint = new EndpointKey(host, port);

        // Try to get existing connection
        if (_connectionsByEndpoint.TryGetValue(endpoint, out var existing) && existing.IsConnected)
        {
            return existing;
        }

        return await GetOrCreateConnectionAsync(-1, host, port, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<IKafkaConnection> GetOrCreateConnectionAsync(
        int brokerId,
        string host,
        int port,
        CancellationToken cancellationToken,
        int retryCount = 0)
    {
        const int MaxRetries = 3;

        if (retryCount >= MaxRetries)
        {
            throw new InvalidOperationException($"Failed to create connection after {MaxRetries} retries");
        }

        var endpoint = new EndpointKey(host, port);

        // Lock-free pattern: Use GetOrAdd with Lazy to ensure only one connection creation per endpoint
        // Note: CancellationToken.None is used to avoid capturing the caller's token in the Lazy delegate
        var lazyConnection = _connectionCreationTasks.GetOrAdd(endpoint,
            _ => new Lazy<ValueTask<IKafkaConnection>>(() => CreateConnectionAsync(brokerId, host, port, CancellationToken.None)));

        try
        {
            var connection = await lazyConnection.Value.ConfigureAwait(false);

            // Verify connection is still valid
            if (!connection.IsConnected)
            {
                // Connection became invalid, remove and retry
                _connectionCreationTasks.TryRemove(endpoint, out _);
                _connectionsByEndpoint.TryRemove(endpoint, out _);
                if (brokerId >= 0)
                    _connectionsById.TryRemove(brokerId, out _);

                // Recursive retry (will create new Lazy)
                return await GetOrCreateConnectionAsync(brokerId, host, port, cancellationToken, retryCount + 1).ConfigureAwait(false);
            }

            // Success: Remove the task to prevent memory leak
            _connectionCreationTasks.TryRemove(endpoint, out _);

            return connection;
        }
        catch
        {
            // On exception, remove the failed lazy so retry can create a new one
            _connectionCreationTasks.TryRemove(endpoint, out _);
            throw;
        }
    }

    private async ValueTask<IKafkaConnection> CreateConnectionAsync(
        int brokerId,
        string host,
        int port,
        CancellationToken cancellationToken)
    {
        var endpoint = new EndpointKey(host, port);

        // Check if there's an existing valid connection (race condition with fast path)
        if (_connectionsByEndpoint.TryGetValue(endpoint, out var existing) && existing.IsConnected)
        {
            return existing;
        }

        // Remove stale connection if any
        if (existing is not null)
        {
            _connectionsByEndpoint.TryRemove(endpoint, out _);
            if (brokerId >= 0)
                _connectionsById.TryRemove(brokerId, out _);
            await existing.DisposeAsync().ConfigureAwait(false);
        }

        // Create new connection
        var connection = new KafkaConnection(
            brokerId,
            host,
            port,
            _clientId,
            _connectionOptions,
            _loggerFactory?.CreateLogger<KafkaConnection>());

        await connection.ConnectAsync(cancellationToken).ConfigureAwait(false);

        _connectionsByEndpoint[endpoint] = connection;
        if (brokerId >= 0)
        {
            _connectionsById[brokerId] = connection;
        }

        _logger?.LogDebug("Created connection to broker {BrokerId} at {Host}:{Port}", brokerId, host, port);

        return connection;
    }

    public async ValueTask RemoveConnectionAsync(int brokerId)
    {
        if (_connectionsById.TryRemove(brokerId, out var connection))
        {
            var endpoint = new EndpointKey(connection.Host, connection.Port);
            _connectionsByEndpoint.TryRemove(endpoint, out _);
            _connectionCreationTasks.TryRemove(endpoint, out _);
            await connection.DisposeAsync().ConfigureAwait(false);
            _logger?.LogDebug("Removed connection to broker {BrokerId}", brokerId);
        }
    }

    public async ValueTask CloseAllAsync()
    {
        await _disposeLock.WaitAsync().ConfigureAwait(false);
        try
        {
            var tasks = new List<ValueTask>();

            // Close single connections (used when _connectionsPerBroker == 1)
            // Note: Single connections and connection groups are mutually exclusive -
            // GetConnectionAsync uses one path or the other based on _connectionsPerBroker
            foreach (var connection in _connectionsByEndpoint.Values)
            {
                tasks.Add(connection.DisposeAsync());
            }

            // Close connection groups (used when _connectionsPerBroker > 1)
            foreach (var connectionGroup in _connectionGroupsById.Values)
            {
                foreach (var connection in connectionGroup)
                {
                    if (connection is not null)
                    {
                        tasks.Add(connection.DisposeAsync());
                    }
                }
            }

            foreach (var task in tasks)
            {
                try
                {
                    await task.ConfigureAwait(false);
                }
                catch
                {
                    // Ignore errors during cleanup
                }
            }

            _connectionsByEndpoint.Clear();
            _connectionsById.Clear();
            _connectionCreationTasks.Clear();
            _connectionGroupsById.Clear();
            _connectionGroupsByEndpoint.Clear();
            _connectionGroupCreationTasks.Clear();
        }
        finally
        {
            _disposeLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;
        await CloseAllAsync().ConfigureAwait(false);
        _disposeLock.Dispose();
    }

    private readonly record struct EndpointKey(string Host, int Port);
    private readonly record struct BrokerInfo(int BrokerId, string Host, int Port);
}
