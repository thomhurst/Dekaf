using Dekaf.Streams;
using Dekaf.Testing;

namespace Dekaf.Tests.Unit.Testing;

public sealed class InMemoryStreamsGroupMemberTests
{
    [Test]
    public async Task JoinAsync_WithoutTopologyThrowsArgumentException()
    {
        await using var member = CreateMember();

        var exception = Assert.Throws<ArgumentException>(() =>
            member.JoinAsync(new StreamsGroupMemberUpdate()));

        await Assert.That(exception.ParamName).IsEqualTo("initialState");
        await Assert.That(member.Snapshot.IsJoined).IsFalse();
    }

    [Test]
    public async Task JoinAsync_PublishesInitialAssignmentAndSnapshot()
    {
        await using var member = new InMemoryStreamsGroupMember(new StreamsGroupMemberOptions
        {
            GroupId = "streams-group",
            InstanceId = "instance-a",
            RackId = "rack-a"
        });
        var activeTasks = new[]
        {
            new StreamsGroupTaskSet { SubtopologyId = "sub-0", Partitions = [0, 1] }
        };
        var update = new StreamsGroupMemberUpdate
        {
            EndpointInformationEpoch = 7,
            Topology = CreateTopology(),
            ActiveTasks = activeTasks,
            StandbyTasks = [],
            WarmupTasks = [],
            ProcessId = "process-a",
            UserEndpoint = new StreamsGroupEndpoint { Host = "localhost", Port = 8080 },
            ClientTags = [new StreamsGroupKeyValue { Key = "zone", Value = "a" }],
            TaskOffsets = [new StreamsGroupTaskOffset { SubtopologyId = "sub-0", Partition = 0, Offset = 10 }],
            TaskEndOffsets = [new StreamsGroupTaskOffset { SubtopologyId = "sub-0", Partition = 0, Offset = 20 }]
        };

        var result = await member.JoinAsync(update);

        await Assert.That(member.GroupId).IsEqualTo("streams-group");
        await Assert.That(member.InstanceId).IsEqualTo("instance-a");
        await Assert.That(member.LastUpdate).IsSameReferenceAs(update);
        await Assert.That(result.MemberEpoch).IsEqualTo(1);
        await Assert.That(result.ActiveTasks![0].SubtopologyId).IsEqualTo("sub-0");
        await Assert.That(member.Snapshot.IsJoined).IsTrue();
        await Assert.That(member.Snapshot.IsClosed).IsFalse();
        await Assert.That(member.Snapshot.EndpointInformationEpoch).IsEqualTo(7);
        await Assert.That(member.Snapshot.Assignment.ActiveTasks[0].Partitions).IsEquivalentTo([0, 1]);
    }

    [Test]
    public async Task UpdateAndReportOffsets_PreserveUnchangedAssignment()
    {
        await using var member = CreateMember();
        await member.JoinAsync(new StreamsGroupMemberUpdate
        {
            Topology = CreateTopology(),
            ActiveTasks = [new StreamsGroupTaskSet { SubtopologyId = "sub-0", Partitions = [2] }],
            StandbyTasks = [],
            WarmupTasks = []
        });

        var updateResult = await member.UpdateAsync(new StreamsGroupMemberUpdate
        {
            EndpointInformationEpoch = 3,
            ClientTags = [new StreamsGroupKeyValue { Key = "build", Value = "42" }]
        });
        var report = new StreamsGroupTaskOffsetReport
        {
            TaskOffsets = [new StreamsGroupTaskOffset { SubtopologyId = "sub-0", Partition = 2, Offset = 5 }],
            TaskEndOffsets = [new StreamsGroupTaskOffset { SubtopologyId = "sub-0", Partition = 2, Offset = 8 }]
        };
        var reportResult = await member.ReportTaskOffsetsAsync(report);

        await Assert.That(updateResult.ActiveTasks).IsNull();
        await Assert.That(member.Snapshot.Assignment.ActiveTasks[0].Partitions).IsEquivalentTo([2]);
        await Assert.That(member.Snapshot.EndpointInformationEpoch).IsEqualTo(3);
        await Assert.That(member.LastTaskOffsetReport).IsSameReferenceAs(report);
        await Assert.That(reportResult.ActiveTasks).IsNull();
    }

    [Test]
    public async Task UpdateAsync_TopologyRejoinClearsOmittedAssignment()
    {
        await using var member = CreateMember();
        await member.JoinAsync(new StreamsGroupMemberUpdate
        {
            Topology = CreateTopology(),
            ActiveTasks = [new StreamsGroupTaskSet { SubtopologyId = "active", Partitions = [0] }],
            StandbyTasks = [new StreamsGroupTaskSet { SubtopologyId = "standby", Partitions = [1] }],
            WarmupTasks = [new StreamsGroupTaskSet { SubtopologyId = "warmup", Partitions = [2] }]
        });

        var result = await member.UpdateAsync(new StreamsGroupMemberUpdate
        {
            Topology = CreateTopology()
        });

        await Assert.That(result.ActiveTasks).IsNull();
        await Assert.That(result.StandbyTasks).IsNull();
        await Assert.That(result.WarmupTasks).IsNull();
        await Assert.That(member.Snapshot.Assignment.ActiveTasks).IsEmpty();
        await Assert.That(member.Snapshot.Assignment.StandbyTasks).IsEmpty();
        await Assert.That(member.Snapshot.Assignment.WarmupTasks).IsEmpty();
    }

