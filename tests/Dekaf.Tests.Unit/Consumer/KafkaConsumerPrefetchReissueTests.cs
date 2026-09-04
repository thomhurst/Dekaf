using System.Reflection;
using Dekaf.Consumer;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.Serialization;
using NSubstitute;

namespace Dekaf.Tests.Unit.Consumer;

/// <summary>
/// Verifies that a broker prefetch task re-issues fetches on its own leased connection while the
/// fetch plan is stable, and hands control back to the central prefetch loop (which acquires a
/// fresh lease) when the plan changes.
/// </summary>
[NotInParallel]
public sealed class KafkaConsumerPrefetchReissueTests
{
    private const string Topic = "reissue-topic";
    private static readonly Guid TopicId = Guid.Parse("00000000-0000-0000-0000-000000000042");
    private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(15);
    private static readonly TopicPartition Partition = new(Topic, 0);

    [Test]
    [Timeout(60_000)]
    public async Task StablePlan_ReissuesConsecutiveFetchesOnOneLease(CancellationToken cancellationToken)
    {
        var connection = new FetchServingConnection();
        await using var metadataManager = CreateMetadataManager(connection);
        await using var consumer = CreateConsumer(connection, metadataManager);
        consumer.IncrementalAssign([new TopicPartitionOffset(Topic, 0, 10)]);

        await StartPrefetchAsync(consumer, cancellationToken);
        await TestWait.UntilAsync(() => connection.FetchCount >= 8, WaitTimeout);

        // Eight consecutive fetches on a stable plan must not have gone back through the loop:
        // the task keeps re-issuing on the single connection lease it started with.
        await Assert.That(connection.LeaseAcquisitionCount).IsEqualTo(1);
        await Assert.That(connection.LeaseCount).IsEqualTo(1);
        await Assert.That(connection.MaxConcurrentFetches).IsEqualTo(1);
        await Assert.That(connection.LastFetchOffset).IsEqualTo(10L);
    }

    [Test]
    [Timeout(60_000)]
    public async Task Pause_EndsTaskAndReleasesLease_ResumeStartsFreshTask(CancellationToken cancellationToken)
    {
        var connection = new FetchServingConnection();
        await using var metadataManager = CreateMetadataManager(connection);
        await using var consumer = CreateConsumer(connection, metadataManager);
        consumer.IncrementalAssign([new TopicPartitionOffset(Topic, 0, 10)]);

        await StartPrefetchAsync(consumer, cancellationToken);
        await TestWait.UntilAsync(() => connection.FetchCount >= 3, WaitTimeout);
        await Assert.That(connection.LeaseAcquisitionCount).IsEqualTo(1);

        consumer.Pause(Partition);

        await TestWait.UntilAsync(() => connection.LeaseCount == 0, WaitTimeout);
        var fetchesAtPause = connection.FetchCount;
        await Task.Delay(TimeSpan.FromMilliseconds(200), cancellationToken);
        await Assert.That(connection.FetchCount).IsEqualTo(fetchesAtPause);

        consumer.Resume(Partition);

        await TestWait.UntilAsync(() => connection.FetchCount >= fetchesAtPause + 3, WaitTimeout);
        await Assert.That(connection.LeaseAcquisitionCount).IsEqualTo(2);
    }

    [Test]
    [Timeout(60_000)]
    public async Task Seek_EndsTaskAndNextTaskFetchesFromNewPosition(CancellationToken cancellationToken)
    {
        var connection = new FetchServingConnection();
        await using var metadataManager = CreateMetadataManager(connection);
        await using var consumer = CreateConsumer(connection, metadataManager);
        consumer.IncrementalAssign([new TopicPartitionOffset(Topic, 0, 10)]);

        await StartPrefetchAsync(consumer, cancellationToken);
        await TestWait.UntilAsync(() => connection.FetchCount >= 3, WaitTimeout);
        await Assert.That(connection.LeaseAcquisitionCount).IsEqualTo(1);

        consumer.Seek(new TopicPartitionOffset(Topic, 0, 500));

        await TestWait.UntilAsync(() => connection.LastFetchOffset == 500L, WaitTimeout);
        await Assert.That(connection.LeaseAcquisitionCount).IsEqualTo(2);
    }

    [Test]
    [Timeout(60_000)]
    public async Task MetadataRefresh_EndsTaskAndNextTaskReissuesOnRefreshedSnapshot(CancellationToken cancellationToken)
    {
        var connection = new FetchServingConnection();
        await using var metadataManager = CreateMetadataManager(connection);
        await using var consumer = CreateConsumer(connection, metadataManager);
        consumer.IncrementalAssign([new TopicPartitionOffset(Topic, 0, 10)]);

        await StartPrefetchAsync(consumer, cancellationToken);
        await TestWait.UntilAsync(() => connection.FetchCount >= 3, WaitTimeout);
        await Assert.That(connection.LeaseAcquisitionCount).IsEqualTo(1);

        // Any refresh publishes a new snapshot, even with identical content; the plan is stamped
        // with the snapshot it was grouped against, so the task hands back to the loop.
        metadataManager.Metadata.Update(CreateMetadataResponse());

        await TestWait.UntilAsync(() => connection.LeaseAcquisitionCount == 2, WaitTimeout);
        var fetchesAtRefresh = connection.FetchCount;
        await TestWait.UntilAsync(() => connection.FetchCount >= fetchesAtRefresh + 3, WaitTimeout);
        await Assert.That(connection.LeaseAcquisitionCount).IsEqualTo(2);
    }

