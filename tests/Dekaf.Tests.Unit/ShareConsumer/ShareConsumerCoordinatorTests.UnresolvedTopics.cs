using System.Reflection;
using Dekaf.Consumer;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.ShareConsumer;
using NSubstitute;

namespace Dekaf.Tests.Unit.ShareConsumer;

/// <summary>
/// The broker sends an assignment only when it changes. One naming a topic created after the last
/// metadata refresh used to drop that topic for good: the partition was never fetched. The
/// coordinator now publishes what it can resolve, refreshes metadata off the heartbeat path, and
/// resolves the rest on a later heartbeat.
/// </summary>
public sealed partial class ShareConsumerCoordinatorTests
{
    private static readonly Guid KnownTopicId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid LateTopicId = Guid.Parse("00000000-0000-0000-0000-000000000002");

    [Test]
    public async Task Assignment_TopicUnknownToMetadata_IsResolvedOnLaterHeartbeat(CancellationToken cancellationToken)
    {
        var cluster = new UnresolvedTopicCluster("share-late-topic");
        var refreshResponse = new TaskCompletionSource<MetadataResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        cluster.Heartbeat = count => Beat(count == 1 ? CreateLateAssignment() : null);
        // The refresh the join starts finds the new topic, once the test lets it answer.
        cluster.Metadata = _ => new ValueTask<MetadataResponse>(refreshResponse.Task);
        await using var metadata = cluster.CreateMetadataManager();
        await using var coordinator = new ShareConsumerCoordinator(cluster.Options, cluster.Pool, metadata);
        coordinator.UpdateSubscription(["first", "late"]);

        await coordinator.EnsureActiveGroupAsync(cancellationToken);

        // The resolvable part is usable immediately; the rest stays pending.
        await Assert.That(coordinator.State).IsEqualTo(CoordinatorState.Stable);
        await Assert.That(coordinator.Assignment).IsEquivalentTo([new TopicPartition("first", 0)]);
        await Assert.That(coordinator.HasUnresolvedAssignment).IsTrue();

        refreshResponse.SetResult(CreateClusterMetadata(includeLateTopic: true));
        await coordinator.UnresolvedAssignmentRefreshTask.WaitAsync(cancellationToken);
        await Assert.That(cluster.MetadataRequests).IsEqualTo(1);

        await SendHeartbeatAsync(coordinator, cancellationToken);

        await Assert.That(coordinator.Assignment).IsEquivalentTo(
            [new TopicPartition("first", 0), new TopicPartition("late", 0)]);
        await Assert.That(coordinator.HasUnresolvedAssignment).IsFalse();

        // Resolved: steady heartbeats no longer refresh metadata.
        await SendHeartbeatAsync(coordinator, cancellationToken);
        await coordinator.UnresolvedAssignmentRefreshTask.WaitAsync(cancellationToken);
        await Assert.That(cluster.MetadataRequests).IsEqualTo(1);
        await Assert.That(cluster.HeartbeatRequests).IsEqualTo(3);
    }

    // Metadata can resolve one assigned topic while losing another, leaving the number of unknown
    // topics unchanged. The pending assignment must still be processed again.
    [Test]
    public async Task Assignment_UnknownTopicsSwappedAtSameCount_IsProcessedAgain(CancellationToken cancellationToken)
    {
        var cluster = new UnresolvedTopicCluster("share-swapped-unknown");
        var refreshResponse = new TaskCompletionSource<MetadataResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        cluster.Heartbeat = count => Beat(count == 1 ? CreateLateAssignment() : null);
        cluster.Metadata = _ => new ValueTask<MetadataResponse>(refreshResponse.Task);
        await using var metadata = cluster.CreateMetadataManager();
        await using var coordinator = new ShareConsumerCoordinator(cluster.Options, cluster.Pool, metadata);
        coordinator.UpdateSubscription(["first", "late"]);

        await coordinator.EnsureActiveGroupAsync(cancellationToken);
        await Assert.That(coordinator.Assignment).IsEquivalentTo([new TopicPartition("first", 0)]);

        // The late topic resolves while the first one disappears: still one unknown topic.
        refreshResponse.SetResult(CreateClusterMetadata(includeLateTopic: true, includeFirstTopic: false));
        await coordinator.UnresolvedAssignmentRefreshTask.WaitAsync(cancellationToken);
        await SendHeartbeatAsync(coordinator, cancellationToken);

        await Assert.That(coordinator.Assignment).IsEquivalentTo([new TopicPartition("late", 0)]);
        await Assert.That(coordinator.HasUnresolvedAssignment).IsTrue();
    }

