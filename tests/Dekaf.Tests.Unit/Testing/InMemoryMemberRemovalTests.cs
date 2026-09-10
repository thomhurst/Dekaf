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
    public async Task AliasedIdentities_ResolveAgainstTheSameMembershipSnapshot(bool staticFirst)
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.RegisterConsumerGroupMember("group", "member", [], out _, "instance");
        cluster.RegisterConsumerGroupMember("group", "keep", [], out _);
        await using var admin = new InMemoryAdminClient(cluster);
        ConsumerGroupMemberIdentity byInstance = new() { GroupInstanceId = "instance" };
        ConsumerGroupMemberIdentity byMember = new() { MemberId = "member" };
        var result = await admin.RemoveMembersFromConsumerGroupAsync("group", new ConsumerGroupMemberRemovalOptions
        {
            Members = [staticFirst ? byInstance : byMember, staticFirst ? byMember : byInstance,
                new() { MemberId = "missing" }]
        });

        await Assert.That(result.Members[0].Succeeded).IsTrue();
        await Assert.That(result.Members[1].Succeeded).IsTrue();
        await Assert.That(result.Members[2].ErrorCode).IsEqualTo(ErrorCode.UnknownMemberId);
        await Assert.That(cluster.SnapshotConsumerGroupMembers("group").Single().MemberId).IsEqualTo("keep");
        var removedAlias = cluster.RemoveConsumerGroupMembers("group", [byInstance]);
        await Assert.That(removedAlias.Members[0].ErrorCode).IsEqualTo(ErrorCode.UnknownMemberId);
    }

    [Test]
    public async Task StaticConsumerMetadata_PreservesInstanceIdentity()
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic("topic");
        await using var consumer = new InMemoryConsumer<string, string>(cluster,
            new InMemoryConsumerOptions { GroupId = "group", GroupInstanceId = "instance" });
        consumer.Subscribe("topic");
        await Assert.That(consumer.ConsumerGroupMetadata!.GroupInstanceId).IsEqualTo("instance");
    }

    [Test]
    public async Task StaticReplacementAndRemoval_DoNotRetainStaleIdentityMappings()
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.RegisterConsumerGroupMember("group", "old", [], out var oldRegistration, "instance");
        cluster.RegisterConsumerGroupMember("group", "new", [], out var newRegistration, "instance");
        cluster.UnregisterConsumerGroupMember("group", "old", oldRegistration);
        await Assert.That(cluster.SnapshotConsumerGroupMembers("group")).Count().IsEqualTo(1);

        // Reusing a member ID with a different identity must remove its old alias.
        cluster.RegisterConsumerGroupMember("group", "new", [], out _, "replacement");
        var stale = cluster.RemoveConsumerGroupMembers("group", [new() { GroupInstanceId = "instance" }]);
        await Assert.That(stale.Members[0].ErrorCode).IsEqualTo(ErrorCode.UnknownMemberId);
        cluster.UnregisterConsumerGroupMember("group", "new", newRegistration);
        await Assert.That(cluster.SnapshotConsumerGroupMembers("group")).Count().IsEqualTo(1);

        var removed = cluster.RemoveConsumerGroupMembers("group", [new() { MemberId = "new" }]);
        await Assert.That(removed.Succeeded).IsTrue();
        cluster.RegisterConsumerGroupMember("group", "new", [], out _);
        var oldAlias = cluster.RemoveConsumerGroupMembers("group", [new() { GroupInstanceId = "replacement" }]);
        await Assert.That(oldAlias.Members[0].ErrorCode).IsEqualTo(ErrorCode.UnknownMemberId);
        await Assert.That(cluster.SnapshotConsumerGroupMembers("group").Single().MemberId).IsEqualTo("new");
    }

    [Test]
    public async Task StaticIdentity_IsScopedToGroupAndReusableAfterUnregister()
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.RegisterConsumerGroupMember("first", "member", [], out var registration, "same-instance");
        cluster.RegisterConsumerGroupMember("second", "member", [], out _, "same-instance");
        cluster.UnregisterConsumerGroupMember("first", "member", registration);
        cluster.RegisterConsumerGroupMember("first", "replacement", [], out _, "same-instance");
        await Assert.That(cluster.RemoveConsumerGroupMembers("first", [new() { GroupInstanceId = "same-instance" }]).Succeeded).IsTrue();
        await Assert.That(cluster.SnapshotConsumerGroupMembers("second")).Count().IsEqualTo(1);
    }

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
        var exception = await Assert.That(async () => await concrete.RemoveMembersFromConsumerGroupAsync("group",
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
        var result = await concrete.RemoveMembersFromConsumerGroupAsync("group", new ConsumerGroupMemberRemovalOptions
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
    [Arguments(0)]
    [Arguments(2)]
    public async Task LegacyStaticRemoval_ValidatesEveryMemberBeforeDuplicates(int nullIndex)
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.RegisterConsumerGroupMember("group", "member", [], out _, "instance");
        await using var admin = new InMemoryAdminClient(cluster);
        ConsumerGroupMemberToRemove[] members =
        [
            new() { GroupInstanceId = "instance" },
            new() { GroupInstanceId = "instance" },
            new() { GroupInstanceId = "instance" }
        ];
        members[nullIndex] = null!;
        var exception = await Assert.That(async () => await admin.RemoveMembersFromConsumerGroupAsync("group", members))
            .Throws<ArgumentNullException>();
        await Assert.That(exception!.ParamName).IsEqualTo("member");
        await Assert.That(cluster.SnapshotConsumerGroupMembers("group")).Count().IsEqualTo(1);
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
    [Arguments("share", "targeted")]
    [Arguments("share", "legacy")]
    [Arguments("share", "all")]
    [Arguments("empty-share", "targeted")]
    [Arguments("empty-share", "legacy")]
    [Arguments("empty-share", "all")]
    [Arguments("streams", "targeted")]
    [Arguments("streams", "legacy")]
    [Arguments("streams", "all")]
    public async Task Removal_RejectsNonconsumerGroups(string groupType, string mode)
    {
        var cluster = new InMemoryKafkaCluster();
        if (groupType == "streams")
        {
            cluster.CreateTopic("input");
            cluster.AlterStreamsGroupOffsets("group", [new TopicPartitionOffset("input", 0, 17)]);
        }
        else
        {
            var registration = cluster.RegisterShareGroupMember("group", "member");
            if (groupType == "empty-share")
                cluster.UnregisterShareGroupMember("group", "member", registration);
        }
        await using var admin = new InMemoryAdminClient(cluster);
        var exception = await Assert.That(async () =>
        {
            if (mode == "legacy")
                await admin.RemoveMembersFromConsumerGroupAsync("group",
                    [new ConsumerGroupMemberToRemove { GroupInstanceId = "instance" }]);
            else
                await admin.RemoveMembersFromConsumerGroupAsync("group", new ConsumerGroupMemberRemovalOptions
                {
                    RemoveAll = mode == "all",
                    Members = mode == "all" ? [] : [new ConsumerGroupMemberIdentity { MemberId = "member" }]
                });
        }).Throws<GroupException>();
        await Assert.That(exception!.ErrorCode).IsEqualTo(ErrorCode.UnsupportedVersion);
        await Assert.That(exception.GroupId).IsEqualTo("group");
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