    [Test]
    [Timeout(60_000)]
    public async Task TopicIdentityMarkerBehindPlan_EndsTaskUntilLoopReprocessesSnapshot(CancellationToken cancellationToken)
    {
        var connection = new FetchServingConnection();
        await using var metadataManager = CreateMetadataManager(connection);
        await using var consumer = CreateConsumer(connection, metadataManager);
        consumer.IncrementalAssign([new TopicPartitionOffset(Topic, 0, 10)]);

        await StartPrefetchAsync(consumer, cancellationToken);
        await TestWait.UntilAsync(() => connection.FetchCount >= 3, WaitTimeout);
        await Assert.That(connection.LeaseAcquisitionCount).IsEqualTo(1);

        // The current snapshot still matches the plan, but the topic-identity pass no longer
        // vouches for it (an assignment publish replaces the marker the same way). The task must
        // stop re-issuing; the loop reprocesses the snapshot, restamps, and a fresh task resumes.
        SetObservedTopicIdentityMarker(consumer, new object());

        await TestWait.UntilAsync(() => connection.LeaseAcquisitionCount == 2, WaitTimeout);
        var fetchesAtRestamp = connection.FetchCount;
        await TestWait.UntilAsync(() => connection.FetchCount >= fetchesAtRestamp + 3, WaitTimeout);
        await Assert.That(connection.LeaseAcquisitionCount).IsEqualTo(2);
        await Assert.That(GetObservedTopicIdentityMarker(consumer))
            .IsSameReferenceAs(metadataManager.Metadata.CaptureSnapshot());
    }

    [Test]
    [Timeout(60_000)]
    public async Task ScaleDecisionDue_EndsTaskSoLoopCanScaleUp(CancellationToken cancellationToken)
    {
        var connection = new FetchServingConnection();
        await using var metadataManager = CreateMetadataManager(connection);
        await using var consumer = CreateConsumer(connection, metadataManager, adaptiveConnections: true);
        consumer.IncrementalAssign([new TopicPartitionOffset(Topic, 0, 10)]);

        await StartPrefetchAsync(consumer, cancellationToken);
        await TestWait.UntilAsync(() => connection.FetchCount >= 3, WaitTimeout);
        await Assert.That(connection.LeaseAcquisitionCount).IsEqualTo(1);

        var scaler = GetConnectionScaler(consumer);
        await Assert.That(scaler.CurrentConnectionCount).IsEqualTo(1);

        // Steady re-issuing never accumulates saturation (each fetch restarts the window, as the
        // loop-owned path did). A fetch that outlasts the scale-up threshold is a due decision:
        // the task must end so the loop, the only MaybeScale caller, can act on it.
        scaler.TestAdvanceTime(TimeSpan.FromSeconds(6));

        await TestWait.UntilAsync(() => scaler.CurrentConnectionCount == 2, WaitTimeout);
        await TestWait.UntilAsync(() => connection.LeaseAcquisitionCount >= 2, WaitTimeout);
        var fetchesAtScale = connection.FetchCount;
        await TestWait.UntilAsync(() => connection.FetchCount >= fetchesAtScale + 3, WaitTimeout);
        await Assert.That(scaler.CurrentConnectionCount).IsEqualTo(2);
    }

    private static ConsumerConnectionScaler GetConnectionScaler(KafkaConsumer<string, string> consumer) =>
        (ConsumerConnectionScaler?)GetPrivateField("_connectionScaler").GetValue(consumer)
        ?? throw new InvalidOperationException("Adaptive connection scaler not created.");

    private static object? GetObservedTopicIdentityMarker(KafkaConsumer<string, string> consumer) =>
        GetPrivateField("_observedTopicIdentityMarker").GetValue(consumer);

    private static void SetObservedTopicIdentityMarker(KafkaConsumer<string, string> consumer, object marker) =>
        GetPrivateField("_observedTopicIdentityMarker").SetValue(consumer, marker);