    // A slow refresh (busy refresh lock, a broker at its request timeout) must neither hold back
    // the assignment the heartbeat received nor the heartbeats after it.
    [Test]
    public async Task Assignment_TopicUnknownToMetadata_SlowRefreshDoesNotDelayHeartbeats(CancellationToken cancellationToken)
    {
        var cluster = new UnresolvedTopicCluster("share-slow-refresh");
        var refreshStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var refreshResponse = new TaskCompletionSource<MetadataResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        cluster.Heartbeat = count => Beat(count == 1 ? CreateLateAssignment() : null);
        cluster.Metadata = _ =>
        {
            refreshStarted.TrySetResult();
            return new ValueTask<MetadataResponse>(refreshResponse.Task);
        };
        await using var metadata = cluster.CreateMetadataManager();
        await using var coordinator = new ShareConsumerCoordinator(cluster.Options, cluster.Pool, metadata);
        coordinator.UpdateSubscription(["first", "late"]);

        await coordinator.EnsureActiveGroupAsync(cancellationToken);
        await refreshStarted.Task.WaitAsync(cancellationToken);

        await Assert.That(coordinator.Assignment).IsEquivalentTo([new TopicPartition("first", 0)]);

        // Heartbeats go on while the refresh hangs, and start no second one.
        await SendHeartbeatAsync(coordinator, cancellationToken);
        await SendHeartbeatAsync(coordinator, cancellationToken);
        await Assert.That(cluster.HeartbeatRequests).IsEqualTo(3);
        await Assert.That(cluster.MetadataRequests).IsEqualTo(1);
        await Assert.That(coordinator.UnresolvedAssignmentRefreshTask.IsCompleted).IsFalse();

        refreshResponse.SetResult(CreateClusterMetadata(includeLateTopic: true));
        await coordinator.UnresolvedAssignmentRefreshTask.WaitAsync(cancellationToken);
        await SendHeartbeatAsync(coordinator, cancellationToken);

        await Assert.That(coordinator.Assignment).IsEquivalentTo(
            [new TopicPartition("first", 0), new TopicPartition("late", 0)]);
    }

    // A fenced epoch ends the membership the pending assignment belonged to. A rejoin answered
    // without an assignment must not publish partitions the new membership was never given.
    [Test]
    public async Task Assignment_FencedWhileUnresolved_RejoinDoesNotReuseStaleAssignment(CancellationToken cancellationToken)
    {
        var cluster = new UnresolvedTopicCluster("share-fenced-unresolved");
        var includeLateTopic = false;
        cluster.Heartbeat = count => count switch
        {
            1 => Beat(CreateLateAssignment(), heartbeatIntervalMs: 1),
            2 => ValueTask.FromResult(new ShareGroupHeartbeatResponse
            {
                ErrorCode = ErrorCode.FencedMemberEpoch,
                ErrorMessage = "fenced",
                HeartbeatIntervalMs = 1
            }),
            3 => Beat(null, heartbeatIntervalMs: 1),
            _ => Beat(new ShareGroupHeartbeatAssignment
            {
                TopicPartitions = [new ShareGroupHeartbeatTopicPartitions { TopicId = KnownTopicId, Partitions = [1] }]
            })
        };
        cluster.Metadata = _ => ValueTask.FromResult(CreateClusterMetadata(Volatile.Read(ref includeLateTopic)));
        await using var metadata = cluster.CreateMetadataManager();
        await using var coordinator = new ShareConsumerCoordinator(cluster.Options, cluster.Pool, metadata);
        coordinator.UpdateSubscription(["first", "late"]);

        // Joins with the late topic pending, then the heartbeat loop is fenced.
        await coordinator.EnsureActiveGroupAsync(cancellationToken);
        while (coordinator.State != CoordinatorState.Unjoined)
            await Task.Delay(1, cancellationToken);
        await coordinator.StopHeartbeatAsync();
        await coordinator.UnresolvedAssignmentRefreshTask.WaitAsync(cancellationToken);

        await Assert.That(coordinator.HasUnresolvedAssignment).IsFalse();

        // Metadata now knows the late topic, which the stale assignment would resolve to.
        Volatile.Write(ref includeLateTopic, true);
        metadata.Metadata.Update(CreateClusterMetadata(includeLateTopic: true));

        await coordinator.EnsureActiveGroupAsync(cancellationToken);

        await Assert.That(coordinator.State).IsEqualTo(CoordinatorState.Stable);
        await Assert.That(coordinator.Assignment).IsEquivalentTo([new TopicPartition("first", 1)]);
    }

    private static ValueTask<ShareGroupHeartbeatResponse> Beat(
        ShareGroupHeartbeatAssignment? assignment,
        int heartbeatIntervalMs = 60_000) =>
        ValueTask.FromResult(new ShareGroupHeartbeatResponse
        {
            ErrorCode = ErrorCode.None,
            MemberId = "member-1",
            MemberEpoch = 1,
            HeartbeatIntervalMs = heartbeatIntervalMs,
            Assignment = assignment
        });

    private static ShareGroupHeartbeatAssignment CreateLateAssignment() => new()
    {
        TopicPartitions =
        [
            new ShareGroupHeartbeatTopicPartitions { TopicId = KnownTopicId, Partitions = [0] },
            new ShareGroupHeartbeatTopicPartitions { TopicId = LateTopicId, Partitions = [0] }
        ]
    };

