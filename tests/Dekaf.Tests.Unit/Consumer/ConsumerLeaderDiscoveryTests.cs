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
    public async Task HandleNotLeaderOrFollower_WithInlineLeader_UpdatesMetadataWithoutRefresh()
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

        await InvokeHandleNotLeaderOrFollowerAsync(
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
    public async Task HandleNotLeaderOrFollower_WithoutInlineLeader_DeduplicatesRefreshPerTopic()
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

        await InvokeHandleNotLeaderOrFollowerAsync(consumer, partitionResponse, []);
        await connectionRequested.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await InvokeHandleNotLeaderOrFollowerAsync(consumer, partitionResponse, []);

        _ = pool.Received(1).GetConnectionAsync("localhost", 9092, Arg.Any<CancellationToken>());

        releaseConnection.SetException(new InvalidOperationException("stop refresh"));
        await WaitForLeaderRefreshToDrainAsync(consumer);
    }

    [Test]
    public async Task HandleNotLeaderOrFollower_WithoutInlineLeader_UsesDedicatedRefreshToken()
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

        await InvokeHandleNotLeaderOrFollowerAsync(consumer, partitionResponse, []);

        var refreshToken = await refreshTokenCaptured.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.That(refreshToken.IsCancellationRequested).IsFalse();

        refreshResponse.SetResult(CreateMetadataResponse());
        await WaitForLeaderRefreshToDrainAsync(consumer);
    }

    [Test]
    public async Task HandleNotLeaderOrFollower_WithoutInlineLeader_SchedulesRefreshWithoutFetchCycleToken()
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

        await InvokeHandleNotLeaderOrFollowerAsync(consumer, partitionResponse, []);

        var refreshToken = await refreshTokenCaptured.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await Assert.That(refreshToken.IsCancellationRequested).IsFalse();

        refreshResponse.SetResult(CreateMetadataResponse());
        await WaitForLeaderRefreshToDrainAsync(consumer);
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

            await InvokeHandleNotLeaderOrFollowerAsync(consumer, partitionResponse, []);
            await refreshStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

            disposeTask = consumer.DisposeAsync().AsTask();
            await refreshCancellationObserved.Task.WaitAsync(TimeSpan.FromSeconds(5));

            await Assert.That(poolDisposeStarted.Task.IsCompleted).IsFalse();

            refreshResponse.SetResult(CreateMetadataResponse());
            await disposeTask.WaitAsync(TimeSpan.FromSeconds(5));

            await Assert.That(poolDisposeStarted.Task.IsCompleted).IsTrue();
        }
        finally
        {
            refreshResponse.TrySetCanceled();

            if (disposeTask is not null)
                await disposeTask.WaitAsync(TimeSpan.FromSeconds(5));
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
            await task.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        }
    }

    private static async ValueTask InvokeHandleNotLeaderOrFollowerAsync(
        KafkaConsumer<string, string> consumer,
        FetchResponsePartition partitionResponse,
        IReadOnlyList<NodeEndpoint> nodeEndpoints)
    {
        var method = typeof(KafkaConsumer<string, string>)
            .GetMethod("HandleNotLeaderOrFollowerAsync", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("HandleNotLeaderOrFollowerAsync method not found");

        var result = method.Invoke(consumer, [Topic, partitionResponse, nodeEndpoints]);
        if (result is not ValueTask valueTask)
            throw new InvalidOperationException("HandleNotLeaderOrFollowerAsync did not return ValueTask");

        await valueTask.ConfigureAwait(false);
    }
}
