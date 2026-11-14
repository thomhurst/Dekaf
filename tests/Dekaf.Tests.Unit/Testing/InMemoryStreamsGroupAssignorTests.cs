using Dekaf.Streams;
using Dekaf.Testing;

namespace Dekaf.Tests.Unit.Testing;

public sealed class InMemoryStreamsGroupAssignorTests
{
    [Test]
    public async Task Join_SingleMemberGetsEveryTaskAndDistinctMemberIds()
    {
        var cluster = CreateCluster();
        await using var first = await JoinAsync(cluster, "streams-group");
        await using var other = await JoinAsync(cluster, "other-group");

        await Assert.That(Tasks(first)).IsEquivalentTo(["sub-0:0", "sub-0:1", "sub-0:2", "sub-0:3"]);
        await Assert.That(Tasks(other)).IsEquivalentTo(["sub-0:0", "sub-0:1", "sub-0:2", "sub-0:3"]);
        await Assert.That(first.Snapshot.MemberId).IsNotNull();
    }

    [Test]
    public async Task Join_SecondMemberSplitsActiveTasks()
    {
        var cluster = CreateCluster();
        await using var first = await JoinAsync(cluster, "streams-group");
        var firstEpoch = first.Snapshot.MemberEpoch;

        await using var second = new InMemoryStreamsGroupMember(cluster, Options("streams-group"));
        await second.InitializeAsync();
        var joinResult = await second.JoinAsync(new StreamsGroupMemberUpdate { Topology = CreateTopology() });

        await Assert.That(second.Snapshot.MemberId).IsNotEqualTo(first.Snapshot.MemberId);
        await Assert.That(Tasks(first)).IsEquivalentTo(["sub-0:0", "sub-0:2"]);
        await Assert.That(Tasks(second)).IsEquivalentTo(["sub-0:1", "sub-0:3"]);
        await Assert.That(joinResult.ActiveTasks![0].Partitions).IsEquivalentTo([1, 3]);
        await Assert.That(first.Snapshot.MemberEpoch).IsGreaterThan(firstEpoch);

        var firstHeartbeat = await first.UpdateAsync(new StreamsGroupMemberUpdate());
        var repeatHeartbeat = await first.UpdateAsync(new StreamsGroupMemberUpdate());
        await Assert.That(firstHeartbeat.ActiveTasks![0].Partitions).IsEquivalentTo([0, 2]);
        await Assert.That(repeatHeartbeat.ActiveTasks).IsNull();
    }

    [Test]
    public async Task Leave_MovesTasksToRemainingMembers()
    {
        var cluster = CreateCluster();
        await using var first = await JoinAsync(cluster, "streams-group");
        var second = await JoinAsync(cluster, "streams-group");
        await using var third = await JoinAsync(cluster, "streams-group");
        await Assert.That(Tasks(second)).IsEquivalentTo(["sub-0:1"]);

        await second.CloseAsync();

        await Assert.That(second.Snapshot.IsJoined).IsFalse();
        await Assert.That(Tasks(first)).IsEquivalentTo(["sub-0:0", "sub-0:2"]);
        await Assert.That(Tasks(third)).IsEquivalentTo(["sub-0:1", "sub-0:3"]);
        var heartbeat = await third.ReportTaskOffsetsAsync(new StreamsGroupTaskOffsetReport
        {
            TaskOffsets = [],
            TaskEndOffsets = []
        });
        await Assert.That(heartbeat.ActiveTasks![0].Partitions).IsEquivalentTo([1, 3]);
    }

    [Test]
    public async Task StaticMember_KeepsTasksWhileAwayAndReclaimsThemOnRejoin()
    {
        var cluster = CreateCluster();
        await using var first = await JoinAsync(cluster, "streams-group");
        var staticMember = await JoinAsync(cluster, "streams-group", instanceId: "static-a");
        var memberId = staticMember.Snapshot.MemberId;

        await staticMember.CloseAsync();
        await Assert.That(Tasks(first)).IsEquivalentTo(["sub-0:0", "sub-0:2"]);

        await using var replacement = await JoinAsync(cluster, "streams-group", instanceId: "static-a");
        await Assert.That(replacement.Snapshot.MemberId).IsEqualTo(memberId);
        await Assert.That(Tasks(replacement)).IsEquivalentTo(["sub-0:1", "sub-0:3"]);
        await Assert.That(Tasks(first)).IsEquivalentTo(["sub-0:0", "sub-0:2"]);
    }

    [Test]
    public async Task StaticMember_ClosedMemberGainsNoTasksWhenAnotherMemberLeaves()
    {
        var cluster = CreateCluster();
        await using var first = await JoinAsync(cluster, "streams-group");
        var staticMember = await JoinAsync(cluster, "streams-group", instanceId: "static-a");
        var third = await JoinAsync(cluster, "streams-group");
        await Assert.That(Tasks(staticMember)).IsEquivalentTo(["sub-0:1"]);

        await staticMember.CloseAsync();
        await third.CloseAsync();

        await Assert.That(Tasks(staticMember)).IsEquivalentTo(["sub-0:1"]);
        await Assert.That(Tasks(first)).IsEquivalentTo(["sub-0:0", "sub-0:2", "sub-0:3"]);
    }