    [Test]
    public async Task JoinAndUpdateAsync_DeepCopyPublishedTaskSets()
    {
        await using var member = CreateMember();
        var joinPartitions = new List<int> { 0, 1 };
        var joinTasks = new List<StreamsGroupTaskSet>
        {
            new() { SubtopologyId = "join", Partitions = joinPartitions }
        };

        var joinResult = await member.JoinAsync(new StreamsGroupMemberUpdate
        {
            Topology = CreateTopology(),
            ActiveTasks = joinTasks
        });

        joinPartitions[0] = 99;
        joinTasks.Clear();
        await Assert.That(joinResult.ActiveTasks![0].Partitions).IsEquivalentTo([0, 1]);
        await Assert.That(member.Snapshot.Assignment.ActiveTasks[0].Partitions).IsEquivalentTo([0, 1]);

        var updatePartitions = new List<int> { 2, 3 };
        var updateTasks = new List<StreamsGroupTaskSet>
        {
            new() { SubtopologyId = "update", Partitions = updatePartitions }
        };

        var updateResult = await member.UpdateAsync(new StreamsGroupMemberUpdate
        {
            StandbyTasks = updateTasks
        });

        updatePartitions.Add(4);
        updateTasks[0] = new StreamsGroupTaskSet { SubtopologyId = "replacement", Partitions = [9] };
        await Assert.That(updateResult.StandbyTasks![0].Partitions).IsEquivalentTo([2, 3]);
        await Assert.That(member.Snapshot.Assignment.StandbyTasks[0].Partitions).IsEquivalentTo([2, 3]);
    }

    [Test]
    [Arguments(null, StreamsGroupMembershipOperation.Default, false)]
    [Arguments("static-a", StreamsGroupMembershipOperation.Default, true)]
    [Arguments(null, StreamsGroupMembershipOperation.RemainInGroup, true)]
    [Arguments("static-a", StreamsGroupMembershipOperation.LeaveGroup, false)]
    public async Task CloseAsync_AppliesExplicitMembershipBehavior(
        string? instanceId,
        StreamsGroupMembershipOperation operation,
        bool remainsJoined)
    {
        var member = new InMemoryStreamsGroupMember(new StreamsGroupMemberOptions
        {
            GroupId = "streams-group",
            InstanceId = instanceId
        });
        await member.JoinAsync(new StreamsGroupMemberUpdate { Topology = CreateTopology() });
        var closeOptions = new StreamsGroupCloseOptions
        {
            GroupMembershipOperation = operation,
            ShutdownApplication = true
        };

        await member.CloseAsync(closeOptions);

        await Assert.That(member.Snapshot.IsClosed).IsTrue();
        await Assert.That(member.Snapshot.IsJoined).IsEqualTo(remainsJoined);
        await Assert.That(member.LastCloseOptions).IsSameReferenceAs(closeOptions);
        await member.DisposeAsync();
    }

    [Test]
    public async Task UpdateAsync_BeforeJoin_Throws()
    {
        await using var member = CreateMember();

        var action = async () => await member.UpdateAsync(new StreamsGroupMemberUpdate());

        await Assert.That(action).Throws<InvalidOperationException>();
    }

    private static InMemoryStreamsGroupMember CreateMember() =>
        new(new StreamsGroupMemberOptions { GroupId = "streams-group" });

    private static StreamsGroupTopology CreateTopology() => new()
    {
        Epoch = 4,
        Subtopologies =
        [
            new StreamsGroupSubtopology
            {
                SubtopologyId = "sub-0",
                SourceTopics = ["input"],
                SourceTopicRegex = ["input-.*"],
                StateChangelogTopics =
                [
                    new StreamsGroupTopicInfo
                    {
                        Name = "store-changelog",
                        Partitions = 2,
                        ReplicationFactor = 3,
                        TopicConfigs = [new StreamsGroupKeyValue { Key = "cleanup.policy", Value = "compact" }]
                    }
                ],
                RepartitionSinkTopics = ["sink"],
                RepartitionSourceTopics = [],
                CopartitionGroups =
                [
                    new StreamsGroupCopartitionGroup
                    {
                        SourceTopics = [0],
                        SourceTopicRegex = [0],
                        RepartitionSourceTopics = []
                    }
                ]
            }
        ]
    };
}
