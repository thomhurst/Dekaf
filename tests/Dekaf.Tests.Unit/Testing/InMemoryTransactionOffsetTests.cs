using Dekaf.Admin;
using Dekaf.Consumer;
using Dekaf.Producer;
using Dekaf.Protocol.Messages;
using Dekaf.ShareConsumer;
using Dekaf.Testing;

namespace Dekaf.Tests.Unit.Testing;

public sealed class InMemoryTransactionOffsetTests
{
    private static readonly TopicPartition Partition = new("events", 0);

    [Test]
    [Arguments(true, false)]
    [Arguments(false, false)]
    [Arguments(true, true)]
    [Arguments(false, true)]
    public async Task CompleteTransaction_ReservesOneOffsetPerParticipatingPartition(bool commit, bool prepared)
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic(Partition.Topic, partitionCount: 3);
        await using var producer = new InMemoryProducer<string, string>(cluster);
        await producer.InitTransactionsAsync();
        await using (var transaction = producer.BeginTransaction())
        {
            await transaction.ProduceAsync(Message(0));
            await transaction.ProduceAsync(Message(0));
            await transaction.ProduceAsync(Message(1));
            if (prepared)
            {
                var state = await transaction.PrepareAsync();
                await producer.CompletePreparedTransactionAsync(state, commit);
            }
            else if (commit)
                await transaction.CommitAsync();
            else
                await transaction.AbortAsync();
        }

        await Assert.That(cluster.GetWatermarks(Partition).High).IsEqualTo(3L);
        await Assert.That(cluster.GetWatermarks(new TopicPartition(Partition.Topic, 1)).High).IsEqualTo(2L);
        await Assert.That(cluster.GetWatermarks(new TopicPartition(Partition.Topic, 2)).High).IsEqualTo(0L);
        await Assert.That(cluster.GetOffsetForTimestamp(Partition, TopicPartitionTimestamp.Latest)).IsEqualTo(3L);
        await Assert.That(cluster.ReadRecords(Partition.Topic).Count).IsEqualTo(commit ? 2 : 0);
        await using var admin = new InMemoryAdminClient(cluster);
        var endOffsets = await admin.ListOffsetsAsync([
            new TopicPartitionOffsetSpec { TopicPartition = Partition, Spec = OffsetSpec.Latest }
        ]);
        await Assert.That(endOffsets[Partition].Offset).IsEqualTo(3L);

