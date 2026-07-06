using Dekaf.Admin;
using Dekaf.Errors;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using NSubstitute;

namespace Dekaf.Tests.Unit.Admin;

public sealed class AdminClientKRaftQuorumTests
{
    [Test]
    public async Task DescribeMetadataQuorumAsync_SendsMetadataPartitionAndMapsResult()
    {
        await using var context = new AdminTestContext();
        var directoryId = Guid.NewGuid();
        context.EnqueueDescribe(new DescribeQuorumResponse
        {
            ErrorCode = ErrorCode.None,
            Topics =
            [
                new DescribeQuorumResponseTopic
                {
                    TopicName = "__cluster_metadata",
                    Partitions =
                    [
                        new DescribeQuorumResponsePartition
                        {
                            PartitionIndex = 0,
                            ErrorCode = ErrorCode.None,
                            LeaderId = 1,
                            LeaderEpoch = 7,
                            HighWatermark = 42,
                            CurrentVoters =
                            [
                                new DescribeQuorumReplicaState
                                {
                                    ReplicaId = 1,
                                    ReplicaDirectoryId = directoryId,
                                    LogEndOffset = 41,
                                    LastFetchTimestamp = 100,
                                    LastCaughtUpTimestamp = 99
                                }
                            ],
                            Observers = []
                        }
                    ]
                }
            ],
            Nodes =
            [
                new DescribeQuorumResponseNode
                {
                    NodeId = 1,
                    Listeners =
                    [
                        new RaftVoterEndpointData
                        {
                            Name = "CONTROLLER",
                            Host = "localhost",
                            Port = 9093
                        }
                    ]
                }
            ]
        });

        var result = await context.Client.DescribeMetadataQuorumAsync();

        var request = context.SingleRequest<DescribeQuorumRequest>();
        var requestTopic = request.Topics.Single();
        await Assert.That(context.SingleVersion<DescribeQuorumRequest>()).IsEqualTo((short)2);
        await Assert.That(requestTopic.TopicName).IsEqualTo("__cluster_metadata");
        await Assert.That(requestTopic.Partitions.Single().PartitionIndex).IsEqualTo(0);
        await Assert.That(result.LeaderId).IsEqualTo(1);
        await Assert.That(result.LeaderEpoch).IsEqualTo(7);
        await Assert.That(result.HighWatermark).IsEqualTo(42);
        var voter = result.CurrentVoters.Single();
        await Assert.That(voter.ReplicaDirectoryId).IsEqualTo(directoryId);
        var listener = result.Nodes.Single().Listeners.Single();
        await Assert.That(listener.Port).IsEqualTo(9093);
    }

    [Test]
    public async Task AddRaftVoterAsync_SendsControllerRequest()
    {
        await using var context = new AdminTestContext();
        var directoryId = Guid.NewGuid();
        context.EnqueueAdd(new AddRaftVoterResponse
        {
            ErrorCode = ErrorCode.None
        });

        await context.Client.AddRaftVoterAsync(
            voterId: 2,
            voterDirectoryId: directoryId,
            endpoints:
            [
                new RaftVoterEndpoint
                {
                    Name = "CONTROLLER",
                    Host = "localhost",
                    Port = 9093
                }
            ],
            options: new AddRaftVoterOptions
            {
                ClusterId = "cluster-a",
                TimeoutMs = 1234,
                AckWhenCommitted = false
            });

        var request = context.SingleRequest<AddRaftVoterRequest>();
        await Assert.That(context.SingleVersion<AddRaftVoterRequest>()).IsEqualTo((short)1);
        await Assert.That(request.ClusterId).IsEqualTo("cluster-a");
        await Assert.That(request.TimeoutMs).IsEqualTo(1234);
        await Assert.That(request.VoterId).IsEqualTo(2);
        await Assert.That(request.VoterDirectoryId).IsEqualTo(directoryId);
        await Assert.That(request.AckWhenCommitted).IsFalse();
        await Assert.That(request.Listeners.Single().Port).IsEqualTo((ushort)9093);
    }