    [Test]
    public async Task DynamicMember_RemainInGroupCloseReleasesItsTasks()
    {
        var cluster = CreateCluster();
        await using var first = await JoinAsync(cluster, "streams-group");
        var second = await JoinAsync(cluster, "streams-group");

        await second.CloseAsync(new StreamsGroupCloseOptions
        {
            GroupMembershipOperation = StreamsGroupMembershipOperation.RemainInGroup
        });

        await Assert.That(Tasks(first)).IsEquivalentTo(["sub-0:0", "sub-0:1", "sub-0:2", "sub-0:3"]);
    }

    [Test]
    public async Task Join_RoundRobinContinuesAcrossSubtopologiesAndUsesLargestSourcePartitionCount()
    {
        var cluster = CreateCluster();
        cluster.CreateTopic("wide", partitionCount: 3);
        var topology = new StreamsGroupTopology
        {
            Epoch = 1,
            Subtopologies =
            [
                Subtopology("sub-0", sourceTopics: ["input", "wide"]),
                Subtopology("sub-1", sourceTopics: [], repartitionSourceTopics:
                [
                    new StreamsGroupTopicInfo { Name = "absent-repartition", Partitions = 3, TopicConfigs = [] }
                ])
            ]
        };

        await using var first = new InMemoryStreamsGroupMember(cluster, Options("streams-group"));
        await first.InitializeAsync();
        await first.JoinAsync(new StreamsGroupMemberUpdate { Topology = topology });
        await using var second = new InMemoryStreamsGroupMember(cluster, Options("streams-group"));
        await second.InitializeAsync();
        await second.JoinAsync(new StreamsGroupMemberUpdate { Topology = topology });

        await Assert.That(Tasks(first)).IsEquivalentTo(["sub-0:0", "sub-0:2", "sub-1:0", "sub-1:2"]);
        await Assert.That(Tasks(second)).IsEquivalentTo(["sub-0:1", "sub-0:3", "sub-1:1"]);
        await Assert.That(first.Snapshot.Assignment.StandbyTasks).IsEmpty();
        await Assert.That(first.Snapshot.Assignment.WarmupTasks).IsEmpty();
        await Assert.That(second.Snapshot.Assignment.StandbyTasks).IsEmpty();
        await Assert.That(second.Snapshot.Assignment.WarmupTasks).IsEmpty();
    }

    [Test]
    public async Task Heartbeat_ReassignsAfterSourcePartitionsGrow()
    {
        var cluster = CreateCluster();
        await using var member = await JoinAsync(cluster, "streams-group");

        cluster.CreatePartitions("input", 5);
        var heartbeat = await member.UpdateAsync(new StreamsGroupMemberUpdate());

        await Assert.That(heartbeat.ActiveTasks![0].Partitions).IsEquivalentTo([0, 1, 2, 3, 4]);
    }

    [Test]
    public async Task Join_IgnoresCallerReportedTasks()
    {
        var cluster = CreateCluster();
        await using var member = new InMemoryStreamsGroupMember(cluster, Options("streams-group"));
        await member.InitializeAsync();
        await member.JoinAsync(new StreamsGroupMemberUpdate
        {
            Topology = CreateTopology(),
            ActiveTasks = [new StreamsGroupTaskSet { SubtopologyId = "sub-0", Partitions = [9] }]
        });

        await member.UpdateAsync(new StreamsGroupMemberUpdate
        {
            ActiveTasks = [new StreamsGroupTaskSet { SubtopologyId = "other", Partitions = [7] }]
        });

        await Assert.That(Tasks(member)).IsEquivalentTo(["sub-0:0", "sub-0:1", "sub-0:2", "sub-0:3"]);
    }

    private static InMemoryKafkaCluster CreateCluster()
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic("input", partitionCount: 4);
        return cluster;
    }

    private static async Task<InMemoryStreamsGroupMember> JoinAsync(
        InMemoryKafkaCluster cluster,
        string groupId,
        string? instanceId = null)
    {
        var member = new InMemoryStreamsGroupMember(cluster, Options(groupId, instanceId));
        await member.InitializeAsync();
        await member.JoinAsync(new StreamsGroupMemberUpdate { Topology = CreateTopology() });
        return member;
    }

    private static StreamsGroupMemberOptions Options(string groupId, string? instanceId = null) =>
        new() { GroupId = groupId, InstanceId = instanceId };

    private static string[] Tasks(InMemoryStreamsGroupMember member)
    {
        var tasks = new List<string>();
        foreach (var taskSet in member.Snapshot.Assignment.ActiveTasks)
        {
            foreach (var partition in taskSet.Partitions)
                tasks.Add($"{taskSet.SubtopologyId}:{partition}");
        }

        return tasks.ToArray();
    }

    private static StreamsGroupTopology CreateTopology() => new()
    {
        Epoch = 1,
        Subtopologies = [Subtopology("sub-0", sourceTopics: ["input"])]
    };

    private static StreamsGroupSubtopology Subtopology(
        string id,
        string[] sourceTopics,
        StreamsGroupTopicInfo[]? repartitionSourceTopics = null) => new()
    {
        SubtopologyId = id,
        SourceTopics = sourceTopics,
        SourceTopicRegex = [],
        StateChangelogTopics = [],
        RepartitionSinkTopics = [],
        RepartitionSourceTopics = repartitionSourceTopics ?? [],
        CopartitionGroups = []
    };
}
