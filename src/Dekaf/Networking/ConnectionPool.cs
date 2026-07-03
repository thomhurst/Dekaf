using System.Collections.Concurrent;
using System.Diagnostics;
using Dekaf.Internal;
using Dekaf.Security.Sasl;
using Dekaf.Telemetry;
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
    private readonly ClientTelemetryMetricCollector? _telemetryMetricCollector;
    private readonly OAuthBearerTokenProvider? _sharedOAuthBearerTokenProvider;
    private CancellationTokenSource? _idleReaperCts;
    private Task? _idleReaperTask;

    /// <summary>
    /// Shared memory pool for all connections. Bounds total retained memory to one set of
    /// array buckets regardless of connection count, preventing multi-GB WorkingSet growth
    /// in multi-broker scenarios with adaptive scaling. See <see cref="PipeMemoryPool"/>.
    /// Bucket capacity is either provided by broker-aware producer sizing or falls back to
    /// scaling by <c>_connectionsPerBroker</c> for direct ConnectionPool use.
    /// </summary>
    private PipeMemoryPool? _sharedPipeMemoryPool;
    private int _currentPipeMemoryBucketCapacity;
    private readonly Lock _pipeMemoryRatchetLock = new();

    /// <summary>
    /// Optional factory for creating connections, used by tests to inject fakes.
    /// When null, the default KafkaConnection constructor is used.
    /// Parameters: brokerId, host, port, index, cancellationToken.
    /// </summary>
    private readonly Func<int, string, int, int, CancellationToken, ValueTask<IKafkaConnection>>? _connectionFactory;

    // Default BufferMemory if not configured (256 MB)
    private const ulong DefaultBufferMemory = 268435456;

    private readonly ConcurrentDictionary<int, BrokerInfo> _brokers = new();

    /// <summary>
    /// Current broker count for pipeline threshold calculation. Returns at least 1
    /// to avoid division by zero when connections are created before brokers are registered.
    /// </summary>
    /// <remarks>
    /// Known limitation: BrokerCount is a stale snapshot at connection-creation time. Early
    /// connections may be created before all brokers are discovered (e.g., BrokerCount=1 when
    /// there are actually 3 brokers), resulting in a higher per-connection threshold than
    /// intended. This is acceptable in practice because MaximumPauseThresholdBytes (4 MB)
    /// caps the per-connection threshold regardless of how few brokers are known at the time.
    /// </remarks>
    private int BrokerCount => Math.Max(1, _brokers.Count);
    private readonly ConcurrentDictionary<EndpointKey, IKafkaConnection> _connectionsByEndpoint = new();
    private readonly ConcurrentDictionary<int, IKafkaConnection> _connectionsById = new();
    // Per-endpoint semaphores to deduplicate concurrent single-connection creation
    private readonly ConcurrentDictionary<EndpointKey, SemaphoreSlim> _connectionCreationLocks = new();
    private readonly ConcurrentDictionary<EndpointKey, ReconnectBackoffState> _reconnectBackoffs = new();

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
        ResponseBufferPool responseBufferPool,
        int? pipeMemoryBucketCapacity = null,
        ClientTelemetryMetricCollector? telemetryMetricCollector = null)
    {
        _clientId = clientId;
        _connectionOptions = ConfigureSharedOAuthBearerProvider(
            connectionOptions ?? new ConnectionOptions(),
            out _sharedOAuthBearerTokenProvider);
        ValidateReconnectBackoff(_connectionOptions);
        _loggerFactory = loggerFactory;
        _logger = loggerFactory?.CreateLogger<ConnectionPool>() ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<ConnectionPool>.Instance;
        _connectionsPerBroker = Math.Max(1, connectionsPerBroker);
        _responseBufferPool = responseBufferPool;
        _telemetryMetricCollector = telemetryMetricCollector;
        var bucketCapacity = pipeMemoryBucketCapacity ?? ScaledBucketCapacity(_connectionsPerBroker);
        _sharedPipeMemoryPool = new PipeMemoryPool(maxArraysPerBucket: bucketCapacity);
        _currentPipeMemoryBucketCapacity = bucketCapacity;
        StartIdleConnectionReaper();
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
        _connectionOptions = ConfigureSharedOAuthBearerProvider(
            connectionOptions ?? new ConnectionOptions(),
            out _sharedOAuthBearerTokenProvider);
        ValidateReconnectBackoff(_connectionOptions);
        _loggerFactory = null;
        _logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<ConnectionPool>.Instance;
        _connectionsPerBroker = Math.Max(1, connectionsPerBroker);
        _responseBufferPool = ResponseBufferPool.Default;
        _connectionFactory = connectionFactory;
        // No shared pool needed: factory-created connections manage their own pools.
        StartIdleConnectionReaper();
    }

    /// <summary>
    /// Fallback bucket capacity when broker-aware sizing is not provided.
    /// Scales by <paramref name="connectionsPerBroker"/> only — callers that know the broker
    /// count should use <see cref="Internal.PoolSizing.ForSharedPools"/> instead and pass the
    /// result via the <c>pipeMemoryBucketCapacity</c> constructor parameter.
    /// </summary>
    private static int ScaledBucketCapacity(int connectionsPerBroker)
        => Math.Min(connectionsPerBroker * 32, 256);

    internal ConnectionOptions EffectiveConnectionOptions => _connectionOptions;

    internal bool HasSharedOAuthBearerTokenProvider => _sharedOAuthBearerTokenProvider is not null;

    private static ConnectionOptions ConfigureSharedOAuthBearerProvider(
        ConnectionOptions options,
        out OAuthBearerTokenProvider? sharedProvider)
    {
        if (options.OAuthBearerConfig is null ||
            options.OAuthBearerTokenProvider is not null ||
            options.OAuthBearerToken is not null)
        {
            sharedProvider = null;
            return options;
        }

        sharedProvider = new OAuthBearerTokenProvider(options.OAuthBearerConfig);

        return new ConnectionOptions
        {
            UseTls = options.UseTls,
            TlsConfig = options.TlsConfig,
            RemoteCertificateValidationCallback = options.RemoteCertificateValidationCallback,
            SaslMechanism = options.SaslMechanism,
            SaslUsername = options.SaslUsername,
            SaslPassword = options.SaslPassword,
            GssapiConfig = options.GssapiConfig,
            OAuthBearerTokenProvider = sharedProvider.GetTokenAsync,
            OAuthBearerToken = options.OAuthBearerToken,
            SaslReauthenticationConfig = options.SaslReauthenticationConfig,
            SendBufferSize = options.SendBufferSize,
            ReceiveBufferSize = options.ReceiveBufferSize,
            EnableTcpKeepAlive = options.EnableTcpKeepAlive,
            TcpKeepAliveTime = options.TcpKeepAliveTime,
            TcpKeepAliveInterval = options.TcpKeepAliveInterval,
            TcpKeepAliveRetryCount = options.TcpKeepAliveRetryCount,
            MinimumSegmentSize = options.MinimumSegmentSize,
            MinimumReadSize = options.MinimumReadSize,
            ConnectionTimeout = options.ConnectionTimeout,
            RequestTimeout = options.RequestTimeout,
            ReconnectBackoff = options.ReconnectBackoff,
            ReconnectBackoffMax = options.ReconnectBackoffMax,
            ConnectionsMaxIdleMs = options.ConnectionsMaxIdleMs,
            MaxInFlightRequestsPerConnection = options.MaxInFlightRequestsPerConnection
        };
    }

    private static void ValidateReconnectBackoff(ConnectionOptions options)
    {
        var reconnectBackoffMs = ReconnectBackoffValidation.ToMilliseconds(
            options.ReconnectBackoff,
            nameof(options),
            "Reconnect backoff cannot be negative");
        var reconnectBackoffMaxMs = ReconnectBackoffValidation.ToMilliseconds(
            options.ReconnectBackoffMax,
            nameof(options),
            "Maximum reconnect backoff cannot be negative");

        ReconnectBackoffValidation.ValidateMilliseconds(reconnectBackoffMs, reconnectBackoffMaxMs);
    }

    /// <summary>
    /// Increases the shared PipeMemoryPool bucket capacity if <paramref name="bucketCapacity"/>
    /// exceeds the current value. New connections will use the larger pool; existing connections
    /// continue using the previous pool until they are recycled.
    /// <para/>
    /// The previous pool is not disposed during the swap because active pipes may still return
    /// segments to it. Its lifetime is bounded by the connections that captured it: once those
    /// connections and their in-flight pipe segments are gone, the old pool becomes GC-eligible.
    /// </summary>
    internal void RatchetPipeMemoryBucketCapacity(int bucketCapacity)
    {
        if (bucketCapacity <= Volatile.Read(ref _currentPipeMemoryBucketCapacity))
            return;

        lock (_pipeMemoryRatchetLock)
        {
            if (bucketCapacity <= _currentPipeMemoryBucketCapacity)
                return;

            Volatile.Write(ref _sharedPipeMemoryPool, new PipeMemoryPool(maxArraysPerBucket: bucketCapacity));
            Volatile.Write(ref _currentPipeMemoryBucketCapacity, bucketCapacity);
        }
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
            return MarkConnectionAcquired(existing);
        }

        // Get broker info
        if (!_brokers.TryGetValue(brokerId, out var brokerInfo))
        {
            throw new InvalidOperationException($"Unknown broker ID: {brokerId}");
        }

        var connection = await GetOrCreateConnectionAsync(brokerId, brokerInfo.Host, brokerInfo.Port, cancellationToken)
            .ConfigureAwait(false);
        return MarkConnectionAcquired(connection);
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
                return MarkConnectionAcquired(connection);
            }

            // Connection at this index is invalid, try to replace it
            LogConnectionReplacement(brokerId, (int)index);
            var replacement = await ReplaceConnectionInGroupAsync(brokerId, brokerInfo, (int)index, cancellationToken).ConfigureAwait(false);
            return MarkConnectionAcquired(replacement);
        }

        // Need to create new connection group
        var created = await CreateConnectionGroupAsync(brokerId, brokerInfo, cancellationToken).ConfigureAwait(false);
        return MarkConnectionAcquired(created);
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
            return MarkConnectionAcquired(connection);
        }

        var replacement = await ReplaceConnectionInGroupAsync(brokerId, brokerInfo, effectiveIndex, cancellationToken).ConfigureAwait(false);
        return MarkConnectionAcquired(replacement);
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
                tasks[i] = CreateConnectionForGroupAsync(
                    brokerId,
                    brokerInfo.Host,
                    brokerInfo.Port,
                    index,
                    token,
                    cancellationToken).AsTask();
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
            ResetReconnectBackoff(new EndpointKey(brokerInfo.Host, brokerInfo.Port), brokerId, brokerInfo.Host, brokerInfo.Port);

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
                tasks[i] = CreateConnectionForGroupAsync(
                    brokerId,
                    brokerInfo.Host,
                    brokerInfo.Port,
                    index,
                    linkedCts.Token,
                    cancellationToken).AsTask();
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
            ResetReconnectBackoff(new EndpointKey(brokerInfo.Host, brokerInfo.Port), brokerId, brokerInfo.Host, brokerInfo.Port);

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

            var connection = await CreateConnectionForGroupAsync(
                brokerId,
                brokerInfo.Host,
                brokerInfo.Port,
                index,
                token,
                cancellationToken).ConfigureAwait(false);

            // Acquire _scaleLock to protect the array write against concurrent
            // ShrinkConnectionGroupAsync, which may have swapped the array reference.
            bool stored = false;
            IKafkaConnection? oldConnection = null;
            await _scaleLock.WaitAsync(token).ConfigureAwait(false);
            try
            {
                if (_connectionGroupsById.TryGetValue(brokerId, out var connections) && index < connections.Length)
                {
                    // Capture old connection for disposal — it still holds Pipe buffers,
                    // StreamPipeWriter memory, and socket resources that leak without disposal.
                    oldConnection = connections[index];
                    connections[index] = connection;
                    stored = true;
                }
            }
            finally
            {
                _scaleLock.Release();
            }

            // Dispose old connection outside the lock to avoid blocking scale operations.
            // Fire-and-forget with exception observation to prevent UnobservedTaskException.
            if (oldConnection is not null)
            {
                _ = oldConnection.DisposeAsync().AsTask().ContinueWith(
                    static (t, state) =>
                    {
                        var (logger, id, idx) = ((ILogger, int, int))state!;
                        LogOldConnectionDisposalFailed(logger, id, idx, t.Exception!);
                    },
                    (_logger, brokerId, index),
                    CancellationToken.None,
                    TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);
            }

            // If the index is now out of bounds (shrink happened concurrently),
            // dispose the orphaned connection to avoid leaking TCP handles.
            if (!stored)
            {
                await connection.DisposeAsync().ConfigureAwait(false);
                throw new KafkaException(
                    $"Connection slot {index} for broker {brokerId} was removed by a concurrent shrink");
            }

            ResetReconnectBackoff(new EndpointKey(brokerInfo.Host, brokerInfo.Port), brokerId, brokerInfo.Host, brokerInfo.Port);

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

    private async ValueTask<IKafkaConnection> CreateConnectionForGroupAsync(
        int brokerId,
        string host,
        int port,
        int index,
        CancellationToken cancellationToken,
        CancellationToken callerCancellationToken)
    {
        var endpoint = new EndpointKey(host, port);
        return await RunWithReconnectBackoffAsync(
            endpoint,
            brokerId,
            host,
            port,
            () => CreateConnectionForGroupCoreAsync(
                brokerId,
                host,
                port,
                index,
                cancellationToken,
                callerCancellationToken),
            cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<IKafkaConnection> CreateConnectionForGroupCoreAsync(
        int brokerId,
        string host,
        int port,
        int index,
        CancellationToken cancellationToken,
        CancellationToken callerCancellationToken)
    {
        var endpoint = new EndpointKey(host, port);

        try
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
                BrokerCount,
                _responseBufferPool,
                _sharedPipeMemoryPool,
                _telemetryMetricCollector);

            await connection.ConnectAsync(cancellationToken).ConfigureAwait(false);

            LogCreatedConnectionForGroup(index, brokerId, host, port);

            return connection;
        }
        catch (Exception) when (!callerCancellationToken.IsCancellationRequested)
        {
            RecordReconnectFailure(endpoint, brokerId, host, port);
            throw;
        }
    }

    public async ValueTask<IKafkaConnection> GetConnectionAsync(string host, int port, CancellationToken cancellationToken = default)
    {
        if (Volatile.Read(ref _disposed) != 0)
            throw new ObjectDisposedException(nameof(ConnectionPool));

        var endpoint = new EndpointKey(host, port);

        // Try to get existing connection
        if (_connectionsByEndpoint.TryGetValue(endpoint, out var existing) && existing.IsConnected)
        {
            return MarkConnectionAcquired(existing);
        }

        var connection = await GetOrCreateConnectionAsync(-1, host, port, cancellationToken).ConfigureAwait(false);
        return MarkConnectionAcquired(connection);
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

                var connection = await CreateConnectionAsync(brokerId, host, port, token, cancellationToken).ConfigureAwait(false);

                // Verify connection is still valid
                if (connection.IsConnected)
                {
                    ResetReconnectBackoff(endpoint, brokerId, host, port);
                    return connection;
                }

                // Connection failed immediately after creation — clean up and retry
                _connectionsByEndpoint.TryRemove(endpoint, out _);
                if (brokerId >= 0)
                    _connectionsById.TryRemove(brokerId, out _);

                try { await connection.DisposeAsync().ConfigureAwait(false); }
                catch { /* best-effort cleanup */ }

                RecordReconnectFailure(endpoint, brokerId, host, port);
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
        CancellationToken cancellationToken,
        CancellationToken callerCancellationToken)
    {
        var endpoint = new EndpointKey(host, port);
        return await RunWithReconnectBackoffAsync(
            endpoint,
            brokerId,
            host,
            port,
            () => CreateConnectionCoreAsync(
                brokerId,
                host,
                port,
                cancellationToken,
                callerCancellationToken),
            cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<IKafkaConnection> CreateConnectionCoreAsync(
        int brokerId,
        string host,
        int port,
        CancellationToken cancellationToken,
        CancellationToken callerCancellationToken)
    {
        var endpoint = new EndpointKey(host, port);

        // Note: Stale connection cleanup is handled by the caller (GetOrCreateConnectionAsync)
        // within the creation semaphore, so no duplicate cleanup is needed here.

        try
        {
            if (_connectionFactory is not null)
            {
                var factoryConnection = await _connectionFactory(brokerId, host, port, 0, cancellationToken).ConfigureAwait(false);
                _connectionsByEndpoint[endpoint] = factoryConnection;
                if (brokerId >= 0)
                {
                    _connectionsById[brokerId] = factoryConnection;
                }

                LogCreatedConnection(brokerId, host, port);
                return factoryConnection;
            }

            var connection = new KafkaConnection(
                brokerId, host, port,
                _clientId, _connectionOptions,
                _loggerFactory?.CreateLogger<KafkaConnection>(),
                DefaultBufferMemory, _connectionsPerBroker,
                BrokerCount,
                _responseBufferPool,
                _sharedPipeMemoryPool,
                _telemetryMetricCollector);

            await connection.ConnectAsync(cancellationToken).ConfigureAwait(false);

            _connectionsByEndpoint[endpoint] = connection;
            if (brokerId >= 0)
            {
                _connectionsById[brokerId] = connection;
            }

            LogCreatedConnection(brokerId, host, port);

            return connection;
        }
        catch (Exception) when (!callerCancellationToken.IsCancellationRequested)
        {
            RecordReconnectFailure(endpoint, brokerId, host, port);
            throw;
        }
    }

    private async ValueTask<IKafkaConnection> RunWithReconnectBackoffAsync(
        EndpointKey endpoint,
        int brokerId,
        string host,
        int port,
        Func<ValueTask<IKafkaConnection>> createConnection,
        CancellationToken cancellationToken)
    {
        if (_reconnectBackoffs.TryGetValue(endpoint, out var state) && state.TryAddUser())
        {
            var gateAcquired = false;
            try
            {
                await state.AttemptGate.WaitAsync(cancellationToken).ConfigureAwait(false);
                gateAcquired = true;

                await WaitForReconnectBackoffAsync(endpoint, state, brokerId, host, port, cancellationToken).ConfigureAwait(false);
                return await createConnection().ConfigureAwait(false);
            }
            finally
            {
                if (gateAcquired)
                    state.AttemptGate.Release();
                state.ReleaseUser();
            }
        }

        return await createConnection().ConfigureAwait(false);
    }

    private async ValueTask WaitForReconnectBackoffAsync(
        EndpointKey endpoint,
        ReconnectBackoffState state,
        int brokerId,
        string host,
        int port,
        CancellationToken cancellationToken)
    {
        while (_reconnectBackoffs.TryGetValue(endpoint, out var currentState)
               && ReferenceEquals(currentState, state))
        {
            TimeSpan delay;
            int failureCount;
            lock (state.Sync)
            {
                var retryAfterTicks = state.NextAttemptTimestamp - Stopwatch.GetTimestamp();
                if (state.FailureCount == 0 || retryAfterTicks <= 0)
                    return;

                delay = TimeSpan.FromSeconds((double)retryAfterTicks / Stopwatch.Frequency);
                failureCount = state.FailureCount;
            }

            LogReconnectBackoffDelay(delay.TotalMilliseconds, brokerId, host, port, failureCount);
            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
        }
    }

    private void RecordReconnectFailure(EndpointKey endpoint, int brokerId, string host, int port)
    {
        var state = _reconnectBackoffs.GetOrAdd(endpoint, static _ => new ReconnectBackoffState());

        TimeSpan delay;
        int failureCount;
        lock (state.Sync)
        {
            state.FailureCount = Math.Min(state.FailureCount + 1, 30);
            failureCount = state.FailureCount;
            delay = CalculateReconnectBackoffDelay(failureCount);
            state.NextAttemptTimestamp = Stopwatch.GetTimestamp() + ToStopwatchTicks(delay);
        }

        LogReconnectBackoffScheduled(delay.TotalMilliseconds, brokerId, host, port, failureCount);
    }

    private void ResetReconnectBackoff(EndpointKey endpoint, int brokerId, string host, int port)
    {
        if (!_reconnectBackoffs.TryRemove(endpoint, out var state))
            return;

        lock (state.Sync)
        {
            state.FailureCount = 0;
            state.NextAttemptTimestamp = 0;
        }
        state.RequestDispose();

        LogReconnectBackoffReset(brokerId, host, port);
    }

    private TimeSpan CalculateReconnectBackoffDelay(int failureCount)
    {
        var minMs = _connectionOptions.ReconnectBackoff.TotalMilliseconds;
        var maxMs = _connectionOptions.ReconnectBackoffMax.TotalMilliseconds;
        if (minMs <= 0 || maxMs <= 0)
            return TimeSpan.Zero;

        var exponent = Math.Min(failureCount - 1, 30);
        var baseMs = Math.Min(maxMs, minMs * Math.Pow(2, exponent));
        if (baseMs < maxMs)
        {
            var jitterMaxMs = Math.Max(1, (int)Math.Ceiling(baseMs * 0.2));
            baseMs = Math.Min(maxMs, baseMs + Random.Shared.Next(jitterMaxMs + 1));
        }

        return TimeSpan.FromMilliseconds(baseMs);
    }

    private static long ToStopwatchTicks(TimeSpan delay)
    {
        if (delay <= TimeSpan.Zero)
            return 0;

        return (long)(delay.TotalSeconds * Stopwatch.Frequency);
    }

    internal async ValueTask<int> ReapIdleConnectionsAsync()
    {
        if (_connectionOptions.ConnectionsMaxIdleMs < 0 || Volatile.Read(ref _disposed) != 0)
            return 0;

        await _disposeLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (Volatile.Read(ref _disposed) != 0)
                return 0;

            var now = Environment.TickCount64;
            var reaped = 0;

            foreach (var (endpoint, connection) in _connectionsByEndpoint.ToArray())
            {
                if (await TryReapEndpointConnectionAsync(endpoint, connection, now).ConfigureAwait(false))
                    reaped++;
            }

            foreach (var (_, group) in _connectionGroupsById.ToArray())
            {
                var connectedCount = CountConnected(group);
                for (var i = 0; i < group.Length; i++)
                {
                    var connection = group[i];
                    if (connection is not null
                        && await TryReapGroupConnectionAsync(group, i, connection, connectedCount, now).ConfigureAwait(false))
                    {
                        connectedCount--;
                        reaped++;
                    }
                }
            }

            return reaped;
        }
        finally
        {
            _disposeLock.Release();
        }
    }

    private async ValueTask<bool> TryReapEndpointConnectionAsync(EndpointKey endpoint, IKafkaConnection connection, long now)
    {
        if (!IsIdleReapable(connection, now))
            return false;

        if (!TryRemoveExact(_connectionsByEndpoint, endpoint, connection))
            return false;

        var removedByBrokerId = connection.BrokerId >= 0
            && TryRemoveExact(_connectionsById, connection.BrokerId, connection);

        var disposed = await DisposeIdleConnectionAsync(connection, now).ConfigureAwait(false);
        if (!disposed)
        {
            if (connection.IsConnected)
            {
                _connectionsByEndpoint.TryAdd(endpoint, connection);
                if (removedByBrokerId)
                {
                    _connectionsById.TryAdd(connection.BrokerId, connection);
                }
            }

            return false;
        }

        return true;
    }

    private async ValueTask<bool> TryReapGroupConnectionAsync(
        IKafkaConnection[] group,
        int index,
        IKafkaConnection connection,
        int connectedCount,
        long now)
    {
        if (!IsIdleReapable(connection, now))
            return false;

        if (connectedCount <= 1 && HasPendingRequests(group))
            return false;

        if (Interlocked.CompareExchange(ref group[index], null!, connection) != connection)
            return false;

        var disposed = await DisposeIdleConnectionAsync(connection, now).ConfigureAwait(false);
        if (!disposed && connection.IsConnected)
            Interlocked.CompareExchange(ref group[index], connection, null!);

        return disposed;
    }

    private bool IsIdleReapable(IKafkaConnection connection, long now)
    {
        if (!connection.IsConnected || connection is not IIdleTrackedKafkaConnection idleState)
            return false;

        if (idleState.PendingRequestCount != 0)
            return false;

        return now - idleState.LastUsedTimestampMs >= _connectionOptions.ConnectionsMaxIdleMs;
    }

    private async ValueTask<bool> DisposeIdleConnectionAsync(IKafkaConnection connection, long now)
    {
        if (!IsIdleReapable(connection, now))
            return false;

        try
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            if (connection is IIdleTrackedKafkaConnection idleState)
            {
                LogReapedIdleConnection(
                    connection.BrokerId,
                    connection.Host,
                    connection.Port,
                    now - idleState.LastUsedTimestampMs);
            }
            return true;
        }
        catch (Exception ex)
        {
            LogIdleConnectionDisposalFailed(ex, connection.BrokerId, connection.Host, connection.Port);
            return false;
        }
    }

    private static int CountConnected(IKafkaConnection[] group)
    {
        var count = 0;
        foreach (var connection in group)
        {
            if (connection is not null && connection.IsConnected)
                count++;
        }

        return count;
    }

    private static bool HasPendingRequests(IKafkaConnection[] group)
    {
        foreach (var connection in group)
        {
            if (connection is IIdleTrackedKafkaConnection { PendingRequestCount: > 0 })
                return true;
        }

        return false;
    }

    private static bool TryRemoveExact<TKey, TValue>(
        ConcurrentDictionary<TKey, TValue> dictionary,
        TKey key,
        TValue value)
        where TKey : notnull
        => ((ICollection<KeyValuePair<TKey, TValue>>)dictionary).Remove(new KeyValuePair<TKey, TValue>(key, value));

    private static IKafkaConnection MarkConnectionAcquired(IKafkaConnection connection)
    {
        if (connection is IIdleTrackedKafkaConnection idleState)
            idleState.Touch();

        return connection;
    }

    private void StartIdleConnectionReaper()
    {
        if (_connectionOptions.ConnectionsMaxIdleMs < 0)
            return;

        _idleReaperCts = new CancellationTokenSource();
        _idleReaperTask = RunIdleConnectionReaperAsync(_idleReaperCts.Token);
    }

    private async Task RunIdleConnectionReaperAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var timer = new PeriodicTimer(GetIdleReaperInterval(_connectionOptions.ConnectionsMaxIdleMs));
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            {
                await ReapIdleConnectionsAsync().ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            LogIdleConnectionReaperFailed(ex);
        }
    }

    private static TimeSpan GetIdleReaperInterval(int connectionsMaxIdleMs)
        => TimeSpan.FromMilliseconds(Math.Clamp(connectionsMaxIdleMs / 2, 1000, 60000));

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
                foreach (var state in _reconnectBackoffs.Values) state.RequestDispose();
            }
            _connectionCreationLocks.Clear();
            _connectionReplacementLocks.Clear();
            _groupCreationLocks.Clear();
            _reconnectBackoffs.Clear();

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

        _idleReaperCts?.Cancel();
        if (_idleReaperTask is not null)
            await _idleReaperTask.ConfigureAwait(false);
        _idleReaperCts?.Dispose();

        await CloseAllAsync().ConfigureAwait(false);

        // Dispose the shared memory pool only when the pool itself is disposed.
        // Public CloseAllAsync is a reset/reconnect operation; keeping the pool alive
        // lets subsequent connections reuse it without tripping ObjectDisposedException.
        _sharedPipeMemoryPool?.Dispose();

        _sharedOAuthBearerTokenProvider?.Dispose();
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

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to dispose replaced connection for broker {BrokerId} index {Index}")]
    private static partial void LogOldConnectionDisposalFailed(ILogger logger, int brokerId, int index, Exception ex);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Waiting {DelayMs}ms before reconnecting to broker {BrokerId} at {Host}:{Port} after {FailureCount} failed attempt(s)")]
    private partial void LogReconnectBackoffDelay(double delayMs, int brokerId, string host, int port, int failureCount);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Scheduled reconnect backoff of {DelayMs}ms for broker {BrokerId} at {Host}:{Port} after {FailureCount} failed attempt(s)")]
    private partial void LogReconnectBackoffScheduled(double delayMs, int brokerId, string host, int port, int failureCount);

    [LoggerMessage(Level = LogLevel.Trace, Message = "Reset reconnect backoff for broker {BrokerId} at {Host}:{Port}")]
    private partial void LogReconnectBackoffReset(int brokerId, string host, int port);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Reaped idle connection to broker {BrokerId} at {Host}:{Port} after {IdleMs}ms")]
    private partial void LogReapedIdleConnection(int brokerId, string host, int port, long idleMs);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Idle connection reaper failed")]
    private partial void LogIdleConnectionReaperFailed(Exception ex);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to dispose idle connection to broker {BrokerId} at {Host}:{Port}")]
    private partial void LogIdleConnectionDisposalFailed(Exception ex, int brokerId, string host, int port);

    #endregion

    private sealed class ReconnectBackoffState
    {
        public readonly object Sync = new();
        public readonly SemaphoreSlim AttemptGate = new(1, 1);
        public int FailureCount;
        public long NextAttemptTimestamp;
        private int _activeUsers;
        private bool _disposeRequested;
        private bool _disposed;

        public bool TryAddUser()
        {
            lock (Sync)
            {
                if (_disposeRequested || _disposed)
                    return false;

                _activeUsers++;
                return true;
            }
        }

        public void ReleaseUser()
        {
            var shouldDispose = false;
            lock (Sync)
            {
                _activeUsers--;
                shouldDispose = _disposeRequested && _activeUsers == 0;
            }

            if (shouldDispose)
                Dispose();
        }

        public void RequestDispose()
        {
            var shouldDispose = false;
            lock (Sync)
            {
                _disposeRequested = true;
                shouldDispose = _activeUsers == 0;
            }

            if (shouldDispose)
                Dispose();
        }

        private void Dispose()
        {
            lock (Sync)
            {
                if (_disposed)
                    return;

                _disposed = true;
            }

            AttemptGate.Dispose();
        }
    }

    private readonly record struct EndpointKey(string Host, int Port);
    private readonly record struct BrokerInfo(int BrokerId, string Host, int Port);
}
