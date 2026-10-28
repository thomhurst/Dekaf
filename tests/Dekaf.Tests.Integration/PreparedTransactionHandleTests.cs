using Dekaf.Errors;
using Dekaf.Producer;
using Dekaf.Protocol.Messages;

namespace Dekaf.Tests.Integration;

/// <summary>
/// Integration tests for KIP-939 prepared transactions completed through the producer.
/// They need a broker that finalizes <c>transaction.version</c> 3; on older brokers
/// <c>InitTransactionsAsync</c> refuses two-phase commit and the tests skip.
/// </summary>
[Category("Transaction")]
public class PreparedTransactionHandleTests(KafkaTestContainer kafka) : TransactionalKafkaIntegrationTest(kafka)
{
    [Test]
    public async Task CompletedThroughProducer_StaleHandleDisposal_LeavesTheNextTransactionOpen()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithTransactionalId($"txn-2pc-stale-handle-{Guid.NewGuid():N}")
            .WithTwoPhaseCommit()
            .WithAcks(Acks.All)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        await InitTwoPhaseCommitOrSkipAsync(producer);

        var first = producer.BeginTransaction();
        await first.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "a",
            Value = "first"
        }, CancellationToken.None);
        var prepared = await first.PrepareAsync();
        await producer.CompletePreparedTransactionAsync(prepared, committed: true);

        await using (var second = producer.BeginTransaction())
        {
            await second.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = "b",
                Value = "second"
            }, CancellationToken.None);

            // Before the fix, disposing the completed handle aborted the second transaction.
            await first.DisposeAsync();
            await Assert.That(async () => await first.AbortAsync()).Throws<InvalidOperationException>();

            await second.CommitAsync();
        }

        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId($"txn-2pc-stale-handle-consumer-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(Consumer.AutoOffsetReset.Earliest)
            .WithIsolationLevel(IsolationLevel.ReadCommitted)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        consumer.Subscribe(topic);

        var messages = await ConsumeMessagesAsync(consumer, 2);

        await Assert.That(messages).Count().IsEqualTo(2);
        await Assert.That(messages[0].Value).IsEqualTo("first");
        await Assert.That(messages[1].Value).IsEqualTo("second");
    }

    private static async Task InitTwoPhaseCommitOrSkipAsync(IKafkaProducer<string, string> producer)
    {
        try
        {
            await producer.InitTransactionsAsync();
        }
        catch (BrokerVersionException ex)
        {
            Skip.Test($"KIP-939 two-phase commit needs transaction.version 3, which Kafka {KafkaContainerDefault.ImageTag} does not finalize (#3894): {ex.Message}");
        }
    }
}
