using Dekaf.Admin;
using Dekaf.Errors;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using NSubstitute;

namespace Dekaf.Tests.Unit.Admin;

public sealed class AdminClientTopicIdTests
{
    private static readonly Guid TopicId = new("00112233-4455-6677-8899-aabbccddeeff");
    private static readonly Guid UnknownTopicId = new("ffeeddcc-bbaa-9988-7766-554433221100");

    [Test]
    public async Task DescribeTopicsAsync_EmptyIds_DoesNotSendRequest()
    {
        await using var context = new AdminTestContext();

        var result = await context.Client.DescribeTopicsAsync(Array.Empty<Guid>());

        await Assert.That(result).IsEmpty();
        await Assert.That(context.MetadataRequests).IsEmpty();
    }

    [Test]
    public async Task DescribeTopicsAsync_DeduplicatesIdsAndUsesMetadataV10()
    {
        await using var context = new AdminTestContext();
        context.EnqueueMetadataResponse(MetadataResponseFor(
            Topic("orders", TopicId, ErrorCode.None)));

        var result = await context.Client.DescribeTopicsAsync([TopicId, TopicId]);

        var request = context.MetadataRequests.Single();
        await Assert.That(context.MetadataVersions.Single()).IsEqualTo((short)13);
        await Assert.That(request.AllowAutoTopicCreation).IsFalse();
        await Assert.That(request.Topics!.Count).IsEqualTo(1);
        await Assert.That(request.Topics![0].TopicId).IsEqualTo(TopicId);
        await Assert.That(request.Topics[0].Name).IsNull();
        await Assert.That(result[TopicId].Name).IsEqualTo("orders");
        await Assert.That(result[TopicId].TopicId).IsEqualTo(TopicId);
    }

    [Test]
    public async Task DescribeTopicsAsync_PreservesPartialErrorsAndUnknownIds()
    {
        await using var context = new AdminTestContext();
        context.EnqueueMetadataResponse(MetadataResponseFor(
            Topic("orders", TopicId, ErrorCode.TopicAuthorizationFailed),
            Topic(string.Empty, UnknownTopicId, ErrorCode.UnknownTopicId)));

        var result = await context.Client.DescribeTopicsAsync([TopicId, UnknownTopicId]);

        await Assert.That(result.Count).IsEqualTo(2);
        await Assert.That(result[TopicId].Name).IsEqualTo("orders");
        await Assert.That(result[TopicId].ErrorCode).IsEqualTo(ErrorCode.TopicAuthorizationFailed);
        await Assert.That(result[UnknownTopicId].Name).IsEmpty();
        await Assert.That(result[UnknownTopicId].TopicId).IsEqualTo(UnknownTopicId);
        await Assert.That(result[UnknownTopicId].ErrorCode).IsEqualTo(ErrorCode.UnknownTopicId);
    }

    [Test]
    public async Task DescribeTopicsAsync_BrokerBeforeMetadataV10_ThrowsBrokerVersionException()
    {
        await using var context = new AdminTestContext(metadataMaxVersion: 9);

        async Task Act() => await context.Client.DescribeTopicsAsync([TopicId]);

        await Assert.That(Act).Throws<BrokerVersionException>();
        await Assert.That(context.MetadataRequests).IsEmpty();
    }

    [Test]
    public async Task DescribeTopicsAsync_CanceledToken_DoesNotSendRequest()
    {
        await using var context = new AdminTestContext();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        async Task Act() => await context.Client.DescribeTopicsAsync([TopicId], cancellation.Token);

        await Assert.That(Act).Throws<OperationCanceledException>();
        await Assert.That(context.MetadataRequests).IsEmpty();
    }

    [Test]
    public async Task DeleteTopicsAsync_EmptyIds_DoesNotSendRequest()
    {
        await using var context = new AdminTestContext();

        await context.Client.DeleteTopicsAsync(Array.Empty<Guid>());

        await Assert.That(context.DeleteRequests).IsEmpty();
    }

    [Test]
    public async Task DeleteTopicsAsync_DeduplicatesIdsAndPassesTimeout()
    {
        await using var context = new AdminTestContext();
        context.EnqueueDeleteResponse(DeleteResponse(
            DeleteResult("orders", TopicId, ErrorCode.None)));

        await context.Client.DeleteTopicsAsync(
            [TopicId, TopicId],
            new DeleteTopicsOptions { TimeoutMs = 12_345 });

        var request = context.DeleteRequests.Single();
        await Assert.That(context.DeleteVersions.Single()).IsEqualTo((short)6);
        await Assert.That(request.TimeoutMs).IsEqualTo(12_345);
        await Assert.That(request.TopicNames).IsNull();
        await Assert.That(request.Topics!.Count).IsEqualTo(1);
        await Assert.That(request.Topics![0].Name).IsNull();
        await Assert.That(request.Topics[0].TopicId).IsEqualTo(TopicId);
    }

