using System.Collections.Concurrent;
using System.Reflection;
using Dekaf.Consumer;
using Dekaf.Internal;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.Serialization;
using NSubstitute;

namespace Dekaf.Tests.Unit.Consumer;

public sealed class ConsumerConnectionOwnershipTests
{
    [Test]
    public async Task ScaleUp_DrainsOldRoutingBeforeApplyingNewWidth()
    {
        var pool = CreatePool();
        var metadataManager = CreateMetadataManager(pool);
        await using var consumer = CreateStandaloneConsumer(pool, metadataManager);
        var scheduler = GetField<BrokerPrefetchScheduler>(consumer, "_brokerPrefetchScheduler");
        var oldFetchCanComplete = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        scheduler.TryStart((BrokerId: 1, ConnectionIndex: 0), async () =>
        {
            await oldFetchCanComplete.Task.ConfigureAwait(false);
        });

        var scaler = GetField<ConsumerConnectionScaler>(consumer, "_connectionScaler");
        TriggerScaleUp(scaler);

        await Assert.That(GetField<int>(consumer, "_appliedConnectionCount")).IsEqualTo(2);
        _ = pool.DidNotReceive().ScaleConnectionGroupAsync(
            Arg.Any<int>(),
            Arg.Any<int>(),
            Arg.Any<CancellationToken>());

        oldFetchCanComplete.SetResult();
        await TestWait.UntilAsync(
            () => GetField<int>(consumer, "_appliedConnectionCount") == 3,
            TimeSpan.FromSeconds(5));
        _ = pool.Received(1).ScaleConnectionGroupAsync(1, 3, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task FailedScaleUp_RollsBackTargetAndRetries()
    {
        var pool = CreatePool();
        pool.ScaleConnectionGroupAsync(1, 3, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(3));
        pool.ScaleConnectionGroupAsync(2, 3, Arg.Any<CancellationToken>())
            .Returns(
                ValueTask.FromException<int>(new IOException("scale failed")),
                ValueTask.FromResult(3));
        await using var consumer = CreateStandaloneConsumer(
            pool,
            CreateMetadataManager(pool, includeSecondBroker: true));
        var scaler = GetField<ConsumerConnectionScaler>(consumer, "_connectionScaler");

        TriggerScaleUp(scaler);
        await TestWait.UntilAsync(
            () => scaler.CurrentConnectionCount == 2,
            TimeSpan.FromSeconds(5));
        await Assert.That(GetField<int>(consumer, "_appliedConnectionCount")).IsEqualTo(2);

        TriggerScaleUp(scaler);
        await TestWait.UntilAsync(
            () => GetField<int>(consumer, "_appliedConnectionCount") == 3,
            TimeSpan.FromSeconds(5));
        _ = pool.Received(2).ScaleConnectionGroupAsync(1, 3, Arg.Any<CancellationToken>());
        _ = pool.Received(2).ScaleConnectionGroupAsync(2, 3, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ScaleDown_DrainsOldRoutingBeforeApplyingNewWidth()
    {
        var pool = CreatePool();
        var shrinkStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var shrinkCanComplete = new TaskCompletionSource<IKafkaConnection?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        pool.ShrinkConnectionGroupAsync(1, 2, Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                shrinkStarted.TrySetResult();
                return new ValueTask<IKafkaConnection?>(shrinkCanComplete.Task);
            });
        await using var consumer = CreateStandaloneConsumer(pool, CreateMetadataManager(pool));
        var scheduler = GetField<BrokerPrefetchScheduler>(consumer, "_brokerPrefetchScheduler");
        var oldFetchCanComplete = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        scheduler.TryStart((BrokerId: 1, ConnectionIndex: 0), async () =>
        {
            await oldFetchCanComplete.Task.ConfigureAwait(false);
        });
        SetField(consumer, "_appliedConnectionCount", 3);

        try
        {
            TriggerScaleDown(consumer);

            await Assert.That(GetField<int>(consumer, "_appliedConnectionCount")).IsEqualTo(3);
            _ = pool.DidNotReceive().ShrinkConnectionGroupAsync(
                Arg.Any<int>(),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>());

            oldFetchCanComplete.SetResult();
            await TestWait.UntilAsync(
                () => GetField<int>(consumer, "_appliedConnectionCount") == 2,
                TimeSpan.FromSeconds(5));
            await shrinkStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            _ = pool.Received(1).ShrinkConnectionGroupAsync(1, 2, Arg.Any<CancellationToken>());
        }
        finally
        {
            oldFetchCanComplete.TrySetResult();
            shrinkCanComplete.TrySetResult(null);
        }
    }

    [Test]
    public async Task SharedConsumer_ScaleDown_DoesNotShrinkSiblingConnection()
    {
        var pool = Substitute.For<IConnectionPool>();
        var siblingConnection = new TrackedConnection(hasPendingRequest: true);
        pool.ShrinkConnectionGroupAsync(1, 2, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<IKafkaConnection?>(siblingConnection));
        var metadataManager = CreateMetadataManager(pool);
        var consumer = CreateSharedConsumer(pool, metadataManager);

        try
        {
            TriggerScaleDown(consumer);

            _ = pool.DidNotReceive().ShrinkConnectionGroupAsync(
                Arg.Any<int>(),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>());

            siblingConnection.CompleteRequest();
            await siblingConnection.ActiveRequest;
            await Assert.That(siblingConnection.DisposeCount).IsEqualTo(0);
        }
        finally
        {
            await consumer.DisposeAsync();
            await metadataManager.DisposeAsync();
        }
    }

    [Test]
    [Arguments(false, 1, true)]
    [Arguments(true, 1, true)]
    [Arguments(false, 2, true)]
    [Arguments(true, 2, false)]
    public async Task Consumer_ScaleDown_ClosesOnlyReachableStaleFetchSession(
        bool ownsInfrastructure,
        int sessionConnectionIndex,
        bool expectsCloseRequest)
    {
        var pool = CreatePool();
        var closeRequest = new TaskCompletionSource<(int SessionId, int SessionEpoch)>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var connection = new TrackedConnection(
            hasPendingRequest: false,
            onFetchRequest: request => closeRequest.TrySetResult((request.SessionId, request.SessionEpoch)));
        pool.GetConnectionByIndexAsync(1, sessionConnectionIndex, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<IKafkaConnection>(connection));

        var metadataManager = CreateMetadataManager(pool);
        var consumer = ownsInfrastructure
            ? CreateStandaloneConsumer(pool, metadataManager)
            : CreateSharedConsumer(pool, metadataManager);

        try
        {
            var fetchSessions = GetField<ConcurrentDictionary<
                (int BrokerId, int ConnectionIndex),
                FetchSessionHandler>>(consumer, "_fetchSessions");
            var handler = new FetchSessionHandler();
            _ = handler.Build([], clusterMetadata: null);
            _ = handler.HandleResponse(new FetchResponse
            {
                ErrorCode = ErrorCode.None,
                SessionId = 42
            });
            fetchSessions[(1, sessionConnectionIndex)] = handler;

            var dispatchMethod = typeof(KafkaConsumer<string, string>).GetMethod(
                "DispatchReadyBrokerPrefetchesAsync",
                BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("DispatchReadyBrokerPrefetchesAsync method not found");
            var dispatch = (ValueTask<(int Started, int TargetCount)>)dispatchMethod.Invoke(
                consumer,
                [CancellationToken.None])!;

            var result = await dispatch;
            if (expectsCloseRequest)
            {
                var close = await closeRequest.Task.WaitAsync(TimeSpan.FromSeconds(1));
                await Assert.That(result.Started).IsEqualTo(1);
                await Assert.That(close.SessionId).IsEqualTo(42);
                await Assert.That(close.SessionEpoch).IsEqualTo(-1);
                await Assert.That(connection.LeaseAcquisitionCount).IsEqualTo(1);
                await Assert.That(connection.LeaseCount).IsEqualTo(0);
            }
            else
            {
                await Assert.That(result.Started).IsEqualTo(0);
                await pool.DidNotReceive().GetConnectionByIndexAsync(
                    1,
                    sessionConnectionIndex,
                    Arg.Any<CancellationToken>());
                await Assert.That(fetchSessions.ContainsKey((1, sessionConnectionIndex))).IsFalse();
            }
        }
        finally
        {
            await consumer.DisposeAsync();
            if (!ownsInfrastructure)
                await metadataManager.DisposeAsync();
        }
    }

    [Test]
    public async Task StandaloneConsumer_ScaleDown_DrainsAndDisposesRemovedConnectionOnce()
    {
        var pool = CreatePool();
        var removedConnection = new TrackedConnection(hasPendingRequest: true);
        pool.ShrinkConnectionGroupAsync(1, 2, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<IKafkaConnection?>(removedConnection));
        await using var consumer = CreateStandaloneConsumer(pool, CreateMetadataManager(pool));

        TriggerScaleDown(consumer);

        _ = pool.Received(1).ShrinkConnectionGroupAsync(1, 2, Arg.Any<CancellationToken>());
        await Assert.That(removedConnection.DisposeCount).IsEqualTo(0);

        removedConnection.CompleteRequest();
        await TestWait.UntilAsync(
            () => removedConnection.DisposeCount == 1,
            TimeSpan.FromSeconds(5));

        await consumer.DisposeAsync();
        await Assert.That(removedConnection.DisposeCount).IsEqualTo(1);
    }

    [Test]
    public async Task StandaloneConsumer_ScaleDown_WaitsForLeasedHolderBeforeDisposal()
    {
        var pool = CreatePool();
        var removedConnection = new TrackedConnection(
            hasPendingRequest: false,
            hasLease: true);
        pool.ShrinkConnectionGroupAsync(1, 2, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<IKafkaConnection?>(removedConnection));
        await using var consumer = CreateStandaloneConsumer(pool, CreateMetadataManager(pool));

        TriggerScaleDown(consumer);

        await Assert.That(removedConnection.DisposeCount).IsEqualTo(0);
        await TestWait.UntilAsync(
            () => GetField<Task>(consumer, "_connectionRoutingTransitionTask").IsCompleted,
            TimeSpan.FromSeconds(5));

        removedConnection.ReleaseLease();
        await TestWait.UntilAsync(
            () => removedConnection.DisposeCount == 1,
            TimeSpan.FromSeconds(5));
    }

    [Test]
    [Timeout(30_000)]
    public async Task StandaloneConsumer_Dispose_WaitsForRetiredConnectionDisposal(
        CancellationToken cancellationToken)
    {
        var pool = CreatePool();
        var removedConnection = new TrackedConnection(
            hasPendingRequest: false,
            hasLease: true);
        pool.ShrinkConnectionGroupAsync(1, 2, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<IKafkaConnection?>(removedConnection));
        var consumer = CreateStandaloneConsumer(pool, CreateMetadataManager(pool));
        var leaseReleased = false;

        TriggerScaleDown(consumer);
        await GetField<Task>(consumer, "_connectionRoutingTransitionTask")
            .WaitAsync(cancellationToken);
        await removedConnection.RetirementLeaseObserved.WaitAsync(cancellationToken);
        var disposeTask = consumer.DisposeAsync().AsTask();

        try
        {
            await Assert.That(disposeTask.IsCompleted).IsFalse();
            _ = pool.DidNotReceive().DisposeAsync();

            removedConnection.ReleaseLease();
            leaseReleased = true;
            await removedConnection.DisposalStarted.WaitAsync(cancellationToken);
            await disposeTask.WaitAsync(cancellationToken);
        }
        finally
        {
            if (!leaseReleased)
                removedConnection.ReleaseLease();
            await consumer.DisposeAsync();
        }

        await Assert.That(removedConnection.DisposeCount).IsEqualTo(1);
        _ = pool.Received(1).DisposeAsync();
    }

    [Test]
    public async Task StandaloneConsumer_ScaleDown_WaitsForActiveOperationBeforeDisposal()
    {
        var pool = CreatePool();
        var removedConnection = new TrackedConnection(
            hasPendingRequest: false,
            activeOperationCount: 1);
        pool.ShrinkConnectionGroupAsync(1, 2, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<IKafkaConnection?>(removedConnection));
        await using var consumer = CreateStandaloneConsumer(pool, CreateMetadataManager(pool));

        TriggerScaleDown(consumer);

        await Assert.That(removedConnection.DisposeCount).IsEqualTo(0);

        removedConnection.CompleteOperation();
        await TestWait.UntilAsync(
            () => removedConnection.DisposeCount == 1,
            TimeSpan.FromSeconds(5));
    }

    [Test]
    public async Task StandaloneConsumer_ScaleDown_DrainsBrokerConnectionsConcurrently()
    {
        var pool = CreatePool();
        var firstConnection = new TrackedConnection(
            hasPendingRequest: false,
            hasLease: true);
        var secondConnection = new TrackedConnection(hasPendingRequest: false);
        pool.ShrinkConnectionGroupAsync(1, 2, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<IKafkaConnection?>(firstConnection));
        pool.ShrinkConnectionGroupAsync(2, 2, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<IKafkaConnection?>(secondConnection));
        var consumer = CreateStandaloneConsumer(
            pool,
            CreateMetadataManager(pool, includeSecondBroker: true));
        var firstLeaseReleased = false;

        try
        {
            TriggerScaleDown(consumer);

            await TestWait.UntilAsync(
                () => secondConnection.DisposeCount == 1,
                TimeSpan.FromSeconds(5));
            _ = pool.Received(1).ShrinkConnectionGroupAsync(2, 2, Arg.Any<CancellationToken>());
            await Assert.That(firstConnection.DisposeCount).IsEqualTo(0);

            firstConnection.ReleaseLease();
            firstLeaseReleased = true;
            await TestWait.UntilAsync(
                () => firstConnection.DisposeCount == 1,
                TimeSpan.FromSeconds(5));
        }
        finally
        {
            if (!firstLeaseReleased)
                firstConnection.ReleaseLease();
            await consumer.DisposeAsync();
        }
    }

    [Test]
    public async Task StandaloneConsumer_Dispose_WaitsForLateShrinkAndDisposesReturnedConnection()
    {
        var pool = CreatePool();
        var shrinkCompletion = new TaskCompletionSource<IKafkaConnection?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var removedConnection = new TrackedConnection(hasPendingRequest: false);
        pool.ShrinkConnectionGroupAsync(1, 2, Arg.Any<CancellationToken>())
            .Returns(new ValueTask<IKafkaConnection?>(shrinkCompletion.Task));
        var consumer = CreateStandaloneConsumer(pool, CreateMetadataManager(pool));

        TriggerScaleDown(consumer);
        var disposeTask = consumer.DisposeAsync().AsTask();

        try
        {
            _ = pool.DidNotReceive().DisposeAsync();
            shrinkCompletion.SetResult(removedConnection);
            await disposeTask;
        }
        finally
        {
            shrinkCompletion.TrySetResult(removedConnection);
            await consumer.DisposeAsync();
        }

        await Assert.That(removedConnection.DisposeCount).IsEqualTo(1);
        _ = pool.Received(1).DisposeAsync();
    }

    [Test]
    public async Task StandaloneConsumer_Dispose_WaitsForFaultedShrinkBeforeDisposingPool()
    {
        var pool = CreatePool();
        var shrinkCompletion = new TaskCompletionSource<IKafkaConnection?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        pool.ShrinkConnectionGroupAsync(1, 2, Arg.Any<CancellationToken>())
            .Returns(new ValueTask<IKafkaConnection?>(shrinkCompletion.Task));
        var consumer = CreateStandaloneConsumer(pool, CreateMetadataManager(pool));

        TriggerScaleDown(consumer);
        var disposeTask = consumer.DisposeAsync().AsTask();

        try
        {
            _ = pool.DidNotReceive().DisposeAsync();
            shrinkCompletion.SetException(new IOException("shrink failed"));
            await disposeTask;
        }
        finally
        {
            shrinkCompletion.TrySetException(new IOException("shrink failed"));
            await consumer.DisposeAsync();
        }

        _ = pool.Received(1).DisposeAsync();
    }

    [Test]
    public async Task StandaloneConsumer_Dispose_KeepsPoolAliveUntilRetiredLeaseReleases()
    {
        var pool = CreatePool();
        var removedConnection = new TrackedConnection(
            hasPendingRequest: false,
            hasLease: true);
        pool.ShrinkConnectionGroupAsync(1, 2, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<IKafkaConnection?>(removedConnection));
        var consumer = CreateStandaloneConsumer(pool, CreateMetadataManager(pool));
        var leaseReleased = false;

        TriggerScaleDown(consumer);
        var disposeTask = consumer.DisposeAsync().AsTask();

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(5.2));
            _ = pool.DidNotReceive().DisposeAsync();

            removedConnection.ReleaseLease();
            leaseReleased = true;
            await disposeTask.WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally
        {
            if (!leaseReleased)
                removedConnection.ReleaseLease();
            await consumer.DisposeAsync();
        }

        await Assert.That(removedConnection.DisposeCount).IsEqualTo(1);
        _ = pool.Received(1).DisposeAsync();
    }

    private static IConnectionPool CreatePool()
    {
        var pool = Substitute.For<IConnectionPool>();
        pool.DisposeAsync().Returns(ValueTask.CompletedTask);
        return pool;
    }

    private static MetadataManager CreateMetadataManager(IConnectionPool pool, bool includeSecondBroker = false)
    {
        var metadataManager = new MetadataManager(pool, ["localhost:9092"]);
        var brokers = new List<BrokerMetadata>
        {
            new() { NodeId = 1, Host = "localhost", Port = 9092 }
        };
        if (includeSecondBroker)
            brokers.Add(new BrokerMetadata { NodeId = 2, Host = "localhost", Port = 9093 });

        metadataManager.SetApiVersion(
            ApiKey.Fetch,
            FetchRequest.LowestSupportedVersion,
            FetchRequest.HighestSupportedVersion);
        metadataManager.Metadata.Update(new MetadataResponse
        {
            Brokers = brokers,
            Topics = []
        });
        return metadataManager;
    }

    private static KafkaConsumer<string, string> CreateStandaloneConsumer(
        IConnectionPool pool,
        MetadataManager metadataManager)
        => new(
            CreateOptions(),
            Serializers.String,
            Serializers.String,
            pool,
            metadataManager);

    private static KafkaConsumer<string, string> CreateSharedConsumer(
        IConnectionPool pool,
        MetadataManager metadataManager)
        => new(
            CreateOptions(),
            Serializers.String,
            Serializers.String,
            pool,
            metadataManager,
            new TestMemoryBudget());

    private static ConsumerOptions CreateOptions() =>
        new()
        {
            BootstrapServers = ["localhost:9092"],
            ClientId = "connection-ownership-test",
            ConnectionsPerBroker = 2,
            MaxConnectionsPerBroker = 4,
            EnableAdaptiveConnections = true,
            QueuedMaxMessagesKbytes = 1,
            IsAutoTuned = false
        };

    private static void TriggerScaleDown(KafkaConsumer<string, string> consumer)
    {
        var scaler = GetField<ConsumerConnectionScaler>(consumer, "_connectionScaler");

        scaler.TestSetConnectionCount(3);
        scaler.ReportPipelineUtilization(0, pipelineDepth: 3);
        scaler.MaybeScale();
        scaler.TestAdvanceTime(TimeSpan.FromSeconds(121));
        scaler.ReportPipelineUtilization(0, pipelineDepth: 3);
        scaler.MaybeScale();
    }

    private static void TriggerScaleUp(ConsumerConnectionScaler scaler)
    {
        scaler.ReportPipelineUtilization(3, pipelineDepth: 3);
        scaler.TestAdvanceTime(TimeSpan.FromSeconds(6));
        scaler.ReportPipelineUtilization(3, pipelineDepth: 3);
        scaler.MaybeScale();
    }

    private static T GetField<T>(object instance, string fieldName) =>
        (T)(instance.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"{fieldName} field not found"))
        .GetValue(instance)!;

    private static void SetField<T>(object instance, string fieldName, T value) =>
        (instance.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"{fieldName} field not found"))
        .SetValue(instance, value);

    private sealed class TrackedConnection(
        bool hasPendingRequest,
        bool hasLease = false,
        int activeOperationCount = 0,
        Action<FetchRequest>? onFetchRequest = null) :
        IKafkaConnection,
        IIdleTrackedKafkaConnection,
        IRetirableKafkaConnection
    {
        private readonly TaskCompletionSource _activeRequest = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _retirementLeaseObserved = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _disposalStarted = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private int _pendingRequestCount = hasPendingRequest ? 1 : 0;
        private int _leaseCount = hasLease ? 1 : 0;
        private int _leaseAcquisitionCount;
        private int _activeOperationCount = activeOperationCount;
        private int _disposeCount;

        public int BrokerId => 1;
        public string Host => "localhost";
        public int Port => 9092;
        public bool IsConnected => true;
        public Task ActiveRequest => _activeRequest.Task;
        public Task RetirementLeaseObserved => _retirementLeaseObserved.Task;
        public Task DisposalStarted => _disposalStarted.Task;
        public int DisposeCount => Volatile.Read(ref _disposeCount);
        public long LastUsedTimestampMs => 0;
        public int PendingRequestCount => Volatile.Read(ref _pendingRequestCount);
        public int LeaseCount
        {
            get
            {
                var leaseCount = Volatile.Read(ref _leaseCount);
                if (leaseCount > 0)
                    _retirementLeaseObserved.TrySetResult();
                return leaseCount;
            }
        }
        public int LeaseAcquisitionCount => Volatile.Read(ref _leaseAcquisitionCount);
        public int ActiveOperationCount => Volatile.Read(ref _activeOperationCount);

        public void CompleteRequest()
        {
            Volatile.Write(ref _pendingRequestCount, 0);
            _activeRequest.TrySetResult();
        }

        public void CompleteOperation() => Interlocked.Decrement(ref _activeOperationCount);

        public void Touch()
        {
        }

        public bool TryAcquireLease()
        {
            Interlocked.Increment(ref _leaseAcquisitionCount);
            Interlocked.Increment(ref _leaseCount);
            return true;
        }

        public void ReleaseLease() => Interlocked.Decrement(ref _leaseCount);

        public void BeginRetirement()
        {
        }

        public void CompleteRetirement()
        {
        }

        public ValueTask ConnectAsync(CancellationToken cancellationToken = default) =>
            ValueTask.CompletedTask;

        public ValueTask<TResponse> SendAsync<TRequest, TResponse>(
            TRequest request,
            short apiVersion,
            CancellationToken cancellationToken = default)
            where TRequest : IKafkaRequest<TResponse>
            where TResponse : IKafkaResponse
        {
            if (request is FetchRequest fetchRequest && typeof(TResponse) == typeof(FetchResponse))
            {
                onFetchRequest?.Invoke(fetchRequest);
                return ValueTask.FromResult((TResponse)(object)new FetchResponse { ErrorCode = ErrorCode.None });
            }

            throw new NotSupportedException();
        }

        public ValueTask SendFireAndForgetAsync<TRequest, TResponse>(
            TRequest request,
            short apiVersion,
            CancellationToken cancellationToken = default)
            where TRequest : IKafkaRequest<TResponse>
            where TResponse : IKafkaResponse =>
            throw new NotSupportedException();

        public Task<TResponse> SendPipelinedAsync<TRequest, TResponse>(
            TRequest request,
            short apiVersion,
            CancellationToken cancellationToken = default)
            where TRequest : IKafkaRequest<TResponse>
            where TResponse : IKafkaResponse =>
            throw new NotSupportedException();

        public ValueTask SendFireAndForgetWithCallerTimeoutAsync<TRequest, TResponse>(
            TRequest request,
            short apiVersion,
            CancellationToken cancellationToken = default)
            where TRequest : IKafkaRequest<TResponse>
            where TResponse : IKafkaResponse =>
            throw new NotSupportedException();

        public Task<TResponse> SendPipelinedWithCallerTimeoutAsync<TRequest, TResponse>(
            TRequest request,
            short apiVersion,
            CancellationToken cancellationToken = default)
            where TRequest : IKafkaRequest<TResponse>
            where TResponse : IKafkaResponse =>
            throw new NotSupportedException();

        public ValueTask DisposeAsync()
        {
            Interlocked.Increment(ref _disposeCount);
            _disposalStarted.TrySetResult();
            _activeRequest.TrySetException(new ObjectDisposedException(nameof(TrackedConnection)));
            return ValueTask.CompletedTask;
        }
    }

    private sealed class TestMemoryBudget : IDekafMemoryBudget
    {
        public ulong PreviewProducerLimit() => 1;
        public ulong PreviewConsumerLimit() => 1;
        public void RegisterProducer(IBudgetedInstance instance) { }
        public void UnregisterProducer(IBudgetedInstance instance) { }
        public void RegisterConsumer(IBudgetedInstance instance) { }
        public void UnregisterConsumer(IBudgetedInstance instance) { }
        public void ReserveExplicit(ulong bytes) { }
        public void ReleaseExplicit(ulong bytes) { }
    }
}
