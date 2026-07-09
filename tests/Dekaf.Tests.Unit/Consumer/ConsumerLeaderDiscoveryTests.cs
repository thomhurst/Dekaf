using System.Collections.Concurrent;
using System.Reflection;
using Dekaf.Consumer;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.Serialization;
using NSubstitute;

namespace Dekaf.Tests.Unit.Consumer;

public sealed class ConsumerLeaderDiscoveryTests
{
    private const string Topic = "test-topic";

    [Test]
    public async Task HandleLeaderEpochRefresh_WithInlineLeader_UpdatesMetadataWithoutRefresh()
    {
        var pool = Substitute.For<IConnectionPool>();
        await using var metadataManager = CreateMetadataManager(pool);
        await using var consumer = CreateConsumer(pool, metadataManager);

        SeedMetadata(metadataManager);

        var partitionResponse = new FetchResponsePartition
        {
            PartitionIndex = 0,
            ErrorCode = ErrorCode.NotLeaderOrFollower,
            CurrentLeader = new LeaderIdAndEpoch
            {
                LeaderId = 2,
                LeaderEpoch = 6
            }
        };

        await InvokeHandleLeaderEpochRefreshAsync(
            consumer,
            partitionResponse,
            [new NodeEndpoint { NodeId = 2, Host = "broker-2", Port = 9094 }]);

        var leader = metadataManager.Metadata.GetPartitionLeader(Topic, 0);

        await Assert.That(leader).IsNotNull();
        await Assert.That(leader!.NodeId).IsEqualTo(2);
        await Assert.That(leader.Host).IsEqualTo("broker-2");
        _ = pool.DidNotReceive().GetConnectionAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task HandleLeaderEpochRefresh_WithoutInlineLeader_DeduplicatesRefreshPerTopic()
    {
        var pool = Substitute.For<IConnectionPool>();
        var connectionRequested = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseConnection = new TaskCompletionSource<IKafkaConnection>(TaskCreationOptions.RunContinuationsAsynchronously);

        pool.GetConnectionAsync("localhost", 9092, Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                connectionRequested.TrySetResult();
                return new ValueTask<IKafkaConnection>(releaseConnection.Task);
            });

        await using var metadataManager = CreateMetadataManager(pool);
        await using var consumer = CreateConsumer(pool, metadataManager);

        var partitionResponse = new FetchResponsePartition
        {
            PartitionIndex = 0,
            ErrorCode = ErrorCode.NotLeaderOrFollower
        };

        await InvokeHandleLeaderEpochRefreshAsync(consumer, partitionResponse, []);
        // The background refresh signals connectionRequested when it calls GetConnectionAsync.
        // Await it directly: a 5s cap raced that continuation under CI thread-pool starvation.
        await connectionRequested.Task;

        await InvokeHandleLeaderEpochRefreshAsync(consumer, partitionResponse, []);

        _ = pool.Received(1).GetConnectionAsync("localhost", 9092, Arg.Any<CancellationToken>());

        releaseConnection.SetException(new InvalidOperationException("stop refresh"));
        await WaitForLeaderRefreshToDrainAsync(consumer);
    }

    [Test]
    public async Task HandleLeaderEpochRefresh_WithoutInlineLeader_UsesDedicatedRefreshToken()
    {
        var pool = Substitute.For<IConnectionPool>();
        var connection = Substitute.For<IKafkaConnection>();
        var refreshTokenCaptured = new TaskCompletionSource<CancellationToken>(TaskCreationOptions.RunContinuationsAsynchronously);
        var refreshResponse = new TaskCompletionSource<MetadataResponse>(TaskCreationOptions.RunContinuationsAsynchronously);

        pool.GetConnectionAsync("localhost", 9092, Arg.Any<CancellationToken>())
            .Returns(new ValueTask<IKafkaConnection>(connection));

        connection.SendAsync<MetadataRequest, MetadataResponse>(
                Arg.Any<MetadataRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                refreshTokenCaptured.TrySetResult((CancellationToken)callInfo[2]!);
                return new ValueTask<MetadataResponse>(refreshResponse.Task);
            });

        await using var metadataManager = CreateMetadataManager(pool);
        SetMetadataApiVersion(metadataManager);
        await using var consumer = CreateConsumer(pool, metadataManager);
        var partitionResponse = new FetchResponsePartition
        {
            PartitionIndex = 0,
            ErrorCode = ErrorCode.NotLeaderOrFollower
        };

        await InvokeHandleLeaderEpochRefreshAsync(consumer, partitionResponse, []);