        await using var next = producer.BeginTransaction();
        var metadata = await next.ProduceAsync(Message(0));
        await Assert.That(metadata.Offset).IsEqualTo(3L);
        await next.CommitAsync();
        await Assert.That(cluster.GetWatermarks(Partition).High).IsEqualTo(5L);
    }

    [Test]
    [Arguments(IsolationLevel.ReadCommitted)]
    [Arguments(IsolationLevel.ReadUncommitted)]
    public async Task ConsumeSnapshot_SkipsMarkersAndAdvancesToLogEnd(IsolationLevel isolationLevel)
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic(Partition.Topic);
        await using var producer = new InMemoryProducer<string, string>(cluster);
        await producer.InitTransactionsAsync();
        await using (var aborted = producer.BeginTransaction())
        {
            await aborted.ProduceAsync(Message(0, "aborted"));
            await aborted.AbortAsync();
        }
        await using (var committed = producer.BeginTransaction())
        {
            await committed.ProduceAsync(Message(0, "committed"));
            await committed.CommitAsync();
        }
        await using var consumer = new InMemoryConsumer<string, string>(cluster,
            new InMemoryConsumerOptions { IsolationLevel = isolationLevel });
        consumer.Assign(Partition);
        consumer.Seek(new TopicPartitionOffset(Partition.Topic, 0, 0));

        var offsets = new List<long>();
        await foreach (var record in consumer.ConsumeSnapshotAsync())
            offsets.Add(record.Offset);

        long[] expected = isolationLevel == IsolationLevel.ReadCommitted ? [2L] : [0L, 2L];
        await Assert.That(offsets).IsEquivalentTo(expected);
        await Assert.That(consumer.GetPosition(Partition)).IsEqualTo(4L);
        await Assert.That(consumer.GetCurrentLag(Partition)).IsEqualTo(0L);
        await Assert.That(cluster.ReadRecords(Partition.Topic).Select(record => record.Offset)).IsEquivalentTo([2L]);
    }

    [Test]
    [Arguments(true, IsolationLevel.ReadCommitted)]
    [Arguments(false, IsolationLevel.ReadCommitted)]
    [Arguments(true, IsolationLevel.ReadUncommitted)]
    [Arguments(false, IsolationLevel.ReadUncommitted)]
    public async Task ConsumeOne_SkipsTrailingMarkerOnNextFetch(bool commit, IsolationLevel isolationLevel)
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic(Partition.Topic);
        await using var producer = new InMemoryProducer<string, string>(cluster);
        await producer.InitTransactionsAsync();
        await using (var transaction = producer.BeginTransaction())
        {
            await transaction.ProduceAsync(Message(0));
            if (commit)
                await transaction.CommitAsync();
            else
                await transaction.AbortAsync();
        }
        await using var consumer = new InMemoryConsumer<string, string>(cluster,
            new InMemoryConsumerOptions { IsolationLevel = isolationLevel });
        consumer.Assign(Partition);
        consumer.Seek(new TopicPartitionOffset(Partition.Topic, 0, 0));

        if (commit || isolationLevel == IsolationLevel.ReadUncommitted)
        {
            var record = await consumer.ConsumeOneAsync(TimeSpan.Zero);
            await Assert.That(record!.Value.Offset).IsEqualTo(0L);
            await Assert.That(consumer.GetPosition(Partition)).IsEqualTo(1L);
            await Assert.That(consumer.GetCurrentLag(Partition)).IsEqualTo(1L);
        }

        await Assert.That(await consumer.ConsumeOneAsync(TimeSpan.Zero)).IsNull();
        await Assert.That(consumer.GetPosition(Partition)).IsEqualTo(2L);
        await Assert.That(consumer.GetCurrentLag(Partition)).IsEqualTo(0L);
    }

    [Test]
    public async Task OverlappingTransactions_KeepStableBoundaryUntilEarlierTransactionCompletes()
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic(Partition.Topic);
        await using var firstProducer = new InMemoryProducer<string, string>(cluster);
        await using var secondProducer = new InMemoryProducer<string, string>(cluster);
        await firstProducer.InitTransactionsAsync();
        await using var first = firstProducer.BeginTransaction();
        await secondProducer.InitTransactionsAsync();
        await using var second = secondProducer.BeginTransaction();
        await first.ProduceAsync(Message(0));
        await second.ProduceAsync(Message(0));
        await second.CommitAsync();

        await Assert.That(cluster.GetWatermarks(Partition).High).IsEqualTo(3L);
        await Assert.That(cluster.GetWatermarks(Partition, IsolationLevel.ReadCommitted).High).IsEqualTo(0L);
        await Assert.That(cluster.TryRead(Partition, 2L, IsolationLevel.ReadCommitted, false,
            out _, out var blocked, out var nextReadableOffset)).IsFalse();
        await Assert.That(blocked).IsTrue();
        await Assert.That(nextReadableOffset).IsEqualTo(0L);

        await first.AbortAsync();

        await Assert.That(cluster.GetWatermarks(Partition, IsolationLevel.ReadCommitted).High).IsEqualTo(4L);
        await Assert.That(cluster.TryRead(Partition, 2L, IsolationLevel.ReadCommitted, false,
            out _, out blocked, out nextReadableOffset)).IsFalse();
        await Assert.That(blocked).IsFalse();
        await Assert.That(nextReadableOffset).IsEqualTo(4L);
    }

    [Test]
    [Arguments(false, false, false)]
    [Arguments(true, false, false)]
    [Arguments(false, true, false)]
    [Arguments(true, true, false)]
    [Arguments(false, false, true)]
    [Arguments(true, false, true)]
    [Arguments(false, true, true)]
    [Arguments(true, true, true)]
    public async Task ShareConsumer_AcknowledgmentsSkipOnlyInvisibleOffsets(
        bool withFault, bool releaseFirst, bool readCommitted)
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic(Partition.Topic);
        await using var producer = new InMemoryProducer<string, string>(cluster);
        await producer.InitTransactionsAsync();
        for (var i = 0; i < 4; i++)
        {
            await using var transaction = producer.BeginTransaction();
            await transaction.ProduceAsync(Message(0));
            if (i % 2 == 0)
                await transaction.AbortAsync();
            else
                await transaction.CommitAsync();
        }
        if (readCommitted)
        {
            await using var admin = new InMemoryAdminClient(cluster);
            await admin.IncrementalAlterConfigsAsync(new Dictionary<ConfigResource, IReadOnlyList<ConfigAlter>>
            {
                [new ConfigResource { Type = ConfigResourceType.Group, Name = "share-markers" }] =
                    [ConfigAlter.Set("share.isolation.level", "read_committed")]
            });
        }
        if (withFault)
            cluster.FaultPlan.PauseNext(new KafkaFaultScope(KafkaFaultOperation.ShareConsume)).Release();
        await using var consumer = new InMemoryShareConsumer<string, string>(cluster,
            new InMemoryShareConsumerOptions { GroupId = "share-markers" });
        consumer.Subscribe(Partition.Topic);

        var offsets = new List<long>();
        var firstOffset = readCommitted ? 2L : 0L;
        await foreach (var record in consumer.PollAsync())
        {
            offsets.Add(record.Offset);
            consumer.Acknowledge(record,
                releaseFirst && record.Offset == firstOffset ? AcknowledgeType.Release : AcknowledgeType.Accept);
        }
        long[] expected = readCommitted ? [2L, 6L] : [0L, 2L, 4L, 6L];
        await Assert.That(offsets).IsEquivalentTo(expected);
        await consumer.CommitAsync();
        await using var remaining = consumer.PollAsync().GetAsyncEnumerator();
        await Assert.That(await remaining.MoveNextAsync()).IsEqualTo(releaseFirst);
        if (releaseFirst)
            await Assert.That(remaining.Current.Offset).IsEqualTo(firstOffset);
    }

    [Test]
    public async Task DeleteRecords_PreservesTrailingControlOffset()
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic(Partition.Topic);
        await using var producer = new InMemoryProducer<string, string>(cluster);
        await producer.InitTransactionsAsync();
        await using (var transaction = producer.BeginTransaction())
        {
            await transaction.ProduceAsync(Message(0));
            await transaction.CommitAsync();
        }

        cluster.DeleteRecords(new Dictionary<TopicPartition, long> { [Partition] = 1L });

        await Assert.That(cluster.GetWatermarks(Partition).Low).IsEqualTo(1L);
        await Assert.That(cluster.GetWatermarks(Partition).High).IsEqualTo(2L);
        await Assert.That(cluster.ReadRecords(Partition.Topic)).IsEmpty();
        await Assert.That(cluster.GetOffsetForTimestamp(Partition, 0L)).IsEqualTo(-1L);
        await using var ordinaryProducer = new InMemoryProducer<string, string>(cluster);
        var next = await ordinaryProducer.ProduceAsync(Message(0));
        await Assert.That(next.Offset).IsEqualTo(2L);
    }

    [Test]
    public async Task EmptyTransaction_DoesNotReserveOffsets()
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic(Partition.Topic);
        await using var producer = new InMemoryProducer<string, string>(cluster);
        await producer.InitTransactionsAsync();
        await using var transaction = producer.BeginTransaction();
        await transaction.CommitAsync();

        await Assert.That(cluster.GetWatermarks(Partition).High).IsEqualTo(0L);
    }

    private static ProducerMessage<string, string> Message(int partition, string value = "value") => new()
    {
        Topic = Partition.Topic,
        Partition = partition,
        Key = "key",
        Value = value
    };
}
