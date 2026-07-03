using System.Reflection;
using Dekaf.Consumer;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.Serialization;
using NSubstitute;

namespace Dekaf.Tests.Unit.Consumer;

public sealed class ConsumerRackAwarenessTests
{
    private const string Topic = "rack-aware-topic";
    private static readonly TopicPartition Partition = new(Topic, 0);

    [Test]
    public async Task FetchRequest_WithClientRack_SendsRackId()
    {
        var pool = Substitute.For<IConnectionPool>();
        var connection = Substitute.For<IKafkaConnection>();
        string? capturedRackId = null;

        connection.SendAsync<FetchRequest, FetchResponse>(
                Arg.Any<FetchRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                capturedRackId = callInfo.Arg<FetchRequest>().RackId;
                return ValueTask.FromResult(CreateFetchResponse(preferredReadReplica: -1));
            });

        pool.GetConnectionByIndexAsync(1, 0, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(connection));

        await using var metadataManager = CreateMetadataManager(pool);
        await using var consumer = CreateConsumer(pool, metadataManager, clientRack: "rack-a");

        await InvokeFetchFromBrokerAsync(consumer, brokerId: 1, [Partition]);

        await Assert.That(capturedRackId).IsEqualTo("rack-a");
    }

    [Test]
    public async Task PreferredReadReplica_RoutesNextFetchToPreferredBroker()
    {
        var pool = Substitute.For<IConnectionPool>();
        var connection = Substitute.For<IKafkaConnection>();

        connection.SendAsync<FetchRequest, FetchResponse>(
                Arg.Any<FetchRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(CreateFetchResponse(preferredReadReplica: 2)));

        pool.GetConnectionByIndexAsync(1, 0, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(connection));

        await using var metadataManager = CreateMetadataManager(pool);
        await using var consumer = CreateConsumer(pool, metadataManager, clientRack: "rack-a");
        consumer.Assign(Partition);

        var initialGroups = await InvokeGroupPartitionsByBrokerAsync(consumer);
        await Assert.That(initialGroups).ContainsKey(1);

        await InvokeFetchFromBrokerAsync(consumer, brokerId: 1, [Partition]);

        var preferredGroups = await InvokeGroupPartitionsByBrokerAsync(consumer);
        await Assert.That(preferredGroups).ContainsKey(2);
        await Assert.That(preferredGroups).DoesNotContainKey(1);
    }

    [Test]
    public async Task PreferredReadReplica_GroupingCache_RemainsActiveWithPreferredReplica()
    {
        var pool = Substitute.For<IConnectionPool>();
        var connection = Substitute.For<IKafkaConnection>();

        connection.SendAsync<FetchRequest, FetchResponse>(
                Arg.Any<FetchRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(CreateFetchResponse(preferredReadReplica: 2)));

        pool.GetConnectionByIndexAsync(1, 0, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(connection));

        await using var metadataManager = CreateMetadataManager(pool);
        await using var consumer = CreateConsumer(pool, metadataManager, clientRack: "rack-a");
        consumer.Assign(Partition);

        await InvokeFetchFromBrokerAsync(consumer, brokerId: 1, [Partition]);

        var preferredGroups = await InvokeGroupPartitionsByBrokerAsync(consumer);
        var cachedGroups = await InvokeGroupPartitionsByBrokerAsync(consumer);

        await Assert.That(cachedGroups).IsSameReferenceAs(preferredGroups);
        await Assert.That(cachedGroups).ContainsKey(2);
    }

    [Test]
    public async Task PreferredReadReplica_IncrementalUnassign_PrunesPreferredReplica()
    {
        var pool = Substitute.For<IConnectionPool>();
        var connection = Substitute.For<IKafkaConnection>();

        connection.SendAsync<FetchRequest, FetchResponse>(
                Arg.Any<FetchRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(CreateFetchResponse(preferredReadReplica: 2)));

        pool.GetConnectionByIndexAsync(1, 0, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(connection));

        await using var metadataManager = CreateMetadataManager(pool);
        await using var consumer = CreateConsumer(pool, metadataManager, clientRack: "rack-a");
        consumer.Assign(Partition);

        await InvokeFetchFromBrokerAsync(consumer, brokerId: 1, [Partition]);
        await Assert.That(GetPreferredReadReplicaCount(consumer)).IsEqualTo(1);

        consumer.IncrementalUnassign([Partition]);

        await Assert.That(GetPreferredReadReplicaCount(consumer)).IsEqualTo(0);
    }

    [Test]
    public async Task PreferredReadReplica_AssignClearsPreviousPreferredReplica()
    {
        var pool = Substitute.For<IConnectionPool>();
        var connection = Substitute.For<IKafkaConnection>();

        connection.SendAsync<FetchRequest, FetchResponse>(
                Arg.Any<FetchRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(CreateFetchResponse(preferredReadReplica: 2)));

        pool.GetConnectionByIndexAsync(1, 0, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(connection));

        await using var metadataManager = CreateMetadataManager(pool);
        await using var consumer = CreateConsumer(pool, metadataManager, clientRack: "rack-a");
        consumer.Assign(Partition);

        await InvokeFetchFromBrokerAsync(consumer, brokerId: 1, [Partition]);
        await Assert.That(GetPreferredReadReplicaCount(consumer)).IsEqualTo(1);

        consumer.Assign(Partition);

        await Assert.That(GetPreferredReadReplicaCount(consumer)).IsEqualTo(0);
    }

    [Test]
    public async Task PreferredReadReplica_FetchErrorFallsBackToLeader()
    {
        var pool = Substitute.For<IConnectionPool>();
        var leaderConnection = Substitute.For<IKafkaConnection>();
        var preferredConnection = Substitute.For<IKafkaConnection>();

        leaderConnection.SendAsync<FetchRequest, FetchResponse>(
                Arg.Any<FetchRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(CreateFetchResponse(preferredReadReplica: 2)));

        preferredConnection.SendAsync<FetchRequest, FetchResponse>(
                Arg.Any<FetchRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(CreateFetchResponse(
                preferredReadReplica: -1,
                errorCode: ErrorCode.UnknownServerError)));

        pool.GetConnectionByIndexAsync(1, 0, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(leaderConnection));
        pool.GetConnectionByIndexAsync(2, 0, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(preferredConnection));

        await using var metadataManager = CreateMetadataManager(pool);
        await using var consumer = CreateConsumer(pool, metadataManager, clientRack: "rack-a");
        consumer.Assign(Partition);

        await InvokeFetchFromBrokerAsync(consumer, brokerId: 1, [Partition]);
        var preferredGroups = await InvokeGroupPartitionsByBrokerAsync(consumer);
        await Assert.That(preferredGroups).ContainsKey(2);

        await InvokeFetchFromBrokerAsync(consumer, brokerId: 2, [Partition]);

        var fallbackGroups = await InvokeGroupPartitionsByBrokerAsync(consumer);
        await Assert.That(fallbackGroups).ContainsKey(1);
        await Assert.That(fallbackGroups).DoesNotContainKey(2);
    }

    [Test]
    public async Task PreferredReadReplica_BrokerFailureFallsBackToLeader()
    {
        var pool = Substitute.For<IConnectionPool>();
        var leaderConnection = Substitute.For<IKafkaConnection>();
        var preferredConnection = Substitute.For<IKafkaConnection>();

        leaderConnection.SendAsync<FetchRequest, FetchResponse>(
                Arg.Any<FetchRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(CreateFetchResponse(preferredReadReplica: 2)));

        preferredConnection.SendAsync<FetchRequest, FetchResponse>(
                Arg.Any<FetchRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => ValueTask.FromException<FetchResponse>(new InvalidOperationException("preferred broker unavailable")));

        pool.GetConnectionByIndexAsync(1, 0, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(leaderConnection));
        pool.GetConnectionByIndexAsync(2, 0, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(preferredConnection));

        await using var metadataManager = CreateMetadataManager(pool);
        await using var consumer = CreateConsumer(pool, metadataManager, clientRack: "rack-a");
        consumer.Assign(Partition);

        await InvokeFetchFromBrokerAsync(consumer, brokerId: 1, [Partition]);
        var preferredGroups = await InvokeGroupPartitionsByBrokerAsync(consumer);
        await Assert.That(preferredGroups).ContainsKey(2);

        await InvokeFetchFromBrokerWithErrorHandlingAsync(consumer, brokerId: 2, [Partition]);

        var fallbackGroups = await InvokeGroupPartitionsByBrokerAsync(consumer);
        await Assert.That(fallbackGroups).ContainsKey(1);
        await Assert.That(fallbackGroups).DoesNotContainKey(2);
    }

    [Test]
    public async Task PreferredReadReplica_TopLevelFetchErrorFallsBackToLeader()
    {
        var pool = Substitute.For<IConnectionPool>();
        var leaderConnection = Substitute.For<IKafkaConnection>();
        var preferredConnection = Substitute.For<IKafkaConnection>();

        leaderConnection.SendAsync<FetchRequest, FetchResponse>(
                Arg.Any<FetchRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(CreateFetchResponse(preferredReadReplica: 2)));

        preferredConnection.SendAsync<FetchRequest, FetchResponse>(
                Arg.Any<FetchRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(CreateFetchResponse(
                preferredReadReplica: -1,
                responseErrorCode: ErrorCode.UnknownServerError)));

        pool.GetConnectionByIndexAsync(1, 0, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(leaderConnection));
        pool.GetConnectionByIndexAsync(2, 0, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(preferredConnection));

        await using var metadataManager = CreateMetadataManager(pool);
        await using var consumer = CreateConsumer(pool, metadataManager, clientRack: "rack-a");
        consumer.Assign(Partition);

        await InvokeFetchFromBrokerAsync(consumer, brokerId: 1, [Partition]);
        var preferredGroups = await InvokeGroupPartitionsByBrokerAsync(consumer);
        await Assert.That(preferredGroups).ContainsKey(2);

        await InvokeFetchFromBrokerAsync(consumer, brokerId: 2, [Partition]);

        var fallbackGroups = await InvokeGroupPartitionsByBrokerAsync(consumer);
        await Assert.That(fallbackGroups).ContainsKey(1);
        await Assert.That(fallbackGroups).DoesNotContainKey(2);
    }

    [Test]
    public async Task PreferredReadReplica_WithoutClientRack_UsesLeader()
    {
        var pool = Substitute.For<IConnectionPool>();
        var connection = Substitute.For<IKafkaConnection>();

        connection.SendAsync<FetchRequest, FetchResponse>(
                Arg.Any<FetchRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(CreateFetchResponse(preferredReadReplica: 2)));

        pool.GetConnectionByIndexAsync(1, 0, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(connection));

        await using var metadataManager = CreateMetadataManager(pool);
        await using var consumer = CreateConsumer(pool, metadataManager, clientRack: null);
        consumer.Assign(Partition);

        await InvokeFetchFromBrokerAsync(consumer, brokerId: 1, [Partition]);

        var groups = await InvokeGroupPartitionsByBrokerAsync(consumer);
        await Assert.That(groups).ContainsKey(1);
        await Assert.That(groups).DoesNotContainKey(2);
    }

    private static MetadataManager CreateMetadataManager(IConnectionPool pool)
    {
        var metadataManager = new MetadataManager(pool, ["localhost:9092"]);
        metadataManager.SetApiVersion(ApiKey.Fetch, FetchRequest.LowestSupportedVersion, FetchRequest.HighestSupportedVersion);
        metadataManager.Metadata.Update(new MetadataResponse
        {
            ClusterId = "test-cluster",
            ControllerId = 1,
            Brokers =
            [
                new BrokerMetadata { NodeId = 1, Host = "broker-1", Port = 9093, Rack = "rack-b" },
                new BrokerMetadata { NodeId = 2, Host = "broker-2", Port = 9094, Rack = "rack-a" }
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
                            ReplicaNodes = [1, 2],
                            IsrNodes = [1, 2]
                        }
                    ]
                }
            ]
        });
        return metadataManager;
    }

    private static KafkaConsumer<string, string> CreateConsumer(
        IConnectionPool pool,
        MetadataManager metadataManager,
        string? clientRack)
        => new(
            new ConsumerOptions
            {
                BootstrapServers = ["localhost:9092"],
                ClientId = "test-consumer",
                ClientRack = clientRack,
                EnableFetchSessions = false
            },
            Serializers.String,
            Serializers.String,
            pool,
            metadataManager);

    private static FetchResponse CreateFetchResponse(
        int preferredReadReplica,
        ErrorCode errorCode = ErrorCode.None,
        ErrorCode responseErrorCode = ErrorCode.None)
        => new()
        {
            ErrorCode = responseErrorCode,
            Responses =
            [
                new FetchResponseTopic
                {
                    Topic = Topic,
                    Partitions =
                    [
                        new FetchResponsePartition
                        {
                            PartitionIndex = 0,
                            ErrorCode = errorCode,
                            HighWatermark = 0,
                            PreferredReadReplica = preferredReadReplica
                        }
                    ]
                }
            ]
        };

    private static async ValueTask InvokeFetchFromBrokerAsync(
        KafkaConsumer<string, string> consumer,
        int brokerId,
        List<TopicPartition> partitions)
    {
        var method = typeof(KafkaConsumer<string, string>)
            .GetMethod("FetchFromBrokerAsync", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("FetchFromBrokerAsync method not found");

        var result = method.Invoke(consumer, [brokerId, partitions, CancellationToken.None]);
        if (result is not ValueTask<List<PendingFetchData>?> valueTask)
            throw new InvalidOperationException("FetchFromBrokerAsync returned unexpected type");

        var pendingItems = await valueTask.ConfigureAwait(false);
        DisposeAndReturn(pendingItems);
    }

    private static async ValueTask InvokeFetchFromBrokerWithErrorHandlingAsync(
        KafkaConsumer<string, string> consumer,
        int brokerId,
        List<TopicPartition> partitions)
    {
        var method = typeof(KafkaConsumer<string, string>)
            .GetMethod("FetchFromBrokerWithErrorHandlingAsync", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("FetchFromBrokerWithErrorHandlingAsync method not found");

        var result = method.Invoke(consumer, [brokerId, partitions, CancellationToken.None, CancellationToken.None]);
        if (result is not Task<List<PendingFetchData>?> task)
            throw new InvalidOperationException("FetchFromBrokerWithErrorHandlingAsync returned unexpected type");

        var pendingItems = await task.ConfigureAwait(false);
        DisposeAndReturn(pendingItems);
    }

    private static void DisposeAndReturn(List<PendingFetchData>? pendingItems)
    {
        if (pendingItems is null)
            return;

        try
        {
            foreach (var pending in pendingItems)
                pending.Dispose();
        }
        finally
        {
            ConsumerFetchPools.ReturnPendingFetchDataList(pendingItems);
        }
    }

    private static async ValueTask<Dictionary<int, List<TopicPartition>>> InvokeGroupPartitionsByBrokerAsync(
        KafkaConsumer<string, string> consumer)
    {
        var method = typeof(KafkaConsumer<string, string>)
            .GetMethod("GroupPartitionsByBrokerAsync", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("GroupPartitionsByBrokerAsync method not found");

        var result = method.Invoke(consumer, [CancellationToken.None]);
        if (result is not ValueTask<Dictionary<int, List<TopicPartition>>> valueTask)
            throw new InvalidOperationException("GroupPartitionsByBrokerAsync returned unexpected type");

        return await valueTask.ConfigureAwait(false);
    }

    private static int GetPreferredReadReplicaCount(KafkaConsumer<string, string> consumer)
    {
        var field = typeof(KafkaConsumer<string, string>)
            .GetField("_preferredReadReplicas", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("_preferredReadReplicas field not found");

        var value = field.GetValue(consumer) ?? throw new InvalidOperationException("_preferredReadReplicas was null");
        return (int)(value.GetType().GetProperty("Count")?.GetValue(value)
            ?? throw new InvalidOperationException("_preferredReadReplicas Count property not found"));
    }
}
