using Dekaf.Admin;
using Dekaf.Errors;
using Dekaf.Protocol;

namespace Dekaf.Tests.Integration;

[Category("Redpanda")]
[ClassDataSource<RedpandaTestContainer>(Shared = SharedType.PerTestSession)]
public sealed class RedpandaCompatibilityTests(RedpandaTestContainer redpanda)
{
    [Test]
    public async Task ListConsumerGroups_BeforeListGroupsV5_ReturnsGroupsByProtocol()
    {
        var topic = $"redpanda-listing-{Guid.NewGuid():N}";
        var group = $"redpanda-simple-{Guid.NewGuid():N}";
        await using var admin = Kafka.CreateAdminClient()
            .WithBootstrapServers(redpanda.BootstrapServers)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .Build();
        await admin.CreateTopicsAsync([new NewTopic { Name = topic }]);
        // An offset commit without a member creates a simple consumer group with an empty protocol.
        await admin.AlterConsumerGroupOffsetsAsync(group, [new TopicPartitionOffset(topic, 0, 0)]);

        // Pins the precondition: this broker cannot filter by group type, so the listing
        // below takes the pre-v5 path.
        var typeFilter = await Assert.That(async () =>
                await admin.ListGroupsAsync(new ListGroupsOptions { Types = ["classic"] }))
            .Throws<KafkaException>();
        await Assert.That(typeFilter!.ErrorCode).IsEqualTo(ErrorCode.UnsupportedVersion);

        var groups = await admin.ListConsumerGroupsAsync();

        var listed = groups.Single(listing => listing.GroupId == group);
        await Assert.That(listed.ProtocolType).IsEqualTo(string.Empty);
        await Assert.That(listed.GroupType).IsNull();
    }
}
