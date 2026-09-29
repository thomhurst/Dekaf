using Dekaf.Consumer;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using NSubstitute;

namespace Dekaf.Tests.Unit.Consumer;

/// <summary>
/// The broker sends an assignment only when it changes. One naming a topic created after the last
/// metadata refresh used to drop that topic for good: the partition was never fetched. The
/// coordinator now publishes what it can resolve, refreshes metadata off the heartbeat path
/// (one refresh at a time, backing off while the topic stays unknown), and resolves the rest on a
/// later heartbeat.
/// </summary>
public sealed partial class ConsumerCoordinatorKip848Tests
{
    private static readonly Guid LateTopicId = Guid.Parse("00000000-0000-0000-0000-000000000002");

    [Test]
    public async Task ConsumerProtocol_AssignmentTopicUnknownToMetadata_IsResolvedOnLaterHeartbeat()
    {
        var refreshResponse = new TaskCompletionSource<MetadataResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        var heartbeats = SetupUnresolvedTopicCluster(
            count => LateTopicBeat(count == 1 ? CreateLateAssignment() : null),
            // The refresh the join starts finds the new topic, once the test lets it answer.
            _ => new ValueTask<MetadataResponse>(refreshResponse.Task),
            out var metadataRequests);
        await using var coordinator = new ConsumerCoordinator(
            CreateConsumerProtocolOptions(heartbeatIntervalMs: 60_000), _connectionPool, _metadataManager);

        await coordinator.EnsureActiveGroupAsync(
            new HashSet<string> { "test-topic", "late-topic" }, CancellationToken.None);

        // The resolvable part is usable immediately; the rest stays pending.
        await Assert.That(coordinator.Assignment).IsEquivalentTo([new TopicPartition("test-topic", 0)]);
        await Assert.That(coordinator.HasUnresolvedAssignment).IsTrue();

        refreshResponse.SetResult(CreateLateTopicMetadata(includeLateTopic: true));
        await coordinator.UnresolvedAssignmentRefreshTask.WaitAsync(TimeSpan.FromSeconds(30));
        await Assert.That(metadataRequests()).IsEqualTo(1);

        await InvokeSteadyConsumerGroupHeartbeatAsync(coordinator);

        await Assert.That(coordinator.Assignment).IsEquivalentTo(
            [new TopicPartition("test-topic", 0), new TopicPartition("late-topic", 0)]);
        await Assert.That(coordinator.HasUnresolvedAssignment).IsFalse();

        // Resolved: steady heartbeats no longer refresh metadata.
        await InvokeSteadyConsumerGroupHeartbeatAsync(coordinator);
        await coordinator.UnresolvedAssignmentRefreshTask.WaitAsync(TimeSpan.FromSeconds(30));
        await Assert.That(metadataRequests()).IsEqualTo(1);
        await Assert.That(heartbeats()).IsEqualTo(3);
    }

    // Metadata can resolve one assigned topic while losing another, leaving the number of unknown
    // topics unchanged. The pending assignment must still be processed again.
    [Test]
    public async Task ConsumerProtocol_UnknownTopicsSwappedAtSameCount_IsProcessedAgain()
    {
        var refreshResponse = new TaskCompletionSource<MetadataResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        SetupUnresolvedTopicCluster(
            count => LateTopicBeat(count == 1 ? CreateLateAssignment() : null),
            _ => new ValueTask<MetadataResponse>(refreshResponse.Task),
            out _);
        await using var coordinator = new ConsumerCoordinator(
            CreateConsumerProtocolOptions(heartbeatIntervalMs: 60_000), _connectionPool, _metadataManager);
        await coordinator.EnsureActiveGroupAsync(
            new HashSet<string> { "test-topic", "late-topic" }, CancellationToken.None);
        await coordinator.StopHeartbeatAsync();
        await Assert.That(coordinator.Assignment).IsEquivalentTo([new TopicPartition("test-topic", 0)]);

        // The late topic resolves while test-topic disappears: still one unknown topic.
        refreshResponse.SetResult(CreateLateTopicMetadata(includeLateTopic: true, includeTestTopic: false));
        await coordinator.UnresolvedAssignmentRefreshTask.WaitAsync(TimeSpan.FromSeconds(30));
        await InvokeSteadyConsumerGroupHeartbeatAsync(coordinator);

        await Assert.That(coordinator.Assignment).IsEquivalentTo([new TopicPartition("late-topic", 0)]);
        await Assert.That(coordinator.HasUnresolvedAssignment).IsTrue();
    }

