using Dekaf.Admin;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using NSubstitute;

namespace Dekaf.Tests.Unit.Admin;

public sealed class AdminDescribeTopicPartitionsTests
{
    [Test]
    public async Task DescribeTopicPartitionsPageAsync_SendsCursorAndReturnsNextCursor()
    {
        await using var context = new AdminTestContext();
        context.Enqueue(PageResponse(
            "topic-a",
            partitionIndex: 5,
            nextCursor: new DescribeTopicPartitionsResponseCursor
            {
                TopicName = "topic-a",
                PartitionIndex = 6
            }));

        var page = await context.Client.DescribeTopicPartitionsPageAsync(
            ["topic-a"],
            new DescribeTopicPartitionsPageOptions
            {
                ResponsePartitionLimit = 1,
                Cursor = new DescribeTopicPartitionsCursor
                {
                    TopicName = "topic-a",
                    PartitionIndex = 5
                }
            });

        var request = context.SingleRequest<DescribeTopicPartitionsRequest>();
        await Assert.That(request.ResponsePartitionLimit).IsEqualTo(1);
        await Assert.That(request.Cursor).IsNotNull();
        await Assert.That(request.Cursor!.TopicName).IsEqualTo("topic-a");
        await Assert.That(request.Cursor.PartitionIndex).IsEqualTo(5);
        await Assert.That(page.NextCursor).IsNotNull();
        await Assert.That(page.NextCursor!.PartitionIndex).IsEqualTo(6);
        await Assert.That(page.Topics["topic-a"].Partitions[0].PartitionIndex).IsEqualTo(5);
    }

    [Test]
    public async Task DescribeTopicPartitionsAsync_FollowsCursorsAndMergesTopicPartitions()
    {
        await using var context = new AdminTestContext();
        context.Enqueue(PageResponse(
            "topic-a",
            partitionIndex: 0,
            nextCursor: new DescribeTopicPartitionsResponseCursor
            {
                TopicName = "topic-a",
                PartitionIndex = 1
            }));
        context.Enqueue(PageResponse(
            "topic-a",
            partitionIndex: 1,
            nextCursor: null));

        var result = await context.Client.DescribeTopicPartitionsAsync(
            ["topic-a"],
            new DescribeTopicPartitionsOptions
            {
                ResponsePartitionLimit = 1
            });

        var requests = context.RequestsOfType<DescribeTopicPartitionsRequest>();
        await Assert.That(requests.Count).IsEqualTo(2);
        await Assert.That(requests[0].Cursor).IsNull();
        await Assert.That(requests[1].Cursor).IsNotNull();
        await Assert.That(requests[1].Cursor!.PartitionIndex).IsEqualTo(1);
        await Assert.That(result["topic-a"].Partitions.Select(static p => p.PartitionIndex).ToArray())
            .IsEquivalentTo([0, 1]);
    }

    [Test]
    public async Task DescribeTopicPartitionsAsync_RepeatedCursor_ThrowsInvalidOperationException()
    {
        await using var context = new AdminTestContext();
        var repeatedCursor = new DescribeTopicPartitionsResponseCursor
        {
            TopicName = "topic-a",
            PartitionIndex = 1
        };
        context.Enqueue(PageResponse(
            "topic-a",
            partitionIndex: 0,
            nextCursor: repeatedCursor));
        context.Enqueue(PageResponse(
            "topic-a",
            partitionIndex: 1,
            nextCursor: repeatedCursor));

        async Task Act() => await context.Client.DescribeTopicPartitionsAsync(
            ["topic-a"],
            new DescribeTopicPartitionsOptions
            {
                ResponsePartitionLimit = 1
            });

        await Assert.That(Act).Throws<InvalidOperationException>();
        await Assert.That(context.RequestsOfType<DescribeTopicPartitionsRequest>().Count).IsEqualTo(2);
    }

