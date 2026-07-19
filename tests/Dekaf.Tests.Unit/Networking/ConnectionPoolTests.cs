using System.Buffers;
using System.Collections.Concurrent;
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
    public async Task DisposeAsync_StopsPendingIdleReaperPromptly()
    {
        var pool = new ConnectionPool("test-client");

        var disposal = pool.DisposeAsync().AsTask();

        await disposal.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.That(disposal.IsCompletedSuccessfully).IsTrue();
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
    public async Task GetConnectionAsync_EndpointChanged_ReplacesCachedConnection()
    {
        var pool = new ConnectionPool(
            clientId: "test-client",
            connectionOptions: new ConnectionOptions(),
            connectionsPerBroker: 1,
            connectionFactory: (brokerId, host, port, _, _) =>
            {
                var connection = CreateConnectedConnection(brokerId, host, port);
                return new ValueTask<IKafkaConnection>(connection);
            });
        await using (pool)
        {
            pool.RegisterBroker(1, "host-a", 9092);
            var original = await pool.GetConnectionAsync(1);

            pool.RegisterBroker(1, "host-b", 9093);
            var replacement = await pool.GetConnectionAsync(1);

            await Assert.That(replacement).IsNotSameReferenceAs(original);
            await Assert.That(replacement.Host).IsEqualTo("host-b");
            await Assert.That(replacement.Port).IsEqualTo(9093);
            await original.Received(1).DisposeAsync();
        }
    }

    [Test]
    [NotInParallel]
    [Timeout(5_000)]
    public async Task RegisterBroker_EndpointChange_DoesNotRetireFreshReplacement(
        CancellationToken cancellationToken)
    {
        var endpointUpdated = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseRegistration = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var connections = new List<IKafkaConnection>();
        var pool = new ConnectionPool(
            clientId: "test-client",
            connectionOptions: new ConnectionOptions(),
            connectionsPerBroker: 1,
            connectionFactory: (brokerId, host, port, _, _) =>
            {
                var connection = CreateConnectedConnection(brokerId, host, port);
                connections.Add(connection);
                return ValueTask.FromResult(connection);
            },
            brokerEndpointUpdated: () =>
            {
                endpointUpdated.TrySetResult();
                releaseRegistration.Task.GetAwaiter().GetResult();
            });

        await using (pool)
        {
            pool.RegisterBroker(1, "host-a", 9092);
            var original = await pool.GetConnectionAsync(1, cancellationToken);
            original.IsConnected.Returns(false);

            var registration = Task.Run(
                () => pool.RegisterBroker(1, "host-b", 9093),
                cancellationToken);
            try
            {
                await endpointUpdated.Task.WaitAsync(cancellationToken);
                var replacement = await pool.GetConnectionAsync(1, cancellationToken);

                releaseRegistration.SetResult();
                await registration.WaitAsync(cancellationToken);

                await Assert.That(replacement.Host).IsEqualTo("host-b");
                await replacement.DidNotReceive().DisposeAsync();
            }
            finally
            {
                releaseRegistration.TrySetResult();
            }
        }

        await Assert.That(connections).Count().IsEqualTo(2);
    }

    [Test]
    public async Task GetConnectionAsync_EndpointChanged_ReplacesCachedConnectionGroup()
    {
        var pool = new ConnectionPool(
            clientId: "test-client",
            connectionOptions: new ConnectionOptions(),
            connectionsPerBroker: 2,
            connectionFactory: (brokerId, host, port, _, _) =>
            {
                var connection = CreateConnectedConnection(brokerId, host, port);
                return new ValueTask<IKafkaConnection>(connection);
            });
        await using (pool)
        {
            pool.RegisterBroker(1, "host-a", 9092);
            var original = await pool.GetConnectionAsync(1);

            pool.RegisterBroker(1, "host-b", 9093);
            var replacement = await pool.GetConnectionByIndexAsync(1, 0);

            await Assert.That(replacement).IsNotSameReferenceAs(original);
            await Assert.That(replacement.Host).IsEqualTo("host-b");
            await Assert.That(replacement.Port).IsEqualTo(9093);
            await original.Received(1).DisposeAsync();
        }
    }

    [Test]
    [NotInParallel]
    [Timeout(5_000)]
    public async Task RegisterBroker_EndpointChange_RetiresMixedConnectionGroup(
        CancellationToken cancellationToken)
    {
        var endpointUpdated = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseRegistration = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var pool = new ConnectionPool(
            clientId: "test-client",
            connectionOptions: new ConnectionOptions(),
            connectionsPerBroker: 2,
            connectionFactory: (brokerId, host, port, _, _) =>
                ValueTask.FromResult(CreateConnectedConnection(brokerId, host, port)),
            brokerEndpointUpdated: () =>
            {
                endpointUpdated.TrySetResult();
                releaseRegistration.Task.GetAwaiter().GetResult();
            });

        pool.RegisterBroker(1, "host-a", 9092);
        var oldFirst = await pool.GetConnectionByIndexAsync(1, 0, cancellationToken);
        var oldSecond = await pool.GetConnectionByIndexAsync(1, 1, cancellationToken);
        oldFirst.IsConnected.Returns(false);

        var registration = Task.Run(
            () => pool.RegisterBroker(1, "host-b", 9093),
            cancellationToken);
        IKafkaConnection replacement;
        try
        {
            await endpointUpdated.Task.WaitAsync(cancellationToken);
            replacement = await pool.GetConnectionByIndexAsync(1, 0, cancellationToken);
        }
        finally
        {
            releaseRegistration.TrySetResult();
        }

        await registration.WaitAsync(cancellationToken);
        var fresh = await pool.GetConnectionByIndexAsync(1, 0, cancellationToken);
        await pool.DisposeAsync();

        await Assert.That(replacement.Host).IsEqualTo("host-b");
        await Assert.That(fresh).IsNotSameReferenceAs(replacement);
        await oldSecond.Received(1).DisposeAsync();
        await replacement.Received(1).DisposeAsync();
    }

    [Test]
    public async Task DisposeAsync_ForceDisposesTrackedRetiredConnection()
    {
        var retired = new TestIdleConnection(1, "host-a", 9092);
        var pool = new ConnectionPool(
            clientId: "test-client",
            connectionOptions: new ConnectionOptions(),
            connectionsPerBroker: 1,
            connectionFactory: (_, host, port, _, _) =>
                ValueTask.FromResult<IKafkaConnection>(host == "host-a"
                    ? retired
                    : new TestIdleConnection(1, host, port)));

        pool.RegisterBroker(1, "host-a", 9092);
        _ = await pool.GetConnectionAsync(1);
        await Assert.That(KafkaConnectionLease.TryAcquire(retired, out var lease)).IsTrue();
        pool.RegisterBroker(1, "host-b", 9093);

        await pool.DisposeAsync();

        await Assert.That(retired.DisposeCount).IsEqualTo(1);
        lease.Dispose();
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
    public async Task ConnectionPools_WithSameConfiguration_SharePipeMemoryPool()
    {
        var first = new ConnectionPool("first-client");
        var second = new ConnectionPool("second-client");
        var firstSharedPool = GetSharedPipeMemoryPool(first);
        var secondSharedPool = GetSharedPipeMemoryPool(second);

        await first.DisposeAsync();

        await Assert.That(firstSharedPool).IsSameReferenceAs(secondSharedPool);
        using var owner = secondSharedPool.Rent(1);
        await Assert.That(owner.Memory.Length).IsGreaterThanOrEqualTo(1);

        await second.DisposeAsync();
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

            var diagnostic = pool.GetConnectionReapDiagnosticsSnapshot().Single();
            await Assert.That(diagnostic.BrokerId).IsEqualTo(1);
            await Assert.That(diagnostic.ConnectionIndex).IsEqualTo(0);
            await Assert.That(diagnostic.IsBootstrapConnection).IsFalse();
            await Assert.That(diagnostic.IdleDurationMs).IsGreaterThanOrEqualTo(IdleThresholdMs);
        }
    }

    [Test]
    public async Task ReapIdleConnectionsAsync_BootstrapDiagnostic_IsExplicitlyIdentified()
    {
        var connection = new TestIdleConnection(-1, "bootstrap", 9092);
        var pool = new ConnectionPool(
            clientId: "test-client",
            connectionOptions: new ConnectionOptions { ConnectionsMaxIdleMs = IdleThresholdMs },
            connectionsPerBroker: 1,
            connectionFactory: (_, _, _, _, _) => new ValueTask<IKafkaConnection>(connection));

        await using (pool)
        {
            await pool.GetConnectionAsync("bootstrap", 9092);
            connection.LastUsedTimestampMs = StaleIdleTimestamp();

            var reaped = await pool.ReapIdleConnectionsAsync();

            await Assert.That(reaped).IsEqualTo(1);
            var diagnostic = pool.GetConnectionReapDiagnosticsSnapshot().Single();
            await Assert.That(diagnostic.BrokerId).IsEqualTo(-1);
            await Assert.That(diagnostic.IsBootstrapConnection).IsTrue();
        }
    }

    [Test]
    public async Task ReapIdleConnectionsAsync_ActiveLease_DrainsBeforeDisposal()
    {
        var connection = new TestIdleConnection(1, "localhost", 9092);
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
            await Assert.That(KafkaConnectionLease.TryAcquire(connection, out var lease)).IsTrue();

            var reapTask = pool.ReapIdleConnectionsAsync().AsTask();
            await Assert.That(SpinWait.SpinUntil(
                () => connection.RetirementState == 1,
                TimeSpan.FromSeconds(5))).IsTrue();
            await Assert.That(connection.DisposeCount).IsEqualTo(0);

            lease.Dispose();
            var reaped = await reapTask;

            await Assert.That(reaped).IsEqualTo(1);
            await Assert.That(connection.DisposeCount).IsEqualTo(1);
            await Assert.That(connection.RetirementState).IsEqualTo(2);
        }
    }

    [Test]
    public async Task ReapIdleConnectionsAsync_RequestWinsRecheck_RestoresConnection()
    {
        var connection = new TestIdleConnection(1, "localhost", 9092);
        var pool = new ConnectionPool(
            clientId: "test-client",
            connectionOptions: new ConnectionOptions { ConnectionsMaxIdleMs = IdleThresholdMs },
            connectionsPerBroker: 1,
            connectionFactory: (_, _, _, _, _) => new ValueTask<IKafkaConnection>(connection));

        await using (pool)
        {
            pool.RegisterBroker(1, "localhost", 9092);
            var first = await pool.GetConnectionAsync(1);
            connection.LastUsedTimestampMs = StaleIdleTimestamp();
            var pendingReads = 0;
            connection.PendingRequestCountProvider = () =>
                Interlocked.Increment(ref pendingReads) == 1 ? 0 : 1;

            var reaped = await pool.ReapIdleConnectionsAsync();
            var second = await pool.GetConnectionAsync(1);

            await Assert.That(reaped).IsEqualTo(0);
            await Assert.That(second).IsSameReferenceAs(first);
            await Assert.That(connection.DisposeCount).IsEqualTo(0);
            await Assert.That(connection.RetirementState).IsEqualTo(0);
        }
    }

    [Test]
    public async Task ReapIdleConnectionsAsync_DrainTimeout_DoesNotBlockPoolDisposal()
    {
        var connection = new TestIdleConnection(1, "localhost", 9092);
        var pool = new ConnectionPool(
            clientId: "test-client",
            connectionOptions: new ConnectionOptions { ConnectionsMaxIdleMs = IdleThresholdMs },
            connectionsPerBroker: 1,
            connectionFactory: (_, _, _, _, _) => new ValueTask<IKafkaConnection>(connection),
            idleReapDrainTimeout: TimeSpan.FromMilliseconds(20));

        pool.RegisterBroker(1, "localhost", 9092);
        await pool.GetConnectionAsync(1);
        connection.LastUsedTimestampMs = StaleIdleTimestamp();
        await Assert.That(KafkaConnectionLease.TryAcquire(connection, out var lease)).IsTrue();

        var reaped = await pool.ReapIdleConnectionsAsync();
        await pool.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(1));

        await Assert.That(reaped).IsEqualTo(1);
        await Assert.That(connection.DisposeCount).IsEqualTo(0);

        lease.Dispose();
        await Assert.That(SpinWait.SpinUntil(
            () => connection.DisposeCount == 1,
            TimeSpan.FromSeconds(5))).IsTrue();
    }

    [Test]
    public async Task ConnectionReapDiagnostics_RetainsMostRecent256Events()
    {
        var pool = new ConnectionPool(
            clientId: "test-client",
            connectionOptions: new ConnectionOptions(),
            connectionsPerBroker: 1);

        await using (pool)
        {
            var recordDiagnostic = typeof(ConnectionPool).GetMethod(
                "RecordConnectionReapDiagnostic",
                BindingFlags.Instance | BindingFlags.NonPublic)!;
            for (var i = 0; i < 513; i++)
                recordDiagnostic.Invoke(pool, [i, 0, 1_000L + i]);

            var diagnostics = pool.GetConnectionReapDiagnosticsSnapshot();

            await Assert.That(diagnostics.Count).IsEqualTo(256);
            await Assert.That(diagnostics[0].BrokerId).IsEqualTo(257);
            await Assert.That(diagnostics[^1].BrokerId).IsEqualTo(512);
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
    public async Task ReapIdleConnectionsAsync_Group_DoesNotDisposeOwnerAttachedConnections()
    {
        var created = new TestIdleConnection[2];
        var pool = new ConnectionPool(
            clientId: "test-client",
            connectionOptions: new ConnectionOptions { ConnectionsMaxIdleMs = IdleThresholdMs },
            connectionsPerBroker: 2,
            connectionFactory: (brokerId, host, port, index, _) =>
            {
                var connection = new TestIdleConnection(brokerId, host, port);
                created[index] = connection;
                return new ValueTask<IKafkaConnection>(connection);
            });

        await using (pool)
        {
            pool.RegisterBroker(1, "localhost", 9092);
            await Assert.That(pool.TryRetainConnectionGroupIndex(1, 0)).IsTrue();
            await Assert.That(pool.TryRetainConnectionGroupIndex(1, 0)).IsTrue();

            try
            {
                await pool.GetConnectionAsync(1);
                created[0].LastUsedTimestampMs = StaleIdleTimestamp();
                created[1].LastUsedTimestampMs = StaleIdleTimestamp();
                var reaped = await pool.ReapIdleConnectionsAsync();

                await Assert.That(reaped).IsEqualTo(1);
                await Assert.That(created[0].DisposeCount).IsEqualTo(0);
                await Assert.That(created[1].DisposeCount).IsEqualTo(1);

                pool.ReleaseConnectionGroupIndex(1, 0);
                var reapedWithOneOwner = await pool.ReapIdleConnectionsAsync();
                await Assert.That(reapedWithOneOwner).IsEqualTo(0);
            }
            finally
            {
                pool.ReleaseConnectionGroupIndex(1, 0);
            }

            var reapedAfterRelease = await pool.ReapIdleConnectionsAsync();
            await Assert.That(reapedAfterRelease).IsEqualTo(1);
            await Assert.That(created[0].DisposeCount).IsEqualTo(1);
        }
    }

    [Test]
    public async Task TryRetainConnectionGroupIndex_DisposedPool_ReturnsFalse()
    {
        var pool = new ConnectionPool("test-client");
        await pool.DisposeAsync();

        await Assert.That(pool.TryRetainConnectionGroupIndex(1, 0)).IsFalse();
    }

    [Test]
    public async Task ReapIdleConnectionsAsync_UnownedGroup_ReapsIdleConnection()
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
    public async Task GetConnectionByIndexAsync_AfterAdaptiveScaleUp_UsesScaledGroup()
    {
        const int scaledCount = 3;
        var captureScaledConnections = 0;
        var scaledConnections = new IKafkaConnection?[scaledCount];
        var pool = new ConnectionPool(
            clientId: "test-client",
            connectionOptions: new ConnectionOptions(),
            connectionsPerBroker: 1,
            connectionFactory: (brokerId, host, port, index, _) =>
            {
                var connection = CreateConnectedConnection(brokerId, host, port);
                if (Volatile.Read(ref captureScaledConnections) != 0)
                    scaledConnections[index] = connection;
                return new ValueTask<IKafkaConnection>(connection);
            });

        await using (pool)
        {
            pool.RegisterBroker(1, "localhost", 9092);

            var originalSingleConnection = await pool.GetConnectionAsync(1);
            Volatile.Write(ref captureScaledConnections, 1);
            var actualScaledCount = await pool.ScaleConnectionGroupAsync(1, newCount: scaledCount);
            Volatile.Write(ref captureScaledConnections, 0);

            var connection0 = await pool.GetConnectionByIndexAsync(1, index: 0);
            var connection1 = await pool.GetConnectionByIndexAsync(1, index: 1);
            var connection2 = await pool.GetConnectionByIndexAsync(1, index: 2);

            await Assert.That(actualScaledCount).IsEqualTo(scaledCount);
            await Assert.That(connection0).IsSameReferenceAs(scaledConnections[0]);
            await Assert.That(connection1).IsSameReferenceAs(scaledConnections[1]);
            await Assert.That(connection2).IsSameReferenceAs(scaledConnections[2]);
            await Assert.That(connection0).IsNotSameReferenceAs(originalSingleConnection);
        }
    }

    [Test]
    [NotInParallel]
    public async Task CreateConnectionGroup_AfterConnectionFailure_WaitsForReconnectBackoff()
    {
        const int connectionsPerBroker = 2;
        var attempts = 0;
        var observedBackoff = new TaskCompletionSource<TimeSpan>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        long timestamp = 0;
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
            },
            randomDouble: static () => 0.5,
            reconnectBackoffDelay: (delay, _) =>
            {
                observedBackoff.TrySetResult(delay);
                Interlocked.Add(ref timestamp, (long)(delay.TotalSeconds * Stopwatch.Frequency));
                return ValueTask.CompletedTask;
            },
            timestampProvider: () => Volatile.Read(ref timestamp));

        await using (pool)
        {
            pool.RegisterBroker(1, "localhost", 9092);

            Func<Task> firstAttempt = () => pool.GetConnectionAsync(1).AsTask();
            await Assert.That(firstAttempt).Throws<InvalidOperationException>();

            var connection = await pool.GetConnectionAsync(1);
            var backoff = await observedBackoff.Task.WaitAsync(TimeSpan.FromSeconds(5));

            await Assert.That(connection.IsConnected).IsTrue();
            await Assert.That(backoff).IsEqualTo(options.ReconnectBackoff);
            await Assert.That(attempts).IsEqualTo(4);
        }
    }

    [Test]
    [Arguments(0.0, 80.0)]
    [Arguments(0.5, 100.0)]
    [Arguments(0.999999, 119.99996)]
    public async Task CalculateReconnectBackoffDelay_UsesSymmetricTwentyPercentJitter(
        double randomValue,
        double expectedMilliseconds)
    {
        await using var pool = CreateReconnectBackoffPool(
            reconnectBackoffMs: 100,
            reconnectBackoffMaxMs: 1000,
            randomValue);

        var delay = pool.CalculateReconnectBackoffDelay(failureCount: 1);

        await Assert.That(delay.TotalMilliseconds).IsEqualTo(expectedMilliseconds).Within(0.001);
    }

    [Test]
    [Arguments(0.0, 98.4)]
    [Arguments(0.5, 123.0)]
    [Arguments(0.999999, 147.5999508)]
    public async Task CalculateReconnectBackoffDelay_FixedBackoffAppliesJitter(
        double randomValue,
        double expectedMilliseconds)
    {
        await using var pool = CreateReconnectBackoffPool(
            reconnectBackoffMs: 123,
            reconnectBackoffMaxMs: 123,
            randomValue);

        var delay = pool.CalculateReconnectBackoffDelay(failureCount: 10);

        await Assert.That(delay.TotalMilliseconds).IsEqualTo(expectedMilliseconds).Within(0.001);
    }

    [Test]
    public async Task CalculateReconnectBackoffDelay_DefaultsGrowExponentiallyToMaximum()
    {
        await using var pool = CreateReconnectBackoffPool(
            reconnectBackoffMs: 50,
            reconnectBackoffMaxMs: 1000,
            randomValue: 0.5);

        var delays = Enumerable.Range(1, 7)
            .Select(pool.CalculateReconnectBackoffDelay)
            .ToArray();

        await Assert.That(delays).IsEquivalentTo(new[]
        {
            TimeSpan.FromMilliseconds(50),
            TimeSpan.FromMilliseconds(100),
            TimeSpan.FromMilliseconds(200),
            TimeSpan.FromMilliseconds(400),
            TimeSpan.FromMilliseconds(800),
            TimeSpan.FromMilliseconds(1000),
            TimeSpan.FromMilliseconds(1000)
        });
    }

    [Test]
    public async Task CalculateReconnectBackoffDelay_CapsBaseBeforeJitter()
    {
        await using var pool = CreateReconnectBackoffPool(
            reconnectBackoffMs: 100,
            reconnectBackoffMaxMs: 150,
            randomValue: 0.999999);

        var delay = pool.CalculateReconnectBackoffDelay(failureCount: 2);

        await Assert.That(delay.TotalMilliseconds).IsEqualTo(179.99994).Within(0.001);
    }

    [Test]
    [Arguments(0, 0.0, 80.0)]
    [Arguments(0, 0.5, 100.0)]
    [Arguments(0, 0.999999, 119.99996)]
    [Arguments(1, 0.0, 160.0)]
    [Arguments(1, 0.5, 200.0)]
    [Arguments(1, 0.999999, 239.99992)]
    [Arguments(6, 0.0, 800.0)]
    [Arguments(6, 0.999999, 1000.0)]
    public async Task CalculateConnectionSetupTimeout_UsesSymmetricTwentyPercentJitter(
        int failureCount,
        double randomValue,
        double expectedMilliseconds)
    {
        await using var pool = CreateConnectionSetupTimeoutPool(randomValue: randomValue);

        var timeout = pool.CalculateConnectionSetupTimeout(failureCount);

        await Assert.That(timeout.TotalMilliseconds).IsEqualTo(expectedMilliseconds).Within(0.001);
    }

    [Test]
    public async Task ConnectionSetupTimeout_FirstAttemptAppliesJitter()
    {
        var observedTimeouts = new List<TimeSpan>();
        await using var pool = CreateConnectionSetupTimeoutPool(
            randomValue: 0,
            connectionFactory: static (_, _, _, _, _) => throw new InvalidOperationException("broker down"),
            timeoutObserver: observedTimeouts.Add);

        Func<Task> connect = () => pool.GetConnectionAsync("broker-a", 9092).AsTask();

        await Assert.That(connect).Throws<InvalidOperationException>();
        await Assert.That(observedTimeouts).IsEquivalentTo(
            [TimeSpan.FromMilliseconds(80)]);
    }

    [Test]
    public async Task ConnectionSetupTimeout_GroupFailureAdvancesOncePerRound()
    {
        var observedTimeouts = new ConcurrentQueue<TimeSpan>();
        await using var pool = CreateConnectionSetupTimeoutPool(
            randomValue: 0.5,
            connectionFactory: static (_, _, _, _, _) => throw new InvalidOperationException("broker down"),
            timeoutObserver: observedTimeouts.Enqueue,
            connectionsPerBroker: 3);
        pool.RegisterBroker(1, "broker-a", 9092);

        for (var round = 0; round < 2; round++)
        {
            Func<Task> connect = () => pool.GetConnectionAsync(1).AsTask();
            await Assert.That(connect).Throws<InvalidOperationException>();
        }

        await Assert.That(observedTimeouts).IsEquivalentTo(new[]
        {
            TimeSpan.FromMilliseconds(100),
            TimeSpan.FromMilliseconds(100),
            TimeSpan.FromMilliseconds(100),
            TimeSpan.FromMilliseconds(200),
            TimeSpan.FromMilliseconds(200),
            TimeSpan.FromMilliseconds(200)
        });
    }

    [Test]
    [NotInParallel]
    [Timeout(5_000)]
    public async Task ConnectionSetupTimeout_GroupRoundBoundsReconnectGateWait(
        CancellationToken cancellationToken)
    {
        var attempts = 0;
        var options = new ConnectionOptions
        {
            ConnectionTimeout = TimeSpan.FromMilliseconds(20),
            ConnectionTimeoutMax = TimeSpan.FromMilliseconds(20),
            ReconnectBackoff = TimeSpan.Zero,
            ReconnectBackoffMax = TimeSpan.Zero
        };
        await using var pool = new ConnectionPool(
            clientId: "test-client",
            connectionOptions: options,
            connectionsPerBroker: 3,
            connectionFactory: (brokerId, host, port, _, _) =>
            {
                if (Interlocked.Increment(ref attempts) == 1)
                    throw new InvalidOperationException("broker down");

                return ValueTask.FromResult(CreateConnectedConnection(brokerId, host, port));
            });

        pool.RegisterBroker(1, "broker-a", 9092);
        Func<Task> establishReconnectState = () => pool.GetConnectionAsync(1).AsTask();
        await Assert.That(establishReconnectState).Throws<InvalidOperationException>();
        var attemptGate = await AssertSingleReconnectBackoffGate(pool);
        await attemptGate.WaitAsync(cancellationToken);
        try
        {
            Func<Task> createGroup = () => pool.GetConnectionAsync(1, cancellationToken).AsTask();
            var exception = await Assert.That(createGroup).Throws<KafkaException>();

            await Assert.That(exception!.ErrorCode).IsEqualTo(ErrorCode.RequestTimedOut);
            await Assert.That(attempts).IsEqualTo(3);
        }
        finally
        {
            attemptGate.Release();
        }
    }

    [Test]
    public async Task CalculateConnectionSetupTimeout_GrowsExponentiallyAndCapsAtMaximum()
    {
        await using var pool = CreateConnectionSetupTimeoutPool(randomValue: 0.5);

        var timeouts = Enumerable.Range(0, 7)
            .Select(pool.CalculateConnectionSetupTimeout)
            .ToArray();

        await Assert.That(timeouts).IsEquivalentTo(new[]
        {
            TimeSpan.FromMilliseconds(100),
            TimeSpan.FromMilliseconds(200),
            TimeSpan.FromMilliseconds(400),
            TimeSpan.FromMilliseconds(800),
            TimeSpan.FromMilliseconds(1000),
            TimeSpan.FromMilliseconds(1000),
            TimeSpan.FromMilliseconds(1000)
        });
    }

    [Test]
    [NotInParallel]
    [Timeout(5_000)]
    public async Task ConnectionSetupTimeout_CancelsStalledAttemptAtEffectiveDeadline(
        CancellationToken cancellationToken)
    {
        await using var pool = new ConnectionPool(
            clientId: "test-client",
            connectionOptions: new ConnectionOptions
            {
                ConnectionTimeout = TimeSpan.FromMilliseconds(20),
                ConnectionTimeoutMax = TimeSpan.FromMilliseconds(40),
                ReconnectBackoff = TimeSpan.Zero,
                ReconnectBackoffMax = TimeSpan.Zero
            },
            connectionsPerBroker: 1,
            connectionFactory: async (_, _, _, _, cancellationToken) =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                throw new InvalidOperationException("unreachable");
            },
            randomDouble: static () => 0.5);

        Func<Task> connect = () => pool.GetConnectionAsync("broker-a", 9092, cancellationToken).AsTask();

        await Assert.That(connect).Throws<KafkaException>()
            .WithMessageContaining("Connection setup timeout after 20ms");
    }

    [Test]
    public async Task ConnectionLockTimeout_IsReportedAsRequestTimeout()
    {
        await using var pool = new ConnectionPool(
            clientId: "test-client",
            connectionOptions: new ConnectionOptions
            {
                ConnectionTimeout = TimeSpan.FromMilliseconds(20)
            });
        using var connectionLock = new SemaphoreSlim(0, 1);
        var waitForLock = typeof(ConnectionPool).GetMethod(
            "WaitForConnectionLockAsync",
            BindingFlags.NonPublic | BindingFlags.Instance)!;

        var exception = await Assert.That(() =>
                ((ValueTask)waitForLock.Invoke(pool, [connectionLock, CancellationToken.None])!).AsTask())
            .Throws<KafkaException>();

        await Assert.That(exception).IsNotNull();
        await Assert.That(exception!.ErrorCode).IsEqualTo(ErrorCode.RequestTimedOut);
    }

    [Test]
    public async Task ConnectionLockTimeout_UsesConfiguredMaximum()
    {
        await using var pool = new ConnectionPool(
            clientId: "test-client",
            connectionOptions: new ConnectionOptions
            {
                ConnectionTimeout = TimeSpan.FromMilliseconds(10),
                ConnectionTimeoutMax = TimeSpan.FromMilliseconds(40)
            });
        using var connectionLock = new SemaphoreSlim(0, 1);
        var waitForLock = typeof(ConnectionPool).GetMethod(
            "WaitForConnectionLockAsync",
            BindingFlags.NonPublic | BindingFlags.Instance)!;

        await Assert.That(() =>
                ((ValueTask)waitForLock.Invoke(pool, [connectionLock, CancellationToken.None])!).AsTask())
            .Throws<KafkaException>()
            .WithMessageContaining("Timed out after 40ms");
    }

    [Test]
    public async Task ConnectionLockWait_CallerCancellationRemainsOperationCanceled()
    {
        await using var pool = new ConnectionPool(
            clientId: "test-client",
            connectionOptions: new ConnectionOptions
            {
                ConnectionTimeout = TimeSpan.FromMilliseconds(100)
            });
        using var connectionLock = new SemaphoreSlim(0, 1);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var waitForLock = typeof(ConnectionPool).GetMethod(
            "WaitForConnectionLockAsync",
            BindingFlags.NonPublic | BindingFlags.Instance)!;

        await Assert.That(() =>
                ((ValueTask)waitForLock.Invoke(pool, [connectionLock, cancellation.Token])!).AsTask())
            .Throws<OperationCanceledException>();
    }

    [Test]
    public async Task ConnectionSetupTimeout_GroupHardStopsFactoryThatIgnoresCancellation()
    {
        var releaseFactory = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        await using var pool = new ConnectionPool(
            clientId: "test-client",
            connectionOptions: new ConnectionOptions
            {
                ConnectionTimeout = TimeSpan.FromMilliseconds(20),
                ConnectionTimeoutMax = TimeSpan.FromMilliseconds(20),
                ReconnectBackoff = TimeSpan.Zero,
                ReconnectBackoffMax = TimeSpan.Zero
            },
            connectionsPerBroker: 2,
            connectionFactory: async (brokerId, host, port, _, _) =>
            {
                await releaseFactory.Task;
                return CreateConnectedConnection(brokerId, host, port);
            },
            randomDouble: static () => 0.5);
        pool.RegisterBroker(1, "broker-a", 9092);

        try
        {
            Func<Task> connect = () => pool.GetConnectionAsync(1).AsTask();

            await Assert.That(connect).Throws<KafkaException>()
                .WithMessageContaining("Connection setup timeout after 20ms");
        }
        finally
        {
            releaseFactory.TrySetResult();
        }
    }

    [Test]
    public async Task ConnectionSetupTimeout_OverallDeadlineIncludesReconnectBackoff()
    {
        var releaseFactory = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var observedBackoff = new TaskCompletionSource<TimeSpan>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseBackoff = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var attempts = 0;
        long timestamp = 0;
        await using var pool = new ConnectionPool(
            clientId: "test-client",
            connectionOptions: new ConnectionOptions
            {
                ConnectionTimeout = TimeSpan.FromMilliseconds(20),
                ConnectionTimeoutMax = TimeSpan.FromMilliseconds(20),
                ReconnectBackoff = TimeSpan.FromMilliseconds(80),
                ReconnectBackoffMax = TimeSpan.FromMilliseconds(80)
            },
            connectionsPerBroker: 1,
            connectionFactory: async (brokerId, host, port, _, _) =>
            {
                if (Interlocked.Increment(ref attempts) == 2)
                    return CreateConnectedConnection(brokerId, host, port);

                await releaseFactory.Task;
                return CreateConnectedConnection(brokerId, host, port);
            },
            randomDouble: static () => 0.5,
            reconnectBackoffDelay: async (delay, cancellationToken) =>
            {
                observedBackoff.TrySetResult(delay);
                await releaseBackoff.Task.WaitAsync(cancellationToken);
                Interlocked.Add(ref timestamp, (long)(delay.TotalSeconds * Stopwatch.Frequency));
            },
            timestampProvider: () => Volatile.Read(ref timestamp));

        try
        {
            Func<Task> firstAttempt = () => pool.GetConnectionAsync("broker-a", 9092).AsTask();
            await Assert.That(firstAttempt).Throws<KafkaException>()
                .WithMessageContaining("timeout after 20ms");

            var secondAttempt = pool.GetConnectionAsync("broker-a", 9092).AsTask();
            var delay = await observedBackoff.Task.WaitAsync(TimeSpan.FromSeconds(5));

            await Assert.That(delay).IsEqualTo(TimeSpan.FromMilliseconds(80));
            await Assert.That(attempts).IsEqualTo(1);

            await Assert.That(async () => await secondAttempt).Throws<KafkaException>()
                .WithMessageContaining("Connection operation timeout after 20ms");
            await Assert.That(attempts).IsEqualTo(1);
        }
        finally
        {
            releaseBackoff.TrySetResult();
            releaseFactory.TrySetResult();
        }
    }

    [Test]
    public async Task ConnectionSetupTimeout_LateSuccessIsNeverPublished()
    {
        var releaseFirstFactory = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var lateConnectionDisposed = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var lateConnection = CreateConnectedConnection(-1, "broker-a", 9092);
        lateConnection.DisposeAsync().Returns(_ =>
        {
            lateConnectionDisposed.TrySetResult();
            return ValueTask.CompletedTask;
        });
        var freshConnection = CreateConnectedConnection(-1, "broker-a", 9092);
        var attempts = 0;

        await using var pool = new ConnectionPool(
            clientId: "test-client",
            connectionOptions: new ConnectionOptions
            {
                ConnectionTimeout = TimeSpan.FromMilliseconds(20),
                // Keep the overall operation deadline distinct so this test exercises
                // disposal of a connection that completes after the per-attempt timeout.
                ConnectionTimeoutMax = TimeSpan.FromSeconds(1),
                ReconnectBackoff = TimeSpan.Zero,
                ReconnectBackoffMax = TimeSpan.Zero
            },
            connectionsPerBroker: 1,
            connectionFactory: async (_, _, _, _, _) =>
            {
                if (Interlocked.Increment(ref attempts) == 1)
                {
                    await releaseFirstFactory.Task;
                    return lateConnection;
                }

                return freshConnection;
            },
            randomDouble: static () => 0.5);

        Func<Task> firstAttempt = () => pool.GetConnectionAsync("broker-a", 9092).AsTask();
        await Assert.That(firstAttempt).Throws<KafkaException>()
            .WithMessageContaining("Connection setup timeout after 20ms");

        releaseFirstFactory.TrySetResult();
        await lateConnectionDisposed.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var acquired = await pool.GetConnectionAsync("broker-a", 9092);

        await Assert.That(acquired).IsSameReferenceAs(freshConnection);
        await Assert.That(attempts).IsEqualTo(2);
    }

    [Test]
    public async Task DisposeAsync_WaitsForLateConnectionSetupDisposal()
    {
        var releaseFactory = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var disposalStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseDisposal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var lateConnection = CreateConnectedConnection(-1, "broker-a", 9092);
        lateConnection.DisposeAsync().Returns(_ =>
        {
            disposalStarted.TrySetResult();
            return new ValueTask(releaseDisposal.Task);
        });
        var pool = new ConnectionPool(
            clientId: "test-client",
            connectionOptions: new ConnectionOptions
            {
                ConnectionTimeout = TimeSpan.FromMilliseconds(20),
                ConnectionTimeoutMax = TimeSpan.FromSeconds(1),
                ReconnectBackoff = TimeSpan.Zero,
                ReconnectBackoffMax = TimeSpan.Zero
            },
            connectionsPerBroker: 1,
            connectionFactory: async (_, _, _, _, _) =>
            {
                await releaseFactory.Task;
                return lateConnection;
            },
            randomDouble: static () => 0.5);

        Func<Task> connect = () => pool.GetConnectionAsync("broker-a", 9092).AsTask();
        await Assert.That(connect).Throws<KafkaException>()
            .WithMessageContaining("Connection setup timeout after 20ms");

        var disposeTask = pool.DisposeAsync().AsTask();
        await Assert.That(disposeTask.IsCompleted).IsFalse();

        releaseFactory.TrySetResult();
        await disposalStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.That(disposeTask.IsCompleted).IsFalse();

        releaseDisposal.TrySetResult();
        await disposeTask.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Test]
    public async Task ConnectionSetupTimeout_SuccessResetsFailureProgression()
    {
        var attempts = 0;
        var observedTimeouts = new List<TimeSpan>();
        await using var pool = CreateConnectionSetupTimeoutPool(
            randomValue: 0.5,
            connectionFactory: (brokerId, host, port, _, _) =>
            {
                if (Interlocked.Increment(ref attempts) == 1)
                    throw new InvalidOperationException("broker down");
                return new ValueTask<IKafkaConnection>(CreateConnectedConnection(brokerId, host, port));
            },
            timeoutObserver: observedTimeouts.Add);
        pool.RegisterBroker(1, "broker-a", 9092);

        Func<Task> firstAttempt = () => pool.GetConnectionAsync(1).AsTask();
        await Assert.That(firstAttempt).Throws<InvalidOperationException>();
        await pool.GetConnectionAsync(1);
        await pool.RemoveConnectionAsync(1);
        await pool.GetConnectionAsync(1);

        await Assert.That(observedTimeouts).IsEquivalentTo(new[]
        {
            TimeSpan.FromMilliseconds(100),
            TimeSpan.FromMilliseconds(200),
            TimeSpan.FromMilliseconds(100)
        });
    }

    [Test]
    public async Task ConnectionSetupTimeout_FailureStateIsIsolatedByEndpoint()
    {
        var observedTimeouts = new List<TimeSpan>();
        await using var pool = CreateConnectionSetupTimeoutPool(
            randomValue: 0.5,
            connectionFactory: (_, _, _, _, _) => throw new InvalidOperationException("broker down"),
            timeoutObserver: observedTimeouts.Add);

        for (var i = 0; i < 2; i++)
        {
            Func<Task> failA = () => pool.GetConnectionAsync("broker-a", 9092).AsTask();
            await Assert.That(failA).Throws<InvalidOperationException>();
        }

        Func<Task> failB = () => pool.GetConnectionAsync("broker-b", 9092).AsTask();
        await Assert.That(failB).Throws<InvalidOperationException>();
        Func<Task> failAAgain = () => pool.GetConnectionAsync("broker-a", 9092).AsTask();
        await Assert.That(failAAgain).Throws<InvalidOperationException>();

        await Assert.That(observedTimeouts).IsEquivalentTo(new[]
        {
            TimeSpan.FromMilliseconds(100),
            TimeSpan.FromMilliseconds(200),
            TimeSpan.FromMilliseconds(100),
            TimeSpan.FromMilliseconds(400)
        });
    }

    [Test]
    public async Task ConnectionSetupTimeout_FailureStateIsIsolatedByBrokerAtSharedEndpoint()
    {
        var observedTimeouts = new List<TimeSpan>();
        await using var pool = CreateConnectionSetupTimeoutPool(
            randomValue: 0.5,
            connectionFactory: (_, _, _, _, _) => throw new InvalidOperationException("broker down"),
            timeoutObserver: observedTimeouts.Add);
        pool.RegisterBroker(1, "shared-endpoint", 9092);
        pool.RegisterBroker(2, "shared-endpoint", 9092);

        for (var i = 0; i < 2; i++)
        {
            Func<Task> failBrokerOne = () => pool.GetConnectionAsync(1).AsTask();
            await Assert.That(failBrokerOne).Throws<InvalidOperationException>();
        }

        Func<Task> failBrokerTwo = () => pool.GetConnectionAsync(2).AsTask();
        await Assert.That(failBrokerTwo).Throws<InvalidOperationException>();
        Func<Task> failBrokerOneAgain = () => pool.GetConnectionAsync(1).AsTask();
        await Assert.That(failBrokerOneAgain).Throws<InvalidOperationException>();

        await Assert.That(observedTimeouts).IsEquivalentTo(new[]
        {
            TimeSpan.FromMilliseconds(100),
            TimeSpan.FromMilliseconds(200),
            TimeSpan.FromMilliseconds(100),
            TimeSpan.FromMilliseconds(400)
        });
    }

    [Test]
    [NotInParallel]
    public async Task ConnectionSetupTimeout_GroupBackoffLongerThanRoundStillAttempts()
    {
        const int connectionsPerBroker = 2;
        var attempts = 0;
        await using var pool = new ConnectionPool(
            clientId: "test-client",
            connectionOptions: new ConnectionOptions
            {
                ConnectionTimeout = TimeSpan.FromMilliseconds(20),
                ConnectionTimeoutMax = TimeSpan.FromMilliseconds(500),
                ReconnectBackoff = TimeSpan.FromMilliseconds(100),
                ReconnectBackoffMax = TimeSpan.FromMilliseconds(100)
            },
            connectionsPerBroker,
            connectionFactory: (brokerId, host, port, _, _) =>
            {
                if (Interlocked.Increment(ref attempts) <= connectionsPerBroker)
                    throw new InvalidOperationException("broker down");

                return ValueTask.FromResult(CreateConnectedConnection(brokerId, host, port));
            },
            randomDouble: static () => 0.5);
        pool.RegisterBroker(1, "broker-a", 9092);

        Func<Task> firstRound = () => pool.GetConnectionAsync(1).AsTask();
        await Assert.That(firstRound).Throws<InvalidOperationException>();

        var connection = await pool.GetConnectionAsync(1);

        await Assert.That(connection.IsConnected).IsTrue();
        await Assert.That(attempts).IsEqualTo(4);
    }

    [Test]
    [NotInParallel]
    public async Task ConnectionSetupTimeout_ScaleBackoffLongerThanRoundStillAttempts()
    {
        const int initialConnections = 2;
        var scaling = 0;
        var scaleAttempts = 0;
        await using var pool = new ConnectionPool(
            clientId: "test-client",
            connectionOptions: new ConnectionOptions
            {
                ConnectionTimeout = TimeSpan.FromMilliseconds(20),
                ConnectionTimeoutMax = TimeSpan.FromMilliseconds(500),
                ReconnectBackoff = TimeSpan.FromMilliseconds(100),
                ReconnectBackoffMax = TimeSpan.FromMilliseconds(100)
            },
            connectionsPerBroker: initialConnections,
            connectionFactory: (brokerId, host, port, _, _) =>
            {
                if (Volatile.Read(ref scaling) != 0
                    && Interlocked.Increment(ref scaleAttempts) <= initialConnections)
                {
                    throw new InvalidOperationException("broker down");
                }

                return ValueTask.FromResult(CreateConnectedConnection(brokerId, host, port));
            },
            randomDouble: static () => 0.5);
        pool.RegisterBroker(1, "broker-a", 9092);
        _ = await pool.GetConnectionAsync(1);
        Volatile.Write(ref scaling, 1);

        Func<Task> firstRound = () => pool.ScaleConnectionGroupAsync(1, newCount: 4).AsTask();
        await Assert.That(firstRound).Throws<InvalidOperationException>();

        var count = await pool.ScaleConnectionGroupAsync(1, newCount: 4);

        await Assert.That(count).IsEqualTo(4);
        await Assert.That(scaleAttempts).IsEqualTo(4);
    }

    [Test]
    public async Task ReconnectBackoff_FailureStateIsIsolatedByBrokerAtSharedEndpoint()
    {
        var observedBackoffs = new List<TimeSpan>();
        long timestamp = 0;
        await using var pool = new ConnectionPool(
            clientId: "test-client",
            connectionOptions: new ConnectionOptions
            {
                ConnectionTimeout = TimeSpan.FromSeconds(1),
                ReconnectBackoff = TimeSpan.FromMilliseconds(100),
                ReconnectBackoffMax = TimeSpan.FromMilliseconds(100)
            },
            connectionsPerBroker: 1,
            connectionFactory: static (_, _, _, _, _) => throw new InvalidOperationException("broker down"),
            randomDouble: static () => 0.5,
            reconnectBackoffDelay: (delay, _) =>
            {
                observedBackoffs.Add(delay);
                Interlocked.Add(ref timestamp, (long)(delay.TotalSeconds * Stopwatch.Frequency));
                return ValueTask.CompletedTask;
            },
            timestampProvider: () => Volatile.Read(ref timestamp));
        pool.RegisterBroker(1, "shared-endpoint", 9092);
        pool.RegisterBroker(2, "shared-endpoint", 9092);

        Func<Task> failBrokerOne = () => pool.GetConnectionAsync(1).AsTask();
        await Assert.That(failBrokerOne).Throws<InvalidOperationException>();
        Func<Task> failBrokerTwo = () => pool.GetConnectionAsync(2).AsTask();
        await Assert.That(failBrokerTwo).Throws<InvalidOperationException>();

        await Assert.That(observedBackoffs).IsEmpty();

        await Assert.That(failBrokerOne).Throws<InvalidOperationException>();
        await Assert.That(observedBackoffs).IsEquivalentTo([TimeSpan.FromMilliseconds(100)]);
    }

    [Test]
    public async Task ConnectionSetupTimeout_BrokerEndpointChangeDropsOldState()
    {
        var observedTimeouts = new List<TimeSpan>();
        await using var pool = CreateConnectionSetupTimeoutPool(
            randomValue: 0.5,
            connectionFactory: (_, _, _, _, _) => throw new InvalidOperationException("broker down"),
            timeoutObserver: observedTimeouts.Add);
        pool.RegisterBroker(1, "broker-a", 9092);

        for (var i = 0; i < 2; i++)
        {
            Func<Task> fail = () => pool.GetConnectionAsync(1).AsTask();
            await Assert.That(fail).Throws<InvalidOperationException>();
        }

        var oldReconnectGate = await AssertSingleReconnectBackoffGate(pool);
        pool.RegisterBroker(1, "broker-b", 9092);
        await Assert.That(() => oldReconnectGate.Wait(0)).Throws<ObjectDisposedException>();
        Func<Task> failAfterMove = () => pool.GetConnectionAsync(1).AsTask();
        await Assert.That(failAfterMove).Throws<InvalidOperationException>();

        await Assert.That(observedTimeouts).IsEquivalentTo(new[]
        {
            TimeSpan.FromMilliseconds(100),
            TimeSpan.FromMilliseconds(200),
            TimeSpan.FromMilliseconds(100)
        });
        await Assert.That(GetConnectionSetupTimeoutStateCount(pool)).IsEqualTo(1);
    }

    [Test]
    public async Task ConnectionSetupTimeout_CallerCancellationDoesNotAdvanceFailures()
    {
        var attempts = 0;
        var factoryEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var observedTimeouts = new List<TimeSpan>();
        await using var pool = CreateConnectionSetupTimeoutPool(
            randomValue: 0.5,
            connectionFactory: async (brokerId, host, port, _, cancellationToken) =>
            {
                var attempt = Interlocked.Increment(ref attempts);
                if (attempt == 1)
                {
                    factoryEntered.SetResult();
                    await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                }
                if (attempt == 2)
                    throw new InvalidOperationException("broker down");
                return CreateConnectedConnection(brokerId, host, port);
            },
            timeoutObserver: observedTimeouts.Add);
        pool.RegisterBroker(1, "broker-a", 9092);

        using var cancellation = new CancellationTokenSource();
        var canceledAttempt = pool.GetConnectionAsync(1, cancellation.Token).AsTask();
        await factoryEntered.Task;
        cancellation.Cancel();
        await Assert.That(async () => await canceledAttempt).Throws<OperationCanceledException>();
        Func<Task> failedAttempt = () => pool.GetConnectionAsync(1).AsTask();
        await Assert.That(failedAttempt).Throws<InvalidOperationException>();
        await pool.GetConnectionAsync(1);

        await Assert.That(observedTimeouts).IsEquivalentTo(new[]
        {
            TimeSpan.FromMilliseconds(100),
            TimeSpan.FromMilliseconds(100),
            TimeSpan.FromMilliseconds(200)
        });
    }

    [Test]
    public async Task ConnectionSetupTimeout_ConcurrentFailuresAdvanceAtomically()
    {
        var observedTimeouts = new List<TimeSpan>();
        var observerLock = new object();
        await using var pool = CreateConnectionSetupTimeoutPool(
            randomValue: 0.5,
            connectionFactory: (_, _, _, _, _) => throw new InvalidOperationException("broker down"),
            timeoutObserver: timeout =>
            {
                lock (observerLock)
                    observedTimeouts.Add(timeout);
            });

        var attempts = Enumerable.Range(0, 4)
            .Select(_ => pool.GetConnectionAsync("broker-a", 9092).AsTask())
            .ToArray();
        await Assert.That(async () => await Task.WhenAll(attempts)).Throws<InvalidOperationException>();

        await Assert.That(observedTimeouts).IsEquivalentTo(new[]
        {
            TimeSpan.FromMilliseconds(100),
            TimeSpan.FromMilliseconds(200),
            TimeSpan.FromMilliseconds(400),
            TimeSpan.FromMilliseconds(800)
        });
    }

    [Test]
    public async Task ConnectionSetupTimeout_DisconnectedGroupAdvancesOncePerRound()
    {
        var observedTimeouts = new ConcurrentQueue<TimeSpan>();
        await using var pool = CreateConnectionSetupTimeoutPool(
            randomValue: 0.5,
            connectionFactory: static (brokerId, host, port, _, _) =>
            {
                var connection = CreateConnectedConnection(brokerId, host, port);
                connection.IsConnected.Returns(false);
                return new ValueTask<IKafkaConnection>(connection);
            },
            timeoutObserver: observedTimeouts.Enqueue,
            connectionsPerBroker: 2);
        pool.RegisterBroker(1, "broker-a", 9092);

        for (var round = 0; round < 2; round++)
        {
            Func<Task> connect = () => pool.GetConnectionAsync(1).AsTask();
            var exception = await Assert.That(connect).Throws<KafkaException>();
            await Assert.That(exception!.ErrorCode).IsEqualTo(ErrorCode.NetworkException);
        }

        await Assert.That(observedTimeouts).IsEquivalentTo(new[]
        {
            TimeSpan.FromMilliseconds(100),
            TimeSpan.FromMilliseconds(100),
            TimeSpan.FromMilliseconds(200),
            TimeSpan.FromMilliseconds(200)
        });
    }

    [Test]
    [NotInParallel]
    public async Task ReplaceConnectionInGroup_DisconnectedReplacementPreservesSetupBackoff()
    {
        const int connectionsPerBroker = 2;
        var observedTimeouts = new ConcurrentQueue<TimeSpan>();
        var initialConnectionsRemaining = connectionsPerBroker;
        var replacementAttempts = 0;
        var options = new ConnectionOptions
        {
            ConnectionTimeout = TimeSpan.FromMilliseconds(100),
            ConnectionTimeoutMax = TimeSpan.FromMilliseconds(400),
            ReconnectBackoff = TimeSpan.Zero,
            ReconnectBackoffMax = TimeSpan.Zero
        };

        var pool = new ConnectionPool(
            clientId: "test-client",
            connectionOptions: options,
            connectionsPerBroker: connectionsPerBroker,
            connectionFactory: (brokerId, host, port, _, _) =>
            {
                var connection = CreateConnectedConnection(brokerId, host, port);
                if (Interlocked.Decrement(ref initialConnectionsRemaining) < 0
                    && Interlocked.Increment(ref replacementAttempts) == 1)
                {
                    connection.IsConnected.Returns(false);
                }

                return new ValueTask<IKafkaConnection>(connection);
            },
            randomDouble: static () => 0.5,
            connectionSetupTimeoutObserver: observedTimeouts.Enqueue);

        await using (pool)
        {
            pool.RegisterBroker(1, "localhost", 9092);

            await pool.GetConnectionAsync(1);
            var oldConnection = await pool.GetConnectionByIndexAsync(1, 0);
            oldConnection.IsConnected.Returns(false);

            Func<Task> failedReplacement = () => pool.GetConnectionByIndexAsync(1, 0).AsTask();
            var exception = await Assert.That(failedReplacement).Throws<KafkaException>();
            await Assert.That(exception!.ErrorCode).IsEqualTo(ErrorCode.NetworkException);

            var replacement = await pool.GetConnectionByIndexAsync(1, 0);
            await Assert.That(replacement.IsConnected).IsTrue();
        }

        await Assert.That(observedTimeouts).IsEquivalentTo(new[]
        {
            TimeSpan.FromMilliseconds(100),
            TimeSpan.FromMilliseconds(100),
            TimeSpan.FromMilliseconds(100),
            TimeSpan.FromMilliseconds(200)
        });
    }

    [Test]
    [NotInParallel]
    public async Task ReplaceConnectionInGroup_ReconnectBackoffRespectsOverallDeadline()
    {
        const int connectionsPerBroker = 2;
        var initialCreationDone = 0;
        var replacementAttempts = 0;
        var options = new ConnectionOptions
        {
            ConnectionTimeout = TimeSpan.FromMilliseconds(100),
            ReconnectBackoff = TimeSpan.FromMilliseconds(200),
            ReconnectBackoffMax = TimeSpan.FromMilliseconds(200)
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
            },
            randomDouble: static () => 0.5);

        await using (pool)
        {
            pool.RegisterBroker(1, "localhost", 9092);

            await pool.GetConnectionAsync(1);
            Volatile.Write(ref initialCreationDone, 1);

            var oldConnection = await pool.GetConnectionByIndexAsync(1, 0);
            oldConnection.IsConnected.Returns(false);

            Func<Task> failedReplacement = () => pool.GetConnectionByIndexAsync(1, 0).AsTask();
            await Assert.That(failedReplacement).Throws<InvalidOperationException>();

            Func<Task> replacement = () => pool.GetConnectionByIndexAsync(1, 0).AsTask();
            await Assert.That(replacement).Throws<KafkaException>()
                .WithMessageContaining("Connection operation timeout after 100ms");
            await Assert.That(replacementAttempts).IsEqualTo(1);
        }
    }

    [Test]
    [NotInParallel]
    public async Task ReplaceConnectionInGroup_ScaleLockWaitIsBoundedAndDisposesReplacement()
    {
        var created = new List<IKafkaConnection>();
        var options = new ConnectionOptions
        {
            ConnectionTimeout = TimeSpan.FromMilliseconds(20),
            ConnectionTimeoutMax = TimeSpan.FromMilliseconds(20),
            ReconnectBackoff = TimeSpan.Zero,
            ReconnectBackoffMax = TimeSpan.Zero
        };
        var pool = new ConnectionPool(
            clientId: "test-client",
            connectionOptions: options,
            connectionsPerBroker: 2,
            connectionFactory: (brokerId, host, port, _, _) =>
            {
                var connection = CreateConnectedConnection(brokerId, host, port);
                created.Add(connection);
                return new ValueTask<IKafkaConnection>(connection);
            });

        await using (pool)
        {
            pool.RegisterBroker(1, "localhost", 9092);
            await pool.GetConnectionAsync(1);
            await Assert.That(created).Count().IsEqualTo(2);
            created[0].IsConnected.Returns(false);
            await Assert.That(created[0].IsConnected).IsFalse();

            var scaleLock = (SemaphoreSlim)typeof(ConnectionPool).GetField(
                "_scaleLock",
                BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(pool)!;
            await scaleLock.WaitAsync();
            await Assert.That(scaleLock.CurrentCount).IsEqualTo(0);
            try
            {
                Func<Task> replaceConnection = () => pool.GetConnectionByIndexAsync(1, 0).AsTask();
                var exception = await Assert.That(replaceConnection).Throws<KafkaException>();

                await Assert.That(exception!.ErrorCode).IsEqualTo(ErrorCode.RequestTimedOut);
            }
            finally
            {
                scaleLock.Release();
            }

            await Assert.That(created).Count().IsEqualTo(3);
            await created[2].Received(1).DisposeAsync();
        }
    }

    [Test]
    [NotInParallel]
    public async Task GetConnectionAsync_AfterReconnectBackoffReset_DisposesAttemptGate()
    {
        var attempts = 0;
        var options = new ConnectionOptions
        {
            ReconnectBackoff = TimeSpan.FromMilliseconds(1),
            ReconnectBackoffMax = TimeSpan.FromMilliseconds(1)
        };
        var pool = new ConnectionPool(
            clientId: "test-client",
            connectionOptions: options,
            connectionsPerBroker: 2,
            connectionFactory: (brokerId, host, port, _, _) =>
            {
                if (Interlocked.Increment(ref attempts) <= 2)
                    return new ValueTask<IKafkaConnection>(Task.FromException<IKafkaConnection>(
                        new InvalidOperationException("broker down")));

                return new ValueTask<IKafkaConnection>(CreateConnectedConnection(brokerId, host, port));
            });

        await using (pool)
        {
            pool.RegisterBroker(1, "localhost", 9092);

            Func<Task> firstAttempt = () => pool.GetConnectionAsync(1).AsTask();
            await Assert.That(firstAttempt).Throws<InvalidOperationException>();
            var gate = await AssertSingleReconnectBackoffGate(pool);

            var connection = await pool.GetConnectionAsync(1);

            await Assert.That(connection.IsConnected).IsTrue();
            await Assert.That(() => gate.Wait(0)).Throws<ObjectDisposedException>();
        }
    }

    [Test]
    [NotInParallel]
    public async Task CloseAllAsync_DisposesReconnectBackoffAttemptGates()
    {
        var pool = new ConnectionPool(
            clientId: "test-client",
            connectionOptions: new ConnectionOptions(),
            connectionsPerBroker: 2,
            connectionFactory: (_, _, _, _, _) => new ValueTask<IKafkaConnection>(Task.FromException<IKafkaConnection>(
                new InvalidOperationException("broker down"))));

        await using (pool)
        {
            pool.RegisterBroker(1, "localhost", 9092);

            Func<Task> failedConnection = () => pool.GetConnectionAsync(1).AsTask();
            await Assert.That(failedConnection).Throws<InvalidOperationException>();
            var gate = await AssertSingleReconnectBackoffGate(pool);

            await pool.CloseAllAsync();

            await Assert.That(() => gate.Wait(0)).Throws<ObjectDisposedException>();
        }
    }

    [Test]
    public async Task CloseAllAsync_ClearsConnectionSetupFailureState()
    {
        var pool = CreateConnectionSetupTimeoutPool(
            randomValue: 0.5,
            connectionFactory: (_, _, _, _, _) => throw new InvalidOperationException("broker down"));
        await using (pool)
        {
            Func<Task> failedConnection = () => pool.GetConnectionAsync("broker-a", 9092).AsTask();
            await Assert.That(failedConnection).Throws<InvalidOperationException>();
            await Assert.That(GetConnectionSetupTimeoutStateCount(pool)).IsEqualTo(1);

            await pool.CloseAllAsync();

            await Assert.That(GetConnectionSetupTimeoutStateCount(pool)).IsEqualTo(0);
        }
    }

    private static OAuthBearerConfig CreateOAuthBearerConfig() => new()
    {
        TokenEndpointUrl = "https://auth.example.invalid/token",
        ClientId = "client",
        ClientSecret = "secret"
    };

    private static ConnectionPool CreateReconnectBackoffPool(
        int reconnectBackoffMs,
        int reconnectBackoffMaxMs,
        double randomValue)
        => new(
            clientId: "test-client",
            connectionOptions: new ConnectionOptions
            {
                ReconnectBackoff = TimeSpan.FromMilliseconds(reconnectBackoffMs),
                ReconnectBackoffMax = TimeSpan.FromMilliseconds(reconnectBackoffMaxMs)
            },
            connectionsPerBroker: 1,
            connectionFactory: (_, _, _, _, _) => throw new InvalidOperationException("Connection not expected"),
            randomDouble: () => randomValue);

    private static ConnectionPool CreateConnectionSetupTimeoutPool(
        double randomValue,
        Func<int, string, int, int, CancellationToken, ValueTask<IKafkaConnection>>? connectionFactory = null,
        Action<TimeSpan>? timeoutObserver = null,
        int connectionsPerBroker = 1)
        => new(
            clientId: "test-client",
            connectionOptions: new ConnectionOptions
            {
                ConnectionTimeout = TimeSpan.FromMilliseconds(100),
                ConnectionTimeoutMax = TimeSpan.FromMilliseconds(1000),
                ReconnectBackoff = TimeSpan.Zero,
                ReconnectBackoffMax = TimeSpan.Zero
            },
            connectionsPerBroker,
            connectionFactory: connectionFactory
                ?? ((_, _, _, _, _) => throw new InvalidOperationException("Connection not expected")),
            randomDouble: () => randomValue,
            connectionSetupTimeoutObserver: timeoutObserver);

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

    private static async Task<SemaphoreSlim> AssertSingleReconnectBackoffGate(ConnectionPool pool)
    {
        var field = typeof(ConnectionPool).GetField(
            "_reconnectBackoffs",
            BindingFlags.NonPublic | BindingFlags.Instance);
        var backoffs = field!.GetValue(pool)!;
        var values = (System.Collections.IEnumerable)backoffs.GetType().GetProperty("Values")!.GetValue(backoffs)!;
        var gates = new List<SemaphoreSlim>();

        foreach (var state in values)
        {
            var gateField = state.GetType().GetField("AttemptGate")!;
            gates.Add((SemaphoreSlim)gateField.GetValue(state)!);
        }

        await Assert.That(gates).Count().IsEqualTo(1);
        return gates[0];
    }

    private static int GetConnectionSetupTimeoutStateCount(ConnectionPool pool)
    {
        var field = typeof(ConnectionPool).GetField(
            "_connectionSetupTimeouts",
            BindingFlags.NonPublic | BindingFlags.Instance);
        var states = field!.GetValue(pool)!;
        return (int)states.GetType().GetProperty("Count")!.GetValue(states)!;
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

    internal sealed class TestIdleConnection(int brokerId, string host, int port) :
        IKafkaConnection,
        IIdleTrackedKafkaConnection,
        IRetirableKafkaConnection
    {
        private int _connected = 1;
        private long _lastUsedTimestampMs = Environment.TickCount64;
        private int _pendingRequestCount;
        private int _disposeCount;
        private int _leaseCount;
        private int _retirementState;

        public int BrokerId { get; } = brokerId;
        public string Host { get; } = host;
        public int Port { get; } = port;
        public bool IsConnected => Volatile.Read(ref _connected) != 0;
        public int DisposeCount => Volatile.Read(ref _disposeCount);
        public int RetirementState => Volatile.Read(ref _retirementState);

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

        public Func<int>? PendingRequestCountProvider { get; set; }

        long IIdleTrackedKafkaConnection.LastUsedTimestampMs => LastUsedTimestampMs;

        int IIdleTrackedKafkaConnection.PendingRequestCount =>
            PendingRequestCountProvider?.Invoke() ?? PendingRequestCount;

        void IIdleTrackedKafkaConnection.Touch() => LastUsedTimestampMs = Environment.TickCount64;

        int IRetirableKafkaConnection.LeaseCount => Volatile.Read(ref _leaseCount);

        int IRetirableKafkaConnection.ActiveOperationCount => 0;

        bool IRetirableKafkaConnection.TryAcquireLease()
        {
            if (Volatile.Read(ref _retirementState) != 0)
                return false;

            Interlocked.Increment(ref _leaseCount);
            if (Volatile.Read(ref _retirementState) == 0)
                return true;

            Interlocked.Decrement(ref _leaseCount);
            return false;
        }

        void IRetirableKafkaConnection.ReleaseLease() => Interlocked.Decrement(ref _leaseCount);

        void IRetirableKafkaConnection.BeginRetirement() => Volatile.Write(ref _retirementState, 1);

        void IRetirableKafkaConnection.CompleteRetirement() => Volatile.Write(ref _retirementState, 2);

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
