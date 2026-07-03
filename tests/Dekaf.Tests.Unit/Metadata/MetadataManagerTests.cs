using System.Reflection;
using Dekaf.Errors;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using NSubstitute;

namespace Dekaf.Tests.Unit.Metadata;

/// <summary>
/// Unit tests for MetadataManager, focusing on endpoint caching behavior.
/// These tests verify that the endpoint cache is properly invalidated when broker
/// topology changes, including both count changes and membership changes.
/// </summary>
public class MetadataManagerTests
{
    /// <summary>
    /// Creates a minimal MetadataManager for testing.
    /// Uses a null connection pool since we're only testing the caching logic.
    /// </summary>
    private static MetadataManager CreateTestManager()
    {
        // MetadataManager requires a connection pool, but we won't call methods that use it
        // We're only testing GetEndpointsToTry which uses ClusterMetadata
        return new MetadataManager(
            connectionPool: null!,
            bootstrapServers: ["localhost:9092"]);
    }

    /// <summary>
    /// Helper to create a test MetadataResponse with the specified brokers.
    /// </summary>
    private static MetadataResponse CreateMetadataResponse(params (int nodeId, string host, int port)[] brokers)
    {
        var brokerList = new List<BrokerMetadata>();
        foreach (var (nodeId, host, port) in brokers)
        {
            brokerList.Add(new BrokerMetadata
            {
                NodeId = nodeId,
                Host = host,
                Port = port
            });
        }

        return new MetadataResponse
        {
            Brokers = brokerList,
            Topics = Array.Empty<TopicMetadata>()
        };
    }

    [Test]
    public async Task GetEndpointsToTry_InitialState_ReturnsBootstrapServers()
    {
        var manager = CreateTestManager();

        var endpoints = manager.GetEndpointsToTry();

        await Assert.That(endpoints.Count).IsEqualTo(1);
        await Assert.That(endpoints[0]).IsEquivalentTo(("localhost", 9092));
    }

    [Test]
    public async Task GetEndpointsToTry_AfterMetadataUpdate_ReturnsBrokersAndBootstrap()
    {
        var manager = CreateTestManager();

        // Update with broker metadata
        var response = CreateMetadataResponse(
            (1, "broker1", 9092),
            (2, "broker2", 9092),
            (3, "broker3", 9092));
        manager.Metadata.Update(response);

        var endpoints = manager.GetEndpointsToTry();

        // Should return 3 brokers + 1 bootstrap server = 4 total
        await Assert.That(endpoints.Count).IsEqualTo(4);

        // First 3 should be known brokers
        await Assert.That(endpoints[0]).IsEquivalentTo(("broker1", 9092));
        await Assert.That(endpoints[1]).IsEquivalentTo(("broker2", 9092));
        await Assert.That(endpoints[2]).IsEquivalentTo(("broker3", 9092));

        // Last should be bootstrap server
        await Assert.That(endpoints[3]).IsEquivalentTo(("localhost", 9092));
    }

    [Test]
    public async Task GetEndpointsToTry_CacheHit_ReturnsSameCachedInstance()
    {
        var manager = CreateTestManager();

        // Update with broker metadata
        var response = CreateMetadataResponse(
            (1, "broker1", 9092),
            (2, "broker2", 9092));
        manager.Metadata.Update(response);

        // First call builds cache
        var endpoints1 = manager.GetEndpointsToTry();

        // Second call should return the same cached list (zero allocation)
        var endpoints2 = manager.GetEndpointsToTry();

        // Verify endpoints are equal
        await Assert.That(endpoints1.Count).IsEqualTo(endpoints2.Count);
        for (int i = 0; i < endpoints1.Count; i++)
        {
            await Assert.That(endpoints1[i]).IsEquivalentTo(endpoints2[i]);
        }

        // IReadOnlyList return type prevents modification, so same instance is safe
        await Assert.That(ReferenceEquals(endpoints1, endpoints2)).IsTrue();
    }

