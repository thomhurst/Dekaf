using Dekaf.Admin;
using Dekaf.Errors;
using Dekaf.Protocol;
using Dekaf.Testing;

namespace Dekaf.Tests.Unit.Testing;

public class InMemoryShareGroupOffsetMutationTests
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ActiveGroup_ThrowsWithoutChangingOffsets_ThenSucceedsAfterClose(bool delete)
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic("orders");
        await using IAdminClient admin = new InMemoryAdminClient(cluster);
        var partition = new TopicPartition("orders", 0);
        await admin.AlterShareGroupOffsetsAsync("group", [new() { TopicPartition = partition, StartOffset = 7 }]);
        await using var consumer = new InMemoryShareConsumer<string, string>(cluster,
            new InMemoryShareConsumerOptions { GroupId = "group" });
        consumer.Subscribe("orders");

        var exception = await Assert.ThrowsAsync<GroupException>(() => MutateAsync());

        await Assert.That(exception!.ErrorCode).IsEqualTo(ErrorCode.NonEmptyGroup);
        await Assert.That(exception.GroupId).IsEqualTo("group");
        await Assert.That(cluster.GetShareGroupOffsets("group")[partition]).IsEqualTo(7);
        await consumer.CloseAsync();
        await MutateAsync();
        var stored = cluster.GetShareGroupOffsets("group");
        if (delete)
            await Assert.That(stored).IsEmpty();
        else
            await Assert.That(stored[partition]).IsEqualTo(42);

        Task MutateAsync() => delete
            ? admin.DeleteShareGroupOffsetsAsync("group", ["orders"]).AsTask()
            : admin.AlterShareGroupOffsetsAsync("group", [new() { TopicPartition = partition, StartOffset = 42 }]).AsTask();
    }

    [Test]
    [Arguments("missing", 0, 42, ErrorCode.UnknownTopicOrPartition)]
    [Arguments("orders", 1, 42, ErrorCode.UnknownTopicOrPartition)]
    [Arguments("orders", 0, -1, ErrorCode.InvalidRequest)]
    public async Task Alter_InvalidOffset_ThrowsWithoutStoringOffset(string topic, int partition, long offset, ErrorCode expected)
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic("orders");
        await using IAdminClient admin = new InMemoryAdminClient(cluster);

        var exception = await Assert.ThrowsAsync<GroupException>(() => admin.AlterShareGroupOffsetsAsync("group",
            [new() { TopicPartition = new(topic, partition), StartOffset = offset }]).AsTask());

        await Assert.That(exception!.ErrorCode).IsEqualTo(expected);
        await Assert.That(exception.GroupId).IsEqualTo("group");
        await Assert.That(cluster.GetShareGroupOffsets("group")).IsEmpty();
    }

    [Test]
    [Arguments("group", "missing", ErrorCode.UnknownTopicOrPartition)]
    [Arguments("absent", "orders", ErrorCode.GroupIdNotFound)]
    public async Task Delete_InvalidGroupOrTopic_ThrowsWithoutDeletingOffsets(string group, string topic, ErrorCode expected)
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic("orders");
        await using IAdminClient admin = new InMemoryAdminClient(cluster);
        var partition = new TopicPartition("orders", 0);
        await admin.AlterShareGroupOffsetsAsync("group", [new() { TopicPartition = partition, StartOffset = 7 }]);

        var exception = await Assert.ThrowsAsync<GroupException>(() => admin.DeleteShareGroupOffsetsAsync(group, [topic]).AsTask());

        await Assert.That(exception!.ErrorCode).IsEqualTo(expected);
        await Assert.That(exception.GroupId).IsEqualTo(group);
        await Assert.That(cluster.GetShareGroupOffsets("group")[partition]).IsEqualTo(7);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task CancellationDuringFault_PreventsMutation(bool delete)
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic("orders");
        await using IAdminClient admin = new InMemoryAdminClient(cluster);
        var partition = new TopicPartition("orders", 0);
        await admin.AlterShareGroupOffsetsAsync("group", [new() { TopicPartition = partition, StartOffset = 7 }]);
        var barrier = cluster.FaultPlan.PauseNext(new KafkaFaultScope(KafkaFaultOperation.Admin, "orders", groupId: "group"));
        using var cancellation = new CancellationTokenSource();
        var pending = delete
            ? admin.DeleteShareGroupOffsetsAsync("group", ["orders"], cancellation.Token).AsTask()
            : admin.AlterShareGroupOffsetsAsync("group", [new() { TopicPartition = partition, StartOffset = 42 }], cancellation.Token).AsTask();
        try
        {
            await barrier.WaitUntilEnteredAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10));
            cancellation.Cancel();
            await Assert.That(async () => await pending.WaitAsync(TimeSpan.FromSeconds(10))).Throws<OperationCanceledException>();
            await Assert.That(cluster.GetShareGroupOffsets("group")[partition]).IsEqualTo(7);
        }
        finally
        {
            barrier.Release();
        }
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task MixedBatch_AppliesValidEntriesAndThrowsFirstError(bool delete)
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic("orders", 2);
        cluster.CreateTopic("retained");
        await using IAdminClient admin = new InMemoryAdminClient(cluster);
        ShareGroupOffsetAlteration[] valid =
        [
            new() { TopicPartition = new("orders", 0), StartOffset = 7 },
            new() { TopicPartition = new("orders", 1), StartOffset = 8 },
            new() { TopicPartition = new("retained", 0), StartOffset = 9 }
        ];
        await admin.AlterShareGroupOffsetsAsync("group", valid);

        var exception = await Assert.ThrowsAsync<GroupException>(() => delete
            ? admin.DeleteShareGroupOffsetsAsync("group", ["missing", "orders"]).AsTask()
            : admin.AlterShareGroupOffsetsAsync("group",
                [new() { TopicPartition = new("missing", 0), StartOffset = 0 },
                 new() { TopicPartition = new("orders", 0), StartOffset = 42 },
                 new() { TopicPartition = new("orders", 1), StartOffset = -1 }]).AsTask());

        await Assert.That(exception!.ErrorCode).IsEqualTo(ErrorCode.UnknownTopicOrPartition);
        await Assert.That(exception.GroupId).IsEqualTo("group");
        var stored = cluster.GetShareGroupOffsets("group");
        await Assert.That(stored[new("retained", 0)]).IsEqualTo(9);
        if (delete)
            await Assert.That(stored.Count).IsEqualTo(1);
        else
        {
            await Assert.That(stored[new("orders", 0)]).IsEqualTo(42);
            await Assert.That(stored[new("orders", 1)]).IsEqualTo(8);
        }
    }
}
