using Dekaf.Admin;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using NSubstitute;

namespace Dekaf.Tests.Unit.Admin;

public sealed class AdminLogDirsTests
{
    [Test]
    public async Task DescribeLogDirsAsync_FansOutToBrokersAndMapsResults()
    {
        await using var context = new AdminTestContext();
        context.EnqueueDescribe(DescribeResponse("/data-1", "topic-a", partition: 0, size: 100, offsetLag: 2, isFuture: false));
        context.EnqueueDescribe(DescribeResponse("/data-2", "topic-a", partition: 1, size: 200, offsetLag: 3, isFuture: true));

        var result = await context.Client.DescribeLogDirsAsync(
            [1, 2],
            [new TopicPartition("topic-a", 0)]);

        var requests = context.RequestsOfType<DescribeLogDirsRequest>();
        var brokerOneReplica = result[1]["/data-1"].ReplicaInfos[new TopicPartition("topic-a", 0)];
        var brokerTwoReplica = result[2]["/data-2"].ReplicaInfos[new TopicPartition("topic-a", 1)];
        var topics = requests[0].Topics!;

        await Assert.That(requests.Count).IsEqualTo(2);
        await Assert.That(requests[0].Topics).IsNotNull();
        await Assert.That(topics[0].Topic).IsEqualTo("topic-a");
        await Assert.That(topics[0].Partitions).IsEquivalentTo([0]);
        await Assert.That(brokerOneReplica.Size).IsEqualTo(100);
        await Assert.That(brokerOneReplica.OffsetLag).IsEqualTo(2);
        await Assert.That(brokerOneReplica.IsFuture).IsFalse();
        await Assert.That(brokerTwoReplica.Size).IsEqualTo(200);
        await Assert.That(brokerTwoReplica.OffsetLag).IsEqualTo(3);
        await Assert.That(brokerTwoReplica.IsFuture).IsTrue();
    }

    [Test]
    public async Task AlterReplicaLogDirsAsync_GroupsAssignmentsByBrokerAndDirectory()
    {
        await using var context = new AdminTestContext();
        context.EnqueueAlter(AlterResponse("topic-a", (0, ErrorCode.None), (1, ErrorCode.KafkaStorageError)));
        context.EnqueueAlter(AlterResponse("topic-b", (0, ErrorCode.None)));

        var brokerOneFirst = new TopicPartitionReplica("topic-a", 0, 1);
        var brokerOneSecond = new TopicPartitionReplica("topic-a", 1, 1);
        var brokerTwo = new TopicPartitionReplica("topic-b", 0, 2);

        var result = await context.Client.AlterReplicaLogDirsAsync(new Dictionary<TopicPartitionReplica, string>
        {
            [brokerOneFirst] = "/data-1",
            [brokerOneSecond] = "/data-2",
            [brokerTwo] = "/data-3"
        });

        var requests = context.RequestsOfType<AlterReplicaLogDirsRequest>();
        var brokerOneDirs = requests[0].Dirs.ToDictionary(static d => d.Path);

        await Assert.That(requests.Count).IsEqualTo(2);
        await Assert.That(brokerOneDirs.Keys).IsEquivalentTo(["/data-1", "/data-2"]);
        await Assert.That(brokerOneDirs["/data-1"].Topics[0].Name).IsEqualTo("topic-a");
        await Assert.That(brokerOneDirs["/data-1"].Topics[0].Partitions).IsEquivalentTo([0]);
        await Assert.That(brokerOneDirs["/data-2"].Topics[0].Partitions).IsEquivalentTo([1]);
        await Assert.That(result[brokerOneFirst].ErrorCode).IsEqualTo(ErrorCode.None);
        await Assert.That(result[brokerOneSecond].ErrorCode).IsEqualTo(ErrorCode.KafkaStorageError);
        await Assert.That(result[brokerTwo].ErrorCode).IsEqualTo(ErrorCode.None);
    }

    private static DescribeLogDirsResponse DescribeResponse(
        string logDir,
        string topicName,
        int partition,
        long size,
        long offsetLag,
        bool isFuture) => new()
        {
            ThrottleTimeMs = 0,
            ErrorCode = ErrorCode.None,
            Results =
            [
                new DescribeLogDirsResponseDir
                {
                    ErrorCode = ErrorCode.None,
                    LogDir = logDir,
                    TotalBytes = 10000,
                    UsableBytes = 9000,
                    IsCordoned = false,
                    Topics =
                    [
                        new DescribeLogDirsResponseTopic
                        {
                            Name = topicName,
                            Partitions =
                            [
                                new DescribeLogDirsResponsePartition
                                {
                                    PartitionIndex = partition,
                                    PartitionSize = size,
                                    OffsetLag = offsetLag,
                                    IsFutureKey = isFuture
                                }
                            ]
                        }
                    ]
                }
            ]
        };

