using Dekaf.Consumer;
using Dekaf.Errors;
using Dekaf.Producer;
using Dekaf.Protocol;
using Dekaf.Testing;

namespace Dekaf.Tests.Unit.Testing;

public sealed class InMemoryConsumerOffsetResetTests
{
    [Test]
    [Arguments(false, false)]
    [Arguments(true, false)]
    [Arguments(false, true)]
    [Arguments(true, true)]
    public async Task ByDuration_NoMatchingRecord_StartsAtHighWatermark(bool snapshot, bool empty)
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic("events");
        if (!empty)
            await ProduceAsync(cluster, DateTimeOffset.UtcNow.AddDays(-1));
        await using var consumer = CreateConsumer(cluster, AutoOffsetReset.ByDuration);
        var partition = new TopicPartition("events", 0);
        consumer.Assign(partition);

        var expectedPosition = empty ? 0L : 1L;
        await Assert.That(consumer.GetPosition(partition)).IsEqualTo(expectedPosition);
        await Assert.That(await ReadAsync(consumer, snapshot)).IsNull();
        await Assert.That(consumer.GetPosition(partition)).IsEqualTo(expectedPosition);
    }

    [Test]
    [Arguments(AutoOffsetReset.Earliest, false, false)]
    [Arguments(AutoOffsetReset.Latest, false, false)]
    [Arguments(AutoOffsetReset.ByDuration, false, false)]
    [Arguments(AutoOffsetReset.Earliest, true, false)]
    [Arguments(AutoOffsetReset.Latest, true, false)]
    [Arguments(AutoOffsetReset.ByDuration, true, false)]
    [Arguments(AutoOffsetReset.Earliest, false, true)]
    [Arguments(AutoOffsetReset.Latest, false, true)]
    [Arguments(AutoOffsetReset.ByDuration, false, true)]
    [Arguments(AutoOffsetReset.Earliest, true, true)]
    [Arguments(AutoOffsetReset.Latest, true, true)]
    [Arguments(AutoOffsetReset.ByDuration, true, true)]
    public async Task OutOfRange_ResetsByPolicy(AutoOffsetReset policy, bool pastEnd, bool snapshot)
    {
        var cluster = new InMemoryKafkaCluster();
        await ProduceAsync(cluster, DateTimeOffset.UtcNow.AddDays(-1));
        await ProduceAsync(cluster, DateTimeOffset.UtcNow.AddDays(-1));
        await ProduceAsync(cluster, DateTimeOffset.UtcNow);
        cluster.DeleteRecords(new Dictionary<TopicPartition, long> { [new("events", 0)] = 1 });
        await using var consumer = CreateConsumer(cluster, policy);
        var partition = new TopicPartition("events", 0);
        consumer.Assign(partition);
        consumer.Seek(new TopicPartitionOffset(partition.Topic, partition.Partition, pastEnd ? 10 : 0));

        var result = await ReadAsync(consumer, snapshot);

        if (snapshot && pastEnd)
        {
            await Assert.That(result).IsNull();
            await Assert.That(consumer.GetPosition(partition)).IsEqualTo(10L);
        }
        else if (policy == AutoOffsetReset.Latest)
        {
            await Assert.That(result).IsNull();
            await Assert.That(consumer.GetPosition(partition)).IsEqualTo(3L);
        }
        else
        {
            await Assert.That(result).IsNotNull();
            await Assert.That(result!.Value.Offset).IsEqualTo(policy == AutoOffsetReset.Earliest ? 1L : 2L);
        }
    }

    [Test]
    [Arguments(AutoOffsetReset.Latest)]
    [Arguments(AutoOffsetReset.None)]
    public async Task CommittedOffsetBelowLogStart_UsesResetPolicy(AutoOffsetReset policy)
    {
        var cluster = new InMemoryKafkaCluster();
        await ProduceAsync(cluster, DateTimeOffset.UtcNow);
        await ProduceAsync(cluster, DateTimeOffset.UtcNow);
        var partition = new TopicPartition("events", 0);
        cluster.CommitOffsets("group", new TopicPartitionOffset[] { new(partition.Topic, partition.Partition, 0) });
        cluster.DeleteRecords(new Dictionary<TopicPartition, long> { [partition] = 1 });
        await using var consumer = CreateConsumer(cluster, policy);
        consumer.Subscribe("events");

        if (policy == AutoOffsetReset.None)
            await AssertResetExceptionAsync(consumer, snapshot: false);
        else
        {
            await Assert.That(await consumer.ConsumeOneAsync(TimeSpan.Zero)).IsNull();
            await Assert.That(consumer.GetPosition(partition)).IsEqualTo(2L);
        }
    }

    [Test]
    [Arguments("assign", false)]
    [Arguments("incremental", false)]
    [Arguments("subscribe", false)]
    [Arguments("assign", true)]
    [Arguments("incremental", true)]
    [Arguments("subscribe", true)]
    public async Task None_MissingCommittedOffset_ThrowsOnlyWhenConsuming(string assignment, bool snapshot)
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic("events");
        await using var consumer = CreateConsumer(cluster, AutoOffsetReset.None);
        var partition = new TopicPartition("events", 0);
        switch (assignment)
        {
            case "assign": consumer.Assign(partition); break;
            case "incremental": consumer.IncrementalAssign([new TopicPartitionOffset(partition.Topic, partition.Partition, -1)]); break;
            case "subscribe": consumer.Subscribe("events"); break;
        }

        await Assert.That(consumer.Assignment).Contains(partition);
        await Assert.That(consumer.GetPosition(partition)).IsNull();
        await AssertResetExceptionAsync(consumer, snapshot);
        // Failure must not consume or replace the unresolved position.
        await AssertResetExceptionAsync(consumer, snapshot);
    }

    [Test]
    [Arguments(false, false)]
    [Arguments(true, false)]
    [Arguments(false, true)]
    [Arguments(true, true)]
    public async Task None_OutOfRange_ThrowsKafkaException(bool pastEnd, bool snapshot)
    {
        var cluster = new InMemoryKafkaCluster();
        await ProduceAsync(cluster, DateTimeOffset.UtcNow);
        cluster.DeleteRecords(new Dictionary<TopicPartition, long> { [new("events", 0)] = 1 });
        await using var consumer = CreateConsumer(cluster, AutoOffsetReset.None);
        consumer.Assign(new TopicPartition("events", 0));
        consumer.Seek(new TopicPartitionOffset("events", 0, pastEnd ? 10 : 0));

        if (snapshot && pastEnd)
        {
            await Assert.That(await ReadAsync(consumer, snapshot)).IsNull();
            await Assert.That(consumer.GetPosition(new TopicPartition("events", 0))).IsEqualTo(10L);
        }
        else
            await AssertResetExceptionAsync(consumer, snapshot);
    }

    [Test]
    public async Task None_PositionAtHighWatermark_WaitsForNextRecord()
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic("events");
        await using var consumer = CreateConsumer(cluster, AutoOffsetReset.None);
        consumer.IncrementalAssign([new TopicPartitionOffset("events", 0, 0)]);
        await Assert.That(await consumer.ConsumeOneAsync(TimeSpan.Zero)).IsNull();
        await ProduceAsync(cluster, DateTimeOffset.UtcNow);
        await Assert.That((await consumer.ConsumeOneAsync(TimeSpan.Zero))!.Value.Offset).IsEqualTo(0L);
    }

    [Test]
    public async Task ByDuration_MatchingRecord_StartsAtMatchingOffset()
    {
        var cluster = new InMemoryKafkaCluster();
        await ProduceAsync(cluster, DateTimeOffset.UtcNow.AddDays(-1));
        await ProduceAsync(cluster, DateTimeOffset.UtcNow);
        await using var consumer = CreateConsumer(cluster, AutoOffsetReset.ByDuration);
        consumer.Assign(new TopicPartition("events", 0));

        await Assert.That((await consumer.ConsumeOneAsync(TimeSpan.Zero))!.Value.Offset).IsEqualTo(1L);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ByDuration_BelowLogStartWithNoMatch_ResetsToHighWatermark(bool snapshot)
    {
        var cluster = new InMemoryKafkaCluster();
        await ProduceAsync(cluster, DateTimeOffset.UtcNow.AddDays(-1));
        await using var consumer = CreateConsumer(cluster, AutoOffsetReset.ByDuration);
        var partition = new TopicPartition("events", 0);
        consumer.Assign(partition);
        cluster.DeleteRecords(new Dictionary<TopicPartition, long> { [partition] = 1 });
        consumer.Seek(new TopicPartitionOffset("events", 0, 0));

        await Assert.That(await ReadAsync(consumer, snapshot)).IsNull();
        await Assert.That(consumer.GetPosition(partition)).IsEqualTo(1L);
    }

    [Test]
    [Arguments(TopicPartitionTimestamp.Earliest, 0L)]
    [Arguments(TopicPartitionTimestamp.Latest, 1L)]
    public async Task None_SeekSentinel_ResolvesWithoutReset(long sentinel, long expectedPosition)
    {
        var cluster = new InMemoryKafkaCluster();
        await ProduceAsync(cluster, DateTimeOffset.UtcNow);
        await using var consumer = CreateConsumer(cluster, AutoOffsetReset.None);
        var partition = new TopicPartition("events", 0);
        consumer.Assign(partition);
        consumer.Seek(new TopicPartitionOffset("events", 0, sentinel));

        var result = await consumer.ConsumeOneAsync(TimeSpan.Zero);

        if (sentinel == TopicPartitionTimestamp.Earliest)
            await Assert.That(result!.Value.Offset).IsEqualTo(expectedPosition);
        else
            await Assert.That(result).IsNull();
        await Assert.That(consumer.GetPosition(partition)).IsEqualTo(1L);
    }

    [Test]
    public async Task None_ValidCommittedOffset_DoesNotReset()
    {
        var cluster = new InMemoryKafkaCluster();
        await ProduceAsync(cluster, DateTimeOffset.UtcNow);
        await ProduceAsync(cluster, DateTimeOffset.UtcNow);
        cluster.CommitOffsets("group", new TopicPartitionOffset[] { new("events", 0, 1) });
        await using var consumer = CreateConsumer(cluster, AutoOffsetReset.None);
        consumer.Subscribe("events");

        await Assert.That((await consumer.ConsumeOneAsync(TimeSpan.Zero))!.Value.Offset).IsEqualTo(1L);
    }

    [Test]
    public async Task ReadCommitted_PositionAfterTransactionBoundary_DoesNotReset()
    {
        var cluster = new InMemoryKafkaCluster();
        await using var producer = new InMemoryProducer<string, string>(cluster);
        await producer.InitTransactionsAsync();
        await using var transaction = producer.BeginTransaction();
        await transaction.ProduceAsync("events", "key", "pending");
        await ProduceAsync(cluster, DateTimeOffset.UtcNow);
        await using var consumer = new InMemoryConsumer<string, string>(cluster, new InMemoryConsumerOptions
        {
            AutoOffsetReset = AutoOffsetReset.None,
            IsolationLevel = Dekaf.Protocol.Messages.IsolationLevel.ReadCommitted
        });
        var partition = new TopicPartition("events", 0);
        consumer.IncrementalAssign([new TopicPartitionOffset("events", 0, 1)]);

        await Assert.That(await consumer.ConsumeOneAsync(TimeSpan.Zero)).IsNull();
        await Assert.That(consumer.GetPosition(partition)).IsEqualTo(1L);
        await transaction.CommitAsync();
        await Assert.That((await consumer.ConsumeOneAsync(TimeSpan.Zero))!.Value.Offset).IsEqualTo(1L);
    }

    [Test]
    [Arguments(false, false)]
    [Arguments(true, false)]
    [Arguments(false, true)]
    [Arguments(true, true)]
    public async Task Earliest_AssignedBeforeTopicExists_ReadsFirstRecord(bool incremental, bool snapshot)
    {
        var cluster = new InMemoryKafkaCluster();
        await using var consumer = CreateConsumer(cluster, AutoOffsetReset.Earliest);
        var partition = new TopicPartition("events", 0);
        if (incremental)
            consumer.IncrementalAssign([new TopicPartitionOffset("events", 0, -1)]);
        else
            consumer.Assign(partition);
        await ProduceAsync(cluster, DateTimeOffset.UtcNow);

        var result = await ReadAsync(consumer, snapshot);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value.Offset).IsEqualTo(0L);
    }

    [Test]
    [Arguments(AutoOffsetReset.Latest, false, false)]
    [Arguments(AutoOffsetReset.Latest, true, false)]
    [Arguments(AutoOffsetReset.Latest, false, true)]
    [Arguments(AutoOffsetReset.Latest, true, true)]
    [Arguments(AutoOffsetReset.ByDuration, false, false)]
    [Arguments(AutoOffsetReset.ByDuration, true, false)]
    [Arguments(AutoOffsetReset.ByDuration, false, true)]
    [Arguments(AutoOffsetReset.ByDuration, true, true)]
    public async Task EmptyResetThenClose_DoesNotCommitResetOrStaleStore(
        AutoOffsetReset policy, bool snapshot, bool autoStore)
    {
        var cluster = new InMemoryKafkaCluster();
        await ProduceAsync(cluster, DateTimeOffset.UtcNow.AddDays(-1));
        await ProduceAsync(cluster, DateTimeOffset.UtcNow.AddDays(-1));
        var partition = new TopicPartition("events", 0);
        cluster.CommitOffsets("group", [new TopicPartitionOffset("events", 0, 0)]);
        cluster.DeleteRecords(new Dictionary<TopicPartition, long> { [partition] = 1 });
        await using var consumer = new InMemoryConsumer<string, string>(cluster, new InMemoryConsumerOptions
        {
            GroupId = "group",
            AutoOffsetReset = policy,
            AutoOffsetResetDuration = policy == AutoOffsetReset.ByDuration ? TimeSpan.FromHours(1) : null,
            EnableAutoOffsetStore = autoStore
        });
        consumer.Assign(partition);
        // A reset clears explicitly stored progress too, even with automatic storage disabled.
        consumer.StoreOffset(new TopicPartitionOffset("events", 0, 1));

        await Assert.That(await ReadAsync(consumer, snapshot)).IsNull();
        await Assert.That(consumer.GetPosition(partition)).IsEqualTo(2L);
        await consumer.CloseAsync();

        await Assert.That(cluster.GetCommittedOffset("group", partition)).IsEqualTo(0L);
    }

    private static InMemoryConsumer<string, string> CreateConsumer(InMemoryKafkaCluster cluster, AutoOffsetReset policy) =>
        new(cluster, new InMemoryConsumerOptions
        {
            GroupId = "group",
            AutoOffsetReset = policy,
            AutoOffsetResetDuration = policy == AutoOffsetReset.ByDuration ? TimeSpan.FromHours(1) : null
        });

    private static async Task ProduceAsync(InMemoryKafkaCluster cluster, DateTimeOffset timestamp)
    {
        await using var producer = new InMemoryProducer<string, string>(cluster);
        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = "events", Partition = 0, Key = "key", Value = "value", Timestamp = timestamp
        });
    }

    private static async Task<ConsumeResult<string, string>?> ReadAsync(InMemoryConsumer<string, string> consumer, bool snapshot)
    {
        if (!snapshot)
            return await consumer.ConsumeOneAsync(TimeSpan.Zero);
        await using var enumerator = consumer.ConsumeSnapshotAsync().GetAsyncEnumerator();
        return await enumerator.MoveNextAsync() ? enumerator.Current : null;
    }

    private static async Task AssertResetExceptionAsync(InMemoryConsumer<string, string> consumer, bool snapshot)
    {
        var exception = await Assert.ThrowsAsync<KafkaException>(async () => { _ = await ReadAsync(consumer, snapshot); });
        var expected = Assert.Throws<KafkaException>(() => AutoOffsetResetStrategy.GetListOffsetsTimestamp(
            new ConsumerOptions { BootstrapServers = [], GroupId = "group", AutoOffsetReset = AutoOffsetReset.None },
            DateTimeOffset.UtcNow, new TopicPartition("events", 0)));
        await Assert.That(exception!.ErrorCode).IsEqualTo(ErrorCode.OffsetOutOfRange);
        await Assert.That(exception.Message).IsEqualTo(expected!.Message);
    }
}