    [Test]
    public async Task GetEndpointsToTry_BrokerCountChanges_InvalidatesCache()
    {
        var manager = CreateTestManager();

        // Initial: 2 brokers
        var response1 = CreateMetadataResponse(
            (1, "broker1", 9092),
            (2, "broker2", 9092));
        manager.Metadata.Update(response1);
        var endpoints1 = manager.GetEndpointsToTry();

        // Update: 3 brokers (count increased)
        var response2 = CreateMetadataResponse(
            (1, "broker1", 9092),
            (2, "broker2", 9092),
            (3, "broker3", 9092));
        manager.Metadata.Update(response2);
        var endpoints2 = manager.GetEndpointsToTry();

        // Verify cache was invalidated and new broker appears
        await Assert.That(endpoints1.Count).IsEqualTo(3); // 2 brokers + 1 bootstrap
        await Assert.That(endpoints2.Count).IsEqualTo(4); // 3 brokers + 1 bootstrap

        // Verify new broker is in the list
        await Assert.That(endpoints2.Any(e => e.Host == "broker3" && e.Port == 9092)).IsTrue();
    }

    [Test]
    public async Task GetEndpointsToTry_BrokerMembershipChanges_InvalidatesCache()
    {
        var manager = CreateTestManager();

        // Initial: brokers 1, 2, 3
        var response1 = CreateMetadataResponse(
            (1, "broker1", 9092),
            (2, "broker2", 9092),
            (3, "broker3", 9092));
        manager.Metadata.Update(response1);
        var endpoints1 = manager.GetEndpointsToTry();

        // Update: broker 3 replaced by broker 4 (same count, different membership)
        // This is the critical scenario fixed by PR #86
        var response2 = CreateMetadataResponse(
            (1, "broker1", 9092),
            (2, "broker2", 9092),
            (4, "broker4", 9092));  // broker3 replaced by broker4
        manager.Metadata.Update(response2);
        var endpoints2 = manager.GetEndpointsToTry();

        // Count should be the same
        await Assert.That(endpoints1.Count).IsEqualTo(endpoints2.Count);

        // But broker3 should be gone and broker4 should be present
        await Assert.That(endpoints1.Any(e => e.Host == "broker3")).IsTrue();
        await Assert.That(endpoints1.Any(e => e.Host == "broker4")).IsFalse();

        await Assert.That(endpoints2.Any(e => e.Host == "broker3")).IsFalse();
        await Assert.That(endpoints2.Any(e => e.Host == "broker4")).IsTrue();
    }

    [Test]
    public async Task GetEndpointsToTry_BrokerHostOrPortChanges_InvalidatesCache()
    {
        var manager = CreateTestManager();

        // Initial: brokers on port 9092
        var response1 = CreateMetadataResponse(
            (1, "broker1", 9092),
            (2, "broker2", 9092));
        manager.Metadata.Update(response1);
        var endpoints1 = manager.GetEndpointsToTry();

        // Update: broker2's port changed (same node ID, different port)
        var response2 = CreateMetadataResponse(
            (1, "broker1", 9092),
            (2, "broker2", 9093));  // port changed
        manager.Metadata.Update(response2);
        var endpoints2 = manager.GetEndpointsToTry();

        // Verify endpoints changed
        var broker2Port1 = endpoints1.FirstOrDefault(e => e.Host == "broker2").Port;
        var broker2Port2 = endpoints2.FirstOrDefault(e => e.Host == "broker2").Port;

        await Assert.That(broker2Port1).IsEqualTo(9092);
        await Assert.That(broker2Port2).IsEqualTo(9093);
    }

