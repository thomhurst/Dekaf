using System.Reflection;
using Dekaf.Consumer;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.Serialization;
using NSubstitute;

namespace Dekaf.Tests.Unit.Consumer;

[NotInParallel]
public sealed class QueryWatermarkOffsetsTests
{
    private const string Topic = "watermark-topic";
    private const int Partition = 0;
    private const long EarliestOffsetTimestamp = -2;
    private const long LatestOffsetTimestamp = -1;
    private static readonly Guid InitialTopicId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    [Test]
    public async Task QueryWatermarkOffsetsAsync_UsesCoordinationConnectionAndStartsRequestsConcurrently()
    {
        var connectionPool = Substitute.For<IConnectionPool>();
        var connection = new LeaseTrackingConnection();
        connectionPool.GetConnectionByIndexAsync(0, 1, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<IKafkaConnection>(connection));

        var metadataManager = new MetadataManager(connectionPool, ["localhost:9092"]);
        metadataManager.SetApiVersion(ApiKey.ListOffsets, ListOffsetsRequest.LowestSupportedVersion, ListOffsetsRequest.HighestSupportedVersion);
        metadataManager.Metadata.Update(CreateMetadataResponse());

        await using var consumer = new KafkaConsumer<string, string>(
            new ConsumerOptions
            {
                BootstrapServers = ["localhost:9092"],
                GroupId = "test-group"
            },
            Serializers.String,
            Serializers.String,
            connectionPool,
            metadataManager);
        SetInitialized(consumer);

        var earliestStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var latestStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseResponses = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        connection.SendHandler = request =>
        {
            var timestamp = request.Topics[0].Partitions[0].Timestamp;
            var currentLeaderEpoch = request.Topics[0].Partitions[0].CurrentLeaderEpoch;

            if (currentLeaderEpoch != 3)
                throw new InvalidOperationException($"Unexpected CurrentLeaderEpoch {currentLeaderEpoch}");

            if (timestamp == EarliestOffsetTimestamp)
                earliestStarted.TrySetResult();
            else if (timestamp == LatestOffsetTimestamp)
                latestStarted.TrySetResult();
            else
                throw new InvalidOperationException($"Unexpected ListOffsets timestamp {timestamp}");

            return new ValueTask<ListOffsetsResponse>(CreateListOffsetsResponseAsync(timestamp, releaseResponses.Task));
        };

        var queryTask = consumer.QueryWatermarkOffsetsAsync(new TopicPartition(Topic, Partition), CancellationToken.None).AsTask();

        await earliestStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));
        await Assert.That(connection.LeaseAcquisitionCount).IsEqualTo(1);
        await Assert.That(connection.LeaseCount).IsEqualTo(1);
        try
        {
            await latestStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));
        }
        catch
        {
            releaseResponses.TrySetResult();
            await queryTask.WaitAsync(TimeSpan.FromSeconds(1));
            throw;
        }

        releaseResponses.SetResult();
        var watermarks = await queryTask.WaitAsync(TimeSpan.FromSeconds(1));

        await Assert.That(watermarks.Low).IsEqualTo(10);
        await Assert.That(watermarks.High).IsEqualTo(42);
        _ = connectionPool.Received(1).GetConnectionByIndexAsync(
            0,
            1,
            Arg.Any<CancellationToken>());
        await Assert.That(connection.LeaseCount).IsEqualTo(0);
    }

    [Test]
    public async Task QueryCurrentLagAsync_RefreshesEndOffsetAndUsesLatestPosition()
    {
        var connectionPool = Substitute.For<IConnectionPool>();
        var latestOffset = 42L;
        var connection = new LeaseTrackingConnection();
        connection.SendHandler = request =>
        {
            var timestamp = request.Topics[0].Partitions[0].Timestamp;
            var offset = timestamp == LatestOffsetTimestamp
                ? Volatile.Read(ref latestOffset)
                : (long?)null;
            return ValueTask.FromResult(CreateListOffsetsResponse(request, offset));
        };
        connectionPool.GetConnectionByIndexAsync(0, 1, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<IKafkaConnection>(connection));

        var metadataManager = new MetadataManager(connectionPool, ["localhost:9092"]);
        metadataManager.SetApiVersion(
            ApiKey.ListOffsets,
            ListOffsetsRequest.LowestSupportedVersion,
            ListOffsetsRequest.HighestSupportedVersion);
        metadataManager.Metadata.Update(CreateMetadataResponse());

        await using var consumer = new KafkaConsumer<string, string>(
            new ConsumerOptions
            {
                BootstrapServers = ["localhost:9092"],
                GroupId = "test-group"
            },
            Serializers.String,
            Serializers.String,
            connectionPool,
            metadataManager);
        SetInitialized(consumer);
        consumer.IncrementalAssign([new TopicPartitionOffset(Topic, Partition, 32)]);

        var lag = await consumer.QueryCurrentLagAsync(new TopicPartition(Topic, Partition));

        await Assert.That(lag).IsEqualTo(10);
        await Assert.That(consumer.GetCurrentLag(new TopicPartition(Topic, Partition))).IsEqualTo(10);
        await Assert.That(consumer.GetWatermarkOffsets(new TopicPartition(Topic, Partition))).IsNull();
        await Assert.That(connection.LeaseAcquisitionCount).IsEqualTo(1);
        await Assert.That(connection.SendCount).IsEqualTo(1);

        var watermarks = await consumer.QueryWatermarkOffsetsAsync(new TopicPartition(Topic, Partition));
        await Assert.That(watermarks).IsEqualTo(new WatermarkOffsets(10, 42));
        await Assert.That(connection.SendCount).IsEqualTo(3);

        Volatile.Write(ref latestOffset, 50);
        await Assert.That(await consumer.QueryCurrentLagAsync(new TopicPartition(Topic, Partition))).IsEqualTo(18);
        await Assert.That(consumer.GetCurrentLag(new TopicPartition(Topic, Partition))).IsEqualTo(18);
        await Assert.That(consumer.GetWatermarkOffsets(new TopicPartition(Topic, Partition)))
            .IsEqualTo(new WatermarkOffsets(10, 42));
        await Assert.That(connection.SendCount).IsEqualTo(4);
    }

    [Test]
    public async Task QueryCurrentLagAsync_RetryPublishesNewerOffset()
    {
        var connectionPool = Substitute.For<IConnectionPool>();
        var connection = new LeaseTrackingConnection();
        connectionPool.GetConnectionByIndexAsync(0, 1, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<IKafkaConnection>(connection));
        var metadataManager = new MetadataManager(connectionPool, ["localhost:9092"]);
        metadataManager.SetApiVersion(
            ApiKey.ListOffsets,
            ListOffsetsRequest.LowestSupportedVersion,
            ListOffsetsRequest.HighestSupportedVersion);
        metadataManager.Metadata.Update(CreateMetadataResponse());
        await using var consumer = new KafkaConsumer<string, string>(
            new ConsumerOptions
            {
                BootstrapServers = ["localhost:9092"],
                GroupId = "test-group",
                RetryBackoffMs = 1,
                RetryBackoffMaxMs = 1
            },
            Serializers.String,
            Serializers.String,
            connectionPool,
            metadataManager);
        SetInitialized(consumer);
        var partition = new TopicPartition(Topic, Partition);
        consumer.IncrementalAssign([new TopicPartitionOffset(Topic, Partition, 32)]);

        var requestCount = 0;
        connection.SendHandler = request =>
        {
            if (Interlocked.Increment(ref requestCount) == 1)
            {
                var interveningSequence = AdvanceWatermarkUpdateSequence(consumer);
                UpdateCachedLagEndOffset(consumer, partition, 100, interveningSequence);
                return ValueTask.FromResult(CreateListOffsetsResponse(
                    request,
                    errorCode: ErrorCode.LeaderNotAvailable));
            }

            return ValueTask.FromResult(CreateListOffsetsResponse(request, offset: 110));
        };

        await Assert.That(await consumer.QueryCurrentLagAsync(partition)).IsEqualTo(78);
        await Assert.That(consumer.GetCurrentLag(partition)).IsEqualTo(78);
        await Assert.That(connection.SendCount).IsEqualTo(2);
    }

    [Test]
    public async Task QueryCurrentLagAsync_AssignmentChangeDuringRefreshReturnsNull()
    {
        var connectionPool = Substitute.For<IConnectionPool>();
        var connection = new LeaseTrackingConnection();
        connectionPool.GetConnectionByIndexAsync(0, 1, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<IKafkaConnection>(connection));
        var metadataManager = new MetadataManager(connectionPool, ["localhost:9092"]);
        metadataManager.SetApiVersion(
            ApiKey.ListOffsets,
            ListOffsetsRequest.LowestSupportedVersion,
            ListOffsetsRequest.HighestSupportedVersion);
        metadataManager.Metadata.Update(CreateMetadataResponse());
        await using var consumer = new KafkaConsumer<string, string>(
            new ConsumerOptions
            {
                BootstrapServers = ["localhost:9092"],
                GroupId = "test-group"
            },
            Serializers.String,
            Serializers.String,
            connectionPool,
            metadataManager);
        SetInitialized(consumer);
        var partition = new TopicPartition(Topic, Partition);
        consumer.IncrementalAssign([new TopicPartitionOffset(Topic, Partition, 32)]);
        var requestCount = 0;
        var requestsStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseResponses = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.SendHandler = async request =>
        {
            if (Interlocked.Increment(ref requestCount) == 1)
                requestsStarted.TrySetResult();
            await releaseResponses.Task.ConfigureAwait(false);
            return CreateListOffsetsResponse(request);
        };

        var pending = consumer.QueryCurrentLagAsync(partition).AsTask();
        try
        {
            await requestsStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));
            consumer.IncrementalUnassign([partition]);
            consumer.IncrementalAssign([new TopicPartitionOffset(Topic, Partition, 32)]);
        }
        finally
        {
            releaseResponses.TrySetResult();
        }

        await Assert.That(await pending.WaitAsync(TimeSpan.FromSeconds(1))).IsNull();
        await Assert.That(consumer.GetCurrentLag(partition)).IsNull();
    }

    [Test]
    public async Task QueryCurrentLagAsync_TopicIdentityChangeDuringRefreshReturnsNull()
    {
        var connectionPool = Substitute.For<IConnectionPool>();
        var connection = new LeaseTrackingConnection();
        connectionPool.GetConnectionByIndexAsync(0, 1, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<IKafkaConnection>(connection));
        var metadataManager = new MetadataManager(connectionPool, ["localhost:9092"]);
        metadataManager.SetApiVersion(
            ApiKey.ListOffsets,
            ListOffsetsRequest.LowestSupportedVersion,
            ListOffsetsRequest.HighestSupportedVersion);
        metadataManager.Metadata.Update(CreateMetadataResponse(InitialTopicId));
        await using var consumer = new KafkaConsumer<string, string>(
            new ConsumerOptions
            {
                BootstrapServers = ["localhost:9092"],
                GroupId = "test-group"
            },
            Serializers.String,
            Serializers.String,
            connectionPool,
            metadataManager);
        SetInitialized(consumer);
        var partition = new TopicPartition(Topic, Partition);
        consumer.IncrementalAssign([new TopicPartitionOffset(Topic, Partition, 32)]);
        await InvokeHandleTopicIdentityChangesAsync(consumer);

        var requestCount = 0;
        var requestsStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseOldTopicResponses = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.SendHandler = async request =>
        {
            var requestNumber = Interlocked.Increment(ref requestCount);
            if (requestNumber == 1)
            {
                requestsStarted.TrySetResult();
                await releaseOldTopicResponses.Task.ConfigureAwait(false);
            }

            return CreateListOffsetsResponse(request);
        };

        var pending = consumer.QueryCurrentLagAsync(partition).AsTask();
        try
        {
            await requestsStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));
            metadataManager.Metadata.Update(CreateMetadataResponse(Guid.NewGuid()));
            await InvokeHandleTopicIdentityChangesAsync(consumer);
        }
        finally
        {
            releaseOldTopicResponses.TrySetResult();
        }

        await Assert.That(await pending.WaitAsync(TimeSpan.FromSeconds(1))).IsNull();
        await Assert.That(consumer.GetCurrentLag(partition)).IsNull();
    }

    [Test]
    public async Task QueryCurrentLagAsync_TopicIdentityResetInProgressReturnsNull()
    {
        var connectionPool = Substitute.For<IConnectionPool>();
        var connection = new LeaseTrackingConnection();
        connectionPool.GetConnectionByIndexAsync(0, 1, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<IKafkaConnection>(connection));
        var metadataManager = new MetadataManager(connectionPool, ["localhost:9092"]);
        metadataManager.SetApiVersion(
            ApiKey.ListOffsets,
            ListOffsetsRequest.LowestSupportedVersion,
            ListOffsetsRequest.HighestSupportedVersion);
        metadataManager.Metadata.Update(CreateMetadataResponse(InitialTopicId));
        await using var consumer = new KafkaConsumer<string, string>(
            new ConsumerOptions
            {
                BootstrapServers = ["localhost:9092"],
                GroupId = "test-group",
                AutoOffsetReset = AutoOffsetReset.ByDuration,
                AutoOffsetResetDuration = TimeSpan.FromHours(1)
            },
            Serializers.String,
            Serializers.String,
            connectionPool,
            metadataManager);
        SetInitialized(consumer);
        var partition = new TopicPartition(Topic, Partition);
        consumer.IncrementalAssign([new TopicPartitionOffset(Topic, Partition, 32)]);
        await InvokeHandleTopicIdentityChangesAsync(consumer);

        var resetStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseReset = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.SendHandler = async request =>
        {
            var timestamp = request.Topics[0].Partitions[0].Timestamp;
            if (timestamp is EarliestOffsetTimestamp or LatestOffsetTimestamp)
                return CreateListOffsetsResponse(request);

            resetStarted.TrySetResult();
            await releaseReset.Task.ConfigureAwait(false);
            return CreateListOffsetsResponse(request, offset: 5);
        };

        metadataManager.Metadata.Update(CreateMetadataResponse(Guid.NewGuid()));
        var resetTask = InvokeHandleTopicIdentityChangesAsync(consumer).AsTask();
        try
        {
            await resetStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));

            var lagDuringReset = consumer.QueryCurrentLagAsync(partition);
            await Assert.That(lagDuringReset.IsCompletedSuccessfully).IsTrue();
            await Assert.That(lagDuringReset.Result).IsNull();
            await Assert.That(consumer.GetCurrentLag(partition)).IsNull();
            await Assert.That(connection.SendCount).IsEqualTo(1);
        }
        finally
        {
            releaseReset.TrySetResult();
        }
        await resetTask.WaitAsync(TimeSpan.FromSeconds(1));

        await Assert.That(await consumer.QueryCurrentLagAsync(partition)).IsEqualTo(37);
        await Assert.That(connection.SendCount).IsEqualTo(2);
    }

    [Test]
    public async Task QueryWatermarkOffsetsAsync_AssignmentChangeDoesNotPublishLagCache()
    {
        var connectionPool = Substitute.For<IConnectionPool>();
        var connection = new LeaseTrackingConnection();
        connectionPool.GetConnectionByIndexAsync(0, 1, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<IKafkaConnection>(connection));
        var metadataManager = new MetadataManager(connectionPool, ["localhost:9092"]);
        metadataManager.SetApiVersion(
            ApiKey.ListOffsets,
            ListOffsetsRequest.LowestSupportedVersion,
            ListOffsetsRequest.HighestSupportedVersion);
        metadataManager.Metadata.Update(CreateMetadataResponse());
        await using var consumer = new KafkaConsumer<string, string>(
            new ConsumerOptions
            {
                BootstrapServers = ["localhost:9092"],
                GroupId = "test-group"
            },
            Serializers.String,
            Serializers.String,
            connectionPool,
            metadataManager);
        SetInitialized(consumer);
        var partition = new TopicPartition(Topic, Partition);
        consumer.IncrementalAssign([new TopicPartitionOffset(Topic, Partition, 32)]);
        var requestCount = 0;
        var requestsStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseResponses = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.SendHandler = async request =>
        {
            if (Interlocked.Increment(ref requestCount) == 2)
                requestsStarted.TrySetResult();
            await releaseResponses.Task.ConfigureAwait(false);
            return CreateListOffsetsResponse(request);
        };

        var pending = consumer.QueryWatermarkOffsetsAsync(partition).AsTask();
        try
        {
            await requestsStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));
            consumer.IncrementalUnassign([partition]);
            consumer.IncrementalAssign([new TopicPartitionOffset(Topic, Partition, 32)]);
        }
        finally
        {
            releaseResponses.TrySetResult();
        }

        var watermarks = await pending.WaitAsync(TimeSpan.FromSeconds(1));
        await Assert.That(watermarks.High).IsEqualTo(42);
        await Assert.That(consumer.GetWatermarkOffsets(partition)).IsNull();
        await Assert.That(consumer.GetCurrentLag(partition)).IsNull();
    }

    [Test]
    public async Task QueryWatermarkOffsetsAsync_UnrelatedAssignmentChangeCachesResult()
    {
        var connectionPool = Substitute.For<IConnectionPool>();
        var connection = new LeaseTrackingConnection();
        connectionPool.GetConnectionByIndexAsync(0, 1, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<IKafkaConnection>(connection));
        var metadataManager = new MetadataManager(connectionPool, ["localhost:9092"]);
        metadataManager.SetApiVersion(
            ApiKey.ListOffsets,
            ListOffsetsRequest.LowestSupportedVersion,
            ListOffsetsRequest.HighestSupportedVersion);
        metadataManager.Metadata.Update(CreateMetadataResponse());
        await using var consumer = new KafkaConsumer<string, string>(
            new ConsumerOptions
            {
                BootstrapServers = ["localhost:9092"],
                GroupId = "test-group"
            },
            Serializers.String,
            Serializers.String,
            connectionPool,
            metadataManager);
        SetInitialized(consumer);
        var partition = new TopicPartition(Topic, Partition);
        consumer.IncrementalAssign([new TopicPartitionOffset(Topic, Partition, 32)]);
        var requestCount = 0;
        var requestsStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseResponses = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.SendHandler = async request =>
        {
            if (Interlocked.Increment(ref requestCount) == 2)
                requestsStarted.TrySetResult();
            await releaseResponses.Task.ConfigureAwait(false);
            return CreateListOffsetsResponse(request);
        };

        var pending = consumer.QueryWatermarkOffsetsAsync(partition).AsTask();
        try
        {
            await requestsStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));
            consumer.IncrementalAssign([new TopicPartitionOffset(Topic, Partition + 1, 0)]);
        }
        finally
        {
            releaseResponses.TrySetResult();
        }

        var watermarks = await pending.WaitAsync(TimeSpan.FromSeconds(1));
        await Assert.That(watermarks).IsEqualTo(new WatermarkOffsets(10, 42));
        await Assert.That(consumer.GetWatermarkOffsets(partition)).IsEqualTo(watermarks);
        await Assert.That(consumer.GetCurrentLag(partition)).IsEqualTo(10);
    }

    private static async Task<ListOffsetsResponse> CreateListOffsetsResponseAsync(long timestamp, Task release)
    {
        await release.ConfigureAwait(false);
        var offset = timestamp == EarliestOffsetTimestamp ? 10 : 42;

        return new ListOffsetsResponse
        {
            Topics =
            [
                new ListOffsetsResponseTopic
                {
                    Name = Topic,
                    Partitions =
                    [
                        new ListOffsetsResponsePartition
                        {
                            PartitionIndex = Partition,
                            ErrorCode = ErrorCode.None,
                            Offset = offset
                        }
                    ]
                }
            ]
        };
    }

    private static ListOffsetsResponse CreateListOffsetsResponse(
        ListOffsetsRequest request,
        long? offset = null,
        ErrorCode errorCode = ErrorCode.None)
    {
        var timestamp = request.Topics[0].Partitions[0].Timestamp;
        var responseOffset = offset ?? (timestamp == EarliestOffsetTimestamp ? 10 : 42);
        return new ListOffsetsResponse
        {
            Topics =
            [
                new ListOffsetsResponseTopic
                {
                    Name = Topic,
                    Partitions =
                    [
                        new ListOffsetsResponsePartition
                        {
                            PartitionIndex = Partition,
                            ErrorCode = errorCode,
                            Offset = responseOffset
                        }
                    ]
                }
            ]
        };
    }

    private static MetadataResponse CreateMetadataResponse(Guid topicId = default) => new()
    {
        Brokers =
        [
            new BrokerMetadata
            {
                NodeId = 0,
                Host = "localhost",
                Port = 9092
            }
        ],
        Topics =
        [
            new TopicMetadata
            {
                Name = Topic,
                TopicId = topicId,
                ErrorCode = ErrorCode.None,
                Partitions =
                [
                    new PartitionMetadata
                    {
                        PartitionIndex = Partition,
                        LeaderId = 0,
                        LeaderEpoch = 3,
                        ErrorCode = ErrorCode.None,
                        ReplicaNodes = [0],
                        IsrNodes = [0]
                    }
                ]
            }
        ]
    };

    private static void SetInitialized(KafkaConsumer<string, string> consumer)
    {
        var initializedField = typeof(KafkaConsumer<string, string>)
            .GetField("_initialized", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("_initialized field not found - was it renamed?");

        initializedField.SetValue(consumer, true);
    }

    private static long AdvanceWatermarkUpdateSequence(KafkaConsumer<string, string> consumer)
    {
        var sequenceField = typeof(KafkaConsumer<string, string>)
            .GetField("_watermarkUpdateSequence", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("_watermarkUpdateSequence field not found.");
        var sequence = (long)sequenceField.GetValue(consumer)! + 1;
        sequenceField.SetValue(consumer, sequence);
        return sequence;
    }

    private static void UpdateCachedLagEndOffset(
        KafkaConsumer<string, string> consumer,
        TopicPartition partition,
        long lagEndOffset,
        long watermarkUpdateSequence)
    {
        var method = typeof(KafkaConsumer<string, string>).GetMethod(
            "UpdateCachedLagEndOffset",
            BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("UpdateCachedLagEndOffset method not found.");
        method.Invoke(consumer, [partition, lagEndOffset, watermarkUpdateSequence]);
    }

    private static async ValueTask InvokeHandleTopicIdentityChangesAsync(
        KafkaConsumer<string, string> consumer)
    {
        var method = typeof(KafkaConsumer<string, string>).GetMethod(
            "HandleTopicIdentityChangesAsync",
            BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("HandleTopicIdentityChangesAsync method not found.");
        var result = method.Invoke(consumer, [CancellationToken.None, null, Guid.Empty]);
        if (result is not ValueTask valueTask)
            throw new InvalidOperationException("HandleTopicIdentityChangesAsync returned unexpected type.");

        await valueTask.ConfigureAwait(false);
    }

    private sealed class LeaseTrackingConnection : IKafkaConnection, IRetirableKafkaConnection
    {
        private int _leaseCount;
        private int _leaseAcquisitionCount;
        private int _sendCount;

        public Func<ListOffsetsRequest, ValueTask<ListOffsetsResponse>>? SendHandler { get; set; }
        public int BrokerId => 0;
        public string Host => "localhost";
        public int Port => 9092;
        public bool IsConnected => true;
        public int LeaseCount => Volatile.Read(ref _leaseCount);
        public int LeaseAcquisitionCount => Volatile.Read(ref _leaseAcquisitionCount);
        public int SendCount => Volatile.Read(ref _sendCount);
        public int ActiveOperationCount => 0;

        public bool TryAcquireLease()
        {
            Interlocked.Increment(ref _leaseAcquisitionCount);
            Interlocked.Increment(ref _leaseCount);
            return true;
        }

        public void ReleaseLease() => Interlocked.Decrement(ref _leaseCount);
        public void BeginRetirement() { }
        public void CompleteRetirement() { }
        public ValueTask ConnectAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public async ValueTask<TResponse> SendAsync<TRequest, TResponse>(
            TRequest request,
            short apiVersion,
            CancellationToken cancellationToken = default)
            where TRequest : IKafkaRequest<TResponse>
            where TResponse : IKafkaResponse
        {
            if (request is not ListOffsetsRequest listOffsetsRequest
                || typeof(TResponse) != typeof(ListOffsetsResponse)
                || SendHandler is null)
            {
                throw new NotSupportedException();
            }

            Interlocked.Increment(ref _sendCount);
            var response = await SendHandler(listOffsetsRequest);
            return (TResponse)(object)response;
        }

        public ValueTask SendFireAndForgetAsync<TRequest, TResponse>(
            TRequest request,
            short apiVersion,
            CancellationToken cancellationToken = default)
            where TRequest : IKafkaRequest<TResponse>
            where TResponse : IKafkaResponse => throw new NotSupportedException();

        public Task<TResponse> SendPipelinedAsync<TRequest, TResponse>(
            TRequest request,
            short apiVersion,
            CancellationToken cancellationToken = default)
            where TRequest : IKafkaRequest<TResponse>
            where TResponse : IKafkaResponse => throw new NotSupportedException();

        public ValueTask SendFireAndForgetWithCallerTimeoutAsync<TRequest, TResponse>(
            TRequest request,
            short apiVersion,
            CancellationToken cancellationToken = default)
            where TRequest : IKafkaRequest<TResponse>
            where TResponse : IKafkaResponse => throw new NotSupportedException();

        public Task<TResponse> SendPipelinedWithCallerTimeoutAsync<TRequest, TResponse>(
            TRequest request,
            short apiVersion,
            CancellationToken cancellationToken = default)
            where TRequest : IKafkaRequest<TResponse>
            where TResponse : IKafkaResponse => throw new NotSupportedException();
    }
}