    [Test]
    public async Task DeleteTopicsAsync_PartialError_ThrowsWithBrokerIdentity()
    {
        await using var context = new AdminTestContext();
        context.EnqueueDeleteResponse(DeleteResponse(
            DeleteResult("orders", TopicId, ErrorCode.None),
            DeleteResult("missing", UnknownTopicId, ErrorCode.InvalidRequest, "cannot delete")));

        var exception = await Assert.ThrowsAsync<KafkaException>(async () =>
            await context.Client.DeleteTopicsAsync([TopicId, UnknownTopicId]));

        await Assert.That(exception!.ErrorCode).IsEqualTo(ErrorCode.InvalidRequest);
        await Assert.That(exception.Message).Contains("missing");
        await Assert.That(exception.Message).Contains(UnknownTopicId.ToString());
    }

    [Test]
    public async Task DeleteTopicsAsync_PartialRetriableError_RetriesOnlyUnresolvedIds()
    {
        await using var context = new AdminTestContext();
        context.EnqueueDeleteResponse(DeleteResponse(
            DeleteResult("orders", TopicId, ErrorCode.None),
            DeleteResult("pending", UnknownTopicId, ErrorCode.RequestTimedOut)));
        context.EnqueueDeleteResponse(DeleteResponse(
            DeleteResult("pending", UnknownTopicId, ErrorCode.None)));

        await context.Client.DeleteTopicsAsync([TopicId, UnknownTopicId]);

        await Assert.That(context.DeleteRequests.Count).IsEqualTo(2);
        await Assert.That(context.DeleteRequests[0].Topics!.Select(topic => topic.TopicId))
            .IsEquivalentTo([TopicId, UnknownTopicId]);
        await Assert.That(context.DeleteRequests[1].Topics!.Select(topic => topic.TopicId))
            .IsEquivalentTo([UnknownTopicId]);
    }

    [Test]
    public async Task DeleteTopicsAsync_BrokerBeforeV6_ThrowsBrokerVersionException()
    {
        await using var context = new AdminTestContext(deleteMaxVersion: 5);

        async Task Act() => await context.Client.DeleteTopicsAsync([TopicId]);

        await Assert.That(Act).Throws<BrokerVersionException>();
        await Assert.That(context.DeleteRequests).IsEmpty();
    }

    [Test]
    public async Task DeleteTopicsAsync_CanceledToken_DoesNotSendRequest()
    {
        await using var context = new AdminTestContext();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        async Task Act() => await context.Client.DeleteTopicsAsync([TopicId], cancellationToken: cancellation.Token);

        await Assert.That(Act).Throws<OperationCanceledException>();
        await Assert.That(context.DeleteRequests).IsEmpty();
    }

    [Test]
    public async Task TopicIdOperations_RejectEmptyUuid()
    {
        await using var context = new AdminTestContext();

        async Task Describe() => await context.Client.DescribeTopicsAsync([Guid.Empty]);
        async Task Delete() => await context.Client.DeleteTopicsAsync([Guid.Empty]);

        await Assert.That(Describe).Throws<ArgumentException>();
        await Assert.That(Delete).Throws<ArgumentException>();
    }

    [Test]
    public async Task TopicIdExtensions_CustomClientWithoutCapability_ThrowsNotSupportedException()
    {
        var admin = Substitute.For<IAdminClient>();

        async Task Describe() => await admin.DescribeTopicsAsync([TopicId]);
        async Task Delete() => await admin.DeleteTopicsAsync([TopicId]);

        await Assert.That(Describe).Throws<NotSupportedException>();
        await Assert.That(Delete).Throws<NotSupportedException>();
    }

    private static TopicMetadata Topic(string name, Guid topicId, ErrorCode errorCode) => new()
    {
        Name = name,
        TopicId = topicId,
        ErrorCode = errorCode,
        Partitions = errorCode == ErrorCode.None
            ?
            [
                new PartitionMetadata
                {
                    PartitionIndex = 0,
                    LeaderId = 1,
                    LeaderEpoch = 3,
                    ReplicaNodes = [1],
                    IsrNodes = [1],
                    OfflineReplicas = [],
                    ErrorCode = ErrorCode.None
                }
            ]
            : []
    };

    private static MetadataResponse MetadataResponseFor(params TopicMetadata[] topics) => new()
    {
        Brokers = [Broker()],
        ClusterId = "test-cluster",
        ControllerId = 1,
        Topics = topics
    };

