using Dekaf.Consumer;
using Dekaf.Producer;
using Dekaf.Protocol.Messages;
using Dekaf.Testing;

namespace Dekaf.Tests.Unit.Testing;

public sealed class InMemoryConsumerWatermarkTests
{
    private static readonly TopicPartition Partition = new("events", 0);

    [Test]
    [Arguments(IsolationLevel.ReadCommitted, false)]
    [Arguments(IsolationLevel.ReadCommitted, true)]
    [Arguments(IsolationLevel.ReadUncommitted, false)]
    [Arguments(IsolationLevel.ReadUncommitted, true)]
    public async Task Watermarks_HonorIsolationAndTrackTransactionCompletion(IsolationLevel isolationLevel, bool commit)
    {
        var cluster = new InMemoryKafkaCluster();
        await using var producer = new InMemoryProducer<string, string>(cluster);
        await ProduceCommittedAsync(producer);
        await producer.InitTransactionsAsync();
        await using var transaction = producer.BeginTransaction();
        await ProducePendingAsync(transaction);
        await using var consumer = CreateConsumer(cluster, isolationLevel);
        consumer.Assign(Partition);
        consumer.Seek(new TopicPartitionOffset(Partition.Topic, Partition.Partition, 2));
        var expectedHigh = isolationLevel == IsolationLevel.ReadCommitted ? 10L : 15L;

        var queried = await consumer.QueryWatermarkOffsetsAsync(Partition);
        var cached = consumer.GetWatermarkOffsets(Partition)!.Value;

        await Assert.That(queried.Low).IsEqualTo(0L);
        await Assert.That(queried.High).IsEqualTo(expectedHigh);
        await Assert.That(cached).IsEqualTo(queried);
        await Assert.That(consumer.GetCurrentLag(Partition)).IsEqualTo(queried.High - 2);

        if (commit)
            await transaction.CommitAsync();
        else
            await transaction.AbortAsync();

        // Completion reserves one invisible COMMIT/ABORT control offset after the five pending records.
        await Assert.That((await consumer.QueryWatermarkOffsetsAsync(Partition)).High).IsEqualTo(16L);
        await Assert.That(consumer.GetWatermarkOffsets(Partition)!.Value.High).IsEqualTo(16L);
    }

    [Test]
    [Arguments(IsolationLevel.ReadCommitted, 0)]
    [Arguments(IsolationLevel.ReadCommitted, 3)]
    [Arguments(IsolationLevel.ReadCommitted, 100)]
    [Arguments(IsolationLevel.ReadUncommitted, 0)]
    [Arguments(IsolationLevel.ReadUncommitted, 3)]
    [Arguments(IsolationLevel.ReadUncommitted, 100)]
    public async Task SeekToTailAsync_UsesIsolationAwareEnd(IsolationLevel isolationLevel, int offsetCount)
    {
        var cluster = new InMemoryKafkaCluster();
        await using var producer = new InMemoryProducer<string, string>(cluster);
        await ProduceCommittedAsync(producer);
        await producer.InitTransactionsAsync();
        await using var transaction = producer.BeginTransaction();
        await ProducePendingAsync(transaction);
        await using var consumer = CreateConsumer(cluster, isolationLevel);
        consumer.Assign(Partition);
        var end = isolationLevel == IsolationLevel.ReadCommitted ? 10L : 15L;
        var expectedOffset = Math.Max(0, end - offsetCount);

        var tail = await consumer.SeekToTailAsync(Partition, offsetCount);

        await Assert.That(tail.Offset).IsEqualTo(expectedOffset);
        await Assert.That(consumer.GetPosition(Partition)).IsEqualTo(expectedOffset);
        var record = await consumer.ConsumeOneAsync(TimeSpan.Zero);
        if (offsetCount == 0)
            await Assert.That(record).IsNull();
        else
            await Assert.That(record!.Value.Offset).IsEqualTo(expectedOffset);
    }