    [Test]
    public async Task AddRaftVoterAsync_AckWhenCommittedFalseOnV0Broker_ThrowsBrokerVersionException()
    {
        await using var context = new AdminTestContext(addRaftVoterMaxVersion: 0);

        await Assert.That(async () => await context.Client.AddRaftVoterAsync(
                voterId: 2,
                voterDirectoryId: Guid.NewGuid(),
                endpoints:
                [
                    new RaftVoterEndpoint
                    {
                        Name = "CONTROLLER",
                        Host = "localhost",
                        Port = 9093
                    }
                ],
                options: new AddRaftVoterOptions { AckWhenCommitted = false }))
            .Throws<BrokerVersionException>();

        await Assert.That(context.RequestCount<AddRaftVoterRequest>()).IsEqualTo(0);
    }

    [Test]
    public async Task RemoveRaftVoterAsync_SendsControllerRequest()
    {
        await using var context = new AdminTestContext();
        var directoryId = Guid.NewGuid();
        context.EnqueueRemove(new RemoveRaftVoterResponse
        {
            ErrorCode = ErrorCode.None
        });

        await context.Client.RemoveRaftVoterAsync(
            voterId: 2,
            voterDirectoryId: directoryId,
            options: new RemoveRaftVoterOptions { ClusterId = "cluster-a" });

        var request = context.SingleRequest<RemoveRaftVoterRequest>();
        await Assert.That(context.SingleVersion<RemoveRaftVoterRequest>()).IsEqualTo((short)0);
        await Assert.That(request.ClusterId).IsEqualTo("cluster-a");
        await Assert.That(request.VoterId).IsEqualTo(2);
        await Assert.That(request.VoterDirectoryId).IsEqualTo(directoryId);
    }

    [Test]
    public async Task UnregisterBrokerAsync_SendsControllerRequest()
    {
        await using var context = new AdminTestContext();
        context.EnqueueUnregister(new UnregisterBrokerResponse
        {
            ErrorCode = ErrorCode.None
        });

        await context.Client.UnregisterBrokerAsync(brokerId: 3);

        var request = context.SingleRequest<UnregisterBrokerRequest>();
        await Assert.That(context.SingleVersion<UnregisterBrokerRequest>()).IsEqualTo((short)0);
        await Assert.That(request.BrokerId).IsEqualTo(3);
    }

    private sealed class AdminTestContext : IAsyncDisposable
    {
        private readonly IConnectionPool _pool;
        private readonly MetadataManager _metadataManager;
        private readonly Queue<DescribeQuorumResponse> _describeResponses = new();
        private readonly Queue<AddRaftVoterResponse> _addResponses = new();
        private readonly Queue<RemoveRaftVoterResponse> _removeResponses = new();
        private readonly Queue<UnregisterBrokerResponse> _unregisterResponses = new();
        private readonly List<(object Request, short Version)> _requests = [];