    // A slow refresh (busy refresh lock, a broker at its request timeout) must neither hold back
    // the assignment the heartbeat received, revocations included, nor the heartbeats after it.
    [Test]
    public async Task ConsumerProtocol_UnresolvedAssignment_SlowRefreshDoesNotDelayHeartbeats()
    {
        var refreshStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var refreshResponse = new TaskCompletionSource<MetadataResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        var heartbeats = SetupUnresolvedTopicCluster(
            count => count switch
            {
                1 => LateTopicBeat(CreateAssignment(TestTopicId, 0, 1)),
                // Revokes test-topic-1 and adds a partition of the new topic.
                2 => LateTopicBeat(CreateLateAssignment()),
                _ => LateTopicBeat(null)
            },
            _ =>
            {
                refreshStarted.TrySetResult();
                return new ValueTask<MetadataResponse>(refreshResponse.Task);
            },
            out var metadataRequests);
        await using var coordinator = new ConsumerCoordinator(
            CreateConsumerProtocolOptions(heartbeatIntervalMs: 60_000), _connectionPool, _metadataManager);
        await coordinator.EnsureActiveGroupAsync(
            new HashSet<string> { "test-topic", "late-topic" }, CancellationToken.None);
        await coordinator.StopHeartbeatAsync();

        await InvokeSteadyConsumerGroupHeartbeatAsync(coordinator);
        await refreshStarted.Task.WaitAsync(TimeSpan.FromSeconds(30));

        // The revocation is published at once, while the refresh hangs.
        await Assert.That(coordinator.Assignment).IsEquivalentTo([new TopicPartition("test-topic", 0)]);

        // Heartbeats go on, and start no second refresh.
        await InvokeSteadyConsumerGroupHeartbeatAsync(coordinator);
        await InvokeSteadyConsumerGroupHeartbeatAsync(coordinator);
        await Assert.That(heartbeats()).IsEqualTo(4);
        await Assert.That(metadataRequests()).IsEqualTo(1);
        await Assert.That(coordinator.UnresolvedAssignmentRefreshTask.IsCompleted).IsFalse();

        refreshResponse.SetResult(CreateLateTopicMetadata(includeLateTopic: true));
        await coordinator.UnresolvedAssignmentRefreshTask.WaitAsync(TimeSpan.FromSeconds(30));
        await InvokeSteadyConsumerGroupHeartbeatAsync(coordinator);

        await Assert.That(coordinator.Assignment).IsEquivalentTo(
            [new TopicPartition("test-topic", 0), new TopicPartition("late-topic", 0)]);
    }

    // A topic ID that never resolves (a deleted topic, say) must not cost a Metadata request per
    // heartbeat; a new assignment starts over without the backoff.
    [Test]
    public async Task ConsumerProtocol_UnresolvedAssignment_BacksOffRefreshesUntilAssignmentChanges()
    {
        SetupUnresolvedTopicCluster(
            count => LateTopicBeat(count is 1 or 4 ? CreateLateAssignment() : null),
            _ => ValueTask.FromResult(CreateLateTopicMetadata(includeLateTopic: false)),
            out var metadataRequests);
        await using var coordinator = new ConsumerCoordinator(
            CreateConsumerProtocolOptions(heartbeatIntervalMs: 60_000, retryBackoffMs: 60_000, retryBackoffMaxMs: 60_000),
            _connectionPool, _metadataManager);
        await coordinator.EnsureActiveGroupAsync(
            new HashSet<string> { "test-topic", "late-topic" }, CancellationToken.None);
        await coordinator.StopHeartbeatAsync();
        await coordinator.UnresolvedAssignmentRefreshTask.WaitAsync(TimeSpan.FromSeconds(30));
        await Assert.That(metadataRequests()).IsEqualTo(1);

        await InvokeSteadyConsumerGroupHeartbeatAsync(coordinator);
        await InvokeSteadyConsumerGroupHeartbeatAsync(coordinator);
        await coordinator.UnresolvedAssignmentRefreshTask.WaitAsync(TimeSpan.FromSeconds(30));
        await Assert.That(metadataRequests()).IsEqualTo(1);
        await Assert.That(coordinator.HasUnresolvedAssignment).IsTrue();

        // The broker sends a new assignment: its refreshes start without the previous backoff.
        await InvokeSteadyConsumerGroupHeartbeatAsync(coordinator);
        await coordinator.UnresolvedAssignmentRefreshTask.WaitAsync(TimeSpan.FromSeconds(30));
        await Assert.That(metadataRequests()).IsEqualTo(2);
    }

