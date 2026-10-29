using Dekaf.Admin;
using Dekaf.Consumer;
using Dekaf.Producer;
using Dekaf.Protocol.Messages;
using Dekaf.Testing;

namespace Dekaf.Tests.Unit.Testing;

public sealed class InMemoryConsumerTimestampTests
{
    private static readonly TopicPartition Partition = new("events", 0);

    [Test]
    [Arguments(IsolationLevel.ReadCommitted, false)]
    [Arguments(IsolationLevel.ReadCommitted, true)]
    [Arguments(IsolationLevel.ReadUncommitted, false)]
    [Arguments(IsolationLevel.ReadUncommitted, true)]
    public async Task GetOffsetsForTimesAsync_HonorsIsolationAndTransactionCompletion(
        IsolationLevel isolationLevel, bool commit)
    {
        var cluster = new InMemoryKafkaCluster();
        await using var producer = new InMemoryProducer<string, string>(cluster);
        for (var i = 0; i < 10; i++)
            await producer.ProduceAsync(Message(1000 + i));
        await producer.InitTransactionsAsync();
        await using var transaction = producer.BeginTransaction();
        for (var i = 0; i < 5; i++)
            await transaction.ProduceAsync(Message(2000 + i));
        // Even non-transactional records after the first unstable offset remain hidden.
        await using var otherProducer = new InMemoryProducer<string, string>(cluster);
        await otherProducer.ProduceAsync(Message(3000));
        await using var consumer = CreateConsumer(cluster, isolationLevel);
        var readCommitted = isolationLevel == IsolationLevel.ReadCommitted;

        await Assert.That(await QueryOffsetAsync(consumer, TopicPartitionTimestamp.Earliest)).IsEqualTo(0L);
        await Assert.That(await QueryOffsetAsync(consumer, TopicPartitionTimestamp.Latest)).IsEqualTo(readCommitted ? 10L : 16L);
        await Assert.That(await QueryOffsetAsync(consumer, 1005)).IsEqualTo(5L);
        await Assert.That(await QueryOffsetAsync(consumer, 2000)).IsEqualTo(readCommitted ? -1L : 10L);
        await Assert.That(await QueryOffsetAsync(consumer, 3000)).IsEqualTo(readCommitted ? -1L : 15L);
        await Assert.That(await QueryOffsetAsync(consumer, 4000)).IsEqualTo(-1L);

        if (commit)
            await transaction.CommitAsync();
        else
            await transaction.AbortAsync();

        // ListOffsets bounds timestamps by LSO; filtering aborted records belongs to fetching.
        await Assert.That(await QueryOffsetAsync(consumer, TopicPartitionTimestamp.Latest)).IsEqualTo(17L);
        await Assert.That(await QueryOffsetAsync(consumer, TopicPartitionTimestamp.Earliest)).IsEqualTo(0L);
        await Assert.That(await QueryOffsetAsync(consumer, 2000)).IsEqualTo(10L);
        await Assert.That(await QueryOffsetAsync(consumer, 3000)).IsEqualTo(15L);
        await Assert.That(await QueryOffsetAsync(consumer, 4000)).IsEqualTo(-1L);
    }