    [Test]
    public async Task DescribeTopicPartitionsAsync_ValidatesResponsePartitionLimitWhenTopicsEmpty()
    {
        await using var context = new AdminTestContext();

        async Task Act() => await context.Client.DescribeTopicPartitionsAsync(
            [],
            new DescribeTopicPartitionsOptions
            {
                ResponsePartitionLimit = 0
            });

        await Assert.That(Act).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task DescribeTopicPartitionsPageAsync_ValidatesResponsePartitionLimitWhenTopicsEmpty()
    {
        await using var context = new AdminTestContext();

        async Task Act() => await context.Client.DescribeTopicPartitionsPageAsync(
            [],
            new DescribeTopicPartitionsPageOptions
            {
                ResponsePartitionLimit = 0
            });

        await Assert.That(Act).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task DescribeTopicPartitionsPageAsync_MapsElrAndAuthorizedOperations()
    {
        await using var context = new AdminTestContext();
        context.Enqueue(new DescribeTopicPartitionsResponse
        {
            ThrottleTimeMs = 3,
            Topics =
            [
                new DescribeTopicPartitionsResponseTopic
                {
                    ErrorCode = ErrorCode.None,
                    Name = "topic-a",
                    TopicId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                    IsInternal = true,
                    TopicAuthorizedOperations = 123,
                    Partitions =
                    [
                        new DescribeTopicPartitionsResponsePartition
                        {
                            ErrorCode = ErrorCode.None,
                            PartitionIndex = 0,
                            LeaderId = 1,
                            LeaderEpoch = 8,
                            ReplicaNodes = [1, 2],
                            IsrNodes = [1],
                            EligibleLeaderReplicas = [2],
                            LastKnownElr = [1],
                            OfflineReplicas = [2]
                        }
                    ]
                }
            ],
            NextCursor = null
        });

        var page = await context.Client.DescribeTopicPartitionsPageAsync(["topic-a"]);
        var topic = page.Topics["topic-a"];
        var partition = topic.Partitions[0];

        await Assert.That(page.ThrottleTimeMs).IsEqualTo(3);
        await Assert.That(topic.IsInternal).IsTrue();
        await Assert.That(topic.TopicAuthorizedOperations).IsEqualTo(123);
        await Assert.That(partition.EligibleLeaderReplicas).IsEquivalentTo([2]);
        await Assert.That(partition.LastKnownElr).IsEquivalentTo([1]);
        await Assert.That(partition.OfflineReplicas).IsEquivalentTo([2]);
    }

    [Test]
    public async Task DisposeAsync_WithInjectedResources_DoesNotDisposeExternalPool()
    {
        await using var context = new AdminTestContext();

        await context.Client.DisposeAsync();

        await context.AssertPoolNotDisposedAsync();
    }

    private static DescribeTopicPartitionsResponse PageResponse(
        string topicName,
        int partitionIndex,
        DescribeTopicPartitionsResponseCursor? nextCursor) => new()
        {
            ThrottleTimeMs = 0,
            Topics =
        [
            new DescribeTopicPartitionsResponseTopic
            {
                ErrorCode = ErrorCode.None,
                Name = topicName,
                TopicId = Guid.Empty,
                IsInternal = false,
                Partitions =
                [
                    new DescribeTopicPartitionsResponsePartition
                    {
                        ErrorCode = ErrorCode.None,
                        PartitionIndex = partitionIndex,
                        LeaderId = 0,
                        LeaderEpoch = -1,
                        ReplicaNodes = [0],
                        IsrNodes = [0],
                        EligibleLeaderReplicas = null,
                        LastKnownElr = null,
                        OfflineReplicas = []
                    }
                ]
            }
        ],
            NextCursor = nextCursor
        };

    private sealed class AdminTestContext : IAsyncDisposable
    {
        private readonly IConnectionPool _pool;
        private readonly IKafkaConnection _connection;
        private readonly MetadataManager _metadataManager;
        private readonly Queue<DescribeTopicPartitionsResponse> _responses = new();
        private readonly List<object> _requests = [];

        public AdminTestContext()
        {
            _connection = Substitute.For<IKafkaConnection>();
            _connection.BrokerId.Returns(0);
            _connection.Host.Returns("localhost");
            _connection.Port.Returns(9092);
            _connection.IsConnected.Returns(true);
            _connection
                .SendAsync<DescribeTopicPartitionsRequest, DescribeTopicPartitionsResponse>(
                    Arg.Any<DescribeTopicPartitionsRequest>(),
                    Arg.Any<short>(),
                    Arg.Any<CancellationToken>())
                .Returns(callInfo =>
                {
                    var request = callInfo.ArgAt<DescribeTopicPartitionsRequest>(0);
                    _requests.Add(request);
                    if (!_responses.TryDequeue(out var response))
                    {
                        throw new InvalidOperationException($"No queued response for {nameof(DescribeTopicPartitionsRequest)}.");
                    }

                    return new ValueTask<DescribeTopicPartitionsResponse>((DescribeTopicPartitionsResponse)response);
                });

            _pool = Substitute.For<IConnectionPool>();
            _pool.GetConnectionAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns(new ValueTask<IKafkaConnection>(_connection));
            _pool.GetConnectionByIndexAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns(new ValueTask<IKafkaConnection>(_connection));
            _pool.GetConnectionAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns(new ValueTask<IKafkaConnection>(_connection));

            _metadataManager = new MetadataManager(_pool, ["localhost:9092"]);
            _metadataManager.SetApiVersion(ApiKey.DescribeTopicPartitions, 0, 0);
            _metadataManager.Metadata.Update(new MetadataResponse
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
                ClusterId = "test-cluster",
                ControllerId = 0,
                Topics = []
            });

            Client = new AdminClient(
                new AdminClientOptions
                {
                    BootstrapServers = ["localhost:9092"]
                },
                _pool,
                _metadataManager);
        }

        public AdminClient Client { get; }

        public void Enqueue(DescribeTopicPartitionsResponse response) => _responses.Enqueue(response);

        public T SingleRequest<T>() => RequestsOfType<T>().Single();

        public IReadOnlyList<T> RequestsOfType<T>() => _requests.OfType<T>().ToArray();

        public async ValueTask AssertPoolNotDisposedAsync() => await _pool.DidNotReceive().DisposeAsync();

        public async ValueTask DisposeAsync()
        {
            await Client.DisposeAsync().ConfigureAwait(false);
            await _metadataManager.DisposeAsync().ConfigureAwait(false);
            await _pool.DisposeAsync().ConfigureAwait(false);
        }
    }
}
