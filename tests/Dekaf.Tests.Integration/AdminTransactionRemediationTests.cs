using Dekaf.Admin;
using Dekaf.Consumer;
using Dekaf.Errors;
using Dekaf.Producer;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;

namespace Dekaf.Tests.Integration;

[Category("Transaction")]
public sealed class AdminTransactionRemediationTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    [Test]
    public async Task FenceProducersAsync_FencesActiveTransactionalProducer()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var txnId = $"admin-fence-{Guid.NewGuid():N}";

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithTransactionalId(txnId)
            .WithAcks(Acks.All)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        await producer.InitTransactionsAsync();
        await using var transaction = producer.BeginTransaction();

        await transaction.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "fenced-key",
            Value = "fenced-value"
        }, CancellationToken.None);

        await using var admin = KafkaContainer.CreateAdminClient();
        var results = await admin.FenceProducersAsync([txnId]);

        await Assert.That(results[txnId].ErrorCode).IsEqualTo(ErrorCode.None);
        await Assert.That(results[txnId].ProducerId).IsGreaterThanOrEqualTo(0);

        var commit = () => transaction.CommitAsync().AsTask();
        await Assert.That(commit).Throws<FatalTransactionException>();
    }

    [Test]
    public async Task AbortTransactionAsync_WritesAbortMarkerForOpenTransaction()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var topicPartition = new TopicPartition(topic, 0);
        var hungTxnId = $"admin-abort-hung-{Guid.NewGuid():N}";
        var laterTxnId = $"admin-abort-later-{Guid.NewGuid():N}";

        await using var hungProducer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithTransactionalId(hungTxnId)
            .WithAcks(Acks.All)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        await hungProducer.InitTransactionsAsync();
        var hungTransaction = hungProducer.BeginTransaction();

        try
        {
            await hungTransaction.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Partition = 0,
                Key = "aborted-key",
                Value = "aborted-value"
            }, CancellationToken.None);

            await using var admin = KafkaContainer.CreateAdminClient();
            var producerState = await admin.DescribeProducersAsync([topicPartition]);
            var activeProducer = producerState[topicPartition].ActiveProducers
                .Single(producer => producer.CurrentTransactionStartOffset >= 0);

            var abortResult = await admin.AbortTransactionAsync(new AbortTransactionSpec
            {
                TopicPartition = topicPartition,
                ProducerId = activeProducer.ProducerId,
                ProducerEpoch = checked((short)activeProducer.ProducerEpoch),
                CoordinatorEpoch = activeProducer.CoordinatorEpoch
            });

            await Assert.That(abortResult.ErrorCode).IsEqualTo(ErrorCode.None);

            await using var laterProducer = await Kafka.CreateProducer<string, string>()
                .WithBootstrapServers(KafkaContainer.BootstrapServers)
                .WithTransactionalId(laterTxnId)
                .WithAcks(Acks.All)
                .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
                .BuildAsync();

            await laterProducer.InitTransactionsAsync();
            await using (var laterTransaction = laterProducer.BeginTransaction())
            {
                await laterTransaction.ProduceAsync(new ProducerMessage<string, string>
                {
                    Topic = topic,
                    Partition = 0,
                    Key = "later-key",
                    Value = "later-value"
                }, CancellationToken.None);
                await laterTransaction.CommitAsync();
            }

            await using var consumer = await Kafka.CreateConsumer<string, string>()
                .WithBootstrapServers(KafkaContainer.BootstrapServers)
                .WithGroupId($"admin-abort-verify-{Guid.NewGuid():N}")
                .WithAutoOffsetReset(AutoOffsetReset.Earliest)
                .WithIsolationLevel(IsolationLevel.ReadCommitted)
                .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
                .BuildAsync();

            consumer.Subscribe(topic);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token);

            await Assert.That(result).IsNotNull();
            await Assert.That(result!.Value.Key).IsEqualTo("later-key");
            await Assert.That(result.Value.Value).IsEqualTo("later-value");
        }
        finally
        {
            try
            {
                await hungTransaction.DisposeAsync();
            }
            catch (TransactionException)
            {
                // Best-effort cleanup after the admin abort has already ended the broker transaction.
            }
        }
    }
}
