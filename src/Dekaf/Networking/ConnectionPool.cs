using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Dekaf.Networking;

/// <summary>
/// Connection pool for managing connections to Kafka brokers.
/// Supports multiple connections per broker for parallel request handling.
/// </summary>
public sealed partial class ConnectionPool : IConnectionPool
{
    private readonly string? _clientId;
    private readonly ConnectionOptions _connectionOptions;
    private readonly ILoggerFactory? _loggerFactory;
    private readonly ILogger _logger;
    private readonly int _connectionsPerBroker;
    private readonly ResponseBufferPool _responseBufferPool;

    /// <summary>
    /// Optional factory for creating connections, used by tests to inject fakes.
    /// When null, the default KafkaConnection constructor is used.
    /// Parameters: brokerId, host, port, index, cancellationToken.
    /// </summary>
    private readonly Func<int, string, int, int, CancellationToken, ValueTask<IKafkaConnection>>? _connectionFactory;

    // Default BufferMemory if not configured (256 MB)
    private const ulong DefaultBufferMemory = 268435456;

    private readonly ConcurrentDictionary<int, BrokerInfo> _brokers = new();
    private readonly ConcurrentDictionary<EndpointKey, IKafkaConnection> _connectionsByEndpoint = new();
    private readonly ConcurrentDictionary<int, IKafkaConnection> _connectionsById = new();
    // Per-endpoint semaphores to deduplicate concurrent single-connection creation
    private readonly ConcurrentDictionary<EndpointKey, SemaphoreSlim> _connectionCreationLocks = new();

    // Multi-connection support: connection groups and round-robin index
    private readonly ConcurrentDictionary<int, IKafkaConnection[]> _connectionGroupsById = new();
    private readonly ConcurrentDictionary<(int BrokerId, int Index), SemaphoreSlim> _connectionReplacementLocks = new();

    // Per-broker semaphores to deduplicate concurrent group creation
    private readonly ConcurrentDictionary<int, SemaphoreSlim> _groupCreationLocks = new();

    // Thread-local round-robin counter to eliminate atomic contention on hot path
    // Each thread maintains its own counter, avoiding Interlocked contention
    // Inspired by librdkafka's per-thread state to minimize cross-thread synchronization
    [ThreadStatic]
    private static int t_nextConnectionIndex;

    private readonly SemaphoreSlim _disposeLock = new(1, 1);
    private readonly SemaphoreSlim _scaleLock = new(1, 1);
    private int _disposed;

    public ConnectionPool(
        string? clientId = null,
        ConnectionOptions? connectionOptions = null,
        ILoggerFactory? loggerFactory = null,
        int connectionsPerBroker = 1)
        : this(clientId, connectionOptions, loggerFactory, connectionsPerBroker, ResponseBufferPool.Default)
    {
    }

    internal ConnectionPool(
        string? clientId,
        ConnectionOptions? connectionOptions,
        ILoggerFactory? loggerFactory,
        int connectionsPerBroker,
        ResponseBufferPool responseBufferPool)
    {
        _clientId = clientId;
        _connectionOptions = connectionOptions ?? new ConnectionOptions();
        _loggerFactory = loggerFactory;
        _logger = loggerFactory?.CreateLogger<ConnectionPool>() ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<ConnectionPool>.Instance;
        _connectionsPerBroker = Math.Max(1, connectionsPerBroker);
        _responseBufferPool = responseBufferPool;
    }

    /// <summary>
    /// Test-only constructor that accepts a connection factory delegate,
    /// allowing tests to inject fake connections without real TCP I/O.
    /// </summary>
    internal ConnectionPool(
        string? clientId,
        ConnectionOptions? connectionOptions,
        int connectionsPerBroker,
        Func<int, string, int, int, CancellationToken, ValueTask<IKafkaConnection>> connectionFactory)
    {
        _clientId = clientId;
        _connectionOptions = connectionOptions ?? new ConnectionOptions();
        _loggerFactory = null;
        _logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<ConnectionPool>.Instance;
        _connectionsPerBroker = Math.Max(1, connectionsPerBroker);
        _responseBufferPool = ResponseBufferPool.Default;
        _connectionFactory = connectionFactory;
    }

    public void RegisterBroker(int brokerId, string host, int port)
    {
        _brokers[brokerId] = new BrokerInfo(brokerId, host, port);
        LogRegisteredBroker(brokerId, host, port);
    }

