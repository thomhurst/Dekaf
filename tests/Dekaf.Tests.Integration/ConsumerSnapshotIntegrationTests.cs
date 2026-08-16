using Dekaf.Admin;
using Dekaf.Consumer;
using Dekaf.Producer;
using Dekaf.Protocol.Messages;

namespace Dekaf.Tests.Integration;

[Category("Consumer")]
public sealed class ConsumerSnapshotIntegrationTests(KafkaTestContainer kafka)
    : TransactionalKafkaIntegrationTest(kafka)
{
    [Test]
    public async Task ConsumeSnapshotAsync_EmptyPartitionCompletesWithoutWaiting()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync().ConfigureAwait(false);
        await using var consumer = await CreateConsumerAsync(queuedMinMessages: 10).ConfigureAwait(false);
        consumer.Partitions.Assign(new TopicPartition(topic, 0));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));

        var records = await CollectAsync(consumer.ConsumeSnapshotAsync(timeout.Token)).ConfigureAwait(false);

        await Assert.That(records).IsEmpty();
    }

    [Test]
    public async Task SeekToTailAndSnapshot_MultiplePartitionsReturnFinalOffsetsWithPrefetch()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 2).ConfigureAwait(false);
        await ProduceRangeAsync(topic, partition: 0, count: 5).ConfigureAwait(false);
        await ProduceRangeAsync(topic, partition: 1, count: 5).ConfigureAwait(false);
        await using var consumer = await CreateConsumerAsync(queuedMinMessages: 10).ConfigureAwait(false);
        var partition0 = new TopicPartition(topic, 0);
        var partition1 = new TopicPartition(topic, 1);
        consumer.Partitions.Assign(partition0, partition1);
        await consumer.SeekToTailAsync(partition0, 2).ConfigureAwait(false);
        await consumer.SeekToTailAsync(partition1, 2).ConfigureAwait(false);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        var records = await CollectAsync(consumer.ConsumeSnapshotAsync(timeout.Token)).ConfigureAwait(false);

        await Assert.That(records).Count().IsEqualTo(4);
        await Assert.That(records.Where(record => record.Partition == 0).Select(record => record.Offset))
            .IsEquivalentTo([3L, 4L]);
        await Assert.That(records.Where(record => record.Partition == 1).Select(record => record.Offset))
            .IsEquivalentTo([3L, 4L]);
    }

    [Test]
    public async Task ConsumeSnapshotAsync_RecordAppendedAfterCaptureIsLeftForNormalConsumption()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync().ConfigureAwait(false);
        await ProduceRangeAsync(topic, partition: 0, count: 2).ConfigureAwait(false);
        await using var consumer = await CreateConsumerAsync(queuedMinMessages: 10).ConfigureAwait(false);
        consumer.Partitions.Assign(new TopicPartition(topic, 0));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await using var snapshot = consumer.ConsumeSnapshotAsync(timeout.Token).GetAsyncEnumerator();

        await Assert.That(await snapshot.MoveNextAsync()).IsTrue();
        var first = snapshot.Current;
        await ProduceRangeAsync(topic, partition: 0, start: 2, count: 1).ConfigureAwait(false);
        await Assert.That(await snapshot.MoveNextAsync()).IsTrue();
        var second = snapshot.Current;
        await Assert.That(await snapshot.MoveNextAsync()).IsFalse();
        var appended = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(15), timeout.Token)
            .ConfigureAwait(false);

        await Assert.That(new[] { first.Offset, second.Offset }).IsEquivalentTo([0L, 1L]);
        await Assert.That(appended).IsNotNull();
        await Assert.That(appended!.Value.Offset).IsEqualTo(2L);
    }

    [Test]
    public async Task ConsumeSnapshotAsync_ReadCommittedAbortedOnlyLogCompletesWithoutRecords()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync().ConfigureAwait(false);
        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithTransactionalId($"snapshot-abort-{Guid.NewGuid():N}")
            .WithAcks(Acks.All)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync().ConfigureAwait(false);
        await producer.InitTransactionsAsync().ConfigureAwait(false);
        await using (var transaction = producer.BeginTransaction())
        {
            for (var i = 0; i < 3; i++)
            {
                await transaction.ProduceAsync(new ProducerMessage<string, string>
                {
                    Topic = topic,
                    Partition = 0,
                    Key = $"aborted-{i}",
                    Value = $"aborted-{i}"
                }, CancellationToken.None).ConfigureAwait(false);
            }

            await transaction.AbortAsync().ConfigureAwait(false);
        }

        await using var consumer = await CreateConsumerAsync(
            queuedMinMessages: 1,
            isolationLevel: IsolationLevel.ReadCommitted).ConfigureAwait(false);
        var partition = new TopicPartition(topic, 0);
        consumer.Partitions.Assign(partition);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        var records = await CollectAsync(consumer.ConsumeSnapshotAsync(timeout.Token)).ConfigureAwait(false);
        var watermarks = await consumer.Offsets.QueryWatermarkOffsetsAsync(partition, timeout.Token)
            .ConfigureAwait(false);

        await Assert.That(records).IsEmpty();
        await Assert.That(consumer.Positions.GetPosition(partition)).IsEqualTo(watermarks.High);
    }

    [Test]
    public async Task ConsumeSnapshotAsync_RetentionGapResetsAndCompletesAtCapturedEnd()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync().ConfigureAwait(false);
        await ProduceRangeAsync(topic, partition: 0, count: 10, valueSize: 8 * 1024).ConfigureAwait(false);
        var partition = new TopicPartition(topic, 0);
        await using var consumer = await CreateConsumerAsync(
            queuedMinMessages: 1,
            maxPartitionFetchBytes: 9 * 1024).ConfigureAwait(false);
        consumer.Partitions.Assign(partition);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await using var snapshot = consumer.ConsumeSnapshotAsync(timeout.Token).GetAsyncEnumerator();
        await Assert.That(await snapshot.MoveNextAsync()).IsTrue();
        var offsets = new List<long> { snapshot.Current.Offset };

        await using (var admin = new AdminClientBuilder()
                         .WithBootstrapServers(KafkaContainer.BootstrapServers)
                         .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
                         .Build())
        {
            var deleted = await admin.DeleteRecordsAsync(new Dictionary<TopicPartition, long>
            {
                [partition] = 5
            }).ConfigureAwait(false);
            await Assert.That(deleted[partition]).IsGreaterThanOrEqualTo(5L);
        }

        while (await snapshot.MoveNextAsync())
            offsets.Add(snapshot.Current.Offset);

        await Assert.That(offsets).IsEquivalentTo([0L, 5L, 6L, 7L, 8L, 9L]);
        await Assert.That(consumer.Positions.GetPosition(partition)).IsEqualTo(10L);
    }

    [Test]
    public async Task ConsumeSnapshotAsync_GroupRebalanceInvalidatesCapturedAssignment()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 2).ConfigureAwait(false);
        await ProduceRangeAsync(topic, partition: 0, count: 3).ConfigureAwait(false);
        await ProduceRangeAsync(topic, partition: 1, count: 3).ConfigureAwait(false);
        var groupId = $"snapshot-rebalance-{Guid.NewGuid():N}";
        await using var firstConsumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithQueuedMinMessages(1)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync().ConfigureAwait(false);
        firstConsumer.Subscribe(topic);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        await using var snapshot = firstConsumer.ConsumeSnapshotAsync(timeout.Token).GetAsyncEnumerator();
        await Assert.That(await snapshot.MoveNextAsync()).IsTrue();

        await using var secondConsumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithQueuedMinMessages(1)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync().ConfigureAwait(false);
        secondConsumer.Subscribe(topic);
        var secondResult = await secondConsumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), timeout.Token)
            .ConfigureAwait(false);
        await Assert.That(secondResult).IsNotNull();

        var assignmentInvalidated = false;
        try
        {
            while (await snapshot.MoveNextAsync())
            {
            }
        }
        catch (InvalidOperationException)
        {
            assignmentInvalidated = true;
        }

        await Assert.That(assignmentInvalidated).IsTrue();
    }

    private async ValueTask<IKafkaConsumer<string, string>> CreateConsumerAsync(
        int queuedMinMessages,
        IsolationLevel isolationLevel = IsolationLevel.ReadUncommitted,
        int? maxPartitionFetchBytes = null)
    {
        var builder = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithQueuedMinMessages(queuedMinMessages)
            .WithIsolationLevel(isolationLevel)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory());
        if (maxPartitionFetchBytes.HasValue)
            builder.WithMaxPartitionFetchBytes(maxPartitionFetchBytes.Value);

        return await builder.BuildAsync().ConfigureAwait(false);
    }

    private async Task ProduceRangeAsync(
        string topic,
        int partition,
        int count,
        int start = 0,
        int valueSize = 0)
    {
        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithAcks(Acks.All)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync().ConfigureAwait(false);

        for (var i = start; i < start + count; i++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Partition = partition,
                Key = $"key-{i}",
                Value = valueSize > 0 ? new string((char)('a' + i % 26), valueSize) : $"value-{i}"
            }, CancellationToken.None).ConfigureAwait(false);
        }
    }

    private static async Task<List<ConsumeResult<string, string>>> CollectAsync(
        IAsyncEnumerable<ConsumeResult<string, string>> source)
    {
        var records = new List<ConsumeResult<string, string>>();
        await foreach (var record in source.ConfigureAwait(false))
            records.Add(record);
        return records;
    }
}