    [Test]
    [Arguments(IsolationLevel.ReadCommitted, false)]
    [Arguments(IsolationLevel.ReadCommitted, true)]
    [Arguments(IsolationLevel.ReadUncommitted, false)]
    [Arguments(IsolationLevel.ReadUncommitted, true)]
    public async Task SeekToEnd_UsesIsolationAwareEnd(IsolationLevel isolationLevel, bool allAssignedPartitions)
    {
        var cluster = new InMemoryKafkaCluster();
        await using var producer = new InMemoryProducer<string, string>(cluster);
        await ProduceCommittedAsync(producer);
        await producer.InitTransactionsAsync();
        await using var transaction = producer.BeginTransaction();
        await ProducePendingAsync(transaction);
        await using var consumer = CreateConsumer(cluster, isolationLevel);
        consumer.Assign(Partition);

        if (allAssignedPartitions)
            consumer.SeekToEnd();
        else
            consumer.SeekToEnd(Partition);

        await Assert.That(consumer.GetPosition(Partition))
            .IsEqualTo(isolationLevel == IsolationLevel.ReadCommitted ? 10L : 15L);
        await Assert.That(await consumer.ConsumeOneAsync(TimeSpan.Zero)).IsNull();
        await transaction.CommitAsync();
        var record = await consumer.ConsumeOneAsync(TimeSpan.Zero);
        if (isolationLevel == IsolationLevel.ReadCommitted)
            await Assert.That(record!.Value.Offset).IsEqualTo(10L);
        else
            await Assert.That(record).IsNull();
    }

    [Test]
    [Arguments(IsolationLevel.ReadCommitted, false)]
    [Arguments(IsolationLevel.ReadCommitted, true)]
    [Arguments(IsolationLevel.ReadUncommitted, false)]
    [Arguments(IsolationLevel.ReadUncommitted, true)]
    public async Task LatestReset_UsesIsolationAwareEnd(IsolationLevel isolationLevel, bool subscribe)
    {
        var cluster = new InMemoryKafkaCluster();
        await using var producer = new InMemoryProducer<string, string>(cluster);
        await ProduceCommittedAsync(producer);
        await producer.InitTransactionsAsync();
        await using var transaction = producer.BeginTransaction();
        await ProducePendingAsync(transaction);
        await using var consumer = new InMemoryConsumer<string, string>(
            cluster,
            new InMemoryConsumerOptions
            {
                GroupId = subscribe ? "latest-group" : null,
                AutoOffsetReset = AutoOffsetReset.Latest,
                IsolationLevel = isolationLevel
            });

        if (subscribe)
            consumer.Subscribe(Partition.Topic);
        else
            consumer.Assign(Partition);

        await Assert.That(consumer.GetPosition(Partition))
            .IsEqualTo(isolationLevel == IsolationLevel.ReadCommitted ? 10L : 15L);
        await transaction.CommitAsync();
        var record = await consumer.ConsumeOneAsync(TimeSpan.Zero);
        if (isolationLevel == IsolationLevel.ReadCommitted)
            await Assert.That(record!.Value.Offset).IsEqualTo(10L);
        else
            await Assert.That(record).IsNull();
    }

    [Test]
    [Arguments(IsolationLevel.ReadCommitted, false)]
    [Arguments(IsolationLevel.ReadCommitted, true)]
    [Arguments(IsolationLevel.ReadUncommitted, false)]
    [Arguments(IsolationLevel.ReadUncommitted, true)]
    public async Task LatestReset_MissingPartition_ResolvesAfterCreation(IsolationLevel isolationLevel, bool missingTopic)
    {
        var cluster = new InMemoryKafkaCluster();
        var partition = new TopicPartition(Partition.Topic, missingTopic ? 0 : 1);
        if (!missingTopic)
            cluster.CreateTopic(partition.Topic);
        await using var consumer = new InMemoryConsumer<string, string>(
            cluster,
            new InMemoryConsumerOptions
            {
                AutoOffsetReset = AutoOffsetReset.Latest,
                IsolationLevel = isolationLevel
            });
        consumer.Assign(partition);

        if (missingTopic)
            cluster.CreateTopic(partition.Topic);
        else
            cluster.CreatePartitions(partition.Topic, 2);
        await using var producer = new InMemoryProducer<string, string>(cluster);
        var message = new ProducerMessage<string, string>
        {
            Topic = partition.Topic,
            Partition = partition.Partition,
            Key = "key",
            Value = "value"
        };
        await producer.ProduceAsync(message);
        await producer.InitTransactionsAsync();
        await using var transaction = producer.BeginTransaction();
        await transaction.ProduceAsync(message);

        await Assert.That(await consumer.ConsumeOneAsync(TimeSpan.Zero)).IsNull();
        await Assert.That(consumer.GetPosition(partition))
            .IsEqualTo(isolationLevel == IsolationLevel.ReadCommitted ? 1L : 2L);

        await transaction.CommitAsync();
        var record = await consumer.ConsumeOneAsync(TimeSpan.Zero);
        if (isolationLevel == IsolationLevel.ReadCommitted)
            await Assert.That(record!.Value.Offset).IsEqualTo(1L);
        else
            await Assert.That(record).IsNull();
    }