        public AdminTestContext(short addRaftVoterMaxVersion = 1)
        {
            Connection = Substitute.For<IKafkaConnection>();
            Connection.BrokerId.Returns(1);
            Connection.Host.Returns("localhost");
            Connection.Port.Returns(9092);
            Connection.IsConnected.Returns(true);

            Connection.SendAsync<DescribeQuorumRequest, DescribeQuorumResponse>(
                    Arg.Any<DescribeQuorumRequest>(),
                    Arg.Any<short>(),
                    Arg.Any<CancellationToken>())
                .Returns(callInfo =>
                {
                    Capture(callInfo.ArgAt<DescribeQuorumRequest>(0), callInfo.ArgAt<short>(1));
                    return new ValueTask<DescribeQuorumResponse>(_describeResponses.Dequeue());
                });

            Connection.SendAsync<AddRaftVoterRequest, AddRaftVoterResponse>(
                    Arg.Any<AddRaftVoterRequest>(),
                    Arg.Any<short>(),
                    Arg.Any<CancellationToken>())
                .Returns(callInfo =>
                {
                    Capture(callInfo.ArgAt<AddRaftVoterRequest>(0), callInfo.ArgAt<short>(1));
                    return new ValueTask<AddRaftVoterResponse>(_addResponses.Dequeue());
                });

            Connection.SendAsync<RemoveRaftVoterRequest, RemoveRaftVoterResponse>(
                    Arg.Any<RemoveRaftVoterRequest>(),
                    Arg.Any<short>(),
                    Arg.Any<CancellationToken>())
                .Returns(callInfo =>
                {
                    Capture(callInfo.ArgAt<RemoveRaftVoterRequest>(0), callInfo.ArgAt<short>(1));
                    return new ValueTask<RemoveRaftVoterResponse>(_removeResponses.Dequeue());
                });

            Connection.SendAsync<UnregisterBrokerRequest, UnregisterBrokerResponse>(
                    Arg.Any<UnregisterBrokerRequest>(),
                    Arg.Any<short>(),
                    Arg.Any<CancellationToken>())
                .Returns(callInfo =>
                {
                    Capture(callInfo.ArgAt<UnregisterBrokerRequest>(0), callInfo.ArgAt<short>(1));
                    return new ValueTask<UnregisterBrokerResponse>(_unregisterResponses.Dequeue());
                });

            _pool = Substitute.For<IConnectionPool>();
            _pool.GetConnectionAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns(new ValueTask<IKafkaConnection>(Connection));
            _pool.GetConnectionAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns(new ValueTask<IKafkaConnection>(Connection));

            _metadataManager = new MetadataManager(_pool, ["localhost:9092"]);
            _metadataManager.Metadata.Update(CreateMetadataResponse());
            _metadataManager.SetApiVersion(ApiKey.DescribeQuorum, 0, 2);
            _metadataManager.SetApiVersion(ApiKey.AddRaftVoter, 0, addRaftVoterMaxVersion);
            _metadataManager.SetApiVersion(ApiKey.RemoveRaftVoter, 0, 0);
            _metadataManager.SetApiVersion(ApiKey.UnregisterBroker, 0, 0);

            Client = new AdminClient(
                new AdminClientOptions { BootstrapServers = ["localhost:9092"] },
                _pool,
                _metadataManager);
        }

        public AdminClient Client { get; }
        public IKafkaConnection Connection { get; }

        public void EnqueueDescribe(DescribeQuorumResponse response) => _describeResponses.Enqueue(response);
        public void EnqueueAdd(AddRaftVoterResponse response) => _addResponses.Enqueue(response);
        public void EnqueueRemove(RemoveRaftVoterResponse response) => _removeResponses.Enqueue(response);
        public void EnqueueUnregister(UnregisterBrokerResponse response) => _unregisterResponses.Enqueue(response);

        public T SingleRequest<T>() where T : class => _requests.Select(static request => request.Request).OfType<T>().Single();

        public short SingleVersion<T>() where T : class => _requests.Single(request => request.Request is T).Version;

        public int RequestCount<T>() where T : class => _requests.Count(request => request.Request is T);

        public async ValueTask DisposeAsync()
        {
            await Client.DisposeAsync().ConfigureAwait(false);
            await _metadataManager.DisposeAsync().ConfigureAwait(false);
            await _pool.DisposeAsync().ConfigureAwait(false);
        }

        private void Capture(object request, short version) => _requests.Add((request, version));

        private static MetadataResponse CreateMetadataResponse() => new()
        {
            Brokers =
            [
                new BrokerMetadata
                {
                    NodeId = 1,
                    Host = "localhost",
                    Port = 9092
                }
            ],
            ClusterId = "test-cluster",
            ControllerId = 1,
            Topics = []
        };
    }
}