    private static MetadataResponse CreateClusterMetadata(bool includeLateTopic, bool includeFirstTopic = true)
    {
        List<TopicMetadata> topics = [];
        if (includeFirstTopic)
            topics.Add(CreateTopic("first", KnownTopicId));
        if (includeLateTopic)
            topics.Add(CreateTopic("late", LateTopicId));

        return new MetadataResponse
        {
            Brokers = [new BrokerMetadata { NodeId = 0, Host = "broker-0", Port = 9092 }],
            Topics = topics
        };

        static TopicMetadata CreateTopic(string name, Guid topicId) => new()
        {
            ErrorCode = ErrorCode.None,
            Name = name,
            TopicId = topicId,
            Partitions =
            [
                new PartitionMetadata
                {
                    ErrorCode = ErrorCode.None, PartitionIndex = 0, LeaderId = 0, ReplicaNodes = [0], IsrNodes = [0]
                },
                new PartitionMetadata
                {
                    ErrorCode = ErrorCode.None, PartitionIndex = 1, LeaderId = 0, ReplicaNodes = [0], IsrNodes = [0]
                }
            ]
        };
    }

    private static async Task SendHeartbeatAsync(ShareConsumerCoordinator coordinator, CancellationToken cancellationToken)
    {
        var method = typeof(ShareConsumerCoordinator).GetMethod(
            "SendShareGroupHeartbeatAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
        await (ValueTask<bool>)method.Invoke(coordinator, [0, cancellationToken])!;
    }

    /// <summary>
    /// One broker that is also the share coordinator, with scriptable heartbeat and metadata
    /// answers. Cluster metadata starts without the late topic.
    /// </summary>
    private sealed class UnresolvedTopicCluster
    {
        private int _heartbeatRequests;
        private int _metadataRequests;

        public UnresolvedTopicCluster(string groupId)
        {
            Options = new ShareConsumerOptions { BootstrapServers = ["broker-0:9092"], GroupId = groupId };
            var connection = Substitute.For<IKafkaConnection>();
            connection.SendAsync<FindCoordinatorRequest, FindCoordinatorResponse>(
                    Arg.Any<FindCoordinatorRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
                .Returns(ValueTask.FromResult(new FindCoordinatorResponse
                {
                    Coordinators = [new Coordinator
                    {
                        Key = groupId, NodeId = 0, Host = "broker-0", Port = 9092, ErrorCode = ErrorCode.None
                    }]
                }));
            connection.SendAsync<ShareGroupHeartbeatRequest, ShareGroupHeartbeatResponse>(
                    Arg.Any<ShareGroupHeartbeatRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
                .Returns(_ => Heartbeat(Interlocked.Increment(ref _heartbeatRequests)));
            connection.SendAsync<MetadataRequest, MetadataResponse>(
                    Arg.Any<MetadataRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
                .Returns(_ => Metadata(Interlocked.Increment(ref _metadataRequests)));
            connection.SendAsync<ApiVersionsRequest, ApiVersionsResponse>(
                    Arg.Any<ApiVersionsRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
                .Returns(ValueTask.FromResult(new ApiVersionsResponse
                {
                    ErrorCode = ErrorCode.None,
                    ApiKeys =
                    [
                        new ApiVersion(ApiKey.Metadata, 12, 12),
                        new ApiVersion(ApiKey.FindCoordinator,
                            FindCoordinatorRequest.LowestSupportedVersion, FindCoordinatorRequest.HighestSupportedVersion),
                        new ApiVersion(ApiKey.ShareGroupHeartbeat, 0, 1)
                    ]
                }));
            Pool = Substitute.For<IConnectionPool>();
            Pool.GetConnectionAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns(ValueTask.FromResult(connection));
            Pool.GetConnectionByIndexAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns(ValueTask.FromResult(connection));
        }

        public ShareConsumerOptions Options { get; }

        public IConnectionPool Pool { get; }

        public Func<int, ValueTask<ShareGroupHeartbeatResponse>> Heartbeat { get; set; } =
            static _ => throw new InvalidOperationException("No heartbeat response scripted.");

        public Func<int, ValueTask<MetadataResponse>> Metadata { get; set; } =
            static _ => throw new InvalidOperationException("No metadata response scripted.");

        public int HeartbeatRequests => Volatile.Read(ref _heartbeatRequests);

        public int MetadataRequests => Volatile.Read(ref _metadataRequests);

        public MetadataManager CreateMetadataManager()
        {
            var metadata = new MetadataManager(Pool, Options.BootstrapServers);
            metadata.SetApiVersion(ApiKey.ShareGroupHeartbeat, 0, 1);
            metadata.SetApiVersion(ApiKey.FindCoordinator,
                FindCoordinatorRequest.LowestSupportedVersion, FindCoordinatorRequest.HighestSupportedVersion);
            metadata.Metadata.Update(CreateClusterMetadata(includeLateTopic: false));
            return metadata;
        }
    }
}
