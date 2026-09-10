using Dekaf.Admin;
using Dekaf.Protocol;
using Dekaf.Testing;

namespace Dekaf.Tests.Unit.Testing;

public sealed class InMemoryStaticMemberStorageTests
{
    [Test]
    [Arguments(null)]
    [Arguments("instance")]
    public async Task RegistrationSnapshotsDistinctSubscriptions(string? groupInstanceId)
    {
        var cluster = new InMemoryKafkaCluster();
        var partition = new TopicPartition("input", 0);
        var subscribed = new List<TopicPartition> { partition, partition };
        cluster.RegisterConsumerGroupMember("group", "member", subscribed, out var registration, groupInstanceId);
        subscribed.Clear();
        var assigned = cluster.GetConsumerGroupAssignment("group", "member", registration, out var generation);
        await Assert.That(generation).IsGreaterThan(0);
        await Assert.That(assigned).Count().IsEqualTo(1);
        await Assert.That(assigned.Contains(partition)).IsTrue();
    }

    [Test]
    public async Task AddingStaticMemberPreservesExistingDynamicRegistration()
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.RegisterConsumerGroupMember("group", "dynamic", [], out var registration);
        cluster.RegisterConsumerGroupMember("group", "static", [], out _, "instance");
        await Assert.That(cluster.SnapshotConsumerGroupMembers("group")).Count().IsEqualTo(2);
        cluster.UnregisterConsumerGroupMember("group", "dynamic", registration);
        var remaining = cluster.SnapshotConsumerGroupMembers("group");
        await Assert.That(remaining).Count().IsEqualTo(1);
        await Assert.That(remaining[0].GroupInstanceId).IsEqualTo("instance");
    }

    [Test]
    [Arguments(null)]
    [Arguments("new-instance")]
    public async Task ReusingMemberIdDropsPreviousStaticIdentity(string? replacementInstance)
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.RegisterConsumerGroupMember("group", "member", [], out var original, "old-instance");
        cluster.RegisterConsumerGroupMember("group", "member", [], out _, replacementInstance);
        cluster.UnregisterConsumerGroupMember("group", "member", original);
        await using var admin = new InMemoryAdminClient(cluster);
        var old = await admin.RemoveMembersFromConsumerGroupAsync("group",
            [new ConsumerGroupMemberToRemove { GroupInstanceId = "old-instance" }]);
        await Assert.That(old.Members[0].ErrorCode).IsEqualTo(ErrorCode.UnknownMemberId);
        var remaining = cluster.SnapshotConsumerGroupMembers("group");
        await Assert.That(remaining).Count().IsEqualTo(1);
        await Assert.That(remaining[0].GroupInstanceId).IsEqualTo(replacementInstance);
        await Assert.That(remaining[0].MemberId).IsEqualTo(replacementInstance is null ? "member" : null);
    }

    [Test]
    public async Task StaticReplacementAndGroupReuseDoNotLeaveStaleSelectors()
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.RegisterConsumerGroupMember("group", "old", [], out var original, "instance");
        cluster.RegisterConsumerGroupMember("group", "replacement", [], out _, "instance");
        cluster.RegisterConsumerGroupMember("other", "other-member", [], out _, "instance");
        cluster.UnregisterConsumerGroupMember("group", "old", original);
        await using var admin = new InMemoryAdminClient(cluster);
        var removed = await admin.RemoveMembersFromConsumerGroupAsync("group",
            [new ConsumerGroupMemberToRemove { GroupInstanceId = "instance" }]);
        await Assert.That(removed.Members[0].MemberId).IsEqualTo("replacement");
        await Assert.That(removed.Succeeded).IsTrue();
        await Assert.That(cluster.SnapshotConsumerGroupMembers("other")).Count().IsEqualTo(1);
        cluster.RegisterConsumerGroupMember("group", "replacement", [], out _);
        var old = await admin.RemoveMembersFromConsumerGroupAsync("group",
            [new ConsumerGroupMemberToRemove { GroupInstanceId = "instance" }]);
        await Assert.That(old.Members[0].ErrorCode).IsEqualTo(ErrorCode.UnknownMemberId);
        await Assert.That(cluster.SnapshotConsumerGroupMembers("group")[0].MemberId).IsEqualTo("replacement");
    }
}