    // A new assignment that arrives while the previous one's refresh is still running must get a
    // refresh of its own as soon as that one ends, not inherit the backoff the old one applies.
    [Test]
    public async Task ConsumerProtocol_NewAssignmentDuringRefresh_RefreshesWithoutInheritedBackoff()
    {
        var firstRefresh = new TaskCompletionSource<MetadataResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        var refreshStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        SetupUnresolvedTopicCluster(
            count => LateTopicBeat(count is 1 or 2 ? CreateLateAssignment() : null),
            count =>
            {
                if (count != 1)
                    return ValueTask.FromResult(CreateLateTopicMetadata(includeLateTopic: false));
                refreshStarted.TrySetResult();
                return new ValueTask<MetadataResponse>(firstRefresh.Task);
            },
            out var metadataRequests);
        await using var coordinator = new ConsumerCoordinator(
            CreateConsumerProtocolOptions(heartbeatIntervalMs: 60_000, retryBackoffMs: 60_000, retryBackoffMaxMs: 60_000),
            _connectionPool, _metadataManager);
        await coordinator.EnsureActiveGroupAsync(
            new HashSet<string> { "test-topic", "late-topic" }, CancellationToken.None);
        await coordinator.StopHeartbeatAsync();
        await refreshStarted.Task.WaitAsync(TimeSpan.FromSeconds(30));

        // A new assignment arrives while the first refresh hangs.
        await InvokeSteadyConsumerGroupHeartbeatAsync(coordinator);
        await Assert.That(metadataRequests()).IsEqualTo(1);

        firstRefresh.SetResult(CreateLateTopicMetadata(includeLateTopic: false));
        await WaitForRefreshesAsync(coordinator);

        await Assert.That(metadataRequests()).IsEqualTo(2);
    }

    // A fence ends the membership the pending assignment belonged to. A rejoin answered without
    // an assignment must not publish partitions the new membership was never given.
    [Test]
    [Arguments(ErrorCode.FencedMemberEpoch)]
    [Arguments(ErrorCode.UnknownMemberId)]
    public async Task ConsumerProtocol_FencedWhileUnresolved_RejoinDoesNotReuseStaleAssignment(ErrorCode errorCode)
    {
        SetupUnresolvedTopicCluster(
            count => count switch
            {
                1 => LateTopicBeat(CreateLateAssignment()),
                2 => Error(errorCode),
                _ => LateTopicBeat(null)
            },
            _ => ValueTask.FromResult(CreateLateTopicMetadata(includeLateTopic: false)),
            out _);
        await using var coordinator = new ConsumerCoordinator(
            CreateConsumerProtocolOptions(heartbeatIntervalMs: 60_000), _connectionPool, _metadataManager);
        await coordinator.EnsureActiveGroupAsync(
            new HashSet<string> { "test-topic", "late-topic" }, CancellationToken.None);
        await coordinator.StopHeartbeatAsync();
        await coordinator.UnresolvedAssignmentRefreshTask.WaitAsync(TimeSpan.FromSeconds(30));
        await Assert.That(coordinator.HasUnresolvedAssignment).IsTrue();

        await RunHeartbeatLoopUntilItStopsAsync(coordinator);

        await Assert.That(coordinator.State).IsEqualTo(CoordinatorState.Unjoined);
        await Assert.That(coordinator.HasUnresolvedAssignment).IsFalse();

        // Metadata now knows the late topic, which the stale assignment would resolve to.
        _metadataManager.Metadata.Update(CreateLateTopicMetadata(includeLateTopic: true));
        await coordinator.EnsureActiveGroupAsync(
            new HashSet<string> { "test-topic", "late-topic" }, CancellationToken.None);

        await Assert.That(coordinator.State).IsEqualTo(CoordinatorState.Stable);
        await Assert.That(coordinator.Assignment.Count).IsEqualTo(0);
    }

