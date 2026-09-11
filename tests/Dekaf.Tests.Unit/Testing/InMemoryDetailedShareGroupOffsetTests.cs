using Dekaf.Admin;
using Dekaf.Errors;
using Dekaf.Protocol;
using Dekaf.Testing;

namespace Dekaf.Tests.Unit.Testing;

public class InMemoryDetailedShareGroupOffsetTests
{
    [Test]
    public async Task Alter_CreatesEmptyGroupAndPreservesMixedPartitionOutcomes()
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic("orders", 2);
        await using IAdminClient admin = new InMemoryAdminClient(cluster);
        cluster.FaultPlan.Fail(new(KafkaFaultOperation.Admin, "orders", 1, "group"),
            new KafkaException(ErrorCode.TopicAuthorizationFailed, "denied"));
        var results = await admin.AlterShareGroupOffsetsDetailedAsync("group",
            [new() { TopicPartition = new("orders", 0), StartOffset = 42 },
             new() { TopicPartition = new("orders", 1), StartOffset = 54 },
             new() { TopicPartition = new("missing", 0), StartOffset = 0 }]);
        await Assert.That(results[new("orders", 0)].IsSuccess).IsTrue();
        await Assert.That(results[new("orders", 1)].ErrorCode).IsEqualTo(ErrorCode.TopicAuthorizationFailed);
        await Assert.That(results[new("orders", 1)].ErrorMessage).IsEqualTo("denied");
        await Assert.That(results[new("missing", 0)].ErrorCode).IsEqualTo(ErrorCode.UnknownTopicOrPartition);
        var stored = cluster.GetShareGroupOffsets("group");
        await Assert.That(stored.Count).IsEqualTo(1);
        await Assert.That(stored[new("orders", 0)]).IsEqualTo(42);
    }

    [Test]
    public async Task Delete_RemovesWholeTopicAndRetainsSiblingOffsetsAndGroup()
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic("orders", 2);
        cluster.CreateTopic("retained");
        await using var admin = new InMemoryAdminClient(cluster);
        await admin.AlterShareGroupOffsetsDetailedAsync("group",
            [new() { TopicPartition = new("orders", 0), StartOffset = 42 },
             new() { TopicPartition = new("orders", 1), StartOffset = 54 },
             new() { TopicPartition = new("retained", 0), StartOffset = 17 }]);
        var results = await admin.DeleteShareGroupOffsetsDetailedAsync("group", ["orders", "missing"]);
        await Assert.That(results["orders"].IsSuccess).IsTrue();
        await Assert.That(results["missing"].ErrorCode).IsEqualTo(ErrorCode.UnknownTopicOrPartition);
        await Assert.That(cluster.GetShareGroupOffsets("group").Keys).IsEquivalentTo([new TopicPartition("retained", 0)]);
        await admin.DeleteShareGroupOffsetsDetailedAsync("group", ["retained"]);
        await Assert.That((await admin.DeleteShareGroupOffsetsDetailedAsync("group", ["orders"]))["orders"].IsSuccess).IsTrue();
        await Assert.That((await admin.DeleteShareGroupOffsetsDetailedAsync("absent", ["orders"]))["orders"].ErrorCode).IsEqualTo(ErrorCode.GroupIdNotFound);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task GroupFault_ProducesOutcomeForEveryRequestedEntity(bool delete)
    {
        var cluster = new InMemoryKafkaCluster();
        await using var admin = new InMemoryAdminClient(cluster);
        cluster.FaultPlan.Fail(new(KafkaFaultOperation.Admin, groupId: "group"),
            new KafkaException(ErrorCode.GroupAuthorizationFailed, "group denied"));
        var results = delete
            ? (await admin.DeleteShareGroupOffsetsDetailedAsync("group", ["first", "second"])).Values
            : (await admin.AlterShareGroupOffsetsDetailedAsync("group",
                [new() { TopicPartition = new("first", 0), StartOffset = 0 }, new() { TopicPartition = new("second", 0), StartOffset = 0 }])).Values;
        foreach (var result in results)
        {
            await Assert.That(result.ErrorCode).IsEqualTo(ErrorCode.GroupAuthorizationFailed);
            await Assert.That(result.ErrorMessage).IsEqualTo("group denied");
        }
    }

    [Test]
    public async Task Cancellation_PreservesSuccessWithoutApplyingRemainingOffsets()
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic("orders", 3);
        await using var admin = new InMemoryAdminClient(cluster);
        var barrier = cluster.FaultPlan.PauseNext(new(KafkaFaultOperation.Admin, "orders", 1, "group"));
        using var cancellation = new CancellationTokenSource();
        var pending = admin.AlterShareGroupOffsetsDetailedAsync("group",
            [new() { TopicPartition = new("orders", 0), StartOffset = 42 },
             new() { TopicPartition = new("orders", 1), StartOffset = 54 },
             new() { TopicPartition = new("orders", 2), StartOffset = 61 }], cancellationToken: cancellation.Token).AsTask();
        await barrier.WaitUntilEnteredAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10));
        cancellation.Cancel();
        var results = await pending.WaitAsync(TimeSpan.FromSeconds(10));
        barrier.Release();
        await Assert.That(results[new("orders", 0)].IsSuccess).IsTrue();
        await Assert.That(results[new("orders", 1)].Outcome).IsEqualTo(AdminMutationOutcome.NotAttempted);
        await Assert.That(results[new("orders", 2)].Outcome).IsEqualTo(AdminMutationOutcome.NotAttempted);
        await Assert.That(cluster.GetShareGroupOffsets("group").Count).IsEqualTo(1);
    }

    [Test]
    public async Task ActiveGroup_IsRejectedAndCanBeAlteredAfterMembersLeave()
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic("orders");
        var member = cluster.RegisterShareGroupMember("group", "member");
        await using var admin = new InMemoryAdminClient(cluster);
        ShareGroupOffsetAlteration[] offsets = [new() { TopicPartition = new("orders", 0), StartOffset = 7 }];
        await Assert.That((await admin.AlterShareGroupOffsetsDetailedAsync("group", offsets))[new("orders", 0)].ErrorCode).IsEqualTo(ErrorCode.NonEmptyGroup);
        await Assert.That((await admin.DeleteShareGroupOffsetsDetailedAsync("group", ["orders"]))["orders"].ErrorCode).IsEqualTo(ErrorCode.NonEmptyGroup);
        cluster.UnregisterShareGroupMember("group", "member", member);
        await Assert.That((await admin.AlterShareGroupOffsetsDetailedAsync("group", offsets))[new("orders", 0)].IsSuccess).IsTrue();
    }
}
