using Dekaf.Errors;
using Dekaf.Producer;
using Dekaf.Protocol.Messages;

namespace Dekaf.Tests.Integration;

/// <summary>
/// Integration tests for KIP-1050 standardized transaction error handling: verifying that a
/// fenced producer surfaces a <see cref="FatalTransactionException"/> and becomes unusable.
/// </summary>
/// <remarks>
/// The abortable path is covered deterministically at the unit level (state-machine transitions
/// in <c>TransactionTests</c> and classification in <c>TransactionErrorClassifierTests</c>);
/// there is no reliable, non-flaky way to force a broker into returning an abortable error here.
/// </remarks>
[Category("Transaction")]
public class TransactionErrorHandlingTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    [Test]
    public async Task Transaction_FencedByNewerProducer_ThrowsFatalAndProducerIsUnusable()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var txnId = $"txn-fence-{Guid.NewGuid():N}";

        await using var producer1 = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithTransactionalId(txnId)
            .WithAcks(Acks.All)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        await producer1.InitTransactionsAsync();

        await using var txn1 = producer1.BeginTransaction();

        // Produce and await delivery so AddPartitionsToTxn + Produce complete with the current
        // epoch. This makes the fence below observable only on EndTxn — a deterministic path.
        await txn1.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "fence-key",
            Value = "fence-value"
        }, CancellationToken.None);

        // A second producer with the same transactional id bumps the epoch, fencing producer1.
        await using var producer2 = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithTransactionalId(txnId)
            .WithAcks(Acks.All)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        await producer2.InitTransactionsAsync();

        // producer1's commit (EndTxn) is now rejected with a fatal, fencing error.
        var commit = () => txn1.CommitAsync().AsTask();
        await Assert.That(commit).Throws<FatalTransactionException>();

        // The producer is in a fatal state and cannot start further transactions.
        var begin = () => producer1.BeginTransaction();
        await Assert.That(begin).Throws<InvalidOperationException>();
    }
}
