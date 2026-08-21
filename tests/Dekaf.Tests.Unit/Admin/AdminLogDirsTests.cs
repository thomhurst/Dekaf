using System.Collections.Concurrent;
using Dekaf.Admin;
using Dekaf.Errors;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using NSubstitute;

namespace Dekaf.Tests.Unit.Admin;

public sealed class AdminLogDirsTests
{
    [Test]
    public async Task DescribeReplicaLogDirsAsync_MapsCurrentFutureAndMissingReplicas()
    {
        await using var context = new AdminTestContext();
        context.EnqueueDescribe(new DescribeLogDirsResponse
        {
            ErrorCode = ErrorCode.None,
            Results =
            [
                DescribeDirectory("/data-current", "topic-a", partition: 0, offsetLag: 2, isFuture: false),
                DescribeDirectory("/data-future", "topic-a", partition: 0, offsetLag: 7, isFuture: true)
            ]
        });
        var existing = new TopicPartitionReplica("topic-a", 0, 1);
        var missing = new TopicPartitionReplica("topic-a", 1, 1);

        var result = await context.Client.DescribeReplicaLogDirsAsync([existing, missing]);

        var request = context.RequestsOfType<DescribeLogDirsRequest>().Single();
        await Assert.That(request.Topics).IsNotNull();
        await Assert.That(request.Topics![0].Topic).IsEqualTo("topic-a");
        await Assert.That(request.Topics[0].Partitions).IsEquivalentTo([0, 1]);
        await Assert.That(result[existing].CurrentReplicaLogDir).IsEqualTo("/data-current");
        await Assert.That(result[existing].CurrentReplicaOffsetLag).IsEqualTo(2);
        await Assert.That(result[existing].FutureReplicaLogDir).IsEqualTo("/data-future");
        await Assert.That(result[existing].FutureReplicaOffsetLag).IsEqualTo(7);
        await Assert.That(result[existing].ErrorCode).IsEqualTo(ErrorCode.None);
        await Assert.That(result[missing].CurrentReplicaLogDir).IsNull();
        await Assert.That(result[missing].CurrentReplicaOffsetLag).IsEqualTo(-1);
        await Assert.That(result[missing].FutureReplicaLogDir).IsNull();
        await Assert.That(result[missing].FutureReplicaOffsetLag).IsEqualTo(-1);
    }

    [Test]
    public async Task DescribeReplicaLogDirsAsync_DeduplicatesAndGroupsFiltersByBroker()
    {
        await using var context = new AdminTestContext();
        context.EnqueueDescribe(DescribeResponse("/data-1", "topic-a", partition: 0, size: 100, offsetLag: 2, isFuture: false));
        context.EnqueueDescribe(DescribeResponse("/data-2", "topic-b", partition: 1, size: 200, offsetLag: 3, isFuture: false));
        var brokerOne = new TopicPartitionReplica("topic-a", 0, 1);
        var brokerTwo = new TopicPartitionReplica("topic-b", 1, 2);

        var result = await context.Client.DescribeReplicaLogDirsAsync([brokerOne, brokerOne, brokerTwo]);

        var requests = context.RequestsOfType<DescribeLogDirsRequest>();
        await Assert.That(requests.Count).IsEqualTo(2);
        await Assert.That(requests.SelectMany(static request => request.Topics!).Select(static topic => topic.Topic))
            .IsEquivalentTo(["topic-a", "topic-b"]);
        await Assert.That(result.Count).IsEqualTo(2);
    }

    [Test]
    public async Task DescribeReplicaLogDirsAsync_EmptyReplicas_ReturnsEmptyWithoutRequest()
    {
        await using var context = new AdminTestContext();

        var result = await context.Client.DescribeReplicaLogDirsAsync([]);

        await Assert.That(result).IsEmpty();
        await Assert.That(context.RequestsOfType<DescribeLogDirsRequest>()).IsEmpty();
    }

    [Test]
    public async Task DescribeReplicaLogDirsAsync_NullReplicas_ThrowsArgumentNullException()
    {
        await using var context = new AdminTestContext();

        async Task Act() => await context.Client.DescribeReplicaLogDirsAsync(null!);

        await Assert.That(Act).Throws<ArgumentNullException>();
    }

    [Test]
    public async Task DescribeReplicaLogDirsAsync_PreservesPartialBrokerError()
    {
        await using var context = new AdminTestContext();
        context.EnqueueDescribe(DescribeResponse("/data-1", "topic-a", partition: 0, size: 100, offsetLag: 2, isFuture: false));
        context.EnqueueDescribe(new DescribeLogDirsResponse
        {
            ErrorCode = ErrorCode.BrokerNotAvailable,
            Results = []
        });
        var brokerOne = new TopicPartitionReplica("topic-a", 0, 1);
        var brokerTwo = new TopicPartitionReplica("topic-b", 1, 2);

        var result = await context.Client.DescribeReplicaLogDirsAsync([brokerOne, brokerTwo]);

        await Assert.That(result.Values.Count(static value => value.ErrorCode == ErrorCode.None)).IsEqualTo(1);
        await Assert.That(result.Values.Count(static value => value.ErrorCode == ErrorCode.BrokerNotAvailable)).IsEqualTo(1);
    }

