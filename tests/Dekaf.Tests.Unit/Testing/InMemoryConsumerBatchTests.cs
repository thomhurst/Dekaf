using System.Text;
using Dekaf.Consumer;
using Dekaf.Producer;
using Dekaf.Protocol.Messages;
using Dekaf.Serialization;
using Dekaf.Testing;

namespace Dekaf.Tests.Unit.Testing;

public sealed class InMemoryConsumerBatchTests
{
    private static readonly TopicPartition Partition0 = new("topic", 0);
    private static readonly TopicPartition Partition1 = new("topic", 1);

    [Test]
    public async Task ConsumeBatchAsync_YieldsOnePartitionPerBatch()
    {
        var cluster = await CreateClusterAsync(partition0: 3, partition1: 2);
        await using var consumer = CreateConsumer(cluster, OffsetCommitMode.Manual);
        consumer.Assign(Partition0, Partition1);
        await using var batches = consumer.ConsumeBatchAsync().GetAsyncEnumerator();

        await Assert.That(await batches.MoveNextAsync()).IsTrue();
        await Assert.That(batches.Current.TopicPartition).IsEqualTo(Partition0);
        await Assert.That(Values(batches.Current)).IsEquivalentTo(["p0-0", "p0-1", "p0-2"]);

        await Assert.That(await batches.MoveNextAsync()).IsTrue();
        await Assert.That(batches.Current.TopicPartition).IsEqualTo(Partition1);
        await Assert.That(Values(batches.Current)).IsEquivalentTo(["p1-0", "p1-1"]);
        await Assert.That(consumer.GetPosition(Partition0)).IsEqualTo(3);
    }

    [Test]
    public async Task ConsumeBatchAsync_UnenumeratedBatch_IsRedelivered()
    {
        var cluster = await CreateClusterAsync(partition0: 2, partition1: 0);
        await using var consumer = CreateConsumer(cluster, OffsetCommitMode.Manual);
        consumer.Assign(Partition0);
        await using var batches = consumer.ConsumeBatchAsync().GetAsyncEnumerator();

        await Assert.That(await batches.MoveNextAsync()).IsTrue();
        await Assert.That(await batches.MoveNextAsync()).IsTrue();

        await Assert.That(Offsets(batches.Current)).IsEquivalentTo([0L, 1L]);
    }

    [Test]
    public async Task ConsumeBatchAsync_PartialEnumeration_ProvesOnlyEnumeratedRecords()
    {
        var cluster = await CreateClusterAsync(partition0: 3, partition1: 0);
        await using var consumer = CreateConsumer(cluster, OffsetCommitMode.Manual);
        consumer.Assign(Partition0);
        await using var batches = consumer.ConsumeBatchAsync().GetAsyncEnumerator();

        await Assert.That(await batches.MoveNextAsync()).IsTrue();
        foreach (var record in batches.Current)
        {
            if (record.Offset == 1)
                break;
        }

        await Assert.That(await batches.MoveNextAsync()).IsTrue();
        await Assert.That(Offsets(batches.Current)).IsEquivalentTo([2L]);
    }

    [Test]
    public async Task ConsumeBatchAsync_AutoCommit_CommitsProvenBatch()
    {
        var cluster = await CreateClusterAsync(partition0: 3, partition1: 1);
        await using var consumer = CreateConsumer(cluster, OffsetCommitMode.Auto);
        consumer.Subscribe("topic");
        await using var batches = consumer.ConsumeBatchAsync().GetAsyncEnumerator();

        await Assert.That(await batches.MoveNextAsync()).IsTrue();
        _ = Offsets(batches.Current);
        await Assert.That(cluster.GetCommittedOffset("group", Partition0)).IsNull();

        await Assert.That(await batches.MoveNextAsync()).IsTrue();
        await Assert.That(cluster.GetCommittedOffset("group", Partition0)).IsEqualTo(3);
    }