    private static DeleteTopicsResponseTopic DeleteResult(
        string name,
        Guid topicId,
        ErrorCode errorCode,
        string? errorMessage = null) => new()
    {
        Name = name,
        TopicId = topicId,
        ErrorCode = errorCode,
        ErrorMessage = errorMessage
    };

    private static DeleteTopicsResponse DeleteResponse(params DeleteTopicsResponseTopic[] topics) => new()
    {
        Responses = topics
    };

    private static BrokerMetadata Broker() => new()
    {
        NodeId = 1,
        Host = "localhost",
        Port = 9092
    };

    private sealed class AdminTestContext : IAsyncDisposable
    {
        private readonly IKafkaConnection _connection;
        private readonly IConnectionPool _pool;
        private readonly MetadataManager _metadataManager;
        private readonly Queue<MetadataResponse> _metadataResponses = new();
        private readonly Queue<DeleteTopicsResponse> _deleteResponses = new();

        public AdminTestContext(short metadataMaxVersion = 13, short deleteMaxVersion = 6)
        {
            _connection = Substitute.For<IKafkaConnection>();
            _connection.BrokerId.Returns(1);
            _connection.Host.Returns("localhost");
            _connection.Port.Returns(9092);
            _connection.IsConnected.Returns(true);
            _connection.SendAsync<MetadataRequest, MetadataResponse>(
                    Arg.Any<MetadataRequest>(),
                    Arg.Any<short>(),
                    Arg.Any<CancellationToken>())
                .Returns(callInfo =>
                {
                    MetadataRequests.Add(callInfo.ArgAt<MetadataRequest>(0));
                    MetadataVersions.Add(callInfo.ArgAt<short>(1));
                    return ValueTask.FromResult(_metadataResponses.Count > 0
                        ? _metadataResponses.Dequeue()
                        : MetadataResponseFor());
                });
            _connection.SendAsync<DeleteTopicsRequest, DeleteTopicsResponse>(
                    Arg.Any<DeleteTopicsRequest>(),
                    Arg.Any<short>(),
                    Arg.Any<CancellationToken>())
                .Returns(callInfo =>
                {
                    DeleteRequests.Add(callInfo.ArgAt<DeleteTopicsRequest>(0));
                    DeleteVersions.Add(callInfo.ArgAt<short>(1));
                    if (!_deleteResponses.TryDequeue(out var response))
                        throw new InvalidOperationException("No queued DeleteTopics response.");

                    return ValueTask.FromResult(response);
                });
            _connection.SendAsync<ApiVersionsRequest, ApiVersionsResponse>(
                    Arg.Any<ApiVersionsRequest>(),
                    Arg.Any<short>(),
                    Arg.Any<CancellationToken>())
                .Returns(ValueTask.FromResult(new ApiVersionsResponse
                {
                    ErrorCode = ErrorCode.None,
                    ApiKeys =
                    [
                        new ApiVersion(ApiKey.Metadata, 5, metadataMaxVersion),
                        new ApiVersion(ApiKey.DeleteTopics, 4, deleteMaxVersion)
                    ]
                }));

            _pool = Substitute.For<IConnectionPool>();
            _pool.GetConnectionAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns(ValueTask.FromResult(_connection));
            _pool.GetConnectionByIndexAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns(ValueTask.FromResult(_connection));
            _pool.GetConnectionAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns(ValueTask.FromResult(_connection));

            _metadataManager = new MetadataManager(_pool, ["localhost:9092"]);
            _metadataManager.SetApiVersion(ApiKey.Metadata, 5, metadataMaxVersion);
            _metadataManager.SetApiVersion(ApiKey.DeleteTopics, 4, deleteMaxVersion);
            _metadataManager.Metadata.Update(MetadataResponseFor());

            Client = new AdminClient(
                new AdminClientOptions { BootstrapServers = ["localhost:9092"] },
                _pool,
                _metadataManager);
        }

        public AdminClient Client { get; }
        public List<MetadataRequest> MetadataRequests { get; } = [];
        public List<short> MetadataVersions { get; } = [];
        public List<DeleteTopicsRequest> DeleteRequests { get; } = [];
        public List<short> DeleteVersions { get; } = [];

        public void EnqueueMetadataResponse(MetadataResponse response) => _metadataResponses.Enqueue(response);
        public void EnqueueDeleteResponse(DeleteTopicsResponse response) => _deleteResponses.Enqueue(response);

        public async ValueTask DisposeAsync()
        {
            await Client.DisposeAsync().ConfigureAwait(false);
            await _metadataManager.DisposeAsync().ConfigureAwait(false);
            await _pool.DisposeAsync().ConfigureAwait(false);
        }
    }
}
