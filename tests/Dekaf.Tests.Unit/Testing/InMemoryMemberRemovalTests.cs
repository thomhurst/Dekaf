using Dekaf.Admin;
using Dekaf.Errors;
using Dekaf.Protocol;
using Dekaf.Testing;

namespace Dekaf.Tests.Unit.Testing;

public sealed class InMemoryMemberRemovalTests
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ZeroDeadline_PreservesMembership(bool removeAll)
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.RegisterConsumerGroupMember("group", "member", [], out _);
        await using var concrete = new InMemoryAdminClient(cluster);
        // Defeat the asynchronous timer deterministically: zero must expire before this hook.
        concrete.ConfigureTimeoutSourceTestHook = static source => source.CancelAfter(Timeout.Infinite);
        var exception = await Assert.That(async () => await ((IAdminClient)concrete).RemoveMembersFromConsumerGroupAsync("group",
            new ConsumerGroupMemberRemovalOptions
            {
                RemoveAll = removeAll, TimeoutMs = 0,
                Members = removeAll ? [] : [new ConsumerGroupMemberIdentity { MemberId = "member" }]
            })).Throws<KafkaTimeoutException>();
        await Assert.That(exception!.Configured).IsEqualTo(TimeSpan.Zero);
        await Assert.That(cluster.SnapshotConsumerGroupMembers("group")).Count().IsEqualTo(1);
    }

    [Test]
    public async Task RemoveAll_EvictsStaticAndDynamicMembersButDoesNotBanRejoins()
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.RegisterConsumerGroupMember("group", "static-member", [], out var original, "instance");
        cluster.RegisterConsumerGroupMember("group", "dynamic", [], out _);
        await using var concrete = new InMemoryAdminClient(cluster);
        IAdminClient admin = concrete;
        var result = await admin.RemoveMembersFromConsumerGroupAsync("group", new ConsumerGroupMemberRemovalOptions { RemoveAll = true });
        await Assert.That(result.Members).Count().IsEqualTo(2);
        await Assert.That(result.Succeeded).IsTrue();
        await Assert.That(cluster.SnapshotConsumerGroupMembers("group")).IsEmpty();
        cluster.RegisterConsumerGroupMember("group", "static-member", [], out _, "instance");
        cluster.UnregisterConsumerGroupMember("group", "static-member", original);
        await Assert.That(cluster.SnapshotConsumerGroupMembers("group")).Count().IsEqualTo(1);
    }

    [Test]
    public async Task ExplicitSelection_PreservesUntargetedMembersAndUnknownOutcomes()
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.RegisterConsumerGroupMember("group", "keep", [], out _);
        cluster.RegisterConsumerGroupMember("group", "remove", [], out _);
        await using var concrete = new InMemoryAdminClient(cluster);
        var result = await ((IAdminClient)concrete).RemoveMembersFromConsumerGroupAsync("group", new ConsumerGroupMemberRemovalOptions
        {
            Members = [new ConsumerGroupMemberIdentity { MemberId = "remove" }, new ConsumerGroupMemberIdentity { MemberId = "missing" }]
        });
        await Assert.That(result.Members[0].Succeeded).IsTrue();
        await Assert.That(result.Members[1].ErrorCode).IsEqualTo(ErrorCode.UnknownMemberId);
        await Assert.That(cluster.SnapshotConsumerGroupMembers("group").Single().MemberId).IsEqualTo("keep");
    }

    [Test]
    public async Task LegacyStaticRemoval_UsesTheSameMembershipState()
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.RegisterConsumerGroupMember("group", "member", [], out _, "instance");
        await using var admin = new InMemoryAdminClient(cluster);
        var result = await admin.RemoveMembersFromConsumerGroupAsync("group", [new ConsumerGroupMemberToRemove { GroupInstanceId = "instance" }]);
        await Assert.That(result.Succeeded).IsTrue();
        await Assert.That(cluster.SnapshotConsumerGroupMembers("group")).IsEmpty();
        await Assert.That(async () => await admin.RemoveMembersFromConsumerGroupAsync("group", []))
            .Throws<ArgumentException>();
    }

    [Test]
    public async Task CancellationAndDeadline_PreserveMembership()
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.RegisterConsumerGroupMember("group", "member", [], out _);
        await using var concrete = new InMemoryAdminClient(cluster);
        IAdminClient admin = concrete;
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        await Assert.That(async () => await admin.RemoveMembersFromConsumerGroupAsync("group",
            new ConsumerGroupMemberRemovalOptions { RemoveAll = true }, cancellation.Token)).Throws<OperationCanceledException>();
        concrete.ConfigureTimeoutSourceTestHook = static source => source.Cancel();
        await Assert.That(async () => await admin.RemoveMembersFromConsumerGroupAsync("group",
            new ConsumerGroupMemberRemovalOptions { RemoveAll = true })).Throws<KafkaTimeoutException>();
        await Assert.That(cluster.SnapshotConsumerGroupMembers("group")).Count().IsEqualTo(1);
    }

    [Test]
    public async Task RemoveAll_RejectsNonconsumerGroupsAndReportsMissingGroups()
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.RegisterShareGroupMember("share", "member");
        await using var concrete = new InMemoryAdminClient(cluster);
        IAdminClient admin = concrete;
        var unsupported = await Assert.That(async () => await admin.RemoveMembersFromConsumerGroupAsync("share",
            new ConsumerGroupMemberRemovalOptions { RemoveAll = true })).Throws<GroupException>();
        await Assert.That(unsupported!.ErrorCode).IsEqualTo(ErrorCode.UnsupportedVersion);
        var missing = await Assert.That(async () => await admin.RemoveMembersFromConsumerGroupAsync("missing",
            new ConsumerGroupMemberRemovalOptions { RemoveAll = true })).Throws<GroupException>();
        await Assert.That(missing!.ErrorCode).IsEqualTo(ErrorCode.GroupIdNotFound);
    }
}
