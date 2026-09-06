using Dekaf.Admin;
using Dekaf.Errors;

namespace Dekaf.Tests.Integration;

[Category("Transaction")]
public sealed class ConsumerGroupOffsetQueryTests(KafkaTestContainer kafka) : TransactionalKafkaIntegrationTest(kafka)
{
    [Test]
    public async Task CompleteOffsets_RoundTripMetadataEpochAndAbsentCommit()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 2);
        var group = $"offset-query-{Guid.NewGuid():N}";
        var emptyGroup = $"empty-{Guid.NewGuid():N}";
        await using var admin = Kafka.CreateAdminClient().WithBootstrapServers(KafkaContainer.BootstrapServers).Build();
        var checkpoint = new TopicPartitionOffset(topic, 0, 42, 0) { Metadata = "resume-checkpoint" };
        await admin.AlterConsumerGroupOffsetsAsync(group, [checkpoint]);
        var results = await admin.ListConsumerGroupOffsetsAsync(new Dictionary<string, ListConsumerGroupOffsetsSpec>
        {
            [group] = new() { TopicPartitions = [new(topic, 0), new(topic, 1)] },
            [emptyGroup] = new()
        });
        await Assert.That(results[group].Offsets[new(topic, 0)].Offset).IsEqualTo(checkpoint);
        await Assert.That(results[group].Offsets[new(topic, 1)].Offset).IsNull();
        await Assert.That(results[group].Offsets[new(topic, 1)].ErrorCode).IsEqualTo(Protocol.ErrorCode.None);
        await Assert.That(results[emptyGroup].ErrorCode).IsEqualTo(Protocol.ErrorCode.None);
        await Assert.That(results[emptyGroup].Offsets).IsEmpty();
        var legacy = await admin.ListConsumerGroupOffsetsAsync(group);
        await Assert.That(legacy.Count).IsEqualTo(1);
        await Assert.That(legacy[new(topic, 0)]).IsEqualTo(42);
    }

    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task StableOffsets_WaitForPendingTransactionAndObserveCommitOrAbort(bool commit)
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var group = $"stable-offset-query-{Guid.NewGuid():N}";
        await using var admin = Kafka.CreateAdminClient().WithBootstrapServers(KafkaContainer.BootstrapServers).Build();
        var before = new TopicPartitionOffset(topic, 0, 5, 0) { Metadata = "before" };
        var after = new TopicPartitionOffset(topic, 0, 9, 0) { Metadata = "transaction" };
        await admin.AlterConsumerGroupOffsetsAsync(group, [before]);
        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithTransactionalId($"offset-query-txn-{Guid.NewGuid():N}")
            .BuildAsync();
        await producer.InitTransactionsAsync();
        await using var transaction = producer.BeginTransaction();
        await transaction.SendOffsetsToTransactionAsync([after], group);

        var specs = new Dictionary<string, ListConsumerGroupOffsetsSpec>
        {
            [group] = new() { TopicPartitions = [new(topic, 0)] }
        };
        var unstable = await admin.ListConsumerGroupOffsetsAsync(specs);
        await Assert.That(unstable[group].Offsets[new(topic, 0)].Offset).IsEqualTo(before);
        await Assert.That(async () => await admin.ListConsumerGroupOffsetsAsync(specs,
            new ListConsumerGroupOffsetsOptions { RequireStable = true, TimeoutMs = 250 }))
            .Throws<KafkaTimeoutException>();

        var pending = admin.ListConsumerGroupOffsetsAsync(specs,
            new ListConsumerGroupOffsetsOptions { RequireStable = true }).AsTask();
        if (commit)
            await transaction.CommitAsync();
        else
            await transaction.AbortAsync();
        var stable = await pending;
        await Assert.That(stable[group].Offsets[new(topic, 0)].Offset).IsEqualTo(commit ? after : before);
        await Assert.That(stable[group].Offsets[new(topic, 0)].ErrorCode).IsEqualTo(Protocol.ErrorCode.None);
    }
}