    public async ValueTask<IKafkaConnection> GetConnectionAsync(int brokerId, CancellationToken cancellationToken = default)
    {
        if (Volatile.Read(ref _disposed) != 0)
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
            LogConnectionAcquired(brokerId);
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
            // Round-robin selection using thread-local counter (no atomic contention)
            var index = (uint)(++t_nextConnectionIndex) % (uint)connections.Length;
            var connection = connections[index];

            if (connection is not null && connection.IsConnected)
            {
                return connection;
            }

            // Connection at this index is invalid, try to replace it
            LogConnectionReplacement(brokerId, (int)index);
            return await ReplaceConnectionInGroupAsync(brokerId, brokerInfo, (int)index, cancellationToken).ConfigureAwait(false);
        }

        // Need to create new connection group
        return await CreateConnectionGroupAsync(brokerId, brokerInfo, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<IKafkaConnection> GetConnectionByIndexAsync(int brokerId, int index, CancellationToken cancellationToken = default)
    {
        if (Volatile.Read(ref _disposed) != 0)
            throw new ObjectDisposedException(nameof(ConnectionPool));

        if (!_brokers.TryGetValue(brokerId, out var brokerInfo))
        {
            throw new InvalidOperationException($"Unknown broker ID: {brokerId}");
        }

        if (_connectionsPerBroker <= 1)
        {
            // Graceful degradation: when only one connection per broker is configured,
            // fall back to the single-connection path, silently ignoring the requested index.
            return await GetConnectionAsync(brokerId, cancellationToken).ConfigureAwait(false);
        }

        if (!_connectionGroupsById.TryGetValue(brokerId, out var connections))
        {
            // CreateConnectionGroupAsync populates _connectionGroupsById and returns
            // the first connection. If it throws (timeout, broker unreachable), the
            // exception propagates — no null-dereference risk below.
            await CreateConnectionGroupAsync(brokerId, brokerInfo, cancellationToken).ConfigureAwait(false);

            if (!_connectionGroupsById.TryGetValue(brokerId, out connections))
                throw new InvalidOperationException($"Connection group for broker {brokerId} was not created");
        }

        var effectiveIndex = index % connections.Length;
        var connection = connections[effectiveIndex];

        if (connection is not null && connection.IsConnected)
        {
            return connection;
        }

        return await ReplaceConnectionInGroupAsync(brokerId, brokerInfo, effectiveIndex, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<IKafkaConnection> CreateConnectionGroupAsync(int brokerId, BrokerInfo brokerInfo, CancellationToken cancellationToken)
    {
        // Use a per-broker semaphore to ensure only one caller creates the group.
        // Without this, concurrent callers both create full connection groups, leaking TCP connections.
        var groupLock = _groupCreationLocks.GetOrAdd(brokerId, static _ => new SemaphoreSlim(1, 1));

        await groupLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Re-check after acquiring lock: another caller may have already created the group.
            // No need to validate individual connections — GetConnectionFromGroupAsync handles
            // per-slot reconnection via ReplaceConnectionInGroupAsync.
            if (_connectionGroupsById.TryGetValue(brokerId, out var existingGroup))
            {
                return existingGroup[0];
            }

            return await CreateConnectionGroupCoreAsync(brokerId, brokerInfo, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            groupLock.Release();
        }
    }

    private async ValueTask<IKafkaConnection> CreateConnectionGroupCoreAsync(int brokerId, BrokerInfo brokerInfo, CancellationToken cancellationToken)
    {
        // Create all connections for this broker in parallel
        var connections = new IKafkaConnection[_connectionsPerBroker];
        var tasks = new Task<IKafkaConnection>[_connectionsPerBroker];

        using var timeoutCts = new CancellationTokenSource(_connectionOptions.ConnectionTimeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            timeoutCts.Token);

        var token = linkedCts.Token;

        var success = false;
        try
        {
            for (var i = 0; i < _connectionsPerBroker; i++)
            {
                var index = i;
                tasks[i] = CreateConnectionForGroupAsync(brokerId, brokerInfo.Host, brokerInfo.Port, index, token).AsTask();
            }

            // Add timeout to prevent indefinite hang if connection creation stalls
            // Similar pattern to RecordAccumulator disposal fix (commit 8f26f05)
            await Task.WhenAll(tasks).WaitAsync(_connectionOptions.ConnectionTimeout, cancellationToken).ConfigureAwait(false);

            for (var i = 0; i < _connectionsPerBroker; i++)
            {
                connections[i] = await tasks[i].ConfigureAwait(false);
            }

            // Atomically set the connection group
            _connectionGroupsById[brokerId] = connections;

            LogCreatedConnectionGroup(_connectionsPerBroker, brokerId);

            success = true;
            return connections[0];
        }
        catch (TimeoutException)
        {
            throw new KafkaException(
                $"Connection group creation timeout after {(int)_connectionOptions.ConnectionTimeout.TotalMilliseconds}ms to broker {brokerId}");
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            throw new KafkaException(
                $"Connection group creation timeout after {(int)_connectionOptions.ConnectionTimeout.TotalMilliseconds}ms to broker {brokerId}");
        }
        finally
        {
            // On any failure, dispose successfully-created connections to avoid leaking TCP handles.
            if (!success)
                await DisposeCompletedTaskConnectionsAsync(tasks).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Disposes connections from any successfully completed tasks in the array.
    /// Used for cleanup during partial failure of parallel connection creation.
    /// </summary>
    private static async ValueTask DisposeCompletedTaskConnectionsAsync(Task<IKafkaConnection>[] tasks)
    {
        foreach (var task in tasks)
        {
            if (task is not null && task.IsCompletedSuccessfully)
            {
                try { await task.Result.DisposeAsync().ConfigureAwait(false); }
                catch { /* best-effort cleanup */ }
            }
        }
    }

    public async ValueTask<int> ScaleConnectionGroupAsync(int brokerId, int newCount, CancellationToken cancellationToken = default)
    {
        if (Volatile.Read(ref _disposed) != 0)
            throw new ObjectDisposedException(nameof(ConnectionPool));

        if (!_brokers.TryGetValue(brokerId, out var brokerInfo))
            throw new InvalidOperationException($"Unknown broker ID: {brokerId}");

        // Check current group size without locking
        if (_connectionGroupsById.TryGetValue(brokerId, out var currentGroup) && currentGroup.Length >= newCount)
            return currentGroup.Length;

        // Serialize scaling operations per pool (not per broker, to keep implementation simple)
        await _scaleLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Re-check after acquiring lock
            if (_connectionGroupsById.TryGetValue(brokerId, out currentGroup) && currentGroup.Length >= newCount)
                return currentGroup.Length;

            var existingCount = currentGroup?.Length ?? 0;
            var additionalCount = newCount - existingCount;

            // Create additional connections in parallel with a single timeout mechanism
            using var timeoutCts = new CancellationTokenSource(_connectionOptions.ConnectionTimeout);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            var tasks = new Task<IKafkaConnection>[additionalCount];

            for (var i = 0; i < additionalCount; i++)
            {
                var index = existingCount + i;
                tasks[i] = CreateConnectionForGroupAsync(brokerId, brokerInfo.Host, brokerInfo.Port, index, linkedCts.Token).AsTask();
            }

            try
            {
                await Task.WhenAll(tasks).ConfigureAwait(false);
            }
            catch
            {
                // Close any successfully created connections to avoid leaking TCP handles.
                await DisposeCompletedTaskConnectionsAsync(tasks).ConfigureAwait(false);
                throw;
            }

            // Build new array: copy existing + write completed tasks directly
            var newGroup = new IKafkaConnection[newCount];
            if (currentGroup is not null)
                Array.Copy(currentGroup, newGroup, existingCount);
            for (var i = 0; i < additionalCount; i++)
                newGroup[existingCount + i] = tasks[i].Result;

            // Atomically swap the connection group
            _connectionGroupsById[brokerId] = newGroup;

            LogScaledConnectionGroup(existingCount, newCount, brokerId);
            return newCount;
        }
        finally
        {
            _scaleLock.Release();
        }
    }

    public async ValueTask<IKafkaConnection?> ShrinkConnectionGroupAsync(int brokerId, int newCount, CancellationToken cancellationToken = default)
    {
        if (Volatile.Read(ref _disposed) != 0)
            throw new ObjectDisposedException(nameof(ConnectionPool));

        ArgumentOutOfRangeException.ThrowIfLessThan(newCount, 1);

        // Check current group size without locking
        if (!_connectionGroupsById.TryGetValue(brokerId, out var currentGroup) || currentGroup.Length <= newCount)
            return null;

        // Serialize scaling operations per pool
        await _scaleLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Re-check after acquiring lock
            if (!_connectionGroupsById.TryGetValue(brokerId, out currentGroup) || currentGroup.Length <= newCount)
                return null;

            // Remove the last connection from the group
            var removedConnection = currentGroup[^1];
            var shrunkGroup = new IKafkaConnection[currentGroup.Length - 1];
            Array.Copy(currentGroup, shrunkGroup, shrunkGroup.Length);

            // Atomically swap the connection group
            _connectionGroupsById[brokerId] = shrunkGroup;

            // Clean up creation task slot for the removed index.
            // The semaphore is intentionally NOT disposed here: a concurrent
            // ReplaceConnectionInGroupAsync may still be holding or waiting on it.
            // Disposing would cause ObjectDisposedException in that concurrent caller.
            // The semaphore will be disposed later during CloseAllAsync/DisposeAsync cleanup.
            _connectionReplacementLocks.TryRemove((brokerId, currentGroup.Length - 1), out _);

            LogShrunkConnectionGroup(currentGroup.Length, shrunkGroup.Length, brokerId);

            // Return the removed connection for caller-managed draining and disposal
            return removedConnection;
        }
        finally
        {
            _scaleLock.Release();
        }
    }

    private async ValueTask<IKafkaConnection> ReplaceConnectionInGroupAsync(int brokerId, BrokerInfo brokerInfo, int index, CancellationToken cancellationToken)
    {
        // Use a per-(broker,index) SemaphoreSlim instead of Lazy<ValueTask> to avoid
        // capturing a caller-scoped CancellationToken that gets disposed in the caller's finally block.
        // Each caller creates its own timeout CTS, so no token outlives its CTS.
        var replacementLock = _connectionReplacementLocks.GetOrAdd(
            (brokerId, index),
            static _ => new SemaphoreSlim(1, 1));

        using var timeoutCts = new CancellationTokenSource(_connectionOptions.ConnectionTimeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            timeoutCts.Token);

        var token = linkedCts.Token;

        await replacementLock.WaitAsync(token).ConfigureAwait(false);
        try
        {
            // Re-check: another caller may have already replaced this connection
            if (_connectionGroupsById.TryGetValue(brokerId, out var currentConnections)
                && index < currentConnections.Length
                && currentConnections[index] is not null
                && currentConnections[index].IsConnected)
            {
                return currentConnections[index];
            }

            var connection = await CreateConnectionForGroupAsync(brokerId, brokerInfo.Host, brokerInfo.Port, index, token).ConfigureAwait(false);

            // Acquire _scaleLock to protect the array write against concurrent
            // ShrinkConnectionGroupAsync, which may have swapped the array reference.
            bool stored = false;
            await _scaleLock.WaitAsync(token).ConfigureAwait(false);
            try
            {
                if (_connectionGroupsById.TryGetValue(brokerId, out var connections) && index < connections.Length)
                {
                    connections[index] = connection;
                    stored = true;
                }
            }
            finally
            {
                _scaleLock.Release();
            }

            // If the index is now out of bounds (shrink happened concurrently),
            // dispose the orphaned connection to avoid leaking TCP handles.
            if (!stored)
            {
                await connection.DisposeAsync().ConfigureAwait(false);
                throw new KafkaException(
                    $"Connection slot {index} for broker {brokerId} was removed by a concurrent shrink");
            }

            return connection;
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            throw new KafkaException(
                $"Connection replacement timeout after {(int)_connectionOptions.ConnectionTimeout.TotalMilliseconds}ms to broker {brokerId} index {index}");
        }
        finally
        {
            replacementLock.Release();
        }
    }

    private async ValueTask<IKafkaConnection> CreateConnectionForGroupAsync(int brokerId, string host, int port, int index, CancellationToken cancellationToken)
    {
        if (_connectionFactory is not null)
        {
            var factoryConnection = await _connectionFactory(brokerId, host, port, index, cancellationToken).ConfigureAwait(false);
            LogCreatedConnectionForGroup(index, brokerId, host, port);
            return factoryConnection;
        }

        var connection = new KafkaConnection(
            brokerId, host, port,
            _clientId, _connectionOptions,
            _loggerFactory?.CreateLogger<KafkaConnection>(),
            DefaultBufferMemory, _connectionsPerBroker,
            _responseBufferPool);

        await connection.ConnectAsync(cancellationToken).ConfigureAwait(false);

        LogCreatedConnectionForGroup(index, brokerId, host, port);

        return connection;
    }

    public async ValueTask<IKafkaConnection> GetConnectionAsync(string host, int port, CancellationToken cancellationToken = default)
    {
        if (Volatile.Read(ref _disposed) != 0)
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
        CancellationToken cancellationToken)
    {
        const int MaxRetries = 3;

        var endpoint = new EndpointKey(host, port);

        // Use a per-endpoint semaphore to deduplicate concurrent connection creation.
        // Each caller creates its own timeout CTS, so no token outlives its CTS.
        // This replaces the previous Lazy<ValueTask> pattern which violated the ValueTask
        // contract (multiple awaiters) and captured a caller-scoped CTS that could be disposed.
        var creationLock = _connectionCreationLocks.GetOrAdd(endpoint, static _ => new SemaphoreSlim(1, 1));

        using var timeoutCts = new CancellationTokenSource(_connectionOptions.ConnectionTimeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            timeoutCts.Token);

        var token = linkedCts.Token;

        await creationLock.WaitAsync(token).ConfigureAwait(false);
        try
        {
            for (var attempt = 0; attempt < MaxRetries; attempt++)
            {
                // Re-check after acquiring lock: another caller may have already created the connection.
                if (_connectionsByEndpoint.TryGetValue(endpoint, out var existing) && existing.IsConnected)
                {
                    return existing;
                }

                // Dispose disconnected connection before creating a new one to avoid socket/pipe leaks.
                if (existing is not null)
                {
                    _connectionsByEndpoint.TryRemove(endpoint, out _);
                    if (brokerId >= 0)
                        _connectionsById.TryRemove(brokerId, out _);

                    try { await existing.DisposeAsync().ConfigureAwait(false); }
                    catch { /* best-effort cleanup */ }
                }

                var connection = await CreateConnectionAsync(brokerId, host, port, token).ConfigureAwait(false);

                // Verify connection is still valid
                if (connection.IsConnected)
                {
                    return connection;
                }

                // Connection failed immediately after creation — clean up and retry
                _connectionsByEndpoint.TryRemove(endpoint, out _);
                if (brokerId >= 0)
                    _connectionsById.TryRemove(brokerId, out _);

                try { await connection.DisposeAsync().ConfigureAwait(false); }
                catch { /* best-effort cleanup */ }
            }

            throw new InvalidOperationException($"Failed to create connection after {MaxRetries} retries");
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            throw new KafkaException(
                $"Connection timeout after {(int)_connectionOptions.ConnectionTimeout.TotalMilliseconds}ms to broker {brokerId} ({host}:{port})");
        }
        finally
        {
            creationLock.Release();
        }
    }

    private async ValueTask<IKafkaConnection> CreateConnectionAsync(
        int brokerId,
        string host,
        int port,
        CancellationToken cancellationToken)
    {
        var endpoint = new EndpointKey(host, port);

        // Note: Stale connection cleanup is handled by the caller (GetOrCreateConnectionAsync)
        // within the creation semaphore, so no duplicate cleanup is needed here.

        // Create new connection
        var connection = new KafkaConnection(
            brokerId, host, port,
            _clientId, _connectionOptions,
            _loggerFactory?.CreateLogger<KafkaConnection>(),
            DefaultBufferMemory, _connectionsPerBroker,
            _responseBufferPool);

        await connection.ConnectAsync(cancellationToken).ConfigureAwait(false);

        _connectionsByEndpoint[endpoint] = connection;
        if (brokerId >= 0)
        {
            _connectionsById[brokerId] = connection;
        }

        LogCreatedConnection(brokerId, host, port);

        return connection;
    }

    public async ValueTask RemoveConnectionAsync(int brokerId)
    {
        if (_connectionsById.TryRemove(brokerId, out var connection))
        {
            var endpoint = new EndpointKey(connection.Host, connection.Port);
            _connectionsByEndpoint.TryRemove(endpoint, out _);
            if (_connectionCreationLocks.TryRemove(endpoint, out var creationSem))
                creationSem.Dispose();
            await connection.DisposeAsync().ConfigureAwait(false);
            LogRemovedConnection(brokerId);
        }

        if (_connectionGroupsById.TryRemove(brokerId, out var group))
        {
            for (var i = 0; i < _connectionsPerBroker; i++)
            {
                // Intentionally not disposed: a concurrent ReplaceConnectionInGroupAsync
                // may be holding or waiting on it. Will be disposed during DisposeAsync.
                _connectionReplacementLocks.TryRemove((brokerId, i), out _);
            }

            // Same reasoning: a concurrent CreateConnectionGroupAsync may hold a reference.
            _groupCreationLocks.TryRemove(brokerId, out _);

            foreach (var conn in group)
            {
                if (conn is not null)
                    await conn.DisposeAsync().ConfigureAwait(false);
            }

            LogRemovedConnection(brokerId);
        }
    }

    public async ValueTask CloseAllAsync()
    {
        await _disposeLock.WaitAsync().ConfigureAwait(false);
        try
        {
            LogClosingAllConnections();

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

            LogDisposingConnections(tasks.Count);

            // Dispose all connections concurrently with a single overall timeout.
            // Connection.DisposeAsync() was already kicked off above; this just awaits completion.
            // Using Task.WhenAll with one timeout prevents chaining: N connections complete in
            // max(individual) time rather than sum(individual) time.
            if (tasks.Count > 0)
            {
                try
                {
                    var allTasks = new Task[tasks.Count];
                    for (var i = 0; i < tasks.Count; i++)
                    {
                        allTasks[i] = tasks[i].AsTask();
                    }

                    await Task.WhenAll(allTasks)
                        .WaitAsync(_connectionOptions.ConnectionTimeout)
                        .ConfigureAwait(false);
                }
                catch (TimeoutException)
                {
                    LogConnectionDisposalExceededTimeout(_connectionOptions.ConnectionTimeout.TotalMilliseconds);
                }
                catch (Exception ex)
                {
                    LogErrorDuringConnectionDisposal(ex);
                }
            }

            _connectionsByEndpoint.Clear();
            _connectionsById.Clear();
            _connectionGroupsById.Clear();

            if (Volatile.Read(ref _disposed) != 0)
            {
                // _disposed is set — no new callers can arrive, safe to dispose semaphores
                foreach (var sem in _connectionCreationLocks.Values) sem.Dispose();
                foreach (var sem in _connectionReplacementLocks.Values) sem.Dispose();
                foreach (var sem in _groupCreationLocks.Values) sem.Dispose();
            }
            _connectionCreationLocks.Clear();
            _connectionReplacementLocks.Clear();
            _groupCreationLocks.Clear();

            LogAllConnectionsClosed();
        }
        finally
        {
            _disposeLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        await CloseAllAsync().ConfigureAwait(false);

        _disposeLock.Dispose();
        _scaleLock.Dispose();
    }

    #region Logging

    [LoggerMessage(Level = LogLevel.Debug, Message = "Registered broker {BrokerId} at {Host}:{Port}")]
    private partial void LogRegisteredBroker(int brokerId, string host, int port);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Created connection group with {Count} connections to broker {BrokerId}")]
    private partial void LogCreatedConnectionGroup(int count, int brokerId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Created connection {Index} to broker {BrokerId} at {Host}:{Port}")]
    private partial void LogCreatedConnectionForGroup(int index, int brokerId, string host, int port);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Created connection to broker {BrokerId} at {Host}:{Port}")]
    private partial void LogCreatedConnection(int brokerId, string host, int port);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Removed connection to broker {BrokerId}")]
    private partial void LogRemovedConnection(int brokerId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Closing all connections")]
    private partial void LogClosingAllConnections();

    [LoggerMessage(Level = LogLevel.Debug, Message = "Disposing {Count} connections with graceful timeout")]
    private partial void LogDisposingConnections(int count);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Connection disposal exceeded timeout ({Timeout}ms), forcing shutdown")]
    private partial void LogConnectionDisposalExceededTimeout(double timeout);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Connection disposal timed out ({Timeout}ms), forcing shutdown")]
    private partial void LogConnectionDisposalTimedOut(double timeout);

    [LoggerMessage(Level = LogLevel.Error, Message = "Error during connection disposal")]
    private partial void LogErrorDuringConnectionDisposal(Exception ex);

    [LoggerMessage(Level = LogLevel.Information, Message = "All connections closed")]
    private partial void LogAllConnectionsClosed();

    [LoggerMessage(Level = LogLevel.Trace, Message = "Connection acquired for broker {BrokerId}")]
    private partial void LogConnectionAcquired(int brokerId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Replacing connection {Index} for broker {BrokerId}")]
    private partial void LogConnectionReplacement(int brokerId, int index);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Scaled connection group from {OldCount} to {NewCount} connections for broker {BrokerId}")]
    private partial void LogScaledConnectionGroup(int oldCount, int newCount, int brokerId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Shrunk connection group from {OldCount} to {NewCount} connections for broker {BrokerId}")]
    private partial void LogShrunkConnectionGroup(int oldCount, int newCount, int brokerId);

    #endregion

    private readonly record struct EndpointKey(string Host, int Port);
    private readonly record struct BrokerInfo(int BrokerId, string Host, int Port);
}
