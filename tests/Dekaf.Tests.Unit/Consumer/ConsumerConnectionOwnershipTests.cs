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

    private static IConnectionPool CreatePool()
    {
        var pool = Substitute.For<IConnectionPool>();
        pool.DisposeAsync().Returns(ValueTask.CompletedTask);
        return pool;
    }

    private static MetadataManager CreateMetadataManager(IConnectionPool pool)
    {
        var metadataManager = new MetadataManager(pool, ["localhost:9092"]);
        metadataManager.Metadata.Update(new MetadataResponse
        {
            Brokers =
            [
                new BrokerMetadata { NodeId = 1, Host = "localhost", Port = 9092 }
            ],
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
        var scalerField = typeof(KafkaConsumer<string, string>).GetField(
            "_connectionScaler",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("_connectionScaler field not found");
        var scaler = (ConsumerConnectionScaler?)scalerField.GetValue(consumer)
            ?? throw new InvalidOperationException("Connection scaler was not created");

        scaler.TestSetConnectionCount(3);
        scaler.ReportPipelineUtilization(0, pipelineDepth: 3);
        scaler.MaybeScale();
        scaler.TestAdvanceTime(TimeSpan.FromSeconds(121));
        scaler.ReportPipelineUtilization(0, pipelineDepth: 3);
        scaler.MaybeScale();
    }

    private sealed class TrackedConnection(bool hasPendingRequest) :
        IKafkaConnection,
        IIdleTrackedKafkaConnection
    {
        private readonly TaskCompletionSource _activeRequest = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private int _pendingRequestCount = hasPendingRequest ? 1 : 0;
        private int _disposeCount;

        public int BrokerId => 1;
        public string Host => "localhost";
        public int Port => 9092;
        public bool IsConnected => true;
        public Task ActiveRequest => _activeRequest.Task;
        public int DisposeCount => Volatile.Read(ref _disposeCount);
        public long LastUsedTimestampMs => 0;
        public int PendingRequestCount => Volatile.Read(ref _pendingRequestCount);

        public void CompleteRequest()
        {
            Volatile.Write(ref _pendingRequestCount, 0);
            _activeRequest.TrySetResult();
        }

        public void Touch()
        {
        }

        public ValueTask ConnectAsync(CancellationToken cancellationToken = default) =>
            ValueTask.CompletedTask;

        public ValueTask<TResponse> SendAsync<TRequest, TResponse>(
            TRequest request,
            short apiVersion,
            CancellationToken cancellationToken = default)
            where TRequest : IKafkaRequest<TResponse>
            where TResponse : IKafkaResponse =>
            throw new NotSupportedException();

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
