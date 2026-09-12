using Dekaf.Consumer;
using Dekaf.Diagnostics;
using Dekaf.Errors;
using Dekaf.Producer;

namespace Dekaf.Tests.Integration;

[Category("Producer")]
[NotInParallel("ProducerHealthKafkaContainer")]
[ClassDataSource<ProducerHealthKafkaContainer>(Shared = SharedType.PerTestSession)]
public sealed class ProducerPurgeIntegrationTests(ProducerHealthKafkaContainer kafka)
{
    [Test]
    [Arguments(PurgeOptions.Queue)]
    [Arguments(PurgeOptions.InFlight)]
    [Arguments(PurgeOptions.All)]
    [Timeout(90_000)]
    public async Task Purge_CompletesOnceAndProducerRemainsUsable(PurgeOptions options, CancellationToken cancellationToken)
    {
        var topic = await kafka.CreateTestTopicAsync();
        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers).WithAcks(Acks.All)
            .WithIdempotence(true).WithConnectionsPerBroker(1).WithoutAdaptiveConnections().WithLinger(TimeSpan.FromSeconds(30))
            .WithBatchSize(1024 * 1024).WithDeliveryTimeout(TimeSpan.FromSeconds(60))
            .BuildAsync(cancellationToken);
        var warmup = producer.ProduceAsync(topic, "warmup", "warmup", cancellationToken).AsTask();
        await producer.FlushAsync(cancellationToken);
        await warmup;
        var status = (IKafkaClientStatusProvider)producer;
        var callbackCount = 0;
        var callback = new TaskCompletionSource<Exception?>(TaskCreationOptions.RunContinuationsAsynchronously);
        Task? flush = null;
        await kafka.SetPausedAsync(true);
        try
        {
            await producer.FireAsync(new ProducerMessage<string, string>
            {
                Topic = topic, Key = "purged", Value = "purged"
            }, (_, exception) =>
            {
                Interlocked.Increment(ref callbackCount);
                callback.TrySetResult(exception);
            });
            if (options != PurgeOptions.Queue)
            {
                flush = producer.FlushAsync(cancellationToken).AsTask();
                await TestWait.WaitForConditionAsync(
                    () => Task.FromResult(status.GetStatus()),
                    snapshot => snapshot.Producer!.Value.InFlightBatchCount > 0
                        && snapshot.Producer.Value.QueuedBatchCount == 0
                        // BrokerSender releases the accumulator reservation after writing the frame.
                        && snapshot.Producer.Value.BufferedBytes == 0,
                    maxRetries: 10, initialDelayMs: 100,
                    description: "batch leaves the queue and its frame is written while the broker is paused",
                    formatObserved: snapshot => $"{snapshot.Producer}");
            }
            else
            {
                await Assert.That(status.GetStatus().Producer!.Value.UnsealedBatchCount).IsGreaterThan(0);
                await Assert.That(status.GetStatus().Producer!.Value.InFlightBatchCount).IsEqualTo(0);
            }

            await producer.PurgeAsync(options, cancellationToken);
            var failure = await callback.Task.WaitAsync(cancellationToken);
            await Assert.That(failure).IsTypeOf<ProduceException>();
            await Assert.That(((ProduceException)failure!).Kind).IsEqualTo(ProduceErrorKind.Purged);
            await Assert.That(Volatile.Read(ref callbackCount)).IsEqualTo(1);
        }
        finally
        {
            await kafka.SetPausedAsync(false);
            if (flush is not null)
                await flush.WaitAsync(cancellationToken);
        }

        // A later acknowledgement for the purged batch must not complete its callback again
        // or break the next idempotent sequence. Flush also waits for buffer reclamation.
        var after = producer.ProduceAsync(topic, "after", "after", cancellationToken).AsTask();
        await producer.FlushAsync(cancellationToken);
        await after;
        await TestWait.WaitForConditionAsync(() => status.GetStatus().Producer!.Value.BufferedBytes == 0,
            TimeSpan.FromSeconds(10), description: "purged producer releases its reservations");
        await Assert.That(Volatile.Read(ref callbackCount)).IsEqualTo(1);

        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers).WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync(cancellationToken);
        consumer.Assign(new TopicPartition(topic, 0));
        var values = new List<string>();
        while (!values.Contains("after"))
        {
            var record = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(10), cancellationToken);
            await Assert.That(record).IsNotNull();
            values.Add(record!.Value.Value);
        }
        await Assert.That(values.Count(value => value == "after")).IsEqualTo(1);
        if (options == PurgeOptions.Queue)
            await Assert.That(values).IsEquivalentTo(["warmup", "after"]);
    }
}

[Category("Transaction")]
public sealed class TransactionalPurgeIntegrationTests(KafkaTestContainer kafka) : TransactionalKafkaIntegrationTest(kafka)
{
    [Test]
    [Arguments(PurgeOptions.Queue)]
    [Arguments(PurgeOptions.InFlight)]
    [Arguments(PurgeOptions.All)]
    public async Task ActiveTransaction_RejectsPurgeAndCanStillCommit(PurgeOptions options, CancellationToken cancellationToken)
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();
        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithTransactionalId($"purge-transaction-{Guid.NewGuid():N}")
            .BuildAsync(cancellationToken);
        await producer.InitTransactionsAsync(cancellationToken);
        await using var transaction = producer.BeginTransaction();
        await transaction.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic, Key = "key", Value = "committed"
        }, cancellationToken);
        await Assert.That(async () => await producer.PurgeAsync(options, cancellationToken))
            .Throws<InvalidOperationException>();
        await transaction.CommitAsync(cancellationToken);
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers).WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithIsolationLevel(Dekaf.Protocol.Messages.IsolationLevel.ReadCommitted).BuildAsync(cancellationToken);
        consumer.Assign(new TopicPartition(topic, 0));
        var record = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(10), cancellationToken);
        await Assert.That(record).IsNotNull();
        await Assert.That(record!.Value.Value).IsEqualTo("committed");
    }
}