    [Test]
    public async Task ConsumeBatchAsync_PausedDuringIteration_HoldsRemainingRecords()
    {
        var cluster = await CreateClusterAsync(partition0: 3, partition1: 1);
        await using var consumer = CreateConsumer(cluster, OffsetCommitMode.Manual);
        consumer.Assign(Partition0, Partition1);
        await using var batches = consumer.ConsumeBatchAsync().GetAsyncEnumerator();

        await Assert.That(await batches.MoveNextAsync()).IsTrue();
        var delivered = new List<long>();
        foreach (var record in batches.Current)
        {
            delivered.Add(record.Offset);
            consumer.Pause(Partition0);
        }

        await Assert.That(delivered).IsEquivalentTo([0L]);
        await Assert.That(await batches.MoveNextAsync()).IsTrue();
        await Assert.That(batches.Current.TopicPartition).IsEqualTo(Partition1);
        _ = Offsets(batches.Current);

        consumer.Resume(Partition0);
        await Assert.That(await batches.MoveNextAsync()).IsTrue();
        await Assert.That(Offsets(batches.Current)).IsEquivalentTo([1L, 2L]);
    }

    [Test]
    public async Task ConsumeBatchAsync_SeekDuringIteration_StopsBatchAndResumesAtSeekOffset()
    {
        var cluster = await CreateClusterAsync(partition0: 3, partition1: 0);
        await using var consumer = CreateConsumer(cluster, OffsetCommitMode.Manual);
        consumer.Assign(Partition0);
        await using var batches = consumer.ConsumeBatchAsync().GetAsyncEnumerator();

        await Assert.That(await batches.MoveNextAsync()).IsTrue();
        var delivered = new List<long>();
        foreach (var record in batches.Current)
        {
            delivered.Add(record.Offset);
            consumer.Seek(new TopicPartitionOffset("topic", 0, 2));
        }

        await Assert.That(delivered).IsEquivalentTo([0L]);
        await Assert.That(await batches.MoveNextAsync()).IsTrue();
        await Assert.That(Offsets(batches.Current)).IsEquivalentTo([2L]);
    }

    [Test]
    public async Task ConsumeBatchAsync_SeekToBatchStartDuringIteration_StopsBatchAndReplays()
    {
        var cluster = await CreateClusterAsync(partition0: 3, partition1: 0);
        await using var consumer = CreateConsumer(cluster, OffsetCommitMode.Auto);
        consumer.Assign(Partition0);
        await using var batches = consumer.ConsumeBatchAsync().GetAsyncEnumerator();

        await Assert.That(await batches.MoveNextAsync()).IsTrue();
        var delivered = new List<long>();
        foreach (var record in batches.Current)
        {
            delivered.Add(record.Offset);
            consumer.Seek(new TopicPartitionOffset("topic", 0, 0));
        }

        await Assert.That(delivered).IsEquivalentTo([0L]);
        await Assert.That(await batches.MoveNextAsync()).IsTrue();
        await Assert.That(Offsets(batches.Current)).IsEquivalentTo([0L, 1L, 2L]);
        await Assert.That(cluster.GetCommittedOffset("group", Partition0)).IsNull();
    }

    [Test]
    public async Task ConsumeBatchAsync_ReassignedAtBatchStartDuringIteration_StopsBatchAndReplays()
    {
        var cluster = await CreateClusterAsync(partition0: 3, partition1: 0);
        await using var consumer = CreateConsumer(cluster, OffsetCommitMode.Manual);
        consumer.Assign(Partition0);
        await using var batches = consumer.ConsumeBatchAsync().GetAsyncEnumerator();

        await Assert.That(await batches.MoveNextAsync()).IsTrue();
        var delivered = new List<long>();
        foreach (var record in batches.Current)
        {
            delivered.Add(record.Offset);
            consumer.IncrementalUnassign([Partition0]);
            consumer.IncrementalAssign([new TopicPartitionOffset("topic", 0, 0)]);
        }

        await Assert.That(delivered).IsEquivalentTo([0L]);
        await Assert.That(await batches.MoveNextAsync()).IsTrue();
        await Assert.That(Offsets(batches.Current)).IsEquivalentTo([0L, 1L, 2L]);
    }

