using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Dekaf.Networking;

/// <summary>
/// Connection pool for managing connections to Kafka brokers.
/// </summary>
public sealed class ConnectionPool : IConnectionPool
{
    private readonly string? _clientId;
    private readonly ConnectionOptions _connectionOptions;
    private readonly ILoggerFactory? _loggerFactory;
    private readonly ILogger<ConnectionPool>? _logger;

    private readonly ConcurrentDictionary<int, BrokerInfo> _brokers = new();
    private readonly ConcurrentDictionary<EndpointKey, IKafkaConnection> _connectionsByEndpoint = new();
    private readonly ConcurrentDictionary<int, IKafkaConnection> _connectionsById = new();
    private readonly ConcurrentDictionary<EndpointKey, Lazy<ValueTask<IKafkaConnection>>> _connectionCreationTasks = new();
    private readonly SemaphoreSlim _disposeLock = new(1, 1);
    private volatile bool _disposed;

    public ConnectionPool(
        string? clientId = null,
        ConnectionOptions? connectionOptions = null,
        ILoggerFactory? loggerFactory = null)
    {
        _clientId = clientId;
        _connectionOptions = connectionOptions ?? new ConnectionOptions();
        _loggerFactory = loggerFactory;
        _logger = loggerFactory?.CreateLogger<ConnectionPool>();
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

            foreach (var connection in _connectionsByEndpoint.Values)
            {
                tasks.Add(connection.DisposeAsync());
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
