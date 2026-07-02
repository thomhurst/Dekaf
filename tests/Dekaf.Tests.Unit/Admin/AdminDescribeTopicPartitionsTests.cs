using System.Collections.Concurrent;
using Dekaf.Admin;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;

namespace Dekaf.Tests.Unit.Admin;

public sealed class AdminDescribeTopicPartitionsTests
{
    [Test]
    public async Task DescribeTopicPartitionsPageAsync_SendsCursorAndReturnsNextCursor()
    {
        await using var context = new AdminTestContext();
        context.Connection.Enqueue(PageResponse(
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

        var request = context.Connection.SingleRequest<DescribeTopicPartitionsRequest>();
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
        context.Connection.Enqueue(PageResponse(
            "topic-a",
            partitionIndex: 0,
            nextCursor: new DescribeTopicPartitionsResponseCursor
            {
                TopicName = "topic-a",
                PartitionIndex = 1
            }));
        context.Connection.Enqueue(PageResponse(
            "topic-a",
            partitionIndex: 1,
            nextCursor: null));

        var result = await context.Client.DescribeTopicPartitionsAsync(
            ["topic-a"],
            new DescribeTopicPartitionsOptions
            {
                ResponsePartitionLimit = 1
            });

        var requests = context.Connection.RequestsOfType<DescribeTopicPartitionsRequest>();
        await Assert.That(requests.Count).IsEqualTo(2);
        await Assert.That(requests[0].Cursor).IsNull();
        await Assert.That(requests[1].Cursor).IsNotNull();
        await Assert.That(requests[1].Cursor!.PartitionIndex).IsEqualTo(1);
        await Assert.That(result["topic-a"].Partitions.Select(static p => p.PartitionIndex).ToArray())
            .IsEquivalentTo([0, 1]);
    }

    [Test]
    public async Task DescribeTopicPartitionsPageAsync_MapsElrAndAuthorizedOperations()
    {
        await using var context = new AdminTestContext();
        context.Connection.Enqueue(new DescribeTopicPartitionsResponse
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
        private readonly TestConnectionPool _pool;
        private readonly MetadataManager _metadataManager;

        public AdminTestContext()
        {
            _pool = new TestConnectionPool();
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
        public TestKafkaConnection Connection => _pool.Connection;

        public async ValueTask DisposeAsync()
        {
            await Client.DisposeAsync().ConfigureAwait(false);
            await _metadataManager.DisposeAsync().ConfigureAwait(false);
            await _pool.DisposeAsync().ConfigureAwait(false);
        }
    }

    private sealed class TestConnectionPool : IConnectionPool
    {
        public TestKafkaConnection Connection { get; } = new();

        public ValueTask<IKafkaConnection> GetConnectionAsync(int brokerId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<IKafkaConnection>(Connection);

        public ValueTask<IKafkaConnection> GetConnectionByIndexAsync(int brokerId, int index, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<IKafkaConnection>(Connection);

        public ValueTask<IKafkaConnection> GetConnectionAsync(string host, int port, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<IKafkaConnection>(Connection);

        public void RegisterBroker(int brokerId, string host, int port)
        {
        }

        public ValueTask<int> ScaleConnectionGroupAsync(int brokerId, int newCount, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(newCount);

        public ValueTask<IKafkaConnection?> ShrinkConnectionGroupAsync(int brokerId, int newCount, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<IKafkaConnection?>(null);

        public ValueTask RemoveConnectionAsync(int brokerId) => ValueTask.CompletedTask;

        public ValueTask CloseAllAsync() => ValueTask.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class TestKafkaConnection : IKafkaConnection
    {
        private readonly ConcurrentQueue<object> _responses = new();
        private readonly ConcurrentQueue<object> _requests = new();

        public int BrokerId => 0;
        public string Host => "localhost";
        public int Port => 9092;
        public bool IsConnected => true;

        public void Enqueue<TResponse>(TResponse response)
            where TResponse : IKafkaResponse =>
            _responses.Enqueue(response);

        public T SingleRequest<T>() => RequestsOfType<T>().Single();

        public IReadOnlyList<T> RequestsOfType<T>() =>
            _requests.ToArray().OfType<T>().ToArray();

        public ValueTask<TResponse> SendAsync<TRequest, TResponse>(
            TRequest request,
            short apiVersion,
            CancellationToken cancellationToken = default)
            where TRequest : IKafkaRequest<TResponse>
            where TResponse : IKafkaResponse
        {
            _requests.Enqueue(request);
            if (!_responses.TryDequeue(out var response))
            {
                throw new InvalidOperationException($"No queued response for {typeof(TRequest).Name}.");
            }

            return ValueTask.FromResult((TResponse)response);
        }

        public ValueTask SendFireAndForgetAsync<TRequest, TResponse>(
            TRequest request,
            short apiVersion,
            CancellationToken cancellationToken = default)
            where TRequest : IKafkaRequest<TResponse>
            where TResponse : IKafkaResponse =>
            ValueTask.CompletedTask;

        public Task<TResponse> SendPipelinedAsync<TRequest, TResponse>(
            TRequest request,
            short apiVersion,
            CancellationToken cancellationToken = default)
            where TRequest : IKafkaRequest<TResponse>
            where TResponse : IKafkaResponse =>
            Task.FromResult(default(TResponse)!);

        public ValueTask SendFireAndForgetWithCallerTimeoutAsync<TRequest, TResponse>(
            TRequest request,
            short apiVersion,
            CancellationToken cancellationToken = default)
            where TRequest : IKafkaRequest<TResponse>
            where TResponse : IKafkaResponse =>
            ValueTask.CompletedTask;

        public Task<TResponse> SendPipelinedWithCallerTimeoutAsync<TRequest, TResponse>(
            TRequest request,
            short apiVersion,
            CancellationToken cancellationToken = default)
            where TRequest : IKafkaRequest<TResponse>
            where TResponse : IKafkaResponse =>
            Task.FromResult(default(TResponse)!);

        public ValueTask ConnectAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
