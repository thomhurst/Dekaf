using Dekaf.Admin;
using Dekaf.Testing;

namespace Dekaf.Tests.Unit.Testing;

public sealed class InMemoryGroupListingTests
{
    [Test]
    public async Task InventoryAndConveniences_PreserveGroupFamiliesAndEmptyHistory()
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic("input");
        await using var concrete = new InMemoryAdminClient(cluster);
        IAdminClient admin = concrete;
        await admin.AlterConsumerGroupOffsetsAsync("simple", [new TopicPartitionOffset("input", 0, 0)]);
        cluster.RegisterConsumerGroupMember("consumer", "member", [new TopicPartition("input", 0)], out var registration);
        cluster.RegisterShareGroupMember("share", "member");
        await admin.AlterStreamsGroupOffsetsAsync("streams", [new TopicPartitionOffset("input", 0, 0)]);

        var inventory = await admin.ListGroupsAsync();
        await Assert.That(inventory.Select(static group => (group.GroupId, group.GroupType, group.ProtocolType)))
            .IsEquivalentTo(new (string, string?, string?)[]
            {
                ("simple", "classic", ""), ("consumer", "consumer", "consumer"),
                ("share", "share", "share"), ("streams", "streams", "streams")
            });
        await Assert.That((await admin.ListConsumerGroupsAsync()).Select(static group => group.GroupId))
            .IsEquivalentTo(["simple", "consumer"]);
        await Assert.That((await admin.ListShareGroupsAsync()).Select(static group => group.GroupId)).IsEquivalentTo(["share"]);
        await Assert.That((await admin.ListStreamsGroupsAsync()).Select(static group => group.GroupId)).IsEquivalentTo(["streams"]);

        cluster.UnregisterConsumerGroupMember("consumer", "member", registration);
        await admin.DeleteStreamsGroupOffsetsAsync("streams", [new TopicPartition("input", 0)]);
        var empty = await admin.ListGroupsAsync(new ListGroupsOptions
        {
            States = ["empty"], Types = ["consumer", "streams"], ProtocolTypes = ["consumer", "streams"]
        });
        await Assert.That(empty.Select(static group => group.GroupId)).IsEquivalentTo(["consumer", "streams"]);
        // Earlier results remain snapshots after membership changes.
        await Assert.That(inventory.Single(static group => group.GroupId == "consumer").State).IsEqualTo("Stable");
        await admin.DeleteStreamsGroupsAsync(["streams"]);
        await admin.DeleteConsumerGroupsAsync(["consumer"]);
        await Assert.That((await admin.ListGroupsAsync()).Select(static group => group.GroupId)).IsEquivalentTo(["simple", "share"]);
    }

    [Test]
    public async Task ProtocolFilter_DistinguishesEmptyAndExactCase()
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic("input");
        await using var admin = new InMemoryAdminClient(cluster);
        await admin.AlterConsumerGroupOffsetsAsync("simple", [new TopicPartitionOffset("input", 0, 0)]);
        cluster.RegisterConsumerGroupMember("consumer", "member", [], out _);
        await Assert.That((await admin.ListGroupsAsync(new ListGroupsOptions { ProtocolTypes = [""] }))
            .Select(static group => group.GroupId)).IsEquivalentTo(["simple"]);
        await Assert.That(await admin.ListGroupsAsync(new ListGroupsOptions { ProtocolTypes = ["Consumer"] })).IsEmpty();
    }

    [Test]
    public async Task CancellationAndDisposal_AreHonored()
    {
        var admin = new InMemoryAdminClient(new InMemoryKafkaCluster());
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        await Assert.That(async () => await admin.ListGroupsAsync(cancellationToken: canceled.Token))
            .Throws<OperationCanceledException>();
        await admin.DisposeAsync();
        await Assert.That(async () => await admin.ListGroupsAsync()).Throws<ObjectDisposedException>();
    }
}
