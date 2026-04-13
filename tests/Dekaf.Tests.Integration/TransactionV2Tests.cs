using Dekaf.Producer;
using Dekaf.Protocol.Messages;

namespace Dekaf.Tests.Integration;

/// <summary>
/// Integration tests for KIP-890 Transactions V2 behavior.
/// TV2 features (EndTxn epoch bump, skip post-abort InitProducerId)
/// are automatically active when connected to Kafka 4.0+ brokers that support transaction.version >= 2.
/// </summary>
[Category("Transaction")]
public class TransactionV2Tests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    [Test]
    public async Task Transaction_Commit_ThenNewTransaction_Succeeds()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var txnId = $"txn-v2-commit-{Guid.NewGuid():N}";

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithTransactionalId(txnId)
            .WithAcks(Acks.All)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        await producer.InitTransactionsAsync();

        // First transaction: produce and commit
        await using (var txn = producer.BeginTransaction())
        {
            await txn.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic, Key = "k1", Value = "v1"
            }, CancellationToken.None);

            await txn.CommitAsync();
        }

        // Second transaction: should succeed with bumped epoch
        await using (var txn = producer.BeginTransaction())
        {
            await txn.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic, Key = "k2", Value = "v2"
            }, CancellationToken.None);

            await txn.CommitAsync();
        }

        // Verify both messages are visible
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId($"txn-v2-consumer-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(Consumer.AutoOffsetReset.Earliest)
            .WithIsolationLevel(IsolationLevel.ReadCommitted)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        consumer.Subscribe(topic);

        var messages = await ConsumeMessagesAsync(consumer, 2);

        await Assert.That(messages).Count().IsEqualTo(2);
    }

    [Test]
    public async Task Transaction_Abort_ThenNewTransaction_Succeeds()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var txnId = $"txn-v2-abort-{Guid.NewGuid():N}";

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithTransactionalId(txnId)
            .WithAcks(Acks.All)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        await producer.InitTransactionsAsync();

        // First transaction: produce and abort
        await using (var txn = producer.BeginTransaction())
        {
            await txn.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic, Key = "aborted-key", Value = "aborted-value"
            }, CancellationToken.None);

            await txn.AbortAsync();
        }

        // Second transaction after abort: should succeed
        // In TV2, this does NOT call InitProducerId -- the epoch bump from EndTxn is sufficient
        await using (var txn = producer.BeginTransaction())
        {
            await txn.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic, Key = "committed-key", Value = "committed-value"
            }, CancellationToken.None);

            await txn.CommitAsync();
        }

        // Verify only committed message visible
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId($"txn-v2-abort-consumer-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(Consumer.AutoOffsetReset.Earliest)
            .WithIsolationLevel(IsolationLevel.ReadCommitted)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        consumer.Subscribe(topic);

        var consumed = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), CancellationToken.None);

        await Assert.That(consumed).IsNotNull();
        await Assert.That(consumed!.Value.Value).IsEqualTo("committed-value");
    }

    [Test]
    public async Task Transaction_MultipleAborts_ThenCommit_Succeeds()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var txnId = $"txn-v2-multi-abort-{Guid.NewGuid():N}";

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithTransactionalId(txnId)
            .WithAcks(Acks.All)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        await producer.InitTransactionsAsync();

        // Three aborted transactions in a row
        for (var i = 0; i < 3; i++)
        {
            await using var txn = producer.BeginTransaction();
            await txn.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic, Key = $"aborted-{i}", Value = $"aborted-{i}"
            }, CancellationToken.None);
            await txn.AbortAsync();
        }

        // Fourth transaction: commit
        await using (var txn = producer.BeginTransaction())
        {
            await txn.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic, Key = "final", Value = "final-value"
            }, CancellationToken.None);
            await txn.CommitAsync();
        }

        // Verify only the committed message is visible
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId($"txn-v2-multi-abort-consumer-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(Consumer.AutoOffsetReset.Earliest)
            .WithIsolationLevel(IsolationLevel.ReadCommitted)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        consumer.Subscribe(topic);

        var consumed = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), CancellationToken.None);

        await Assert.That(consumed).IsNotNull();
        await Assert.That(consumed!.Value.Value).IsEqualTo("final-value");
    }
}