    [Test]
    [Arguments(IsolationLevel.ReadCommitted, TopicPartitionTimestamp.Latest)]
    [Arguments(IsolationLevel.ReadUncommitted, TopicPartitionTimestamp.Latest)]
    [Arguments(IsolationLevel.ReadCommitted, 100L)]
    [Arguments(IsolationLevel.ReadUncommitted, 100L)]
    public async Task LatestPositionResolution_UsesIsolationAwareEnd(IsolationLevel isolationLevel, long position)
    {
        var cluster = new InMemoryKafkaCluster();
        await using var producer = new InMemoryProducer<string, string>(cluster);
        await ProduceCommittedAsync(producer);
        await producer.InitTransactionsAsync();
        await using var transaction = producer.BeginTransaction();
        await ProducePendingAsync(transaction);
        await using var consumer = new InMemoryConsumer<string, string>(
            cluster,
            new InMemoryConsumerOptions
            {
                AutoOffsetReset = position == TopicPartitionTimestamp.Latest ? AutoOffsetReset.None : AutoOffsetReset.Latest,
                IsolationLevel = isolationLevel
            });
        consumer.Assign(Partition);
        consumer.Seek(new TopicPartitionOffset(Partition.Topic, Partition.Partition, position));

        await Assert.That(await consumer.ConsumeOneAsync(TimeSpan.Zero)).IsNull();
        await Assert.That(consumer.GetPosition(Partition))
            .IsEqualTo(isolationLevel == IsolationLevel.ReadCommitted ? 10L : 15L);

        await transaction.CommitAsync();
        var record = await consumer.ConsumeOneAsync(TimeSpan.Zero);
        if (isolationLevel == IsolationLevel.ReadCommitted)
            await Assert.That(record!.Value.Offset).IsEqualTo(10L);
        else
            await Assert.That(record).IsNull();
    }

    [Test]
    [Arguments(IsolationLevel.ReadCommitted)]
    [Arguments(IsolationLevel.ReadUncommitted)]
    public async Task ByDurationReset_NoMatchingRecord_UsesIsolationAwareEnd(IsolationLevel isolationLevel)
    {
        var cluster = new InMemoryKafkaCluster();
        await using var producer = new InMemoryProducer<string, string>(cluster);
        var message = new ProducerMessage<string, string>
        {
            Topic = Partition.Topic,
            Partition = Partition.Partition,
            Key = "key",
            Value = "value",
            Timestamp = DateTimeOffset.UnixEpoch
        };
        await producer.ProduceAsync(message);
        await producer.InitTransactionsAsync();
        await using var transaction = producer.BeginTransaction();
        await transaction.ProduceAsync(message);
        await using var consumer = new InMemoryConsumer<string, string>(
            cluster,
            new InMemoryConsumerOptions
            {
                AutoOffsetReset = AutoOffsetReset.ByDuration,
                AutoOffsetResetDuration = TimeSpan.Zero,
                IsolationLevel = isolationLevel
            });
        consumer.Assign(Partition);

        await Assert.That(consumer.GetPosition(Partition))
            .IsEqualTo(isolationLevel == IsolationLevel.ReadCommitted ? 1L : 2L);
    }

    [Test]
    [Arguments(IsolationLevel.ReadCommitted, false)]
    [Arguments(IsolationLevel.ReadCommitted, true)]
    [Arguments(IsolationLevel.ReadUncommitted, false)]
    [Arguments(IsolationLevel.ReadUncommitted, true)]
    public async Task Snapshot_CapturesIsolationAwareEnd(IsolationLevel isolationLevel, bool commitDuringSnapshot)
    {
        var cluster = new InMemoryKafkaCluster();
        await using var producer = new InMemoryProducer<string, string>(cluster);
        await ProduceCommittedAsync(producer);
        await producer.InitTransactionsAsync();
        await using var transaction = producer.BeginTransaction();
        await ProducePendingAsync(transaction);
        await using var consumer = CreateConsumer(cluster, isolationLevel);
        consumer.Assign(Partition);
        await using var snapshot = consumer.ConsumeSnapshotAsync().GetAsyncEnumerator();

        await Assert.That(await snapshot.MoveNextAsync()).IsTrue();
        var offsets = new List<long> { snapshot.Current.Offset };
        if (commitDuringSnapshot)
            await transaction.CommitAsync();
        while (await snapshot.MoveNextAsync())
            offsets.Add(snapshot.Current.Offset);

        var expectedEnd = isolationLevel == IsolationLevel.ReadCommitted ? 10 : 15;
        await Assert.That(offsets).IsEquivalentTo(Enumerable.Range(0, expectedEnd).Select(offset => (long)offset));
        await Assert.That(consumer.GetPosition(Partition)).IsEqualTo((long)expectedEnd);
    }

