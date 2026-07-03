using System.Buffers;
using System.Diagnostics;
using System.Reflection;
using Dekaf.Errors;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Security.Sasl;
using NSubstitute;

namespace Dekaf.Tests.Unit.Networking;

/// <summary>
/// Tests for ConnectionPool, including behavior with disposed connections
/// and proper error handling for various states.
/// </summary>
public sealed class ConnectionPoolTests
{
    private const int IdleThresholdMs = 60000;

    [Test]
    public async Task GetConnectionAsync_DisposedPool_ThrowsObjectDisposedException()
    {
        var pool = new ConnectionPool("test-client");
        await pool.DisposeAsync();

        Func<Task> act = () => pool.GetConnectionAsync(0).AsTask();

        await Assert.That(act).Throws<ObjectDisposedException>();
    }

    [Test]
    public async Task GetConnectionAsync_UnknownBrokerId_ThrowsInvalidOperationException()
    {
        await using var pool = new ConnectionPool("test-client");

        Func<Task> act = () => pool.GetConnectionAsync(999).AsTask();

        await Assert.That(act).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task RegisterBroker_ThenGetConnection_UsesRegisteredBrokerInfo()
    {
        await using var pool = new ConnectionPool("test-client");

        // Register a broker that doesn't actually exist - connection will fail
        pool.RegisterBroker(1, "nonexistent-host-12345.invalid", 9092);

        // Should not throw InvalidOperationException("Unknown broker ID")
        // but will fail to connect
        Func<Task> act = () => pool.GetConnectionAsync(1).AsTask();

        // Will throw because it can't connect, but NOT "Unknown broker ID"
        await Assert.That(act).Throws<Exception>();
    }

    [Test]
    public async Task DisposeAsync_CalledMultipleTimes_DoesNotThrow()
    {
        var pool = new ConnectionPool("test-client");

        await pool.DisposeAsync();
        await pool.DisposeAsync();
        await pool.DisposeAsync();
    }

    [Test]
    public async Task GetConnectionByHostPort_DisposedPool_ThrowsObjectDisposedException()
    {
        var pool = new ConnectionPool("test-client");
        await pool.DisposeAsync();

        Func<Task> act = () => pool.GetConnectionAsync("localhost", 9092).AsTask();

        await Assert.That(act).Throws<ObjectDisposedException>();
    }

    [Test]
    public async Task RegisterBroker_SameIdTwice_OverwritesWithoutError()
    {
        await using var pool = new ConnectionPool("test-client");

        pool.RegisterBroker(1, "host-a", 9092);
        pool.RegisterBroker(1, "host-b", 9093);

        // Should not throw - the second registration overwrites the first
    }

    [Test]
    public async Task RegisterBroker_MultipleBrokers_AllRegistered()
    {
        await using var pool = new ConnectionPool("test-client");

        pool.RegisterBroker(0, "broker-0", 9092);
        pool.RegisterBroker(1, "broker-1", 9092);
        pool.RegisterBroker(2, "broker-2", 9092);

        // All should be registered without error
    }

    [Test]
    public async Task RemoveConnectionAsync_UnknownBroker_DoesNotThrow()
    {
        await using var pool = new ConnectionPool("test-client");

        // Removing a non-existent broker should be a no-op
        await pool.RemoveConnectionAsync(999);
    }

    [Test]
    public async Task CloseAllAsync_EmptyPool_DoesNotThrow()
    {
        await using var pool = new ConnectionPool("test-client");

        await pool.CloseAllAsync();
    }

    [Test]
    public async Task CloseAllAsync_DoesNotDisposeSharedPipeMemoryPool()
    {
        var pool = new ConnectionPool("test-client");
        var sharedPool = GetSharedPipeMemoryPool(pool);

        await pool.CloseAllAsync();

        using (var owner = sharedPool.Rent(1))
        {
            await Assert.That(owner.Memory.Length).IsGreaterThanOrEqualTo(1);
        }

        await pool.DisposeAsync();
    }

    [Test]
    public async Task DisposeAsync_DisposesSharedPipeMemoryPool()
    {
        var pool = new ConnectionPool("test-client");
        var sharedPool = GetSharedPipeMemoryPool(pool);

        await pool.DisposeAsync();

        await Assert.That(() => sharedPool.Rent(1)).Throws<ObjectDisposedException>();
    }

    [Test]
    public async Task CloseAllAsync_AfterDispose_ThrowsObjectDisposedException()
    {
        var pool = new ConnectionPool("test-client");
        await pool.DisposeAsync();

        // CloseAll after dispose throws because internal semaphore is disposed
        Func<Task> act = () => pool.CloseAllAsync().AsTask();

        await Assert.That(act).Throws<ObjectDisposedException>();
    }

    [Test]
    public async Task GetConnectionAsync_AfterCloseAll_CanReconnect()
    {
        await using var pool = new ConnectionPool("test-client");
        pool.RegisterBroker(1, "nonexistent-host-12345.invalid", 9092);

        await pool.CloseAllAsync();

        // After closing all connections, getting a connection should attempt
        // to create a new one (will fail because host doesn't exist, but
        // should not throw "Unknown broker ID")
        Func<Task> act = () => pool.GetConnectionAsync(1).AsTask();

        await Assert.That(act).Throws<Exception>();
    }

    [Test]
    public async Task GetConnectionByHostPort_UnreachableHost_ThrowsException()
    {
        await using var pool = new ConnectionPool("test-client");

        Func<Task> act = () => pool.GetConnectionAsync("nonexistent-host-12345.invalid", 9092).AsTask();

        await Assert.That(act).Throws<Exception>();
    }

    [Test]
    public async Task GetConnectionAsync_DisposedPoolByHostPort_ThrowsBeforeAttemptingConnect()
    {
        var pool = new ConnectionPool("test-client");
        await pool.DisposeAsync();

        // Should immediately throw ObjectDisposedException, not attempt to connect
        Func<Task> act = () => pool.GetConnectionAsync("localhost", 9092).AsTask();

        await Assert.That(act).Throws<ObjectDisposedException>();
    }

    [Test]
    public async Task GetConnectionAsync_DisposedPoolById_ThrowsBeforeAttemptingConnect()
    {
        var pool = new ConnectionPool("test-client");
        pool.RegisterBroker(1, "localhost", 9092);
        await pool.DisposeAsync();

        // Should immediately throw ObjectDisposedException, not attempt to connect
        Func<Task> act = () => pool.GetConnectionAsync(1).AsTask();

        await Assert.That(act).Throws<ObjectDisposedException>();
    }

    [Test]
    public async Task Constructor_NullClientId_DoesNotThrow()
    {
        await using var pool = new ConnectionPool();

        // Should create successfully with no client ID
        await Assert.That(pool).IsNotNull();
    }

    [Test]
    public async Task Constructor_CustomConnectionsPerBroker_DoesNotThrow()
    {
        await using var pool = new ConnectionPool("test-client", connectionsPerBroker: 4);

        await Assert.That(pool).IsNotNull();
    }

    [Test]
    public async Task Constructor_OAuthBearerConfig_CreatesSharedTokenProvider()
    {
        var config = CreateOAuthBearerConfig();
        var options = new ConnectionOptions
        {
            SaslMechanism = SaslMechanism.OAuthBearer,
            OAuthBearerConfig = config
        };

        await using var pool = new ConnectionPool(
            clientId: "test-client",
            connectionOptions: options,
            loggerFactory: null,
            connectionsPerBroker: 3);

        await Assert.That(pool.HasSharedOAuthBearerTokenProvider).IsTrue();
        await Assert.That(pool.EffectiveConnectionOptions).IsNotSameReferenceAs(options);
        await Assert.That((object?)pool.EffectiveConnectionOptions.OAuthBearerTokenProvider).IsNotNull();
        await Assert.That(pool.EffectiveConnectionOptions.OAuthBearerConfig).IsNull();
        await Assert.That(pool.EffectiveConnectionOptions.SaslMechanism).IsEqualTo(SaslMechanism.OAuthBearer);
    }

    [Test]
    public async Task Constructor_OAuthBearerTokenProvider_PreservesCustomProvider()
    {
        var config = CreateOAuthBearerConfig();
        Func<CancellationToken, ValueTask<OAuthBearerToken>> tokenProvider =
            _ => new ValueTask<OAuthBearerToken>(CreateOAuthBearerToken());
        var options = new ConnectionOptions
        {
            SaslMechanism = SaslMechanism.OAuthBearer,
            OAuthBearerConfig = config,
            OAuthBearerTokenProvider = tokenProvider
        };

        await using var pool = new ConnectionPool(
            clientId: "test-client",
            connectionOptions: options,
            loggerFactory: null,
            connectionsPerBroker: 3);

        await Assert.That(pool.HasSharedOAuthBearerTokenProvider).IsFalse();
        await Assert.That(pool.EffectiveConnectionOptions).IsSameReferenceAs(options);
        await Assert.That((object?)pool.EffectiveConnectionOptions.OAuthBearerTokenProvider).IsSameReferenceAs(tokenProvider);
        await Assert.That(pool.EffectiveConnectionOptions.OAuthBearerConfig).IsSameReferenceAs(config);
    }

    [Test]
    public async Task Constructor_OAuthBearerToken_PreservesStaticToken()
    {
        var config = CreateOAuthBearerConfig();
        var token = CreateOAuthBearerToken();
        var options = new ConnectionOptions
        {
            SaslMechanism = SaslMechanism.OAuthBearer,
            OAuthBearerConfig = config,
            OAuthBearerToken = token
        };

        await using var pool = new ConnectionPool(
            clientId: "test-client",
            connectionOptions: options,
            loggerFactory: null,
            connectionsPerBroker: 3);

        await Assert.That(pool.HasSharedOAuthBearerTokenProvider).IsFalse();
        await Assert.That(pool.EffectiveConnectionOptions).IsSameReferenceAs(options);
        await Assert.That(pool.EffectiveConnectionOptions.OAuthBearerToken).IsSameReferenceAs(token);
        await Assert.That(pool.EffectiveConnectionOptions.OAuthBearerConfig).IsSameReferenceAs(config);
    }

    [Test]
    public async Task DisposeAsync_ConcurrentCalls_DoesNotThrow()
    {
        var pool = new ConnectionPool("test-client");

        // Launch many concurrent disposal calls to exercise the atomic guard
        var threadCount = Math.Max(2, Environment.ProcessorCount);
        var tasks = new Task[threadCount];
        using var barrier = new Barrier(tasks.Length);

        for (var i = 0; i < tasks.Length; i++)
        {
            tasks[i] = Task.Run(async () =>
            {
                barrier.SignalAndWait();
                await pool.DisposeAsync();
            });
        }

        // All concurrent disposals should complete without throwing
        await Task.WhenAll(tasks);
    }

    [Test]
    public async Task ShrinkConnectionGroupAsync_DisposedPool_ThrowsObjectDisposedException()
    {
        var pool = new ConnectionPool("test-client");
        await pool.DisposeAsync();

        Func<Task> act = () => pool.ShrinkConnectionGroupAsync(1, 1).AsTask();

        await Assert.That(act).Throws<ObjectDisposedException>();
    }

    [Test]
    public async Task ShrinkConnectionGroupAsync_NewCountLessThanOne_ThrowsArgumentOutOfRange()
    {
        await using var pool = new ConnectionPool("test-client");

        Func<Task> act = () => pool.ShrinkConnectionGroupAsync(1, 0).AsTask();

        await Assert.That(act).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task ShrinkConnectionGroupAsync_NoConnectionGroup_ReturnsNull()
    {
        await using var pool = new ConnectionPool("test-client");
        pool.RegisterBroker(1, "localhost", 9092);

        // No connection group exists yet — shrink should be a no-op
        var result = await pool.ShrinkConnectionGroupAsync(1, 1);

        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task ShrinkConnectionGroupAsync_AlreadyAtOrBelowTarget_ReturnsNull()
    {
        await using var pool = new ConnectionPool("test-client", connectionsPerBroker: 2);
        pool.RegisterBroker(1, "localhost", 9092);

        // No group created — shrink to 3 (above any possible group size) is a no-op
        var result = await pool.ShrinkConnectionGroupAsync(1, 3);

        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task ConnectionOptions_ConnectionsMaxIdleMs_DefaultsToNineMinutes()
    {
        var options = new ConnectionOptions();

        await Assert.That(options.ConnectionsMaxIdleMs).IsEqualTo(ConnectionOptions.DefaultConnectionsMaxIdleMs);
    }

    [Test]
    public async Task ConnectionOptions_ConnectionsMaxIdleMs_RejectsValuesBelowDisabled()
    {
        await Assert.That(() => new ConnectionOptions { ConnectionsMaxIdleMs = -2 })
            .Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task ConnectionOptions_ToConnectionsMaxIdleMs_MapsInfiniteTimeSpanToDisabled()
    {
        var value = ConnectionOptions.ToConnectionsMaxIdleMs(Timeout.InfiniteTimeSpan, "idle");

        await Assert.That(value).IsEqualTo(-1);
    }

    [Test]
    public async Task ReapIdleConnectionsAsync_Disabled_DoesNotDisposeIdleConnection()
    {
        var connection = new TestIdleConnection(1, "localhost", 9092);
        var pool = new ConnectionPool(
            clientId: "test-client",
            connectionOptions: new ConnectionOptions { ConnectionsMaxIdleMs = -1 },
            connectionsPerBroker: 1,
            connectionFactory: (_, _, _, _, _) => new ValueTask<IKafkaConnection>(connection));

        await using (pool)
        {
            pool.RegisterBroker(1, "localhost", 9092);

            await pool.GetConnectionAsync(1);
            connection.LastUsedTimestampMs = StaleIdleTimestamp();
            var reaped = await pool.ReapIdleConnectionsAsync();

            await Assert.That(reaped).IsEqualTo(0);
            await Assert.That(connection.DisposeCount).IsEqualTo(0);
            await Assert.That(connection.IsConnected).IsTrue();
        }
    }

    [Test]
    public async Task ReapIdleConnectionsAsync_IdleSingleConnection_DisposesAndRecreates()
    {
        var created = new List<TestIdleConnection>();
        var pool = new ConnectionPool(
            clientId: "test-client",
            connectionOptions: new ConnectionOptions { ConnectionsMaxIdleMs = IdleThresholdMs },
            connectionsPerBroker: 1,
            connectionFactory: (brokerId, host, port, _, _) =>
            {
                var connection = new TestIdleConnection(brokerId, host, port);
                created.Add(connection);
                return new ValueTask<IKafkaConnection>(connection);
            });

        await using (pool)
        {
            pool.RegisterBroker(1, "localhost", 9092);

            var first = await pool.GetConnectionAsync(1);
            created[0].LastUsedTimestampMs = StaleIdleTimestamp();
            var reaped = await pool.ReapIdleConnectionsAsync();
            var second = await pool.GetConnectionAsync(1);

            await Assert.That(reaped).IsEqualTo(1);
            await Assert.That(created.Count).IsEqualTo(2);
            await Assert.That(created[0].DisposeCount).IsEqualTo(1);
            await Assert.That(first).IsNotSameReferenceAs(second);
        }
    }

    [Test]
    public async Task ReapIdleConnectionsAsync_PendingRequest_DoesNotDisposeConnection()
    {
        var connection = new TestIdleConnection(1, "localhost", 9092)
        {
            PendingRequestCount = 1
        };
        var pool = new ConnectionPool(
            clientId: "test-client",
            connectionOptions: new ConnectionOptions { ConnectionsMaxIdleMs = IdleThresholdMs },
            connectionsPerBroker: 1,
            connectionFactory: (_, _, _, _, _) => new ValueTask<IKafkaConnection>(connection));

        await using (pool)
        {
            pool.RegisterBroker(1, "localhost", 9092);

            await pool.GetConnectionAsync(1);
            connection.LastUsedTimestampMs = StaleIdleTimestamp();
            var reapedWithPendingRequest = await pool.ReapIdleConnectionsAsync();
            connection.PendingRequestCount = 0;
            var reapedAfterRequestCompletes = await pool.ReapIdleConnectionsAsync();

            await Assert.That(reapedWithPendingRequest).IsEqualTo(0);
            await Assert.That(reapedAfterRequestCompletes).IsEqualTo(1);
            await Assert.That(connection.DisposeCount).IsEqualTo(1);
        }
    }

    [Test]
    public async Task ReapIdleConnectionsAsync_Group_ReapsOnlyConnectionsWithoutPendingRequests()
    {
        var created = new TestIdleConnection[2];
        var pool = new ConnectionPool(
            clientId: "test-client",
            connectionOptions: new ConnectionOptions { ConnectionsMaxIdleMs = IdleThresholdMs },
            connectionsPerBroker: 2,
            connectionFactory: (brokerId, host, port, index, _) =>
            {
                var connection = new TestIdleConnection(brokerId, host, port)
                {
                    PendingRequestCount = index == 0 ? 1 : 0
                };
                created[index] = connection;
                return new ValueTask<IKafkaConnection>(connection);
            });

        await using (pool)
        {
            pool.RegisterBroker(1, "localhost", 9092);

            await pool.GetConnectionAsync(1);
            created[0].LastUsedTimestampMs = StaleIdleTimestamp();
            created[1].LastUsedTimestampMs = StaleIdleTimestamp();
            var reaped = await pool.ReapIdleConnectionsAsync();

            await Assert.That(reaped).IsEqualTo(1);
            await Assert.That(created[0].DisposeCount).IsEqualTo(0);
            await Assert.That(created[1].DisposeCount).IsEqualTo(1);
        }
    }

    [Test]
    [NotInParallel]
    public async Task CreateConnectionGroup_AfterConnectionFailure_WaitsForReconnectBackoff()
    {
        const int connectionsPerBroker = 2;
        var attempts = 0;
        var options = new ConnectionOptions
        {
            ConnectionTimeout = TimeSpan.FromSeconds(30),
            ReconnectBackoff = TimeSpan.FromMilliseconds(40),
            ReconnectBackoffMax = TimeSpan.FromMilliseconds(40)
        };

        var pool = new ConnectionPool(
            clientId: "test-client",
            connectionOptions: options,
            connectionsPerBroker: connectionsPerBroker,
            connectionFactory: (brokerId, host, port, _, _) =>
            {
                if (Interlocked.Increment(ref attempts) <= connectionsPerBroker)
                    return new ValueTask<IKafkaConnection>(Task.FromException<IKafkaConnection>(
                        new InvalidOperationException("broker down")));

                return new ValueTask<IKafkaConnection>(CreateConnectedConnection(brokerId, host, port));
            });

        await using (pool)
        {
            pool.RegisterBroker(1, "localhost", 9092);

            Func<Task> firstAttempt = () => pool.GetConnectionAsync(1).AsTask();
            await Assert.That(firstAttempt).Throws<InvalidOperationException>();

            var stopwatch = Stopwatch.StartNew();
            var connection = await pool.GetConnectionAsync(1);
            stopwatch.Stop();

            await Assert.That(connection.IsConnected).IsTrue();
            await Assert.That(stopwatch.Elapsed).IsGreaterThanOrEqualTo(TimeSpan.FromMilliseconds(25));
            await Assert.That(attempts).IsEqualTo(4);
        }
    }

    [Test]
    [NotInParallel]
    public async Task ReplaceConnectionInGroup_DuringReconnectBackoff_RespectsConnectionTimeout()
    {
        const int connectionsPerBroker = 2;
        var initialCreationDone = 0;
        var replacementAttempts = 0;
        var options = new ConnectionOptions
        {
            ConnectionTimeout = TimeSpan.FromMilliseconds(100),
            ReconnectBackoff = TimeSpan.FromSeconds(1),
            ReconnectBackoffMax = TimeSpan.FromSeconds(1)
        };

        var pool = new ConnectionPool(
            clientId: "test-client",
            connectionOptions: options,
            connectionsPerBroker: connectionsPerBroker,
            connectionFactory: (brokerId, host, port, _, _) =>
            {
                if (Volatile.Read(ref initialCreationDone) != 0
                    && Interlocked.Increment(ref replacementAttempts) == 1)
                {
                    return new ValueTask<IKafkaConnection>(Task.FromException<IKafkaConnection>(
                        new InvalidOperationException("broker down")));
                }

                return new ValueTask<IKafkaConnection>(CreateConnectedConnection(brokerId, host, port));
            });

        await using (pool)
        {
            pool.RegisterBroker(1, "localhost", 9092);

            await pool.GetConnectionAsync(1);
            Volatile.Write(ref initialCreationDone, 1);

            var oldConnection = await pool.GetConnectionByIndexAsync(1, 0);
            oldConnection.IsConnected.Returns(false);

            Func<Task> failedReplacement = () => pool.GetConnectionByIndexAsync(1, 0).AsTask();
            await Assert.That(failedReplacement).Throws<InvalidOperationException>();

            Func<Task> timedOutReplacement = () => pool.GetConnectionByIndexAsync(1, 0).AsTask();
            await Assert.That(timedOutReplacement).Throws<KafkaException>()
                .WithMessageContaining("Connection replacement timeout");
        }
    }

    private static OAuthBearerConfig CreateOAuthBearerConfig() => new()
    {
        TokenEndpointUrl = "https://auth.example.invalid/token",
        ClientId = "client",
        ClientSecret = "secret"
    };

    private static OAuthBearerToken CreateOAuthBearerToken() => new()
    {
        TokenValue = "token",
        PrincipalName = "principal",
        Expiration = DateTimeOffset.UtcNow.AddHours(1)
    };

    private static MemoryPool<byte> GetSharedPipeMemoryPool(ConnectionPool pool)
    {
        var field = typeof(ConnectionPool).GetField(
            "_sharedPipeMemoryPool",
            BindingFlags.NonPublic | BindingFlags.Instance);

        return (MemoryPool<byte>)field!.GetValue(pool)!;
    }

    private static long StaleIdleTimestamp() => Environment.TickCount64 - IdleThresholdMs - 1;

    private static IKafkaConnection CreateConnectedConnection(int brokerId, string host, int port)
    {
        var connection = Substitute.For<IKafkaConnection>();
        connection.IsConnected.Returns(true);
        connection.BrokerId.Returns(brokerId);
        connection.Host.Returns(host);
        connection.Port.Returns(port);
        return connection;
    }

    private sealed class TestIdleConnection(int brokerId, string host, int port) : IKafkaConnection, IIdleTrackedKafkaConnection
    {
        private int _connected = 1;
        private long _lastUsedTimestampMs = Environment.TickCount64;
        private int _pendingRequestCount;
        private int _disposeCount;

        public int BrokerId { get; } = brokerId;
        public string Host { get; } = host;
        public int Port { get; } = port;
        public bool IsConnected => Volatile.Read(ref _connected) != 0;
        public int DisposeCount => Volatile.Read(ref _disposeCount);

        public long LastUsedTimestampMs
        {
            get => Volatile.Read(ref _lastUsedTimestampMs);
            set => Volatile.Write(ref _lastUsedTimestampMs, value);
        }

        public int PendingRequestCount
        {
            get => Volatile.Read(ref _pendingRequestCount);
            set => Volatile.Write(ref _pendingRequestCount, value);
        }

        long IIdleTrackedKafkaConnection.LastUsedTimestampMs => LastUsedTimestampMs;

        int IIdleTrackedKafkaConnection.PendingRequestCount => PendingRequestCount;

        void IIdleTrackedKafkaConnection.Touch() => LastUsedTimestampMs = Environment.TickCount64;

        public ValueTask ConnectAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public ValueTask<TResponse> SendAsync<TRequest, TResponse>(
            TRequest request,
            short apiVersion,
            CancellationToken cancellationToken = default)
            where TRequest : IKafkaRequest<TResponse>
            where TResponse : IKafkaResponse
            => ValueTask.FromException<TResponse>(new NotSupportedException());

        public ValueTask SendFireAndForgetAsync<TRequest, TResponse>(
            TRequest request,
            short apiVersion,
            CancellationToken cancellationToken = default)
            where TRequest : IKafkaRequest<TResponse>
            where TResponse : IKafkaResponse
            => ValueTask.FromException(new NotSupportedException());

        public Task<TResponse> SendPipelinedAsync<TRequest, TResponse>(
            TRequest request,
            short apiVersion,
            CancellationToken cancellationToken = default)
            where TRequest : IKafkaRequest<TResponse>
            where TResponse : IKafkaResponse
            => Task.FromException<TResponse>(new NotSupportedException());

        public ValueTask SendFireAndForgetWithCallerTimeoutAsync<TRequest, TResponse>(
            TRequest request,
            short apiVersion,
            CancellationToken cancellationToken = default)
            where TRequest : IKafkaRequest<TResponse>
            where TResponse : IKafkaResponse
            => ValueTask.FromException(new NotSupportedException());

        public Task<TResponse> SendPipelinedWithCallerTimeoutAsync<TRequest, TResponse>(
            TRequest request,
            short apiVersion,
            CancellationToken cancellationToken = default)
            where TRequest : IKafkaRequest<TResponse>
            where TResponse : IKafkaResponse
            => Task.FromException<TResponse>(new NotSupportedException());

        public ValueTask DisposeAsync()
        {
            Interlocked.Increment(ref _disposeCount);
            Volatile.Write(ref _connected, 0);
            return ValueTask.CompletedTask;
        }
    }
}
