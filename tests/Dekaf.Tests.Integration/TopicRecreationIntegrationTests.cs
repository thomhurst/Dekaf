using Dekaf.Admin;
using Dekaf.Consumer;
using Dekaf.Errors;
using Dekaf.Producer;
using Dekaf.Protocol;

namespace Dekaf.Tests.Integration;

[Category("Resilience")]
public sealed class TopicRecreationIntegrationTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    private static readonly TimeSpan MetadataMaxAge = TimeSpan.FromSeconds(1);

    [Test]
    [Timeout(120_000)]
    public async Task Producer_TopicRecreated_RefreshesTopicIdAndContinues(
        CancellationToken cancellationToken)
    {
        var topic = await KafkaContainer.CreateTestTopicAsync().ConfigureAwait(false);
        await using var admin = KafkaContainer.CreateAdminClient();
        await using var producer = await CreateProducerAsync(cancellationToken).ConfigureAwait(false);

        await ProduceAsync(producer, topic, partition: 0, "old", cancellationToken).ConfigureAwait(false);
        var oldTopicId = await GetTopicIdAsync(admin, topic, cancellationToken).ConfigureAwait(false);
        var newTopicId = await RecreateTopicAsync(
            admin,
            topic,
            oldTopicId,
            partitions: 1,
            cancellationToken).ConfigureAwait(false);

        await ProduceAsync(producer, topic, partition: 0, "new-0", cancellationToken).ConfigureAwait(false);
        await ProduceAsync(producer, topic, partition: 0, "new-1", cancellationToken).ConfigureAwait(false);

        await using var verifier = await CreateAssignedConsumerAsync(topic, cancellationToken)
            .ConfigureAwait(false);
        var records = await ConsumeCountAsync(verifier, count: 2, cancellationToken).ConfigureAwait(false);

        await Assert.That(newTopicId).IsNotEqualTo(oldTopicId);
        await Assert.That(records.Select(static record => record.Value)).IsEquivalentTo(["new-0", "new-1"]);
        await Assert.That(records.Select(static record => record.Offset)).IsEquivalentTo([0L, 1L]);
    }

    [Test]
    [Timeout(120_000)]
    public async Task Consumer_TopicRecreated_AppliesEarliestOffsetReset(
        CancellationToken cancellationToken)
    {
        var topic = await KafkaContainer.CreateTestTopicAsync().ConfigureAwait(false);
        var groupId = $"topic-recreate-earliest-{Guid.NewGuid():N}";
        await using var admin = KafkaContainer.CreateAdminClient();
        await using var initialProducer = await CreateProducerAsync(cancellationToken).ConfigureAwait(false);
        await using var consumer = await CreateGroupConsumerAsync(topic, groupId, cancellationToken)
            .ConfigureAwait(false);

        await ProduceAsync(initialProducer, topic, partition: 0, "old-0", cancellationToken).ConfigureAwait(false);
        await ProduceAsync(initialProducer, topic, partition: 0, "old-1", cancellationToken).ConfigureAwait(false);
        var oldRecords = await ConsumeCountAsync(consumer, count: 2, cancellationToken).ConfigureAwait(false);
        await consumer.CommitAsync([
            new TopicPartitionOffset(topic, partition: 0, offset: 2, leaderEpoch: -1)
        ], cancellationToken).ConfigureAwait(false);

        var oldTopicId = await GetTopicIdAsync(admin, topic, cancellationToken).ConfigureAwait(false);
        _ = await RecreateTopicAsync(admin, topic, oldTopicId, partitions: 1, cancellationToken)
            .ConfigureAwait(false);
        await using var newProducer = await CreateProducerAsync(cancellationToken).ConfigureAwait(false);
        await ProduceAsync(newProducer, topic, partition: 0, "new-0", cancellationToken).ConfigureAwait(false);
        await ProduceAsync(newProducer, topic, partition: 0, "new-1", cancellationToken).ConfigureAwait(false);

        var firstRecreatedRecord = await ConsumeMatchingAsync(
            consumer,
            static record => record.Value.StartsWith("new-", StringComparison.Ordinal),
            cancellationToken).ConfigureAwait(false);

        await Assert.That(oldRecords.Select(static record => record.Value)).IsEquivalentTo(["old-0", "old-1"]);
        await Assert.That(firstRecreatedRecord.Value).IsEqualTo("new-0");
        await Assert.That(firstRecreatedRecord.Offset).IsEqualTo(0);
    }

    [Test]
    [Timeout(120_000)]
    public async Task Consumer_TopicRecreated_AppliesLatestOffsetReset(
        CancellationToken cancellationToken)
    {
        var topic = await KafkaContainer.CreateTestTopicAsync().ConfigureAwait(false);
        var groupId = $"topic-recreate-latest-{Guid.NewGuid():N}";
        var partition = new TopicPartition(topic, 0);
        await using var admin = KafkaContainer.CreateAdminClient();
        await using var initialProducer = await CreateProducerAsync(cancellationToken).ConfigureAwait(false);
        await using var consumer = await CreateGroupConsumerAsync(
            topic,
            groupId,
            AutoOffsetReset.Latest,
            cancellationToken).ConfigureAwait(false);

        await WaitForPositionWithoutRecordsAsync(consumer, partition, expectedPosition: 0, cancellationToken)
            .ConfigureAwait(false);
        await ProduceAsync(initialProducer, topic, partition: 0, "old-0", cancellationToken).ConfigureAwait(false);
        await ProduceAsync(initialProducer, topic, partition: 0, "old-1", cancellationToken).ConfigureAwait(false);
        await ProduceAsync(initialProducer, topic, partition: 0, "old-2", cancellationToken).ConfigureAwait(false);
        _ = await ConsumeCountAsync(consumer, count: 3, cancellationToken).ConfigureAwait(false);
        await consumer.CommitAsync([
            new TopicPartitionOffset(topic, partition: 0, offset: 3, leaderEpoch: -1)
        ], cancellationToken).ConfigureAwait(false);

        var oldTopicId = await GetTopicIdAsync(admin, topic, cancellationToken).ConfigureAwait(false);
        _ = await RecreateTopicAsync(admin, topic, oldTopicId, partitions: 1, cancellationToken)
            .ConfigureAwait(false);
        await using var newProducer = await CreateProducerAsync(cancellationToken).ConfigureAwait(false);
        await ProduceAsync(newProducer, topic, partition: 0, "new-before-reset-0", cancellationToken)
            .ConfigureAwait(false);
        await ProduceAsync(newProducer, topic, partition: 0, "new-before-reset-1", cancellationToken)
            .ConfigureAwait(false);

        await WaitForPositionWithoutRecordsAsync(consumer, partition, expectedPosition: 2, cancellationToken)
            .ConfigureAwait(false);
        await ProduceAsync(newProducer, topic, partition: 0, "new-after-reset", cancellationToken)
            .ConfigureAwait(false);

        var firstRecordAfterReset = await ConsumeMatchingAsync(
            consumer,
            static record => record.Value.StartsWith("new-", StringComparison.Ordinal),
            cancellationToken).ConfigureAwait(false);

        await Assert.That(firstRecordAfterReset.Value).IsEqualTo("new-after-reset");
        await Assert.That(firstRecordAfterReset.Offset).IsEqualTo(2);
    }

    [Test]
    [Timeout(120_000)]
    public async Task Consumer_TopicRecreatedWithFewerPartitions_DropsVanishedPartition(
        CancellationToken cancellationToken)
    {
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 2).ConfigureAwait(false);
        var groupId = $"topic-recreate-shrink-{Guid.NewGuid():N}";
        await using var admin = KafkaContainer.CreateAdminClient();
        await using var initialProducer = await CreateProducerAsync(cancellationToken).ConfigureAwait(false);
        await using var consumer = await CreateGroupConsumerAsync(topic, groupId, cancellationToken)
            .ConfigureAwait(false);

        await ProduceAsync(initialProducer, topic, partition: 0, "old-p0", cancellationToken).ConfigureAwait(false);
        await ProduceAsync(initialProducer, topic, partition: 1, "old-p1", cancellationToken).ConfigureAwait(false);
        _ = await ConsumeCountAsync(consumer, count: 2, cancellationToken).ConfigureAwait(false);

        var oldTopicId = await GetTopicIdAsync(admin, topic, cancellationToken).ConfigureAwait(false);
        _ = await RecreateTopicAsync(admin, topic, oldTopicId, partitions: 1, cancellationToken)
            .ConfigureAwait(false);
        await using var newProducer = await CreateProducerAsync(cancellationToken).ConfigureAwait(false);
        await ProduceAsync(newProducer, topic, partition: 0, "new-p0", cancellationToken).ConfigureAwait(false);

        var recreatedRecord = await ConsumeMatchingAsync(
            consumer,
            static record => record.Value == "new-p0",
            cancellationToken).ConfigureAwait(false);
        await WaitForAssignmentAsync(
            consumer,
            assignment => assignment.Count == 1 && assignment.Contains(new TopicPartition(topic, 0)),
            cancellationToken).ConfigureAwait(false);

        await Assert.That(recreatedRecord.Partition).IsEqualTo(0);
        await Assert.That(recreatedRecord.Offset).IsEqualTo(0);
        await Assert.That(consumer.Assignment).DoesNotContain(new TopicPartition(topic, 1));
    }

    [Test]
    [Timeout(120_000)]
    public async Task Commit_AgainstRecreatedTopic_DoesNotResurrectStaleOffsets(
        CancellationToken cancellationToken)
    {
        var topic = await KafkaContainer.CreateTestTopicAsync().ConfigureAwait(false);
        var groupId = $"topic-recreate-commit-{Guid.NewGuid():N}";
        await using var admin = KafkaContainer.CreateAdminClient();
        await using var initialProducer = await CreateProducerAsync(cancellationToken).ConfigureAwait(false);

        await using (var consumer = await CreateGroupConsumerAsync(topic, groupId, cancellationToken)
            .ConfigureAwait(false))
        {
            await ProduceAsync(initialProducer, topic, partition: 0, "old-0", cancellationToken)
                .ConfigureAwait(false);
            await ProduceAsync(initialProducer, topic, partition: 0, "old-1", cancellationToken)
                .ConfigureAwait(false);
            _ = await ConsumeCountAsync(consumer, count: 2, cancellationToken).ConfigureAwait(false);
            await consumer.CommitAsync([
                new TopicPartitionOffset(topic, partition: 0, offset: 2, leaderEpoch: -1)
            ], cancellationToken).ConfigureAwait(false);

            var oldTopicId = await GetTopicIdAsync(admin, topic, cancellationToken).ConfigureAwait(false);
            _ = await RecreateTopicAsync(admin, topic, oldTopicId, partitions: 1, cancellationToken)
                .ConfigureAwait(false);
            await using var newProducer = await CreateProducerAsync(cancellationToken).ConfigureAwait(false);
            await ProduceAsync(newProducer, topic, partition: 0, "new-0", cancellationToken)
                .ConfigureAwait(false);

            var recreatedRecord = await ConsumeMatchingAsync(
                consumer,
                static record => record.Value == "new-0",
                cancellationToken).ConfigureAwait(false);
            await consumer.CommitAsync([
                new TopicPartitionOffset(
                    topic,
                    partition: 0,
                    offset: recreatedRecord.Offset + 1,
                    leaderEpoch: recreatedRecord.LeaderEpoch ?? -1)
            ], cancellationToken).ConfigureAwait(false);
            await ProduceAsync(newProducer, topic, partition: 0, "new-1", cancellationToken)
                .ConfigureAwait(false);
        }

        await using var restartedConsumer = await CreateGroupConsumerAsync(topic, groupId, cancellationToken)
            .ConfigureAwait(false);
        var resumedRecord = await ConsumeMatchingAsync(
            restartedConsumer,
            static record => record.Value.StartsWith("new-", StringComparison.Ordinal),
            cancellationToken).ConfigureAwait(false);

        await Assert.That(resumedRecord.Value).IsEqualTo("new-1");
        await Assert.That(resumedRecord.Offset).IsEqualTo(1);
    }

    private async Task<IKafkaProducer<string, string>> CreateProducerAsync(
        CancellationToken cancellationToken) =>
        await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithMetadataMaxAge(MetadataMaxAge)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync(cancellationToken)
            .ConfigureAwait(false);

    private async Task<IKafkaConsumer<string, string>> CreateGroupConsumerAsync(
        string topic,
        string groupId,
        CancellationToken cancellationToken) =>
        await CreateGroupConsumerAsync(topic, groupId, AutoOffsetReset.Earliest, cancellationToken)
            .ConfigureAwait(false);

    private async Task<IKafkaConsumer<string, string>> CreateGroupConsumerAsync(
        string topic,
        string groupId,
        AutoOffsetReset autoOffsetReset,
        CancellationToken cancellationToken)
    {
        var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(groupId)
            .WithAutoOffsetReset(autoOffsetReset)
            .WithOffsetCommitMode(OffsetCommitMode.Manual)
            .WithAutoOffsetStore(false)
            .WithMetadataMaxAge(MetadataMaxAge)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync(cancellationToken)
            .ConfigureAwait(false);
        consumer.Subscribe(topic);
        return consumer;
    }

    private async Task<IKafkaConsumer<string, string>> CreateAssignedConsumerAsync(
        string topic,
        CancellationToken cancellationToken)
    {
        var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync(cancellationToken)
            .ConfigureAwait(false);
        consumer.IncrementalAssign([
            new TopicPartitionOffset(topic, partition: 0, offset: 0, leaderEpoch: -1)
        ]);
        return consumer;
    }

    private static async Task ProduceAsync(
        IKafkaProducer<string, string> producer,
        string topic,
        int partition,
        string value,
        CancellationToken cancellationToken)
    {
        _ = await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Partition = partition,
            Key = value,
            Value = value
        }, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<IReadOnlyList<ConsumeResult<string, string>>> ConsumeCountAsync(
        IKafkaConsumer<string, string> consumer,
        int count,
        CancellationToken cancellationToken)
    {
        var records = new List<ConsumeResult<string, string>>(count);
        while (records.Count < count)
        {
            var record = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(2), cancellationToken)
                .ConfigureAwait(false);
            if (record is not null)
                records.Add(record.Value);
        }

        return records;
    }

    private static async Task<ConsumeResult<string, string>> ConsumeMatchingAsync(
        IKafkaConsumer<string, string> consumer,
        Func<ConsumeResult<string, string>, bool> predicate,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            var record = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(2), cancellationToken)
                .ConfigureAwait(false);
            if (record is not null && predicate(record.Value))
                return record.Value;
        }
    }

    private static async Task WaitForAssignmentAsync(
        IKafkaConsumer<string, string> consumer,
        Func<IReadOnlyCollection<TopicPartition>, bool> predicate,
        CancellationToken cancellationToken)
    {
        while (!predicate(consumer.Assignment))
        {
            _ = await consumer.ConsumeOneAsync(TimeSpan.FromMilliseconds(500), cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private static async Task WaitForPositionWithoutRecordsAsync(
        IKafkaConsumer<string, string> consumer,
        TopicPartition partition,
        long expectedPosition,
        CancellationToken cancellationToken)
    {
        while (consumer.GetPosition(partition) != expectedPosition)
        {
            var record = await consumer.ConsumeOneAsync(TimeSpan.FromMilliseconds(500), cancellationToken)
                .ConfigureAwait(false);
            if (record is not null)
            {
                throw new InvalidOperationException(
                    $"Expected latest offset reset to skip {record.Value.TopicPartitionOffset}.");
            }
        }
    }

    private static async Task<Guid> RecreateTopicAsync(
        IAdminClient admin,
        string topic,
        Guid oldTopicId,
        int partitions,
        CancellationToken cancellationToken)
    {
        await admin.DeleteTopicsAsync([topic], cancellationToken: cancellationToken).ConfigureAwait(false);
        await WaitForTopicDeletionAsync(admin, topic, cancellationToken).ConfigureAwait(false);
        await CreateTopicAfterDeletionAsync(admin, topic, oldTopicId, partitions, cancellationToken)
            .ConfigureAwait(false);

        return await WaitForTopicIdAsync(
            admin,
            topic,
            topicId => topicId != Guid.Empty && topicId != oldTopicId,
            cancellationToken).ConfigureAwait(false);
    }

    private static async Task CreateTopicAfterDeletionAsync(
        IAdminClient admin,
        string topic,
        Guid oldTopicId,
        int partitions,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            try
            {
                await admin.CreateTopicsAsync([new NewTopic
                {
                    Name = topic,
                    NumPartitions = partitions,
                    ReplicationFactor = 1
                }], cancellationToken: cancellationToken).ConfigureAwait(false);
                return;
            }
            catch (KafkaException exception) when (exception.ErrorCode == ErrorCode.TopicAlreadyExists)
            {
                var descriptions = await admin.DescribeTopicsAsync([topic], cancellationToken)
                    .ConfigureAwait(false);
                if (descriptions.TryGetValue(topic, out var description) &&
                    description.ErrorCode == ErrorCode.None &&
                    description.TopicId != Guid.Empty &&
                    description.TopicId != oldTopicId &&
                    description.Partitions.Count == partitions)
                {
                    return;
                }

                await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static async Task<Guid> GetTopicIdAsync(
        IAdminClient admin,
        string topic,
        CancellationToken cancellationToken) =>
        await WaitForTopicIdAsync(
            admin,
            topic,
            static topicId => topicId != Guid.Empty,
            cancellationToken).ConfigureAwait(false);

    private static async Task<Guid> WaitForTopicIdAsync(
        IAdminClient admin,
        string topic,
        Func<Guid, bool> predicate,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            try
            {
                var descriptions = await admin.DescribeTopicsAsync([topic], cancellationToken)
                    .ConfigureAwait(false);
                if (descriptions.TryGetValue(topic, out var description) &&
                    description.ErrorCode == ErrorCode.None &&
                    predicate(description.TopicId))
                {
                    return description.TopicId;
                }
            }
            catch (KafkaException exception) when (
                exception.ErrorCode is ErrorCode.UnknownTopicOrPartition or ErrorCode.UnknownTopicId)
            {
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task WaitForTopicDeletionAsync(
        IAdminClient admin,
        string topic,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            try
            {
                var descriptions = await admin.DescribeTopicsAsync([topic], cancellationToken)
                    .ConfigureAwait(false);
                if (!descriptions.TryGetValue(topic, out var description) ||
                    description.ErrorCode is ErrorCode.UnknownTopicOrPartition or ErrorCode.UnknownTopicId)
                {
                    return;
                }
            }
            catch (KafkaException exception) when (
                exception.ErrorCode is ErrorCode.UnknownTopicOrPartition or ErrorCode.UnknownTopicId)
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken).ConfigureAwait(false);
        }
    }
}