    [Test]
    [Arguments(IsolationLevel.ReadCommitted, 12L, false)]
    [Arguments(IsolationLevel.ReadCommitted, 15L, false)]
    [Arguments(IsolationLevel.ReadUncommitted, 12L, false)]
    [Arguments(IsolationLevel.ReadUncommitted, 15L, false)]
    [Arguments(IsolationLevel.ReadCommitted, 12L, true)]
    [Arguments(IsolationLevel.ReadCommitted, 15L, true)]
    [Arguments(IsolationLevel.ReadUncommitted, 12L, true)]
    [Arguments(IsolationLevel.ReadUncommitted, 15L, true)]
    public async Task EndWatermarks_AfterDeletion_DoNotPrecedeLogStart(
        IsolationLevel isolationLevel, long deletedBefore, bool seekToTail)
    {
        var cluster = new InMemoryKafkaCluster();
        await using var producer = new InMemoryProducer<string, string>(cluster);
        await ProduceCommittedAsync(producer);
        await producer.InitTransactionsAsync();
        await using var transaction = producer.BeginTransaction();
        await ProducePendingAsync(transaction);
        await using var admin = new InMemoryAdminClient(cluster);
        await admin.DeleteRecordsAsync(new Dictionary<TopicPartition, long> { [Partition] = deletedBefore });
        await using var consumer = new InMemoryConsumer<string, string>(
            cluster,
            new InMemoryConsumerOptions
            {
                AutoOffsetReset = AutoOffsetReset.None,
                IsolationLevel = isolationLevel
            });
        consumer.Assign(Partition);
        var expectedEnd = isolationLevel == IsolationLevel.ReadCommitted ? deletedBefore : 15L;

        var watermarks = await consumer.QueryWatermarkOffsetsAsync(Partition);
        await Assert.That(watermarks.Low).IsEqualTo(deletedBefore);
        await Assert.That(watermarks.High).IsEqualTo(expectedEnd);
        await Assert.That(consumer.GetWatermarkOffsets(Partition)!.Value).IsEqualTo(watermarks);
        if (seekToTail)
            await consumer.SeekToTailAsync(Partition, 0);
        else
            consumer.SeekToEnd(Partition);
        await Assert.That(consumer.GetPosition(Partition)).IsEqualTo(expectedEnd);
        await Assert.That(await consumer.ConsumeOneAsync(TimeSpan.Zero)).IsNull();

        await transaction.CommitAsync();
        await using var ordinaryProducer = new InMemoryProducer<string, string>(cluster);
        var produced = await ordinaryProducer.ProduceAsync(Partition.Topic, "after", "after");
        var record = await consumer.ConsumeOneAsync(TimeSpan.Zero);
        var expectedNext = expectedEnd < 15 ? expectedEnd : produced.Offset;
        await Assert.That(record!.Value.Offset).IsEqualTo(expectedNext);
    }

    private static InMemoryConsumer<string, string> CreateConsumer(
        InMemoryKafkaCluster cluster,
        IsolationLevel isolationLevel) => new(
            cluster,
            new InMemoryConsumerOptions
            {
                AutoOffsetReset = AutoOffsetReset.Earliest,
                IsolationLevel = isolationLevel
            });

    private static async Task ProduceCommittedAsync(InMemoryProducer<string, string> producer)
    {
        for (var i = 0; i < 10; i++)
            await producer.ProduceAsync(Partition.Topic, $"key-{i}", $"committed-{i}");
    }

    private static async Task ProducePendingAsync(ITransaction<string, string> transaction)
    {
        for (var i = 0; i < 5; i++)
            await transaction.ProduceAsync(Partition.Topic, $"key-{i}", $"pending-{i}");
    }
}
