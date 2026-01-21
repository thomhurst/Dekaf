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
    private readonly ConcurrentDictionary<string, IKafkaConnection> _connectionsByEndpoint = new();
    private readonly ConcurrentDictionary<int, IKafkaConnection> _connectionsById = new();
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
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

        var endpoint = $"{host}:{port}";

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
        CancellationToken cancellationToken)
    {
        var endpoint = $"{host}:{port}";

        await _connectionLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Double-check after acquiring lock
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
        finally
        {
            _connectionLock.Release();
        }
    }

    public async ValueTask RemoveConnectionAsync(int brokerId)
    {
        if (_connectionsById.TryRemove(brokerId, out var connection))
        {
            var endpoint = $"{connection.Host}:{connection.Port}";
            _connectionsByEndpoint.TryRemove(endpoint, out _);
            await connection.DisposeAsync().ConfigureAwait(false);
            _logger?.LogDebug("Removed connection to broker {BrokerId}", brokerId);
        }
    }

    public async ValueTask CloseAllAsync()
    {
        await _connectionLock.WaitAsync().ConfigureAwait(false);
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
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;
        await CloseAllAsync().ConfigureAwait(false);
        _connectionLock.Dispose();
    }

    private readonly record struct BrokerInfo(int BrokerId, string Host, int Port);
}