    [Test]
    public async Task DescribeReplicaLogDirsAsync_PreservesDirectoryErrorForUnresolvedReplica()
    {
        await using var context = new AdminTestContext();
        context.EnqueueDescribe(new DescribeLogDirsResponse
        {
            ErrorCode = ErrorCode.None,
            Results =
            [
                DescribeDirectory("/data-online", "topic-a", partition: 0, offsetLag: 2, isFuture: false),
                new DescribeLogDirsResponseDir
                {
                    ErrorCode = ErrorCode.KafkaStorageError,
                    LogDir = "/data-offline",
                    Topics = []
                }
            ]
        });
        var online = new TopicPartitionReplica("topic-a", 0, 1);
        var unresolved = new TopicPartitionReplica("topic-a", 1, 1);

        var result = await context.Client.DescribeReplicaLogDirsAsync([online, unresolved]);

        await Assert.That(result[online].ErrorCode).IsEqualTo(ErrorCode.None);
        await Assert.That(result[online].CurrentReplicaLogDir).IsEqualTo("/data-online");
        await Assert.That(result[unresolved].ErrorCode).IsEqualTo(ErrorCode.KafkaStorageError);
        await Assert.That(result[unresolved].CurrentReplicaLogDir).IsNull();
        await Assert.That(result[unresolved].FutureReplicaLogDir).IsNull();
    }

    [Test]
    [Arguments("", 0, 1)]
    [Arguments("topic-a", -1, 1)]
    [Arguments("topic-a", 0, -1)]
    public async Task DescribeReplicaLogDirsAsync_InvalidReplica_Throws(
        string topic,
        int partition,
        int brokerId)
    {
        await using var context = new AdminTestContext();

        async Task Act() => await context.Client.DescribeReplicaLogDirsAsync(
            [new TopicPartitionReplica(topic, partition, brokerId)]);

        await Assert.That(Act).Throws<ArgumentException>();
    }

    [Test]
    public async Task DescribeReplicaLogDirsAsync_Cancelled_ThrowsOperationCanceledException()
    {
        await using var context = new AdminTestContext();
        using var cancellationSource = new CancellationTokenSource();
        await cancellationSource.CancelAsync();

        async Task Act() => await context.Client.DescribeReplicaLogDirsAsync(
            [new TopicPartitionReplica("topic-a", 0, 1)],
            cancellationSource.Token);

        await Assert.That(Act).Throws<OperationCanceledException>();
    }

    [Test]
    public async Task DescribeReplicaLogDirsAsync_Timeout_PropagatesKafkaTimeoutException()
    {
        await using var context = new AdminTestContext();
        context.FailNextDescribe(new KafkaTimeoutException("Describe replica log directories timed out."));

        async Task Act() => await context.Client.DescribeReplicaLogDirsAsync(
            [new TopicPartitionReplica("topic-a", 0, 1)]);

        await Assert.That(Act).Throws<KafkaTimeoutException>();
    }

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

    [Test]
    public async Task DescribeLogDirsAsync_NullBrokerIds_ThrowsArgumentNullException()
    {
        await using var context = new AdminTestContext();

        async Task Act() => await context.Client.DescribeLogDirsAsync(null!);

        await Assert.That(Act).Throws<ArgumentNullException>();
    }

    [Test]
    public async Task DescribeLogDirsAsync_NegativeBrokerId_ThrowsArgumentOutOfRangeException()
    {
        await using var context = new AdminTestContext();

        async Task Act() => await context.Client.DescribeLogDirsAsync([-1]);

        await Assert.That(Act).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task DescribeLogDirsAsync_NegativePartition_ThrowsArgumentOutOfRangeException()
    {
        await using var context = new AdminTestContext();

        async Task Act() => await context.Client.DescribeLogDirsAsync(
            [1],
            [new TopicPartition("topic-a", -1)]);

        await Assert.That(Act).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task AlterReplicaLogDirsAsync_NullAssignments_ThrowsArgumentNullException()
    {
        await using var context = new AdminTestContext();

        async Task Act() => await context.Client.AlterReplicaLogDirsAsync(null!);

        await Assert.That(Act).Throws<ArgumentNullException>();
    }

    [Test]
    public async Task AlterReplicaLogDirsAsync_NegativeBrokerId_ThrowsArgumentOutOfRangeException()
    {
        await using var context = new AdminTestContext();

        async Task Act() => await context.Client.AlterReplicaLogDirsAsync(new Dictionary<TopicPartitionReplica, string>
        {
            [new TopicPartitionReplica("topic-a", 0, -1)] = "/data-1"
        });

        await Assert.That(Act).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task AlterReplicaLogDirsAsync_EmptyLogDir_ThrowsArgumentException()
    {
        await using var context = new AdminTestContext();

        async Task Act() => await context.Client.AlterReplicaLogDirsAsync(new Dictionary<TopicPartitionReplica, string>
        {
            [new TopicPartitionReplica("topic-a", 0, 1)] = " "
        });

        await Assert.That(Act).Throws<ArgumentException>();
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

    private static DescribeLogDirsResponseDir DescribeDirectory(
        string logDir,
        string topicName,
        int partition,
        long offsetLag,
        bool isFuture) => new()
        {
            ErrorCode = ErrorCode.None,
            LogDir = logDir,
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
                            PartitionSize = 100,
                            OffsetLag = offsetLag,
                            IsFutureKey = isFuture
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
        private readonly ConcurrentQueue<DescribeLogDirsResponse> _describeResponses = new();
        private readonly ConcurrentQueue<AlterReplicaLogDirsResponse> _alterResponses = new();
        private readonly ConcurrentQueue<object> _requests = new();
        private Exception? _nextDescribeException;

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
                    _requests.Enqueue(callInfo.ArgAt<DescribeLogDirsRequest>(0));
                    var exception = Interlocked.Exchange(ref _nextDescribeException, null);
                    if (exception is not null)
                    {
                        throw exception;
                    }

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
                    _requests.Enqueue(callInfo.ArgAt<AlterReplicaLogDirsRequest>(0));
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

        public void FailNextDescribe(Exception exception) => _nextDescribeException = exception;

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