    [Test]
    public async Task GetEndpointsToTry_MultipleCalls_ReturnsCachedReadOnlyList()
    {
        var manager = CreateTestManager();

        var response = CreateMetadataResponse((1, "broker1", 9092));
        manager.Metadata.Update(response);

        var endpoints1 = manager.GetEndpointsToTry();
        var endpoints2 = manager.GetEndpointsToTry();

        // Both calls should return the same cached content (IReadOnlyList prevents modification)
        await Assert.That(endpoints1.Count).IsEqualTo(endpoints2.Count);
        await Assert.That(endpoints1[0]).IsEqualTo(endpoints2[0]);
    }

    [Test]
    public async Task GetEndpointsToTry_ThreadSafety_ConcurrentCallsDontCrash()
    {
        var manager = CreateTestManager();

        var response = CreateMetadataResponse(
            (1, "broker1", 9092),
            (2, "broker2", 9092));
        manager.Metadata.Update(response);

        // Call GetEndpointsToTry concurrently from multiple threads
        var tasks = new List<Task>();
        for (int i = 0; i < 10; i++)
        {
            tasks.Add(Task.Run(() =>
            {
                for (int j = 0; j < 100; j++)
                {
                    var _ = manager.GetEndpointsToTry();
                }
            }));
        }

        await Task.WhenAll(tasks);

        // If we get here without exceptions, thread-safety is working
        // No assertion needed - successful completion proves thread-safety
    }

    [Test]
    public async Task InitializeAsync_ConcurrentCalls_StartsOnce()
    {
        var pool = Substitute.For<IConnectionPool>();
        var connection = Substitute.For<IKafkaConnection>();
        var metadataRequests = 0;

        pool.GetConnectionAsync("localhost", 9092, Arg.Any<CancellationToken>())
            .Returns(new ValueTask<IKafkaConnection>(connection));

        connection.SendAsync<ApiVersionsRequest, ApiVersionsResponse>(
                Arg.Any<ApiVersionsRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(new ValueTask<ApiVersionsResponse>(new ApiVersionsResponse
            {
                ErrorCode = ErrorCode.None,
                ApiKeys =
                [
                    new ApiVersion(
                        ApiKey.Metadata,
                        MetadataRequest.LowestSupportedVersion,
                        MetadataRequest.HighestSupportedVersion)
                ]
            }));

        connection.SendAsync<MetadataRequest, MetadataResponse>(
                Arg.Any<MetadataRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                Interlocked.Increment(ref metadataRequests);
                return new ValueTask<MetadataResponse>(CreateMetadataResponse((1, "localhost", 9092)));
            });

        await using var manager = new MetadataManager(
            pool,
            ["localhost:9092"],
            new MetadataOptions { MetadataRefreshInterval = TimeSpan.FromHours(1) });

        await Task.WhenAll(
            manager.InitializeAsync().AsTask(),
            manager.InitializeAsync().AsTask());

        await Assert.That(metadataRequests).IsEqualTo(1);
    }

    [Test]
    public async Task InitializeAsync_DisposeDuringRefresh_DoesNotStartBackgroundRefresh()
    {
        var pool = Substitute.For<IConnectionPool>();
        var connection = Substitute.For<IKafkaConnection>();
        var refreshStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseRefresh = new TaskCompletionSource<MetadataResponse>(TaskCreationOptions.RunContinuationsAsynchronously);

        pool.GetConnectionAsync("localhost", 9092, Arg.Any<CancellationToken>())
            .Returns(new ValueTask<IKafkaConnection>(connection));

        connection.SendAsync<MetadataRequest, MetadataResponse>(
                Arg.Any<MetadataRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                refreshStarted.TrySetResult();
                return new ValueTask<MetadataResponse>(releaseRefresh.Task);
            });

        var manager = new MetadataManager(
            pool,
            ["localhost:9092"],
            new MetadataOptions
            {
                EnableBackgroundRefresh = true,
                MetadataRefreshInterval = TimeSpan.FromHours(1)
            });
        SetMetadataApiVersion(manager);

        var initializeTask = manager.InitializeAsync().AsTask();
        await refreshStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var disposeTask = manager.DisposeAsync().AsTask();
        releaseRefresh.SetResult(CreateMetadataResponse((1, "broker1", 9092)));