        // The background refresh signals refreshTokenCaptured when it calls SendAsync.
        // Await directly: a 5s cap raced that continuation under CI thread-pool starvation.
        var refreshToken = await refreshTokenCaptured.Task;
        await Assert.That(refreshToken.IsCancellationRequested).IsFalse();

        refreshResponse.SetResult(CreateMetadataResponse());
        await WaitForLeaderRefreshToDrainAsync(consumer);
    }

    [Test]
    public async Task HandleLeaderEpochRefresh_WithoutInlineLeader_SchedulesRefreshWithoutFetchCycleToken()
    {
        var pool = Substitute.For<IConnectionPool>();
        var connection = Substitute.For<IKafkaConnection>();
        var refreshTokenCaptured = new TaskCompletionSource<CancellationToken>(TaskCreationOptions.RunContinuationsAsynchronously);
        var refreshResponse = new TaskCompletionSource<MetadataResponse>(TaskCreationOptions.RunContinuationsAsynchronously);

        pool.GetConnectionAsync("localhost", 9092, Arg.Any<CancellationToken>())
            .Returns(new ValueTask<IKafkaConnection>(connection));

        connection.SendAsync<MetadataRequest, MetadataResponse>(
                Arg.Any<MetadataRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                refreshTokenCaptured.TrySetResult((CancellationToken)callInfo[2]!);
                return new ValueTask<MetadataResponse>(refreshResponse.Task);
            });

        await using var metadataManager = CreateMetadataManager(pool);
        SetMetadataApiVersion(metadataManager);
        await using var consumer = CreateConsumer(pool, metadataManager);
        var partitionResponse = new FetchResponsePartition
        {
            PartitionIndex = 0,
            ErrorCode = ErrorCode.NotLeaderOrFollower
        };

        await InvokeHandleLeaderEpochRefreshAsync(consumer, partitionResponse, []);

        // The background refresh signals refreshTokenCaptured when it calls SendAsync.
        // Await directly: a 5s cap raced that continuation under CI thread-pool starvation.
        var refreshToken = await refreshTokenCaptured.Task;

        await Assert.That(refreshToken.IsCancellationRequested).IsFalse();

        refreshResponse.SetResult(CreateMetadataResponse());
        await WaitForLeaderRefreshToDrainAsync(consumer);
    }

    [Test]
    public async Task ResetToDivergingEpoch_RefetchesFromCurrentPosition()
    {
        var pool = Substitute.For<IConnectionPool>();
        await using var metadataManager = CreateMetadataManager(pool);
        await using var consumer = CreateConsumer(pool, metadataManager);
        consumer.IncrementalAssign([new TopicPartitionOffset(Topic, 0, 0)]);

        var partitionResponse = CreateDivergingEpochResponse();

        InvokeResetToDivergingEpoch(consumer, partitionResponse);

        await Assert.That(ClearFetchBufferForPendingCoordinatorRevocations(consumer)).IsTrue();
        await Assert.That(consumer.GetPosition(new TopicPartition(Topic, 0))).IsEqualTo(0);
        await Assert.That(GetLastConsumedLeaderEpoch(consumer)).IsEqualTo(-1);
        await Assert.That(DrainPendingFetchException(consumer)).IsNull();
    }

    [Test]
    public async Task QueueDivergingEpochResets_QueuesEveryPartitionBeforeInvalidatingEpoch()
    {
        var pool = Substitute.For<IConnectionPool>();
        await using var metadataManager = CreateMetadataManager(pool);
        await using var consumer = CreateConsumer(pool, metadataManager);
        consumer.IncrementalAssign(
        [
            new TopicPartitionOffset(Topic, 0, 10, leaderEpoch: 3),
            new TopicPartitionOffset(Topic, 1, 20, leaderEpoch: 4)
        ]);
        var fetchBufferEpoch = GetFetchBufferEpoch(consumer);

        InvokeResetToDivergingEpoch(
            consumer,
            CreateDivergingEpochResponse(partition: 0, epoch: 7, endOffset: 8),
            fetchBufferEpoch);
        InvokeResetToDivergingEpoch(
            consumer,
            CreateDivergingEpochResponse(partition: 1, epoch: 8, endOffset: 18),
            fetchBufferEpoch);

        await Assert.That(GetFetchBufferEpoch(consumer)).IsEqualTo(fetchBufferEpoch);
        InvokeCompleteDivergingEpochResets(consumer, fetchBufferEpoch);

        await Assert.That(GetFetchBufferEpoch(consumer)).IsEqualTo(fetchBufferEpoch + 1);
        await Assert.That(ClearFetchBufferForPendingCoordinatorRevocations(consumer)).IsTrue();
        await Assert.That(GetLastConsumedLeaderEpoch(consumer, partition: 0)).IsEqualTo(-1);
        await Assert.That(GetLastConsumedLeaderEpoch(consumer, partition: 1)).IsEqualTo(-1);
    }

    [Test]
    public async Task ResetToDivergingEpoch_ResumesAfterLastYieldedBufferedRecord()
    {
        var pool = Substitute.For<IConnectionPool>();
        await using var metadataManager = CreateMetadataManager(pool);
        await using var consumer = CreateConsumer(pool, metadataManager);
        consumer.IncrementalAssign([new TopicPartitionOffset(Topic, 0, 0)]);

        var pending = PendingFetchData.Create(Topic, 0, []);
        pending.TrackConsumed(9, messageBytes: 0);
        GetPendingFetches(consumer).Enqueue(pending);

        InvokeResetToDivergingEpoch(consumer, CreateDivergingEpochResponse());

        await Assert.That(ClearFetchBufferForPendingCoordinatorRevocations(consumer)).IsTrue();
        await Assert.That(consumer.GetPosition(new TopicPartition(Topic, 0))).IsEqualTo(10);
    }

    [Test]
    public async Task ResetToDivergingEpoch_DoesNotRewindDeliveredPosition()
    {
        var pool = Substitute.For<IConnectionPool>();
        await using var metadataManager = CreateMetadataManager(pool);
        await using var consumer = CreateConsumer(pool, metadataManager);
        consumer.IncrementalAssign([new TopicPartitionOffset(Topic, 0, 43)]);

        var partitionResponse = CreateDivergingEpochResponse();

        InvokeResetToDivergingEpoch(consumer, partitionResponse);

        await Assert.That(ClearFetchBufferForPendingCoordinatorRevocations(consumer)).IsTrue();
        await Assert.That(consumer.GetPosition(new TopicPartition(Topic, 0))).IsEqualTo(43);
        await Assert.That(GetLastConsumedLeaderEpoch(consumer)).IsEqualTo(-1);
        await Assert.That(DrainPendingFetchException(consumer)).IsNull();
    }

    [Test]
    public async Task ResetToDivergingEpoch_ExplicitSeekSupersedesPendingReset()
    {
        var pool = Substitute.For<IConnectionPool>();
        await using var metadataManager = CreateMetadataManager(pool);
        await using var consumer = CreateConsumer(pool, metadataManager);
        var partition = new TopicPartition(Topic, 0);
        consumer.IncrementalAssign([new TopicPartitionOffset(Topic, 0, 0)]);

        var partitionResponse = CreateDivergingEpochResponse();

        InvokeResetToDivergingEpoch(consumer, partitionResponse);
        consumer.Seek(new TopicPartitionOffset(Topic, 0, 5));

        await Assert.That(ClearFetchBufferForPendingCoordinatorRevocations(consumer)).IsTrue();
        await Assert.That(consumer.GetPosition(partition)).IsEqualTo(5);
    }

    [Test]
    public async Task ResetToDivergingEpoch_ReassignmentSupersedesPendingReset()
    {
        var pool = Substitute.For<IConnectionPool>();
        await using var metadataManager = CreateMetadataManager(pool);
        await using var consumer = CreateConsumer(pool, metadataManager);
        var partition = new TopicPartition(Topic, 0);
        consumer.IncrementalAssign([new TopicPartitionOffset(Topic, 0, 0)]);

        var partitionResponse = CreateDivergingEpochResponse();

        InvokeResetToDivergingEpoch(consumer, partitionResponse);
        consumer.IncrementalUnassign([partition]);
        consumer.IncrementalAssign([new TopicPartitionOffset(Topic, 0, 5)]);

        await Assert.That(ClearFetchBufferForPendingCoordinatorRevocations(consumer)).IsTrue();
        await Assert.That(consumer.GetPosition(partition)).IsEqualTo(5);
    }

    [Test]
    public async Task ResetToDivergingEpoch_ManualAssignSupersedesPendingReset()
    {
        var pool = Substitute.For<IConnectionPool>();
        await using var metadataManager = CreateMetadataManager(pool);
        await using var consumer = CreateConsumer(pool, metadataManager);
        var partition = new TopicPartition(Topic, 0);
        consumer.IncrementalAssign([new TopicPartitionOffset(Topic, 0, 0)]);

        var partitionResponse = CreateDivergingEpochResponse();

        InvokeResetToDivergingEpoch(consumer, partitionResponse);
        consumer.Assign(partition);

        await Assert.That(ClearFetchBufferForPendingCoordinatorRevocations(consumer)).IsTrue();
        await Assert.That(consumer.GetPosition(partition)).IsEqualTo(0);
    }

    [Test]
    public async Task DisposeAsync_WithInFlightLeaderRefresh_WaitsBeforeDisposingDependencies()
    {
        var pool = Substitute.For<IConnectionPool>();
        var connection = Substitute.For<IKafkaConnection>();
        var refreshStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var refreshCancellationObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var refreshResponse = new TaskCompletionSource<MetadataResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        var poolDisposeStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        pool.GetConnectionAsync("localhost", 9092, Arg.Any<CancellationToken>())
            .Returns(new ValueTask<IKafkaConnection>(connection));

        pool.DisposeAsync()
            .Returns(_ =>
            {
                poolDisposeStarted.TrySetResult();
                return ValueTask.CompletedTask;
            });

        connection.SendAsync<MetadataRequest, MetadataResponse>(
                Arg.Any<MetadataRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var cancellationToken = (CancellationToken)callInfo[2]!;
                cancellationToken.Register(static state =>
                    ((TaskCompletionSource)state!).TrySetResult(), refreshCancellationObserved);

                refreshStarted.TrySetResult();
                return new ValueTask<MetadataResponse>(refreshResponse.Task);
            });

        var metadataManager = CreateMetadataManager(pool);
        SetMetadataApiVersion(metadataManager);
        var consumer = CreateConsumer(pool, metadataManager);
        Task? disposeTask = null;

        try
        {
            var partitionResponse = new FetchResponsePartition
            {
                PartitionIndex = 0,
                ErrorCode = ErrorCode.NotLeaderOrFollower
            };

            await InvokeHandleLeaderEpochRefreshAsync(consumer, partitionResponse, []);
            // Each step below is gated by a deterministic signal (the mock's SendAsync, the
            // refresh cancellation callback, and the dispose completing after the refresh
            // response is set). The previous 5s caps raced those continuations under CI
            // thread-pool starvation; TUnit's test-level timeout is the backstop for a real hang.
            await refreshStarted.Task;

            disposeTask = consumer.DisposeAsync().AsTask();
            await refreshCancellationObserved.Task;

            await Assert.That(poolDisposeStarted.Task.IsCompleted).IsFalse();

            refreshResponse.SetResult(CreateMetadataResponse());
            await disposeTask;

            await Assert.That(poolDisposeStarted.Task.IsCompleted).IsTrue();
        }
        finally
        {
            refreshResponse.TrySetCanceled();

            if (disposeTask is not null)
                await disposeTask;
            else
                await consumer.DisposeAsync();
        }
    }

    private static MetadataManager CreateMetadataManager(IConnectionPool pool)
        => new(pool, ["localhost:9092"]);

    private static KafkaConsumer<string, string> CreateConsumer(
        IConnectionPool pool,
        MetadataManager metadataManager)
        => new(
            new ConsumerOptions
            {
                BootstrapServers = ["localhost:9092"],
                ClientId = "test-consumer"
            },
            Serializers.String,
            Serializers.String,
            pool,
            metadataManager);

    private static void SeedMetadata(MetadataManager metadataManager)
    {
        metadataManager.Metadata.Update(CreateMetadataResponse());
    }

    private static MetadataResponse CreateMetadataResponse()
        => new()
        {
            ClusterId = "test-cluster",
            ControllerId = 1,
            Brokers =
            [
                new BrokerMetadata { NodeId = 1, Host = "broker-1", Port = 9093 }
            ],
            Topics =
            [
                new TopicMetadata
                {
                    ErrorCode = ErrorCode.None,
                    Name = Topic,
                    Partitions =
                    [
                        new PartitionMetadata
                        {
                            ErrorCode = ErrorCode.None,
                            PartitionIndex = 0,
                            LeaderId = 1,
                            LeaderEpoch = 5,
                            ReplicaNodes = [1],
                            IsrNodes = [1]
                        }
                    ]
                }
            ]
        };

    private static void SetMetadataApiVersion(MetadataManager metadataManager)
    {
        var field = typeof(MetadataManager)
            .GetField("_metadataApiVersion", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("_metadataApiVersion field not found");

        field.SetValue(metadataManager, MetadataRequest.HighestSupportedVersion);
    }

    private static async Task WaitForLeaderRefreshToDrainAsync(KafkaConsumer<string, string> consumer)
    {
        var field = typeof(KafkaConsumer<string, string>)
            .GetField("_pendingLeaderRefreshTasks", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("_pendingLeaderRefreshTasks field not found");

        var pending = (ConcurrentDictionary<string, Task>)field.GetValue(consumer)!;
        if (pending.TryGetValue(Topic, out var task))
        {
            // Await the pending refresh directly. Every caller signals the mocked metadata
            // response (SetResult/SetException) before draining, so the task always completes.
            // A wall-clock cap here previously flaked when CI thread-pool starvation delayed the
            // completion continuation; TUnit's test-level timeout is the backstop for a real hang.
            await task.ConfigureAwait(false);
        }
    }

    private static FetchResponsePartition CreateDivergingEpochResponse(
        int partition = 0,
        int epoch = 7,
        long endOffset = 42) =>
        new()
        {
            PartitionIndex = partition,
            DivergingEpoch = new EpochEndOffset
            {
                Epoch = epoch,
                EndOffset = endOffset
            }
        };

    private static async ValueTask InvokeHandleLeaderEpochRefreshAsync(
        KafkaConsumer<string, string> consumer,
        FetchResponsePartition partitionResponse,
        IReadOnlyList<NodeEndpoint> nodeEndpoints)
    {
        var method = typeof(KafkaConsumer<string, string>)
            .GetMethod("HandleLeaderEpochRefreshAsync", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("HandleLeaderEpochRefreshAsync method not found");

        var result = method.Invoke(consumer, [Topic, partitionResponse, nodeEndpoints]);
        if (result is not ValueTask valueTask)
            throw new InvalidOperationException("HandleLeaderEpochRefreshAsync did not return ValueTask");

        await valueTask.ConfigureAwait(false);
    }

    private static void InvokeResetToDivergingEpoch(
        KafkaConsumer<string, string> consumer,
        FetchResponsePartition partitionResponse,
        int? fetchBufferEpoch = null)
    {
        var method = typeof(KafkaConsumer<string, string>)
            .GetMethod("ResetToDivergingEpoch", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("ResetToDivergingEpoch method not found");

        var epoch = fetchBufferEpoch ?? GetFetchBufferEpoch(consumer);
        method.Invoke(consumer, [Topic, partitionResponse, epoch]);

        if (fetchBufferEpoch is null)
            InvokeCompleteDivergingEpochResets(consumer, epoch);
    }

    private static void InvokeCompleteDivergingEpochResets(
        KafkaConsumer<string, string> consumer,
        int fetchBufferEpoch)
    {
        var method = typeof(KafkaConsumer<string, string>)
            .GetMethod("CompleteDivergingEpochResets", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("CompleteDivergingEpochResets method not found");

        method.Invoke(consumer, [fetchBufferEpoch]);
    }

    private static int GetFetchBufferEpoch(KafkaConsumer<string, string> consumer)
    {
        var field = typeof(KafkaConsumer<string, string>).GetField(
            "_fetchBufferEpoch",
            BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("_fetchBufferEpoch field not found.");

        return (int)field.GetValue(consumer)!;
    }

    private static Queue<PendingFetchData> GetPendingFetches(KafkaConsumer<string, string> consumer)
    {
        var field = typeof(KafkaConsumer<string, string>).GetField(
            "_pendingFetches",
            BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("_pendingFetches field not found.");

        return (Queue<PendingFetchData>)field.GetValue(consumer)!;
    }

    private static int GetLastConsumedLeaderEpoch(
        KafkaConsumer<string, string> consumer,
        int partition = 0)
    {
        var method = typeof(KafkaConsumer<string, string>).GetMethod(
            "GetLastConsumedLeaderEpoch",
            BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("GetLastConsumedLeaderEpoch method not found");

        return (int)method.Invoke(consumer, [new TopicPartition(Topic, partition)])!;
    }

    private static Exception? DrainPendingFetchException(KafkaConsumer<string, string> consumer)
    {
        var method = typeof(KafkaConsumer<string, string>)
            .GetMethod("ThrowPendingFetchException", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("ThrowPendingFetchException method not found");

        try
        {
            method.Invoke(consumer, []);
            return null;
        }
        catch (TargetInvocationException ex)
        {
            return ex.InnerException;
        }
    }

    private static bool ClearFetchBufferForPendingCoordinatorRevocations(
        KafkaConsumer<string, string> consumer)
    {
        var method = typeof(KafkaConsumer<string, string>).GetMethod(
            "ClearFetchBufferForPendingCoordinatorRevocations",
            BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException(
                "ClearFetchBufferForPendingCoordinatorRevocations method not found");

        return (bool)method.Invoke(consumer, [])!;
    }
}
