using Dekaf.Admin;
using Dekaf.Consumer;

namespace Dekaf.Tests.Integration;

[Category("Offsets")]
public sealed class ConsumerStoredLeaderEpochTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    [Test]
    public async Task ExplicitCommit_WithOlderLeaderEpoch_PreservesStoredCheckpointForNextCommit(
        CancellationToken cancellationToken)
    {
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 1);
        var group = $"stored-epoch-{Guid.NewGuid():N}";
        var partition = new TopicPartition(topic, 0);
        var captured = new TopicPartitionOffset(topic, 0, 5, leaderEpoch: 1);
        var stored = new TopicPartitionOffset(topic, 0, 5, leaderEpoch: 2);
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(group)
            .WithOffsetCommitMode(OffsetCommitMode.Manual)
            .WithAutoOffsetStore(false)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync(cancellationToken);
        consumer.Assign([partition]);
        consumer.StoreOffset(stored);

        // An explicit commit can carry an older checkpoint than the current stored checkpoint.
        await consumer.CommitAsync([captured], cancellationToken);
        await using var admin = Kafka.CreateAdminClient()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .Build();
        var specs = new Dictionary<string, ListConsumerGroupOffsetsSpec>
        {
            [group] = new() { TopicPartitions = [partition] }
        };
        var first = await admin.ListConsumerGroupOffsetsAsync(specs, cancellationToken: cancellationToken);
        await Assert.That(first[group].Offsets[partition].Offset?.Offset).IsEqualTo(captured.Offset);
        await Assert.That(first[group].Offsets[partition].Offset?.LeaderEpoch).IsEqualTo(captured.LeaderEpoch);

        await consumer.CommitAsync(cancellationToken);

        var second = await admin.ListConsumerGroupOffsetsAsync(specs, cancellationToken: cancellationToken);
        await Assert.That(second[group].Offsets[partition].Offset?.Offset).IsEqualTo(stored.Offset);
        await Assert.That(second[group].Offsets[partition].Offset?.LeaderEpoch).IsEqualTo(stored.LeaderEpoch);
    }
}
