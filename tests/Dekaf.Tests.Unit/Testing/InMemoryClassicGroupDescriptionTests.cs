using Dekaf.Admin;
using Dekaf.Errors;
using Dekaf.Protocol;
using Dekaf.Testing;
using Dekaf.Tests.Unit.Admin;

namespace Dekaf.Tests.Unit.Testing;

public sealed class InMemoryClassicGroupDescriptionTests
{
    [Test]
    [Arguments("consumer")]
    [Arguments("connect")]
    public async Task Fixtures_CopyProtocolBytesAndPreserveGroupOutcomes(string protocol)
    {
        var cluster = new InMemoryKafkaCluster();
        await using var admin = new InMemoryAdminClient(cluster);
        var bytes = AdminClientClassicGroupDescriptionTests.AssignmentBytes();
        var original = bytes.ToArray();
        var members = new ClassicGroupMemberDescription[]
        {
            new() { MemberId = "member", AssignmentData = bytes, Metadata = bytes }
        };
        cluster.SetClassicGroupDescription(new()
        {
            GroupId = "fixture", ProtocolType = protocol, ProtocolData = "selected", State = "Stable",
            CoordinatorId = 12, AuthorizedOperations = 123, Members = members
        });
        bytes[0] = 99;
        members[0] = new() { MemberId = "changed" };
        cluster.FaultPlan.Fail(new KafkaFaultScope(KafkaFaultOperation.Admin, groupId: "denied"),
            new GroupException(ErrorCode.GroupAuthorizationFailed, "denied"));
        var results = await admin.DescribeClassicGroupsAsync(["fixture", "denied", "missing"],
            new() { IncludeAuthorizedOperations = true });
        var description = results["fixture"].Description!;
        await Assert.That(description.CoordinatorId).IsEqualTo(12);
        await Assert.That(description.AuthorizedOperations).IsEqualTo(123);
        await Assert.That(description.Members[0].MemberId).IsEqualTo("member");
        await Assert.That(description.Members[0].AssignmentData.ToArray()).IsEquivalentTo(original);
        await Assert.That(description.Members[0].Assignment is not null).IsEqualTo(protocol == "consumer");
        await Assert.That(results["denied"].ErrorCode).IsEqualTo(ErrorCode.GroupAuthorizationFailed);
        await Assert.That(results["missing"].ErrorCode).IsEqualTo(ErrorCode.GroupIdNotFound);
        var inventory = await admin.ListGroupsAsync();
        await Assert.That(inventory.Single().GroupType).IsEqualTo("classic");
        await Assert.That(inventory.Single().ProtocolType).IsEqualTo(protocol);
        await Assert.That((await admin.DescribeClassicGroupsAsync(["fixture"]))["fixture"].Description!.AuthorizedOperations).IsNull();
    }

    [Test]
    public async Task OffsetOnlyGroup_HasEmptyClassicDescription()
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic("topic");
        await using var admin = new InMemoryAdminClient(cluster);
        await admin.AlterConsumerGroupOffsetsAsync("simple", [new TopicPartitionOffset("topic", 0, 0)]);
        var description = (await admin.DescribeClassicGroupsAsync(["simple"]))["simple"].Description!;
        await Assert.That(description.State).IsEqualTo("Empty");
        await Assert.That(description.ProtocolType).IsEqualTo("");
        await Assert.That(description.Members).IsEmpty();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task FaultBarrier_RespectsCallerCancellationAndDeadline(bool timeout)
    {
        var cluster = new InMemoryKafkaCluster();
        await using var admin = new InMemoryAdminClient(cluster);
        var barrier = cluster.FaultPlan.PauseNext(new KafkaFaultScope(KafkaFaultOperation.Admin, groupId: "group"));
        using var cancellation = new CancellationTokenSource();
        var operation = admin.DescribeClassicGroupsAsync(["group"], new() { TimeoutMs = timeout ? 100 : 30000 }, cancellation.Token).AsTask();
        await barrier.WaitUntilEnteredAsync();
        if (timeout)
            await Assert.That(async () => await operation).Throws<KafkaTimeoutException>();
        else
        {
            cancellation.Cancel();
            await Assert.That(async () => await operation).Throws<OperationCanceledException>();
        }
        barrier.Release();
    }

    [Test]
    public async Task ValidationAndEmptyInput_LeaveFaultPlanUntouched()
    {
        var cluster = new InMemoryKafkaCluster();
        await using var admin = new InMemoryAdminClient(cluster);
        cluster.FaultPlan.Fail(new KafkaFaultScope(KafkaFaultOperation.Admin), new InvalidOperationException("pending"));
        await Assert.That(async () => await admin.DescribeClassicGroupsAsync(["a", "a"])).Throws<ArgumentException>();
        await Assert.That(async () => await admin.DescribeClassicGroupsAsync([], new() { TimeoutMs = -1 })).Throws<ArgumentOutOfRangeException>();
        await Assert.That(await admin.DescribeClassicGroupsAsync([])).IsEmpty();
        await Assert.That(async () => await admin.DescribeClassicGroupsAsync(["a"])).Throws<InvalidOperationException>();
    }
}
