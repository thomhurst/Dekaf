using Dekaf.Consumer;
using Dekaf.Producer;
using Dekaf.Protocol.Messages;
using Dekaf.Testing;

namespace Dekaf.Tests.Unit.Testing;

public sealed class InMemoryConsumerLagTests
{
    private static readonly TopicPartition Partition = new("events", 0);

    [Test]
    public async Task CurrentLag_TracksClusterEndAndAssignment()
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic(Partition.Topic);
        var producer = new InMemoryProducer<string, string>(cluster);
        for (var i = 0; i < 3; i++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = Partition.Topic,
                Partition = Partition.Partition,
                Key = $"key-{i}",
                Value = $"value-{i}"
            });
        }

        await using var consumer = new InMemoryConsumer<string, string>(cluster);
        await Assert.That(consumer.GetCurrentLag(Partition)).IsNull();

        consumer.IncrementalAssign([
            new TopicPartitionOffset(Partition.Topic, Partition.Partition, 1)
        ]);

        await Assert.That(consumer.GetCurrentLag(Partition)).IsEqualTo(2);
        await Assert.That(await consumer.QueryCurrentLagAsync(Partition)).IsEqualTo(2);

        consumer.IncrementalUnassign([Partition]);
        await Assert.That(consumer.GetCurrentLag(Partition)).IsNull();
    }

    [Test]
    public async Task QueryCurrentLagAsync_HonorsCancellation()
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic(Partition.Topic);
        await using var consumer = new InMemoryConsumer<string, string>(cluster);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.That(() => { _ = consumer.QueryCurrentLagAsync(Partition, cancellation.Token); })
            .Throws<OperationCanceledException>();
    }

    [Test]
    public async Task CurrentLag_ReadCommittedStopsAtOngoingTransactionBoundary()
    {
        var cluster = new InMemoryKafkaCluster();
        cluster.CreateTopic(Partition.Topic);
        await using var transactionalProducer = new InMemoryProducer<string, string>(cluster);
        await using var transaction = transactionalProducer.BeginTransaction();
        _ = await transaction.ProduceAsync(Partition.Topic, "key", "pending");
        await using var ordinaryProducer = new InMemoryProducer<string, string>(cluster);
        _ = await ordinaryProducer.ProduceAsync(Partition.Topic, "key", "following");
        await using var readCommitted = new InMemoryConsumer<string, string>(
            cluster,
            new InMemoryConsumerOptions { IsolationLevel = IsolationLevel.ReadCommitted });
        await using var readUncommitted = new InMemoryConsumer<string, string>(
            cluster,
            new InMemoryConsumerOptions { IsolationLevel = IsolationLevel.ReadUncommitted });
        readCommitted.IncrementalAssign([
            new TopicPartitionOffset(Partition.Topic, Partition.Partition, 0)
        ]);
        readUncommitted.IncrementalAssign([
            new TopicPartitionOffset(Partition.Topic, Partition.Partition, 0)
        ]);

        await Assert.That(readCommitted.GetCurrentLag(Partition)).IsEqualTo(0);
        await Assert.That(await readCommitted.QueryCurrentLagAsync(Partition)).IsEqualTo(0);
        await Assert.That(readUncommitted.GetCurrentLag(Partition)).IsEqualTo(2);

        await transaction.CommitAsync();

        await Assert.That(readCommitted.GetCurrentLag(Partition)).IsEqualTo(2);
    }
}