        var act = () => initializeTask.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.That(act).Throws<ObjectDisposedException>();

        await disposeTask.WaitAsync(TimeSpan.FromSeconds(5));
        object? backgroundRefreshTask = GetInstanceField<Task?>(manager, "_backgroundRefreshTask");
        await Assert.That(backgroundRefreshTask).IsNull();
    }

    [Test]
    [NotInParallel]
    public async Task DisposeAsync_CancelsBackgroundRefreshCtsCreatedWhileInitializationDrains()
    {
        var timeout = TimeSpan.FromSeconds(15);
        var manager = new MetadataManager(
            Substitute.For<IConnectionPool>(),
            ["localhost:9092"],
            new MetadataOptions { MetadataRefreshInterval = TimeSpan.FromHours(1) });
        var initializeLock = GetInstanceField<SemaphoreSlim>(manager, "_initializeLock");
        await initializeLock.WaitAsync();
        var firstCancelObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var firstCts = new CancellationTokenSource();
        using var registration = firstCts.Token.Register(
            static state => ((TaskCompletionSource)state!).TrySetResult(),
            firstCancelObserved);
        SetInstanceField(manager, "_backgroundRefreshCts", firstCts);

        var disposeTask = manager.DisposeAsync().AsTask();
        await firstCancelObserved.Task.WaitAsync(timeout);
        await Assert.That(disposeTask.IsCompleted).IsFalse();

        var racedCts = new CancellationTokenSource();
        SetInstanceField(manager, "_backgroundRefreshCts", racedCts);
        initializeLock.Release();

        await disposeTask.WaitAsync(timeout);

        await Assert.That(racedCts.IsCancellationRequested).IsTrue();
    }

    [Test]
    public async Task RefreshMetadataAsync_ObjectDisposedConnection_TriesNextEndpoint()
    {
        var pool = Substitute.For<IConnectionPool>();
        var connection = Substitute.For<IKafkaConnection>();
        var firstEndpointAttempts = 0;
        var secondEndpointAttempts = 0;

        pool.GetConnectionAsync("broker1", 9092, Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                Interlocked.Increment(ref firstEndpointAttempts);
                return ValueTask.FromException<IKafkaConnection>(new ObjectDisposedException("KafkaConnection"));
            });

        pool.GetConnectionAsync("broker2", 9093, Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                Interlocked.Increment(ref secondEndpointAttempts);
                return new ValueTask<IKafkaConnection>(connection);
            });

        connection.SendAsync<MetadataRequest, MetadataResponse>(
                Arg.Any<MetadataRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(new ValueTask<MetadataResponse>(CreateMetadataResponse((2, "broker2", 9093))));

        await using var manager = new MetadataManager(
            pool,
            ["broker1:9092", "broker2:9093"],
            new MetadataOptions { EnableBackgroundRefresh = false });
        SetMetadataApiVersion(manager);

        await manager.RefreshMetadataAsync();

        await Assert.That(firstEndpointAttempts).IsEqualTo(1);
        await Assert.That(secondEndpointAttempts).IsEqualTo(1);
    }

    [Test]
    public async Task BackgroundRefreshLoop_ObjectDisposedConnection_DoesNotResetInitialized()
    {
        var pool = Substitute.For<IConnectionPool>();
        using var cts = new CancellationTokenSource();

        pool.GetConnectionAsync("localhost", 9092, Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                cts.Cancel();
                return ValueTask.FromException<IKafkaConnection>(new ObjectDisposedException("KafkaConnection"));
            });

        var manager = new MetadataManager(
            pool,
            ["localhost:9092"],
            new MetadataOptions { MetadataRefreshInterval = TimeSpan.Zero });
        SetMetadataApiVersion(manager);
        SetInstanceField(manager, "_initialized", true);

        var loop = InvokeBackgroundRefreshLoop(manager, cts.Token);
        await loop.WaitAsync(TimeSpan.FromSeconds(5));

        await Assert.That(GetInstanceField<bool>(manager, "_initialized")).IsTrue();
        await manager.DisposeAsync();
    }

    [Test]
    public async Task ObjectDisposedException_IsFatalMetadataErrorOnlyWhenManagerDisposed()
    {
        var manager = CreateTestManager();
        await Assert.That(IsFatalMetadataError(manager, new ObjectDisposedException("KafkaConnection"))).IsFalse();

        await manager.DisposeAsync();

        await Assert.That(IsFatalMetadataError(manager, new ObjectDisposedException(nameof(MetadataManager)))).IsTrue();
    }

    [Test]
    public async Task BackgroundRefreshLoop_FatalError_ResetsInitialized()
    {
        var pool = Substitute.For<IConnectionPool>();
        pool.GetConnectionAsync("localhost", 9092, Arg.Any<CancellationToken>())
            .Returns(_ => ValueTask.FromException<IKafkaConnection>(new AuthenticationException("auth failed")));

        var manager = new MetadataManager(
            pool,
            ["localhost:9092"],
            new MetadataOptions { MetadataRefreshInterval = TimeSpan.Zero });
        SetInstanceField(manager, "_initialized", true);

        var loop = InvokeBackgroundRefreshLoop(manager, CancellationToken.None);
        await loop.WaitAsync(TimeSpan.FromSeconds(5));

        await Assert.That(GetInstanceField<bool>(manager, "_initialized")).IsFalse();
        await manager.DisposeAsync();
    }

    [Test]
    public async Task StartBackgroundRefresh_CompletedTask_AllowsRestart()
    {
        var manager = new MetadataManager(
            Substitute.For<IConnectionPool>(),
            ["localhost:9092"],
            new MetadataOptions { MetadataRefreshInterval = TimeSpan.FromHours(1) });
        SetInstanceField<Task?>(manager, "_backgroundRefreshTask", Task.CompletedTask);

        InvokeStartBackgroundRefresh(manager);

        var backgroundTask = GetInstanceField<Task?>(manager, "_backgroundRefreshTask");
        await Assert.That(backgroundTask).IsNotNull();
        await Assert.That(backgroundTask!.IsCompleted).IsFalse();
        await manager.DisposeAsync();
    }

    private static void SetMetadataApiVersion(MetadataManager metadataManager)
    {
        var field = typeof(MetadataManager)
            .GetField("_metadataApiVersion", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("_metadataApiVersion field not found");

        field.SetValue(metadataManager, MetadataRequest.HighestSupportedVersion);
    }

    private static bool IsFatalMetadataError(MetadataManager metadataManager, Exception exception)
    {
        var method = typeof(MetadataManager)
            .GetMethod("IsFatalMetadataError", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("IsFatalMetadataError method not found");

        return (bool)method.Invoke(metadataManager, [exception])!;
    }

    private static Task InvokeBackgroundRefreshLoop(MetadataManager metadataManager, CancellationToken cancellationToken)
    {
        var method = typeof(MetadataManager)
            .GetMethod("BackgroundRefreshLoopAsync", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("BackgroundRefreshLoopAsync method not found");

        return (Task)method.Invoke(metadataManager, [cancellationToken])!;
    }

    private static void InvokeStartBackgroundRefresh(MetadataManager metadataManager)
    {
        var method = typeof(MetadataManager)
            .GetMethod("StartBackgroundRefresh", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("StartBackgroundRefresh method not found");

        method.Invoke(metadataManager, null);
    }

    private static T GetInstanceField<T>(object instance, string name)
    {
        var field = instance.GetType()
            .GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException($"{name} field not found");

        return (T)field.GetValue(instance)!;
    }

    private static void SetInstanceField<T>(object instance, string name, T value)
    {
        var field = instance.GetType()
            .GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException($"{name} field not found");

        field.SetValue(instance, value);
    }
}
