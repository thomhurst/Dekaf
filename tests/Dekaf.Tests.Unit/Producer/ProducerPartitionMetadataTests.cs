using System.Reflection;
using Dekaf.Errors;
using Dekaf.Internal;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Producer;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.Serialization;
using Dekaf.Testing;
using NSubstitute;

namespace Dekaf.Tests.Unit.Producer;

public sealed class ProducerPartitionMetadataTests
{
    private const string Topic = "orders";

    [Test]
    public async Task GetPartitionsForAsync_CachedMetadata_ReturnsImmutableSnapshot()
    {
        await using var producer = CreateProducer(initialized: true);
        SeedMetadata(producer);

        var partitions = await producer.GetPartitionsForAsync(Topic);

        await Assert.That(partitions.Count).IsEqualTo(2);
        await Assert.That(partitions[0].TopicPartition).IsEqualTo(new TopicPartition(Topic, 0));
        await Assert.That(partitions[0].LeaderId).IsEqualTo(1);
        await Assert.That(partitions[0].LeaderEpoch).IsEqualTo(7);
        await Assert.That(partitions[0].ReplicaIds).IsEquivalentTo([1, 2]);
        await Assert.That(partitions[0].InSyncReplicaIds).IsEquivalentTo([1]);
        await Assert.That(partitions[0].OfflineReplicaIds).IsEquivalentTo([2]);
        await Assert.That(() => ((IList<int>)partitions[0].ReplicaIds)[0] = 99)
            .Throws<NotSupportedException>();
        await Assert.That(() => ((IList<ProducerPartitionMetadata>)partitions)[0] = partitions[1])
            .Throws<NotSupportedException>();
    }