    [Test]
    [Arguments(IsolationLevel.ReadCommitted)]
    [Arguments(IsolationLevel.ReadUncommitted)]
    public async Task GetOffsetsForTimesAsync_PreservesEmptyAndMissingPartitionResults(IsolationLevel isolationLevel)
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic(Partition.Topic);
        await using var consumer = CreateConsumer(cluster, isolationLevel);
        var missing = new TopicPartition("missing", 0);
        var outOfRange = new TopicPartition(Partition.Topic, 1);
        foreach (var timestamp in new[] { TopicPartitionTimestamp.Earliest, TopicPartitionTimestamp.Latest, 1000L })
        {
            var offsets = await consumer.GetOffsetsForTimesAsync([
                new TopicPartitionTimestamp(Partition, timestamp),
                new TopicPartitionTimestamp(missing, timestamp),
                new TopicPartitionTimestamp(outOfRange, timestamp)
            ]);

            await Assert.That(offsets[Partition]).IsEqualTo(timestamp < 0 ? 0L : -1L);
            await Assert.That(offsets[missing]).IsEqualTo(-1L);
            await Assert.That(offsets[outOfRange]).IsEqualTo(-1L);
        }
    }

    [Test]
    [Arguments(IsolationLevel.ReadCommitted)]
    [Arguments(IsolationLevel.ReadUncommitted)]
    public async Task GetOffsetsForTimesAsync_AfterDeletion_PreservesLogStart(IsolationLevel isolationLevel)
    {
        var cluster = new InMemoryKafkaCluster();
        await using var producer = new InMemoryProducer<string, string>(cluster);
        await producer.ProduceAsync(Message(1000));
        await producer.InitTransactionsAsync();
        await using var transaction = producer.BeginTransaction();
        await transaction.ProduceAsync(Message(2000));
        await transaction.ProduceAsync(Message(3000));
        cluster.DeleteRecords(new Dictionary<TopicPartition, long> { [Partition] = 2L });
        await using var consumer = CreateConsumer(cluster, isolationLevel);

        await Assert.That(await QueryOffsetAsync(consumer, TopicPartitionTimestamp.Earliest)).IsEqualTo(2L);
        await Assert.That(await QueryOffsetAsync(consumer, TopicPartitionTimestamp.Latest))
            .IsEqualTo(isolationLevel == IsolationLevel.ReadCommitted ? 2L : 3L);
        await Assert.That(await QueryOffsetAsync(consumer, 0))
            .IsEqualTo(isolationLevel == IsolationLevel.ReadCommitted ? -1L : 2L);
    }

    [Test]
    [Arguments(IsolationLevel.ReadCommitted)]
    [Arguments(IsolationLevel.ReadUncommitted)]
    public async Task ByDurationReset_DoesNotSearchBeyondIsolationBoundary(IsolationLevel isolationLevel)
    {
        var cluster = new InMemoryKafkaCluster();
        await using var producer = new InMemoryProducer<string, string>(cluster);
        await producer.ProduceAsync(Message(1000));
        await producer.InitTransactionsAsync();
        await using var transaction = producer.BeginTransaction();
        await transaction.ProduceAsync(Message(2000));
        await transaction.ProduceAsync(Message(DateTimeOffset.UtcNow.AddYears(10).ToUnixTimeMilliseconds()));
        await using var consumer = new InMemoryConsumer<string, string>(cluster, new InMemoryConsumerOptions
        {
            IsolationLevel = isolationLevel,
            AutoOffsetReset = AutoOffsetReset.ByDuration,
            AutoOffsetResetDuration = TimeSpan.Zero
        });

        consumer.Assign(Partition);

        await Assert.That(consumer.GetPosition(Partition))
            .IsEqualTo(isolationLevel == IsolationLevel.ReadCommitted ? 1L : 2L);
    }

    [Test]
    public async Task AdminTimestampQueries_DefaultToReadUncommitted()
    {
        var cluster = new InMemoryKafkaCluster();
        await using var producer = new InMemoryProducer<string, string>(cluster);
        await producer.ProduceAsync(Message(1000));
        await producer.InitTransactionsAsync();
        await using var transaction = producer.BeginTransaction();
        await transaction.ProduceAsync(Message(2000));
        await using var admin = new InMemoryAdminClient(cluster);

        foreach (var spec in new[] { OffsetSpec.Latest, OffsetSpec.Timestamp })
        {
            var offsets = await admin.ListOffsetsAsync([
                new TopicPartitionOffsetSpec { TopicPartition = Partition, Spec = spec, Timestamp = 2000 }
            ]);
            await Assert.That(offsets[Partition].Offset).IsEqualTo(spec == OffsetSpec.Latest ? 2L : 1L);
        }
    }

    private static InMemoryConsumer<string, string> CreateConsumer(
        InMemoryKafkaCluster cluster, IsolationLevel isolationLevel) => new(
            cluster, new InMemoryConsumerOptions { IsolationLevel = isolationLevel });

    private static async Task<long> QueryOffsetAsync(InMemoryConsumer<string, string> consumer, long timestamp)
    {
        var offsets = await consumer.GetOffsetsForTimesAsync([new TopicPartitionTimestamp(Partition, timestamp)]);
        return offsets[Partition];
    }

    private static ProducerMessage<string, string> Message(long timestamp) => new()
    {
        Topic = Partition.Topic,
        Partition = Partition.Partition,
        Key = "key",
        Value = "value",
        Timestamp = DateTimeOffset.FromUnixTimeMilliseconds(timestamp)
    };
}