    private static FieldInfo GetPrivateField(string name) =>
        typeof(KafkaConsumer<string, string>).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)
        ?? throw new InvalidOperationException($"{name} field not found.");

    private static async Task StartPrefetchAsync(
        KafkaConsumer<string, string> consumer,
        CancellationToken cancellationToken)
    {
        // The prefetch loop is a lifetime loop started by the first consume call; the poll
        // itself times out because the fake broker never returns records.
        var result = await consumer.ConsumeOneAsync(TimeSpan.FromMilliseconds(50), cancellationToken);
        await Assert.That(result).IsNull();
    }

    private static KafkaConsumer<string, string> CreateConsumer(
        FetchServingConnection connection,
        MetadataManager metadataManager,
        bool adaptiveConnections = false)
    {
        var pool = Substitute.For<IConnectionPool>();
        pool.DisposeAsync().Returns(ValueTask.CompletedTask);
        pool.GetConnectionAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<IKafkaConnection>(connection));
        pool.GetConnectionByIndexAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<IKafkaConnection>(connection));

        var consumer = new KafkaConsumer<string, string>(
            new ConsumerOptions
            {
                BootstrapServers = ["localhost:9092"],
                ClientId = "prefetch-reissue-test",
                OffsetCommitMode = OffsetCommitMode.Manual,
                // One fetch connection either way; adaptive mode may add the coordination one.
                ConnectionsPerBroker = 1,
                MaxConnectionsPerBroker = adaptiveConnections ? 2 : 1,
                EnableAdaptiveConnections = adaptiveConnections,
                IsAutoTuned = false
            },
            Serializers.String,
            Serializers.String,
            pool,
            metadataManager);

        GetPrivateField("_initialized").SetValue(consumer, true);
        return consumer;
    }

    private static MetadataManager CreateMetadataManager(FetchServingConnection connection)
    {
        var pool = Substitute.For<IConnectionPool>();
        pool.GetConnectionAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<IKafkaConnection>(connection));
        var metadataManager = new MetadataManager(pool, ["localhost:9092"]);
        metadataManager.SetApiVersion(
            ApiKey.Fetch,
            FetchRequest.LowestSupportedVersion,
            FetchRequest.HighestSupportedVersion);
        metadataManager.Metadata.Update(CreateMetadataResponse());
        return metadataManager;
    }

    private static MetadataResponse CreateMetadataResponse() => new()
    {
        Brokers =
        [
            new BrokerMetadata { NodeId = 1, Host = "localhost", Port = 9092 }
        ],
        Topics =
        [
            new TopicMetadata
            {
                Name = Topic,
                TopicId = TopicId,
                ErrorCode = ErrorCode.None,
                Partitions =
                [
                    new PartitionMetadata
                    {
                        PartitionIndex = 0,
                        LeaderId = 1,
                        ErrorCode = ErrorCode.None,
                        ReplicaNodes = [1],
                        IsrNodes = [1]
                    }
                ]
            }
        ]
    };

    /// <summary>
    /// Answers every fetch with an empty response after a short broker-like wait, and counts
    /// lease acquisitions so tests can tell a re-issued fetch from a loop re-dispatch.
    /// </summary>
    private sealed class FetchServingConnection : IKafkaConnection, IRetirableKafkaConnection
    {
        private static readonly TimeSpan FetchWait = TimeSpan.FromMilliseconds(5);
        private int _leaseCount;
        private int _leaseAcquisitionCount;
        private int _fetchCount;
        private int _fetchesInFlight;
        private int _maxConcurrentFetches;
        private long _lastFetchOffset = -1;

        public int BrokerId => 1;
        public string Host => "localhost";
        public int Port => 9092;
        public bool IsConnected => true;
        public int LeaseCount => Volatile.Read(ref _leaseCount);
        public int LeaseAcquisitionCount => Volatile.Read(ref _leaseAcquisitionCount);
        public int ActiveOperationCount => 0;
        public int FetchCount => Volatile.Read(ref _fetchCount);
        public int MaxConcurrentFetches => Volatile.Read(ref _maxConcurrentFetches);
        public long LastFetchOffset => Interlocked.Read(ref _lastFetchOffset);

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
            if (request is not FetchRequest fetchRequest || typeof(TResponse) != typeof(FetchResponse))
                throw new NotSupportedException($"Unexpected request {typeof(TRequest).Name}");

            if (fetchRequest.Topics is { Count: > 0 } topics && topics[0].Partitions is { Count: > 0 } partitions)
                Interlocked.Exchange(ref _lastFetchOffset, partitions[0].FetchOffset);

            return ServeFetchAsync<TResponse>(cancellationToken);
        }

        private async ValueTask<TResponse> ServeFetchAsync<TResponse>(CancellationToken cancellationToken)
        {
            var inFlight = Interlocked.Increment(ref _fetchesInFlight);
            var observedMax = Volatile.Read(ref _maxConcurrentFetches);
            while (inFlight > observedMax
                   && Interlocked.CompareExchange(ref _maxConcurrentFetches, inFlight, observedMax) != observedMax)
            {
                observedMax = Volatile.Read(ref _maxConcurrentFetches);
            }

            try
            {
                await Task.Delay(FetchWait, cancellationToken);
            }
            finally
            {
                Interlocked.Decrement(ref _fetchesInFlight);
                Interlocked.Increment(ref _fetchCount);
            }

            object response = new FetchResponse { ErrorCode = ErrorCode.None };
            return (TResponse)response;
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

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