    [Test]
    public async Task GetPartitionsForAsync_WithoutInitialize_ThrowsInvalidOperationException()
    {
        await using var producer = CreateProducer(initialized: false);

        await Assert.That(async () => await producer.GetPartitionsForAsync(Topic))
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task GetPartitionsForAsync_PreCanceled_ThrowsOperationCanceledException()
    {
        await using var producer = CreateProducer(initialized: true);
        SeedMetadata(producer);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.That(async () => await producer.GetPartitionsForAsync(Topic, cts.Token))
            .Throws<OperationCanceledException>();
    }

    [Test]
    public async Task GetPartitionsForAsync_CacheMiss_RefreshesMetadata()
    {
        await using var context = CreateNetworkProducer(
            _ => ValueTask.FromResult(CreateMetadataResponse()));

        var partitions = await context.Producer.GetPartitionsForAsync(Topic);

        await Assert.That(partitions.Count).IsEqualTo(2);
        await Assert.That(partitions[1].LeaderId).IsEqualTo(2);
        _ = context.Connection.Received(1).SendAsync<MetadataRequest, MetadataResponse>(
            Arg.Is<MetadataRequest>(request =>
                !request.AllowAutoTopicCreation &&
                request.Topics != null &&
                request.Topics.Count == 1 &&
                request.Topics[0].Name == Topic),
            Arg.Any<short>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task GetPartitionsForAsync_NonRetriableTopicError_ThrowsTypedKafkaException()
    {
        var response = CreateMetadataResponse(ErrorCode.TopicAuthorizationFailed);
        await using var context = CreateNetworkProducer(_ => ValueTask.FromResult(response));

        var exception = await Assert.That(async () => await context.Producer.GetPartitionsForAsync(Topic))
            .Throws<AuthorizationException>();

        await Assert.That(exception!.ErrorCode).IsEqualTo(ErrorCode.TopicAuthorizationFailed);
    }

    [Test]
    public async Task GetPartitionsForAsync_MaxBlockExpires_ThrowsMetadataTimeout()
    {
        await using var context = CreateNetworkProducer(
            static cancellationToken => new ValueTask<MetadataResponse>(WaitForCancellationAsync(cancellationToken)),
            maxBlockMs: 50);

        var exception = await Assert.That(async () => await context.Producer.GetPartitionsForAsync(Topic))
            .Throws<KafkaTimeoutException>();

        await Assert.That(exception!.TimeoutKind).IsEqualTo(TimeoutKind.Metadata);
    }

    [Test]
    public async Task GetPartitionsForAsync_AfterDispose_ThrowsObjectDisposedException()
    {
        var producer = CreateProducer(initialized: true);
        SeedMetadata(producer);
        await producer.DisposeAsync();

        await Assert.That(async () => await producer.GetPartitionsForAsync(Topic))
            .Throws<ObjectDisposedException>();
    }

    [Test]
    public async Task GetPartitionsForAsync_InMemoryProducer_ReturnsCompatibleImmutableSnapshot()
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic(Topic, partitionCount: 3);
        await using IKafkaProducer<string, string> producer = new InMemoryProducer<string, string>(cluster);

        var partitions = await producer.GetPartitionsForAsync(Topic);

        await Assert.That(partitions.Select(static partition => partition.TopicPartition.Partition))
            .IsEquivalentTo([0, 1, 2]);
        await Assert.That(partitions.All(static partition => partition.LeaderId == 0)).IsTrue();
        await Assert.That(partitions.All(static partition => partition.LeaderEpoch == 0)).IsTrue();
        await Assert.That(() => ((IList<ProducerPartitionMetadata>)partitions)[0] = partitions[1])
            .Throws<NotSupportedException>();
    }

    [Test]
    public async Task GetPartitionsForAsync_InMemoryProducer_UnknownTopicDoesNotCreateTopic()
    {
        var cluster = new InMemoryKafkaCluster();
        await using IKafkaProducer<string, string> producer = new InMemoryProducer<string, string>(cluster);

        var exception = await Assert.That(async () => await producer.GetPartitionsForAsync(Topic))
            .Throws<KafkaException>();

        await Assert.That(exception!.ErrorCode).IsEqualTo(ErrorCode.UnknownTopicOrPartition);
        await Assert.That(cluster.ListTopics()).IsEmpty();
    }

    private static KafkaProducer<string, string> CreateProducer(bool initialized)
    {
        var producer = (KafkaProducer<string, string>)Kafka.CreateProducer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .Build();
        SetField(producer, "_initialized", initialized);
        return producer;
    }

    private static void SeedMetadata(KafkaProducer<string, string> producer)
    {
        GetField<MetadataManager>(producer, "_metadataManager").Metadata.Update(CreateMetadataResponse());
    }

    private static MetadataResponse CreateMetadataResponse(ErrorCode topicError = ErrorCode.None)
    {
        PartitionMetadata[] partitions = topicError == ErrorCode.None
            ?
            [
                new PartitionMetadata
                {
                    ErrorCode = ErrorCode.None,
                    PartitionIndex = 0,
                    LeaderId = 1,
                    LeaderEpoch = 7,
                    ReplicaNodes = [1, 2],
                    IsrNodes = [1],
                    OfflineReplicas = [2]
                },
                new PartitionMetadata
                {
                    ErrorCode = ErrorCode.None,
                    PartitionIndex = 1,
                    LeaderId = 2,
                    LeaderEpoch = 8,
                    ReplicaNodes = [2, 1],
                    IsrNodes = [2, 1],
                    OfflineReplicas = []
                }
            ]
            : [];

        return new MetadataResponse
        {
            Brokers =
            [
                new BrokerMetadata { NodeId = 1, Host = "broker-a", Port = 9092 },
                new BrokerMetadata { NodeId = 2, Host = "broker-b", Port = 9093 }
            ],
            ClusterId = "cluster-a",
            ControllerId = 1,
            Topics =
            [
                new TopicMetadata
                {
                    ErrorCode = topicError,
                    Name = Topic,
                    Partitions = partitions
                }
            ]
        };
    }

    private static NetworkProducerContext CreateNetworkProducer(
        Func<CancellationToken, ValueTask<MetadataResponse>> responseFactory,
        int maxBlockMs = 1_000)
    {
        var connection = Substitute.For<IKafkaConnection>();
        connection.IsConnected.Returns(true);
        connection.Host.Returns("localhost");
        connection.Port.Returns(9092);
        connection.SendAsync<ApiVersionsRequest, ApiVersionsResponse>(
                Arg.Any<ApiVersionsRequest>(),
                Arg.Any<short>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(new ApiVersionsResponse
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
            .Returns(call => responseFactory(call.ArgAt<CancellationToken>(2)));

        var options = new ProducerOptions
        {
            BootstrapServers = ["localhost:9092"],
            ClientId = "partition-metadata-test",
            MaxBlockMs = maxBlockMs,
            CloseTimeoutMs = 1_000
        };
        var pool = new ConnectionPool(
            options.ClientId,
            new ConnectionOptions(),
            connectionsPerBroker: 1,
            connectionFactory: (_, _, _, _, _) => ValueTask.FromResult(connection));
        var metadataManager = new MetadataManager(
            pool,
            options.BootstrapServers,
            new MetadataOptions
            {
                EnableBackgroundRefresh = false,
                RetryBackoffMs = 1,
                RetryBackoffMaxMs = 1
            });
        metadataManager.SetApiVersion(ApiKey.Metadata, 0, MetadataRequest.HighestSupportedVersion);
        var producer = new KafkaProducer<string, string>(
            options,
            Serializers.String,
            Serializers.String,
            pool,
            metadataManager,
            DekafMemoryBudget.Global);
        SetField(producer, "_initialized", true);
        return new NetworkProducerContext(producer, metadataManager, pool, connection);
    }

    private static async Task<MetadataResponse> WaitForCancellationAsync(CancellationToken cancellationToken)
    {
        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        throw new InvalidOperationException("Cancellation did not stop metadata request.");
    }

    private static T GetField<T>(object target, string name) =>
        (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(target)!;

    private static void SetField<T>(object target, string name, T value) =>
        target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(target, value);

    private sealed class NetworkProducerContext(
        KafkaProducer<string, string> producer,
        MetadataManager metadataManager,
        ConnectionPool connectionPool,
        IKafkaConnection connection) : IAsyncDisposable
    {
        public KafkaProducer<string, string> Producer { get; } = producer;
        public IKafkaConnection Connection { get; } = connection;

        public async ValueTask DisposeAsync()
        {
            await Producer.DisposeAsync();
            await metadataManager.DisposeAsync();
            await connectionPool.DisposeAsync();
        }
    }
}