    [Test]
    public async Task ConsumeBatchAsync_ReadCommitted_SkipsAbortedRecords()
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic("topic", 1);
        await using (var producer = new InMemoryProducer<string, string>(cluster))
            await ProduceAsync(producer, 0, "before");
        await using (var transactional = new InMemoryProducer<string, string>(
                         cluster,
                         new InMemoryProducerOptions { TransactionalId = "tx" }))
        {
            await transactional.InitTransactionsAsync();
            await using var transaction = transactional.BeginTransaction();
            await transaction.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = "topic", Partition = 0, Key = "key", Value = "aborted"
            });
            await transaction.AbortAsync();
        }
        await using (var producer = new InMemoryProducer<string, string>(cluster))
            await ProduceAsync(producer, 0, "after");

        await using var consumer = new InMemoryConsumer<string, string>(cluster, new InMemoryConsumerOptions
        {
            AutoOffsetReset = AutoOffsetReset.Earliest,
            OffsetCommitMode = OffsetCommitMode.Manual,
            IsolationLevel = IsolationLevel.ReadCommitted
        });
        consumer.Assign(Partition0);
        await using var batches = consumer.ConsumeBatchAsync().GetAsyncEnumerator();

        await Assert.That(await batches.MoveNextAsync()).IsTrue();
        await Assert.That(Values(batches.Current)).IsEquivalentTo(["before", "after"]);
    }

    [Test]
    public async Task ConsumeRawBatchAsync_YieldsRawRecordsAndRedeliversUnenumeratedBatch()
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic("topic", 1);
        await using (var producer = new InMemoryProducer<string, string>(cluster))
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = "topic",
                Partition = 0,
                Key = "key",
                Value = "value",
                Headers = new Headers { { "trace", "abc"u8.ToArray() } }
            });
        }

        await using var consumer = CreateConsumer(cluster, OffsetCommitMode.Manual);
        consumer.Assign(Partition0);
        await using var batches = consumer.ConsumeRawBatchAsync().GetAsyncEnumerator();

        await Assert.That(await batches.MoveNextAsync()).IsTrue();
        await Assert.That(await batches.MoveNextAsync()).IsTrue();
        var records = new List<(long Offset, string Key, string Value, string Header)>();
        foreach (var record in batches.Current)
        {
            var header = record.Headers.Span[0];
            records.Add((
                record.Offset,
                Encoding.UTF8.GetString(record.Key.Span),
                Encoding.UTF8.GetString(record.Value.Span),
                header.Key + "=" + Encoding.UTF8.GetString(header.Value.Span)));
        }

        await Assert.That(records).IsEquivalentTo([(0L, "key", "value", "trace=abc")]);
        await Assert.That(consumer.GetPosition(Partition0)).IsEqualTo(0);
    }

    private static async Task<InMemoryKafkaCluster> CreateClusterAsync(int partition0, int partition1)
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic("topic", 2);
        await using var producer = new InMemoryProducer<string, string>(cluster);
        for (var i = 0; i < partition0; i++)
            await ProduceAsync(producer, 0, $"p0-{i}");
        for (var i = 0; i < partition1; i++)
            await ProduceAsync(producer, 1, $"p1-{i}");
        return cluster;
    }

    private static InMemoryConsumer<string, string> CreateConsumer(
        InMemoryKafkaCluster cluster, OffsetCommitMode mode) => new(cluster, new InMemoryConsumerOptions
        {
            GroupId = "group", AutoOffsetReset = AutoOffsetReset.Earliest, OffsetCommitMode = mode
        });

    private static ValueTask<RecordMetadata> ProduceAsync(
        InMemoryProducer<string, string> producer, int partition, string value) =>
        producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = "topic", Partition = partition, Key = "key", Value = value
        });

    private static List<string> Values(ConsumeBatch<string, string> batch) =>
        Project(batch, static record => record.Value);

    private static List<long> Offsets(ConsumeBatch<string, string> batch) =>
        Project(batch, static record => record.Offset);

    private static List<T> Project<T>(
        ConsumeBatch<string, string> batch, Func<ConsumeResult<string, string>, T> select)
    {
        var projected = new List<T>();
        foreach (var record in batch)
            projected.Add(select(record));
        return projected;
    }
}
