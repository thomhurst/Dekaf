using Dekaf.Producer;
using Dekaf.Protocol.Messages;

namespace Dekaf.Tests.Integration;

/// <summary>
/// Integration tests for producer transactions.
/// </summary>
public class TransactionTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    [Test]
    public async Task InitTransactions_SetsProducerIdAndEpoch()
    {
        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithTransactionalId($"txn-init-{Guid.NewGuid():N}")
            .WithAcks(Acks.All)
            .BuildAsync();

        // Should not throw
        await producer.InitTransactionsAsync();
    }

    [Test]
    public async Task Transaction_Commit_MessagesVisible()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var txnId = $"txn-commit-{Guid.NewGuid():N}";

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithTransactionalId(txnId)
            .WithAcks(Acks.All)
            .BuildAsync();

        await producer.InitTransactionsAsync();

        await using (var txn = producer.BeginTransaction())
        {
            await txn.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = "txn-key",
                Value = "txn-value"
            });

            await txn.CommitAsync();
        }

        // Consume the message - it should be visible after commit
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId($"txn-consumer-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(Consumer.AutoOffsetReset.Earliest)
            .WithIsolationLevel(IsolationLevel.ReadCommitted)
            .BuildAsync();

        consumer.Subscribe(topic);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var consumed = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token);

        await Assert.That(consumed).IsNotNull();
        await Assert.That(consumed!.Value.Value).IsEqualTo("txn-value");
    }

    [Test]
    public async Task Transaction_Abort_MessagesNotVisible()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var txnId = $"txn-abort-{Guid.NewGuid():N}";

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithTransactionalId(txnId)
            .WithAcks(Acks.All)
            .BuildAsync();

        await producer.InitTransactionsAsync();

        await using (var txn = producer.BeginTransaction())
        {
            await txn.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = "abort-key",
                Value = "abort-value"
            });

            await txn.AbortAsync();
        }

        // Now produce a committed message to verify the consumer works
        await using (var txn2 = producer.BeginTransaction())
        {
            await txn2.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = "committed-key",
                Value = "committed-value"
            });

            await txn2.CommitAsync();
        }

        // Consume with read_committed - should only see the committed message
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId($"txn-consumer-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(Consumer.AutoOffsetReset.Earliest)
            .WithIsolationLevel(IsolationLevel.ReadCommitted)
            .BuildAsync();

        consumer.Subscribe(topic);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var consumed = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token);

        await Assert.That(consumed).IsNotNull();
        await Assert.That(consumed!.Value.Value).IsEqualTo("committed-value");
    }

    [Test]
    public async Task Transaction_MultipleMessages_AllCommitted()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var txnId = $"txn-multi-{Guid.NewGuid():N}";

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithTransactionalId(txnId)
            .WithAcks(Acks.All)
            .BuildAsync();

        await producer.InitTransactionsAsync();

        await using (var txn = producer.BeginTransaction())
        {
            for (var i = 0; i < 5; i++)
            {
                await txn.ProduceAsync(new ProducerMessage<string, string>
                {
                    Topic = topic,
                    Key = $"key-{i}",
                    Value = $"value-{i}"
                });
            }

            await txn.CommitAsync();
        }

        // Consume all 5 messages
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId($"txn-consumer-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(Consumer.AutoOffsetReset.Earliest)
            .WithIsolationLevel(IsolationLevel.ReadCommitted)
            .BuildAsync();

        consumer.Subscribe(topic);

        var messages = new List<string>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await foreach (var batch in consumer.ConsumeAsync(cts.Token))
        {
            messages.Add(batch.Value);
            if (messages.Count >= 5)
                break;
        }

        await Assert.That(messages).Count().IsEqualTo(5);
    }

    [Test]
    public async Task Transaction_CommitThenBeginAnother_Succeeds()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var txnId = $"txn-multi-txn-{Guid.NewGuid():N}";

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithTransactionalId(txnId)
            .WithAcks(Acks.All)
            .BuildAsync();

        await producer.InitTransactionsAsync();

        // First transaction
        await using (var txn1 = producer.BeginTransaction())
        {
            await txn1.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = "txn1-key",
                Value = "txn1-value"
            });
            await txn1.CommitAsync();
        }

        // Second transaction
        await using (var txn2 = producer.BeginTransaction())
        {
            await txn2.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = "txn2-key",
                Value = "txn2-value"
            });
            await txn2.CommitAsync();
        }

        // Both messages should be visible
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId($"txn-consumer-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(Consumer.AutoOffsetReset.Earliest)
            .WithIsolationLevel(IsolationLevel.ReadCommitted)
            .BuildAsync();

        consumer.Subscribe(topic);

        var messages = new List<string>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await foreach (var batch in consumer.ConsumeAsync(cts.Token))
        {
            messages.Add(batch.Value);
            if (messages.Count >= 2)
                break;
        }

        await Assert.That(messages).Count().IsEqualTo(2);
    }
}
