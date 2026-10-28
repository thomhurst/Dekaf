using Dekaf.Errors;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.Streams;
using static Dekaf.Admin.AdminClientStreamsGroupDescriptionExtensions;

namespace Dekaf.Tests.Integration;

[Category("ConsumerGroup")]
[SupportsKafka(420)]
public sealed class StreamsGroupMemberIntegrationTests(KafkaTestContainer kafka)
    : KafkaIntegrationTest(kafka)
{
    // Kafka 4.3.1 rejects task offsets and static membership as not yet supported.
    // Kafka 4.4 completes the KIP-1071 lifecycle used by these gated tests.
    private const int CompleteStreamsGroupSupport = 440;

    // StreamsGroupDescribe v1, which reports the group's task assignor (KIP-1357), ships in Kafka 4.4.
    private const int AssignorNameSupport = 440;

    // Kafka 4.4.0 still answers "Topology updates are not supported yet."
    private const int TopologyUpdateSupport = 450;

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
        // Older coordinators negotiate StreamsGroupDescribe v0, which carries no assignor name.
        await Assert.That(described[groupId].AssignorName)
            .IsEqualTo(KafkaContainer.Version >= AssignorNameSupport ? "sticky" : null);

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
    [SupportsKafka(AssignorNameSupport)]
    public async Task Kafka44SelectsAndDescribesStreamsAssignor()
    {
        const string assignorConfig = "streams.assignor.name";
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 1).ConfigureAwait(false);
        var groupId = $"streams-group-{Guid.NewGuid():N}";
        await using var client = Kafka.Connect(KafkaContainer.BootstrapServers, builder =>
            builder.WithLoggerFactory(GlobalTestSetup.GetLoggerFactory()));
        await using var member = client.CreateStreamsGroupMember(new StreamsGroupMemberOptions
        {
            GroupId = groupId
        });
        await member.InitializeAsync();
        await member.JoinAsync(CreateInitialState(topic, "process-assignor"));
        await using var admin = client.CreateAdminClient().Build();
        var group = new Dekaf.Admin.ConfigResource { Type = Dekaf.Admin.ConfigResourceType.Group, Name = groupId };

        // The test broker registers only the built-in assignor, which is also the default.
        var unselected = await admin.DescribeStreamsGroupsAsync([groupId]);
        await Assert.That(unselected[groupId].AssignorName).IsEqualTo("sticky");

        await admin.IncrementalAlterConfigsAsync(new Dictionary<Dekaf.Admin.ConfigResource, IReadOnlyList<Dekaf.Admin.ConfigAlter>>
        {
            [group] = [Dekaf.Admin.ConfigAlter.Set(assignorConfig, "sticky")]
        });
        var selected = await admin.DescribeStreamsGroupsAsync([groupId]);
        await Assert.That(selected[groupId].AssignorName).IsEqualTo("sticky");

        var rejected = await Assert.That(async () => await admin.IncrementalAlterConfigsAsync(
                new Dictionary<Dekaf.Admin.ConfigResource, IReadOnlyList<Dekaf.Admin.ConfigAlter>>
                {
                    [group] = [Dekaf.Admin.ConfigAlter.Set(assignorConfig, "unregistered-assignor")]
                }))
            .Throws<KafkaException>();
        await Assert.That(rejected!.ErrorCode).IsEqualTo(ErrorCode.InvalidConfig);
        var afterRejection = await admin.DescribeStreamsGroupsAsync([groupId]);
        await Assert.That(afterRejection[groupId].AssignorName).IsEqualTo("sticky");

        await member.CloseAsync(new StreamsGroupCloseOptions
        {
            GroupMembershipOperation = StreamsGroupMembershipOperation.LeaveGroup
        });
    }

    [Test]
    [SupportsKafka(CompleteStreamsGroupSupport)]
    public async Task Kafka44DescribeReportsTopologyDescriptionStatus()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 1).ConfigureAwait(false);
        var groupId = $"streams-group-{Guid.NewGuid():N}";
        await using var client = Kafka.Connect(KafkaContainer.BootstrapServers, builder =>
            builder.WithLoggerFactory(GlobalTestSetup.GetLoggerFactory()));
        await using var member = client.CreateStreamsGroupMember(new StreamsGroupMemberOptions
        {
            GroupId = groupId
        });
        await member.InitializeAsync();
        await member.JoinAsync(CreateInitialState(topic, "process-description"));
        await using var admin = client.CreateAdminClient().Build();

        var withoutDescription = await admin.DescribeStreamsGroupsAsync([groupId]);
        // The test broker has no topology-description plugin, so no description is ever stored.
        var withDescription = await admin.DescribeStreamsGroupsAsync(
            [groupId],
            new Dekaf.Admin.DescribeStreamsGroupsOptions { IncludeTopologyDescription = true });

        await Assert.That(withoutDescription[groupId].TopologyDescriptionStatus)
            .IsEqualTo(Dekaf.Admin.StreamsGroupTopologyDescriptionStatus.NotRequested);
        await Assert.That(withDescription[groupId].TopologyDescriptionStatus)
            .IsEqualTo(Dekaf.Admin.StreamsGroupTopologyDescriptionStatus.NotStored);
        await Assert.That(withDescription[groupId].TopologyDescription).IsNull();
        await Assert.That(withDescription[groupId].Members.Single().MemberId).IsEqualTo(member.Snapshot.MemberId);
        await member.CloseAsync(new StreamsGroupCloseOptions
        {
            GroupMembershipOperation = StreamsGroupMembershipOperation.LeaveGroup
        });
    }

    [Test]
    [SupportsKafka(CompleteStreamsGroupSupport)]
    public async Task Kafka44NegotiatesHeartbeatV1ForMembership()
    {
        await using var pool = new ConnectionPool(
            "streams-heartbeat-version",
            new ConnectionOptions { RequestTimeout = TimeSpan.FromSeconds(30) },
            loggerFactory: null);
        var endpoint = BootstrapServerList.Parse(KafkaContainer.BootstrapServers);
        var connection = await pool.GetConnectionAsync(endpoint.Host, endpoint.Port, CancellationToken.None);
        var negotiated = ((IKafkaCapabilityProvider)connection).Capabilities.NegotiateVersion(
            ApiKey.StreamsGroupHeartbeat,
            StreamsGroupHeartbeatRequest.LowestSupportedVersion,
            StreamsGroupHeartbeatRequest.HighestSupportedVersion);
        await Assert.That(negotiated).IsEqualTo((short)1);

        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 1).ConfigureAwait(false);
        await using var client = Kafka.Connect(KafkaContainer.BootstrapServers, builder =>
            builder.WithLoggerFactory(GlobalTestSetup.GetLoggerFactory()));
        await using var member = client.CreateStreamsGroupMember(new StreamsGroupMemberOptions
        {
            GroupId = $"streams-group-{Guid.NewGuid():N}"
        });
        await member.InitializeAsync();

        var join = await member.JoinAsync(CreateInitialState(topic, "process-v1"));

        await Assert.That(join.MemberEpoch).IsGreaterThan(0);
        await Assert.That(member.Snapshot.AcceptableRecoveryLagOffsets).IsEqualTo(join.AcceptableRecoveryLagOffsets);
        await member.CloseAsync(new StreamsGroupCloseOptions
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
    [SupportsKafka(TopologyUpdateSupport, "KIP-1071 topology updates for joined members did not ship in Kafka 4.4.0; tracked in #3625")]
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
        await Assert.That(restarted.Snapshot.IsJoined).IsTrue();

        // Member IDs are client-generated (KIP-1082), so the restarted process joins with a
        // new ID. Static identity is the instance ID: the broker replaces the old member.
        var restartedMemberId = restarted.Snapshot.MemberId;
        var replaced = await admin.DescribeStreamsGroupsAsync([groupId]);
        var staticMembers = replaced[groupId].Members
            .Where(member => member.InstanceId == instanceId)
            .ToList();
        await Assert.That(staticMembers).Count().IsEqualTo(1);
        await Assert.That(staticMembers[0].MemberId).IsEqualTo(restartedMemberId);
        await Assert.That(replaced[groupId].Members
            .Any(member => member.MemberId == firstMemberId)).IsFalse();

        await restarted.CloseAsync(new StreamsGroupCloseOptions
        {
            GroupMembershipOperation = StreamsGroupMembershipOperation.RemainInGroup
        });
        var retained = await admin.DescribeStreamsGroupsAsync([groupId]);
        await Assert.That(retained[groupId].Members
            .Any(member => member.MemberId == restartedMemberId)).IsTrue();

        await TestWait.WaitForConditionAsync(
            async () => (await admin.DescribeStreamsGroupsAsync([groupId]))[groupId].Members,
            members => members.All(member => member.MemberId != restartedMemberId),
            initialDelayMs: 250,
            description: $"Streams member {restartedMemberId} to expire from group {groupId}");

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

        await ((StreamsGroupMember)member).SuspendHeartbeatsForTestingAsync();

        await RemoveRejoinAndFenceRawMemberAsync(groupId, memberId, staleEpoch, topic);

        var recovered = await member.UpdateAsync(new StreamsGroupMemberUpdate
        {
            ProcessId = "recovered-process"
        });
        await Assert.That(recovered.MemberId).IsEqualTo(memberId);
        await Assert.That(recovered.MemberEpoch).IsGreaterThan(staleEpoch);
        await Assert.That(member.Snapshot.IsJoined).IsTrue();
    }

    [Test]
    [SupportsKafka(CompleteStreamsGroupSupport)]
    public async Task StaticMemberRecoversAfterBrokerFencesItsEpoch()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync().ConfigureAwait(false);
        var groupId = $"streams-group-{Guid.NewGuid():N}";
        var instanceId = $"streams-instance-{Guid.NewGuid():N}";
        await using var client = Kafka.Connect(KafkaContainer.BootstrapServers, builder =>
            builder.WithLoggerFactory(GlobalTestSetup.GetLoggerFactory()));
        await using var member = CreateStaticMember(client, groupId, instanceId);
        await member.InitializeAsync();
        await member.JoinAsync(CreateInitialState(topic, "static-fenced-process"));
        await using var admin = client.CreateAdminClient().Build();
        await ReceiveActiveTasksAsync(admin, member, [0]);
        var staleEpoch = member.Snapshot.MemberEpoch;
        var memberId = member.Snapshot.MemberId!;

        await ((StreamsGroupMember)member).SuspendHeartbeatsForTestingAsync();

        await RemoveRejoinAndFenceRawMemberAsync(groupId, memberId, staleEpoch, topic, instanceId);

        // The recovery rejoin must use epoch 0: epoch -2 would make the broker treat it as a
        // temporary static leave, and the member would silently drop out of the group.
        var recovered = await member.UpdateAsync(new StreamsGroupMemberUpdate
        {
            ProcessId = "static-recovered-process"
        });
        await Assert.That(recovered.MemberId).IsEqualTo(memberId);
        await Assert.That(recovered.MemberEpoch).IsGreaterThan(staleEpoch);
        await Assert.That(member.Snapshot.IsJoined).IsTrue();

        var described = await admin.DescribeStreamsGroupsAsync([groupId]);
        var brokerMember = described[groupId].Members.Single(m => m.InstanceId == instanceId);
        await Assert.That(brokerMember.MemberId).IsEqualTo(memberId);
        await Assert.That(brokerMember.MemberEpoch).IsGreaterThan(0);
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
        var memberId = member.Snapshot.MemberId;
        var expected = expectedPartitions.ToHashSet();
        var targeted = await TestWait.WaitForConditionAsync(
            async () =>
            {
                var descriptions = await admin.DescribeStreamsGroupsAsync([member.GroupId]);
                return descriptions[member.GroupId].Members
                    .SingleOrDefault(candidate => candidate.MemberId == memberId)?
                    .TargetAssignment.ActiveTasks
                    .SelectMany(static taskSet => taskSet.Partitions)
                    .ToArray() ?? [];
            },
            expected.SetEquals,
            description: $"Streams target assignment for member {memberId}",
            formatObserved: static partitions => $"[{string.Join(", ", partitions)}]");
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
        string topic,
        string? instanceId = null)
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
                MemberEpoch = -1,
                InstanceId = instanceId
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
                InstanceId = instanceId,
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
                InstanceId = instanceId,
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
