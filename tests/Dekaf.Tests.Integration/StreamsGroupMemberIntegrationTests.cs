using Dekaf.Errors;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.Streams;

namespace Dekaf.Tests.Integration;

[Category("ConsumerGroup")]
[SupportsKafka(420)]
public sealed class StreamsGroupMemberIntegrationTests(KafkaTestContainer kafka)
    : KafkaIntegrationTest(kafka)
{
    // Kafka 4.3.1 rejects task offsets and static membership as not yet supported.
    // Kafka 4.4 completes the KIP-1071 lifecycle used by these gated tests.
    private const int CompleteStreamsGroupSupport = 440;

    [Test]
    public async Task MemberJoinsReceivesTasksLeavesAndRejoins()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 3).ConfigureAwait(false);
        var groupId = $"streams-group-{Guid.NewGuid():N}";
        await using var client = Kafka.Connect(KafkaContainer.BootstrapServers, builder =>
            builder.WithLoggerFactory(GlobalTestSetup.GetLoggerFactory()));
        await using var member = client.CreateStreamsGroupMember(new StreamsGroupMemberOptions
        {
            GroupId = groupId
        });
        await member.InitializeAsync();

        var join = await member.JoinAsync(CreateInitialState(topic, "process-a"));

        await Assert.That(join.MemberEpoch).IsGreaterThan(0);
        await using var admin = client.CreateAdminClient().Build();
        await ReceiveActiveTasksAsync(admin, member, [0, 1, 2]);
        var assignment = member.Snapshot.Assignment;
        var acknowledged = await member.UpdateAsync(new StreamsGroupMemberUpdate
        {
            ActiveTasks = assignment.ActiveTasks,
            StandbyTasks = assignment.StandbyTasks,
            WarmupTasks = assignment.WarmupTasks
        });
        await Assert.That(acknowledged.MemberEpoch).IsGreaterThanOrEqualTo(join.MemberEpoch);

        var described = await admin.DescribeStreamsGroupsAsync([groupId]);
        var brokerMember = described[groupId].Members.Single();
        await Assert.That(brokerMember.MemberId).IsEqualTo(member.Snapshot.MemberId);
        await Assert.That(brokerMember.Assignment.ActiveTasks
            .SelectMany(static taskSet => taskSet.Partitions)).IsEquivalentTo([0, 1, 2]);

        var firstMemberId = member.Snapshot.MemberId;
        await member.CloseAsync(new StreamsGroupCloseOptions
        {
            GroupMembershipOperation = StreamsGroupMembershipOperation.LeaveGroup
        });
        await Assert.That(member.Snapshot.IsClosed).IsTrue();
        var afterLeave = await admin.DescribeStreamsGroupsAsync([groupId]);
        await Assert.That(afterLeave[groupId].Members
            .Any(brokerGroupMember => brokerGroupMember.MemberId == firstMemberId)).IsFalse();

        await using var replacement = client.CreateStreamsGroupMember(new StreamsGroupMemberOptions
        {
            GroupId = groupId
        });
        await replacement.InitializeAsync();
        await replacement.JoinAsync(CreateInitialState(topic, "process-b"));
        await Assert.That(replacement.Snapshot.MemberId).IsNotEqualTo(firstMemberId);
        await replacement.CloseAsync(new StreamsGroupCloseOptions
        {
            GroupMembershipOperation = StreamsGroupMembershipOperation.LeaveGroup
        });
    }

    [Test]
    [SupportsKafka(CompleteStreamsGroupSupport)]
    public async Task MemberReportsTaskOffsets()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync().ConfigureAwait(false);
        var groupId = $"streams-group-{Guid.NewGuid():N}";
        await using var client = Kafka.Connect(KafkaContainer.BootstrapServers, builder =>
            builder.WithLoggerFactory(GlobalTestSetup.GetLoggerFactory()));
        await using var member = client.CreateStreamsGroupMember(new StreamsGroupMemberOptions
        {
            GroupId = groupId
        });
        await member.InitializeAsync();
        await member.JoinAsync(CreateInitialState(topic, "offset-process"));
        await using var admin = client.CreateAdminClient().Build();
        await ReceiveActiveTasksAsync(admin, member, [0]);
        var assignment = member.Snapshot.Assignment;
        await member.UpdateAsync(new StreamsGroupMemberUpdate
        {
            ActiveTasks = assignment.ActiveTasks,
            StandbyTasks = assignment.StandbyTasks,
            WarmupTasks = assignment.WarmupTasks
        });

        await member.ReportTaskOffsetsAsync(new StreamsGroupTaskOffsetReport
        {
            TaskOffsets =
            [
                new StreamsGroupTaskOffset { SubtopologyId = "0", Partition = 0, Offset = 12 }
            ],
            TaskEndOffsets =
            [
                new StreamsGroupTaskOffset { SubtopologyId = "0", Partition = 0, Offset = 20 }
            ]
        });

        var described = await admin.DescribeStreamsGroupsAsync([groupId]);
        var brokerMember = described[groupId].Members.Single();
        await Assert.That(brokerMember.TaskOffsets.Single().Offset).IsEqualTo(12);
        await Assert.That(brokerMember.TaskEndOffsets.Single().Offset).IsEqualTo(20);
    }

    [Test]
    [SupportsKafka(CompleteStreamsGroupSupport)]
    public async Task TopologyEpochMustAdvanceOneAtATime()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync().ConfigureAwait(false);
        await using var client = Kafka.Connect(KafkaContainer.BootstrapServers, builder =>
            builder.WithLoggerFactory(GlobalTestSetup.GetLoggerFactory()));
        var groupId = $"streams-group-{Guid.NewGuid():N}";
        await using var member = client.CreateStreamsGroupMember(new StreamsGroupMemberOptions
        {
            GroupId = groupId
        });
        await member.InitializeAsync();
        var joined = await member.JoinAsync(CreateInitialState(topic, "topology-process"));

        var invalidUpdate = member.UpdateAsync(new StreamsGroupMemberUpdate
        {
            Topology = CreateTopology(topic, epoch: 2)
        });
        var exception = await Assert.That(async () => await invalidUpdate)
            .Throws<GroupException>();
        await Assert.That(exception!.ErrorCode).IsEqualTo(ErrorCode.StreamsInvalidTopologyEpoch);
        await Assert.That(member.Snapshot.IsJoined).IsTrue();

        var updated = await member.UpdateAsync(new StreamsGroupMemberUpdate
        {
            Topology = CreateTopology(topic, epoch: 1)
        });
        await Assert.That(updated.MemberEpoch).IsGreaterThan(joined.MemberEpoch);

        await using var admin = client.CreateAdminClient().Build();
        var described = await admin.DescribeStreamsGroupsAsync([groupId]);
        await Assert.That(described[groupId].Topology!.Epoch).IsEqualTo(1);
        await Assert.That(described[groupId].GroupEpoch).IsGreaterThan(1);
    }

    [Test]
    [SupportsKafka(CompleteStreamsGroupSupport)]
    public async Task StaticMemberRestartsAndCloseWithoutLeaveExpires()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync().ConfigureAwait(false);
        var groupId = $"streams-group-{Guid.NewGuid():N}";
        var instanceId = $"streams-instance-{Guid.NewGuid():N}";
        await using var client = Kafka.Connect(KafkaContainer.BootstrapServers, builder =>
            builder.WithLoggerFactory(GlobalTestSetup.GetLoggerFactory()));
        await using var admin = client.CreateAdminClient().Build();

        await using var first = CreateStaticMember(client, groupId, instanceId);
        await first.InitializeAsync();
        await first.JoinAsync(CreateInitialState(topic, "static-process-a"));
        var firstMemberId = first.Snapshot.MemberId;
        await first.CloseAsync();

        await using var restarted = CreateStaticMember(client, groupId, instanceId);
        await restarted.InitializeAsync();
        await restarted.JoinAsync(CreateInitialState(topic, "static-process-b"));
        await Assert.That(restarted.Snapshot.MemberId).IsEqualTo(firstMemberId);
        await Assert.That(restarted.Snapshot.IsJoined).IsTrue();
        await restarted.CloseAsync(new StreamsGroupCloseOptions
        {
            GroupMembershipOperation = StreamsGroupMembershipOperation.RemainInGroup
        });
        var retained = await admin.DescribeStreamsGroupsAsync([groupId]);
        await Assert.That(retained[groupId].Members
            .Any(member => member.MemberId == firstMemberId)).IsTrue();

        await TestWait.WaitForConditionAsync(
            async () => (await admin.DescribeStreamsGroupsAsync([groupId]))[groupId].Members,
            members => members.All(member => member.MemberId != firstMemberId),
            initialDelayMs: 250,
            description: $"Streams member {firstMemberId} to expire from group {groupId}");

        await using var afterCrash = CreateStaticMember(client, groupId, instanceId);
        await afterCrash.InitializeAsync();
        await afterCrash.JoinAsync(CreateInitialState(topic, "static-process-c"));
        await Assert.That(afterCrash.Snapshot.IsJoined).IsTrue();
        await afterCrash.CloseAsync(new StreamsGroupCloseOptions
        {
            GroupMembershipOperation = StreamsGroupMembershipOperation.LeaveGroup
        });
    }

    [Test]
    public async Task MemberRecoversAfterBrokerFencesItsEpoch()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync().ConfigureAwait(false);
        var groupId = $"streams-group-{Guid.NewGuid():N}";
        await using var client = Kafka.Connect(KafkaContainer.BootstrapServers, builder =>
            builder.WithLoggerFactory(GlobalTestSetup.GetLoggerFactory()));
        await using var member = client.CreateStreamsGroupMember(new StreamsGroupMemberOptions
        {
            GroupId = groupId
        });
        await member.InitializeAsync();
        await member.JoinAsync(CreateInitialState(topic, "fenced-process"));
        await using var admin = client.CreateAdminClient().Build();
        await ReceiveActiveTasksAsync(admin, member, [0]);
        var staleEpoch = member.Snapshot.MemberEpoch;
        var memberId = member.Snapshot.MemberId!;

        await RemoveRejoinAndFenceRawMemberAsync(groupId, memberId, staleEpoch, topic);

        var recovered = await member.UpdateAsync(new StreamsGroupMemberUpdate
        {
            ProcessId = "recovered-process"
        });
        await Assert.That(recovered.MemberId).IsEqualTo(memberId);
        await Assert.That(recovered.MemberEpoch).IsGreaterThan(staleEpoch);
        await Assert.That(member.Snapshot.IsJoined).IsTrue();
    }

    private static IStreamsGroupMember CreateStaticMember(
        KafkaClient client,
        string groupId,
        string instanceId) =>
        client.CreateStreamsGroupMember(new StreamsGroupMemberOptions
        {
            GroupId = groupId,
            InstanceId = instanceId,
            RebalanceTimeout = TimeSpan.FromSeconds(10)
        });

    private static StreamsGroupMemberUpdate CreateInitialState(string topic, string processId) => new()
    {
        Topology = CreateTopology(topic, epoch: 0),
        ActiveTasks = [],
        StandbyTasks = [],
        WarmupTasks = [],
        ProcessId = processId,
        ClientTags = []
    };

    private static StreamsGroupTopology CreateTopology(string topic, int epoch) => new()
    {
        Epoch = epoch,
        Subtopologies =
        [
            new StreamsGroupSubtopology
            {
                SubtopologyId = "0",
                SourceTopics = [topic],
                SourceTopicRegex = [],
                StateChangelogTopics = [],
                RepartitionSinkTopics = [],
                RepartitionSourceTopics = [],
                CopartitionGroups = []
            }
        ]
    };

    private static async Task ReceiveActiveTasksAsync(
        Dekaf.Admin.IAdminClient admin,
        IStreamsGroupMember member,
        IReadOnlyCollection<int> expectedPartitions)
    {
        var descriptions = await admin.DescribeStreamsGroupsAsync([member.GroupId]);
        var targeted = descriptions[member.GroupId].Members.Single().TargetAssignment.ActiveTasks
            .SelectMany(static taskSet => taskSet.Partitions)
            .ToArray();
        await Assert.That(targeted).IsEquivalentTo(expectedPartitions);

        var heartbeat = await member.UpdateAsync(new StreamsGroupMemberUpdate());
        var activeTasks = heartbeat.ActiveTasks ?? member.Snapshot.Assignment.ActiveTasks;
        var assigned = activeTasks
            .SelectMany(static taskSet => taskSet.Partitions)
            .ToArray();
        await Assert.That(assigned).IsEquivalentTo(expectedPartitions);
    }

    private async Task RemoveRejoinAndFenceRawMemberAsync(
        string groupId,
        string memberId,
        int staleEpoch,
        string topic)
    {
        await using var pool = new ConnectionPool(
            "streams-fencing-test",
            new ConnectionOptions { RequestTimeout = TimeSpan.FromSeconds(10) },
            loggerFactory: null);
        var endpoint = BootstrapServerList.Parse(KafkaContainer.BootstrapServers);
        var bootstrap = await pool.GetConnectionAsync(endpoint.Host, endpoint.Port, CancellationToken.None);
        var findVersion = ((IKafkaCapabilityProvider)bootstrap).Capabilities.NegotiateVersion(
            ApiKey.FindCoordinator,
            FindCoordinatorRequest.LowestSupportedVersion,
            FindCoordinatorRequest.HighestSupportedVersion);
        var coordinatorResponse = await bootstrap.SendAsync<FindCoordinatorRequest, FindCoordinatorResponse>(
            new FindCoordinatorRequest { Key = groupId, KeyType = CoordinatorType.Group },
            findVersion,
            CancellationToken.None);
        var coordinator = coordinatorResponse.Coordinators.Single();
        pool.RegisterBroker(coordinator.NodeId, coordinator.Host, coordinator.Port);
        var connection = await pool.GetConnectionAsync(coordinator.NodeId, CancellationToken.None);
        var heartbeatVersion = ((IKafkaCapabilityProvider)connection).Capabilities.NegotiateVersion(
            ApiKey.StreamsGroupHeartbeat,
            StreamsGroupHeartbeatRequest.LowestSupportedVersion,
            StreamsGroupHeartbeatRequest.HighestSupportedVersion);

        var leave = await connection.SendAsync<StreamsGroupHeartbeatRequest, StreamsGroupHeartbeatResponse>(
            new StreamsGroupHeartbeatRequest
            {
                GroupId = groupId,
                MemberId = memberId,
                MemberEpoch = -1
            },
            heartbeatVersion,
            CancellationToken.None);
        await Assert.That(leave.ErrorCode).IsEqualTo(ErrorCode.None);

        var rejoin = await connection.SendAsync<StreamsGroupHeartbeatRequest, StreamsGroupHeartbeatResponse>(
            new StreamsGroupHeartbeatRequest
            {
                GroupId = groupId,
                MemberId = memberId,
                MemberEpoch = 0,
                RebalanceTimeoutMs = 30_000,
                Topology = CreateProtocolTopology(topic),
                ActiveTasks = [],
                StandbyTasks = [],
                WarmupTasks = [],
                ProcessId = "raw-process",
                ClientTags = []
            },
            heartbeatVersion,
            CancellationToken.None);
        await Assert.That(rejoin.ErrorCode).IsEqualTo(ErrorCode.None);
        await Assert.That(rejoin.MemberEpoch).IsGreaterThan(staleEpoch);

        var fenced = await connection.SendAsync<StreamsGroupHeartbeatRequest, StreamsGroupHeartbeatResponse>(
            new StreamsGroupHeartbeatRequest
            {
                GroupId = groupId,
                MemberId = memberId,
                MemberEpoch = staleEpoch,
                ProcessId = "stale-process"
            },
            heartbeatVersion,
            CancellationToken.None);
        await Assert.That(fenced.ErrorCode).IsEqualTo(ErrorCode.FencedMemberEpoch);
    }

    private static StreamsGroupHeartbeatTopology CreateProtocolTopology(string topic) => new()
    {
        Epoch = 0,
        Subtopologies =
        [
            new StreamsGroupHeartbeatSubtopology
            {
                SubtopologyId = "0",
                SourceTopics = [topic],
                SourceTopicRegex = [],
                StateChangelogTopics = [],
                RepartitionSinkTopics = [],
                RepartitionSourceTopics = [],
                CopartitionGroups = []
            }
        ]
    };
}