    /// <summary>
    /// Waits for the latest refresh and any follow-up it started as it ended.
    /// </summary>
    private static async Task WaitForRefreshesAsync(ConsumerCoordinator coordinator)
    {
        Task task;
        while (!(task = coordinator.UnresolvedAssignmentRefreshTask).IsCompleted)
            await task.WaitAsync(TimeSpan.FromSeconds(30));
    }

    /// <summary>
    /// Scripts heartbeat and metadata answers; returns a reader of the heartbeat count.
    /// </summary>
    private Func<int> SetupUnresolvedTopicCluster(
        Func<int, ValueTask<ConsumerGroupHeartbeatResponse>> heartbeat,
        Func<int, ValueTask<MetadataResponse>> metadata,
        out Func<int> metadataRequests)
    {
        SetupFindCoordinator();
        var heartbeatCount = 0;
        var metadataCount = 0;
        _connection.SendAsync<ConsumerGroupHeartbeatRequest, ConsumerGroupHeartbeatResponse>(
                Arg.Any<ConsumerGroupHeartbeatRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(_ => heartbeat(Interlocked.Increment(ref heartbeatCount)));
        _connection.SendAsync<MetadataRequest, MetadataResponse>(
                Arg.Any<MetadataRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(_ => metadata(Interlocked.Increment(ref metadataCount)));
        _connection.SendAsync<ApiVersionsRequest, ApiVersionsResponse>(
                Arg.Any<ApiVersionsRequest>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(new ApiVersionsResponse
            {
                ErrorCode = ErrorCode.None,
                ApiKeys =
                [
                    new ApiVersion(ApiKey.Metadata, 12, 12),
                    new ApiVersion(ApiKey.FindCoordinator, 4, 5),
                    new ApiVersion(ApiKey.ConsumerGroupHeartbeat, 0, 0)
                ]
            }));
        metadataRequests = () => Volatile.Read(ref metadataCount);
        return () => Volatile.Read(ref heartbeatCount);
    }

    private static ValueTask<ConsumerGroupHeartbeatResponse> LateTopicBeat(ConsumerGroupHeartbeatAssignment? assignment) =>
        ValueTask.FromResult(new ConsumerGroupHeartbeatResponse
        {
            ErrorCode = ErrorCode.None,
            MemberId = "member-1",
            MemberEpoch = 1,
            HeartbeatIntervalMs = 60_000,
            Assignment = assignment
        });

    private static ConsumerGroupHeartbeatAssignment CreateLateAssignment() => new()
    {
        AssignedTopicPartitions =
        [
            new ConsumerGroupHeartbeatTopicPartitions { TopicId = TestTopicId, Partitions = [0] },
            new ConsumerGroupHeartbeatTopicPartitions { TopicId = LateTopicId, Partitions = [0] }
        ],
        PendingTopicPartitions = []
    };

    private static MetadataResponse CreateLateTopicMetadata(bool includeLateTopic, bool includeTestTopic = true)
    {
        List<TopicMetadata> topics = [];
        if (includeTestTopic)
            topics.Add(CreateTopic("test-topic", TestTopicId));
        if (includeLateTopic)
            topics.Add(CreateTopic("late-topic", LateTopicId));

        return new MetadataResponse
        {
            Brokers = [new BrokerMetadata { NodeId = 0, Host = "localhost", Port = 9092 }],
            Topics = topics
        };

        static TopicMetadata CreateTopic(string name, Guid topicId) => new()
        {
            Name = name,
            TopicId = topicId,
            ErrorCode = ErrorCode.None,
            Partitions =
            [
                new PartitionMetadata
                {
                    PartitionIndex = 0, LeaderId = 0, ErrorCode = ErrorCode.None, ReplicaNodes = [0], IsrNodes = [0]
                },
                new PartitionMetadata
                {
                    PartitionIndex = 1, LeaderId = 0, ErrorCode = ErrorCode.None, ReplicaNodes = [0], IsrNodes = [0]
                }
            ]
        };
    }
}
