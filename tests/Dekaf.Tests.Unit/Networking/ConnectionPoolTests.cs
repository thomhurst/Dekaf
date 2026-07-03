using System.Buffers;
using System.Diagnostics;
using System.Reflection;
using Dekaf.Errors;
using Dekaf.Networking;
using Dekaf.Security.Sasl;
using NSubstitute;

namespace Dekaf.Tests.Unit.Networking;

/// <summary>
/// Tests for ConnectionPool, including behavior with disposed connections
/// and proper error handling for various states.
/// </summary>
public sealed class ConnectionPoolTests
{
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

    private static IKafkaConnection CreateConnectedConnection(int brokerId, string host, int port)
    {
        var connection = Substitute.For<IKafkaConnection>();
        connection.IsConnected.Returns(true);
        connection.BrokerId.Returns(brokerId);
        connection.Host.Returns(host);
        connection.Port.Returns(port);
        return connection;
    }
}