    private static AlterReplicaLogDirsResponse AlterResponse(
        string topicName,
        params (int Partition, ErrorCode ErrorCode)[] partitions) => new()
        {
            ThrottleTimeMs = 0,
            Results =
            [
                new AlterReplicaLogDirsResponseTopic
                {
                    TopicName = topicName,
                    Partitions = partitions
                        .Select(static p => new AlterReplicaLogDirsResponsePartition
                        {
                            PartitionIndex = p.Partition,
                            ErrorCode = p.ErrorCode
                        })
                        .ToList()
                }
            ]
        };

    private sealed class AdminTestContext : IAsyncDisposable
    {
        private readonly IConnectionPool _pool;
        private readonly IKafkaConnection _connection;
        private readonly MetadataManager _metadataManager;
        private readonly Queue<DescribeLogDirsResponse> _describeResponses = new();
        private readonly Queue<AlterReplicaLogDirsResponse> _alterResponses = new();
        private readonly List<object> _requests = [];

        public AdminTestContext()
        {
            _connection = Substitute.For<IKafkaConnection>();
            _connection.BrokerId.Returns(1);
            _connection.Host.Returns("localhost");
            _connection.Port.Returns(9092);
            _connection.IsConnected.Returns(true);
            _connection
                .SendAsync<DescribeLogDirsRequest, DescribeLogDirsResponse>(
                    Arg.Any<DescribeLogDirsRequest>(),
                    Arg.Any<short>(),
                    Arg.Any<CancellationToken>())
                .Returns(callInfo =>
                {
                    _requests.Add(callInfo.ArgAt<DescribeLogDirsRequest>(0));
                    if (!_describeResponses.TryDequeue(out var response))
                    {
                        throw new InvalidOperationException($"No queued response for {nameof(DescribeLogDirsRequest)}.");
                    }

                    return new ValueTask<DescribeLogDirsResponse>(response);
                });
            _connection
                .SendAsync<AlterReplicaLogDirsRequest, AlterReplicaLogDirsResponse>(
                    Arg.Any<AlterReplicaLogDirsRequest>(),
                    Arg.Any<short>(),
                    Arg.Any<CancellationToken>())
                .Returns(callInfo =>
                {
                    _requests.Add(callInfo.ArgAt<AlterReplicaLogDirsRequest>(0));
                    if (!_alterResponses.TryDequeue(out var response))
                    {
                        throw new InvalidOperationException($"No queued response for {nameof(AlterReplicaLogDirsRequest)}.");
                    }

                    return new ValueTask<AlterReplicaLogDirsResponse>(response);
                });

            _pool = Substitute.For<IConnectionPool>();
            _pool.GetConnectionAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns(new ValueTask<IKafkaConnection>(_connection));
            _pool.GetConnectionByIndexAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns(new ValueTask<IKafkaConnection>(_connection));
            _pool.GetConnectionAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns(new ValueTask<IKafkaConnection>(_connection));

            _metadataManager = new MetadataManager(_pool, ["localhost:9092"]);
            _metadataManager.SetApiVersion(ApiKey.DescribeLogDirs, 1, 5);
            _metadataManager.SetApiVersion(ApiKey.AlterReplicaLogDirs, 1, 2);
            _metadataManager.Metadata.Update(new MetadataResponse
            {
                Brokers =
                [
                    new BrokerMetadata
                    {
                        NodeId = 1,
                        Host = "localhost",
                        Port = 9092
                    },
                    new BrokerMetadata
                    {
                        NodeId = 2,
                        Host = "localhost",
                        Port = 9093
                    }
                ],
                ClusterId = "test-cluster",
                ControllerId = 1,
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

        public void EnqueueDescribe(DescribeLogDirsResponse response) => _describeResponses.Enqueue(response);

        public void EnqueueAlter(AlterReplicaLogDirsResponse response) => _alterResponses.Enqueue(response);

        public IReadOnlyList<T> RequestsOfType<T>() => _requests.OfType<T>().ToArray();

        public async ValueTask DisposeAsync()
        {
            await Client.DisposeAsync().ConfigureAwait(false);
            await _metadataManager.DisposeAsync().ConfigureAwait(false);
            await _pool.DisposeAsync().ConfigureAwait(false);
        }
    }
}
