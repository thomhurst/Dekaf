using Dekaf.Consumer;
using Dekaf.Producer;
using Dekaf.Protocol.Messages;

namespace Dekaf.Tests.Integration;

[Category("Transaction")]
public sealed class TransactionCrashRecoveryTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    [Test]
    public async Task ForceTerminatedProducer_ReplacementAbortsDanglingTransaction()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var transactionId = $"crash-recovery-{Guid.NewGuid():N}";
        var crashedValue = $"crashed-{Guid.NewGuid():N}";
        string[] committedValues =
        [
            $"committed-{Guid.NewGuid():N}",
            $"committed-{Guid.NewGuid():N}"
        ];

        await using (var crashClient = TransactionalCrashClientProcess.Start(
                         KafkaContainer.BootstrapServers,
                         topic,
                         transactionId,
                         "crashed-key",
                         crashedValue))
        {
            await crashClient.WaitUntilReadyAsync(TimeSpan.FromSeconds(45));
            await crashClient.TerminateAsync();
        }

        await using var replacement = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithTransactionalId(transactionId)
            .WithAcks(Acks.All)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        await replacement.InitTransactionsAsync();

        await using (var transaction = replacement.BeginTransaction())
        {
            for (var index = 0; index < committedValues.Length; index++)
            {
                await transaction.ProduceAsync(new ProducerMessage<string, string>
                {
                    Topic = topic,
                    Key = $"committed-key-{index}",
                    Value = committedValues[index]
                }, CancellationToken.None);
            }

            await transaction.CommitAsync();
        }

        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId($"crash-recovery-verify-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithIsolationLevel(IsolationLevel.ReadCommitted)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        consumer.Subscribe(topic);

        var messages = await ConsumeMessagesAsync(consumer, committedValues.Length);
        var unexpectedMessage = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(5));
        var actualValues = messages.Select(message => message.Value).ToArray();

        await Assert.That(actualValues).Count().IsEqualTo(committedValues.Length);
        await Assert.That(actualValues[0]).IsEqualTo(committedValues[0]);
        await Assert.That(actualValues[1]).IsEqualTo(committedValues[1]);
        await Assert.That(actualValues).DoesNotContain(crashedValue);
        await Assert.That(unexpectedMessage).IsNull();
    }
}
