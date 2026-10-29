using Dekaf.Errors;
using Dekaf.Protocol;
using Dekaf.Testing;

namespace Dekaf.Tests.Unit.Testing;

public sealed class InMemoryConsumerGroupAdminTests
{
    [Test]
    public async Task DescribeGroups_ReportsMissingOffsetOnlyAndDepartedGroups()
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic("topic");
        await using var admin = new InMemoryAdminClient(cluster);
        await admin.AlterConsumerGroupOffsetsAsync("offsets", [new("topic", 0, 12)]);
        cluster.RegisterConsumerGroupMember("departed", "member", [new("topic", 0)], out var registration);
        cluster.UnregisterConsumerGroupMember("departed", "member", registration);

        var groups = await admin.DescribeConsumerGroupsAsync(["missing", "offsets", "departed"]);

        await Assert.That(groups["missing"].State).IsEqualTo("Dead");
        await Assert.That(groups["offsets"].State).IsEqualTo("Empty");
        await Assert.That(groups["departed"].State).IsEqualTo("Empty");
        foreach (var (id, description) in groups)
        {
            await Assert.That(description.GroupId).IsEqualTo(id);
            await Assert.That(description.Members).IsEmpty();
        }
        // Describing a missing group must not create it.
        await Assert.That(cluster.ListGroups()).DoesNotContain("missing");
        await admin.DeleteConsumerGroupsAsync(["offsets", "departed"]);
        var deleted = await admin.DescribeConsumerGroupsAsync(["offsets", "departed"]);
        await Assert.That(deleted.Values.All(static group => group.State == "Dead")).IsTrue();
    }

    [Test]
    public async Task DescribeActiveGroup_ReportsIdentitiesAssignmentsAndIndependentSnapshots()
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic("topic", 3);
        TopicPartition[] partitions = [new("topic", 0), new("topic", 1), new("topic", 2)];
        cluster.RegisterConsumerGroupMember("group", "dynamic", partitions, out var dynamicRegistration);
        var generation = cluster.RegisterConsumerGroupMember("group", "static", partitions, out var staticRegistration, "instance");
        await using var admin = new InMemoryAdminClient(cluster);

        var description = (await admin.DescribeConsumerGroupsAsync(["group"]))["group"];

        await Assert.That(description.State).IsEqualTo("Stable");
        await Assert.That(description.ProtocolType).IsEqualTo("consumer");
        await Assert.That(description.GroupEpoch).IsEqualTo(generation);
        await Assert.That(description.AssignmentEpoch).IsEqualTo(generation);
        await Assert.That(description.Members.Count).IsEqualTo(2);
        foreach (var member in description.Members)
        {
            var registration = member.MemberId == "dynamic" ? dynamicRegistration : staticRegistration;
            var assignment = cluster.GetConsumerGroupAssignment("group", member.MemberId, registration, out _);
            await Assert.That(member.GroupInstanceId).IsEqualTo(member.MemberId == "static" ? "instance" : null);
            await Assert.That(member.MemberEpoch).IsEqualTo(generation);
            await Assert.That(member.Assignment!).IsEquivalentTo(assignment);
        }
        await Assert.That(description.Members.SelectMany(static member => member.Assignment!).ToArray())
            .IsEquivalentTo(partitions);

        cluster.UnregisterConsumerGroupMember("group", "static", staticRegistration);
        var updated = (await admin.DescribeConsumerGroupsAsync(["group"]))["group"];
        await Assert.That(updated.Members.Count).IsEqualTo(1);
        await Assert.That(updated.Members[0].Assignment!).IsEquivalentTo(partitions);
        await Assert.That(updated.GroupEpoch).IsNotEqualTo(description.GroupEpoch);
        await Assert.That(description.Members.Count).IsEqualTo(2);
        await Assert.That(description.Members.SelectMany(static member => member.Assignment!).ToArray())
            .IsEquivalentTo(partitions);
    }

    [Test]
    public async Task AlterActiveGroup_ThrowsAndPreservesOffsetsUntilMembersLeave()
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic("topic");
        await using var admin = new InMemoryAdminClient(cluster);
        await admin.AlterConsumerGroupOffsetsAsync("group", [new("topic", 0, 12)]);
        cluster.RegisterConsumerGroupMember("group", "member", [new("topic", 0)], out var registration);

        var exception = await Assert.ThrowsAsync<GroupException>(() =>
            admin.AlterConsumerGroupOffsetsAsync("group", [new("topic", 0, 99)]).AsTask());

        await Assert.That(exception!.ErrorCode).IsEqualTo(ErrorCode.UnknownMemberId);
        await Assert.That(exception.GroupId).IsEqualTo("group");
        await Assert.That(cluster.GetCommittedOffset("group", new("topic", 0))).IsEqualTo(12);
        cluster.UnregisterConsumerGroupMember("group", "member", registration);
        await admin.AlterConsumerGroupOffsetsAsync("group", [new("topic", 0, 99)]);
        await Assert.That(cluster.GetCommittedOffset("group", new("topic", 0))).IsEqualTo(99);
    }

    [Test]
    [Arguments("missing", 0)]
    [Arguments("topic", 2)]
    public async Task AlterUnknownPartition_ThrowsWithoutCreatingOffsets(string topic, int partition)
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic("topic");
        await using var admin = new InMemoryAdminClient(cluster);

        var exception = await Assert.ThrowsAsync<GroupException>(() =>
            admin.AlterConsumerGroupOffsetsAsync("group", [new(topic, partition, 99)]).AsTask());

        await Assert.That(exception!.ErrorCode).IsEqualTo(ErrorCode.UnknownTopicOrPartition);
        await Assert.That(await admin.ListConsumerGroupOffsetsAsync("group")).IsEmpty();
        await Assert.That(cluster.ListGroups()).DoesNotContain("group");
    }

    [Test]
    public async Task AlterMixedPartitions_AppliesValidOffsetsAndThrowsFirstError()
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic("topic");
        await using var admin = new InMemoryAdminClient(cluster);

        var exception = await Assert.ThrowsAsync<GroupException>(() => admin.AlterConsumerGroupOffsetsAsync("group",
            [new("missing", 0, 1), new("topic", 0, 12), new("topic", 2, 3)]).AsTask());

        await Assert.That(exception!.ErrorCode).IsEqualTo(ErrorCode.UnknownTopicOrPartition);
        await Assert.That(exception.Message).Contains("missing-0");
        var offsets = await admin.ListConsumerGroupOffsetsAsync("group");
        await Assert.That(offsets.Count).IsEqualTo(1);
        await Assert.That(offsets[new("topic", 0)]).IsEqualTo(12);
    }
}
