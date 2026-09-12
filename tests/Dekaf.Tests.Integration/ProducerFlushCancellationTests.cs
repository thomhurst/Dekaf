using Dekaf.Consumer;
using Dekaf.Producer;

namespace Dekaf.Tests.Integration;

[Category("Producer")]
[NotInParallel("ProducerHealthKafkaContainer")]
[ClassDataSource<ProducerHealthKafkaContainer>(Shared = SharedType.PerTestSession)]
public sealed class ProducerFlushCancellationTests(ProducerHealthKafkaContainer kafka)
{
    [Test]
    [Timeout(90_000)]
    public async Task CancelPendingFlush_PreservesOtherWaiterAndDeliversEveryRecord(CancellationToken cancellationToken)
    {
        var topic = await kafka.CreateTestTopicAsync();
        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithAcks(Acks.All)
            .WithLinger(TimeSpan.Zero)
            .WithDeliveryTimeout(TimeSpan.FromSeconds(60))
            .BuildAsync(cancellationToken);
        await producer.ProduceAsync(topic, "warmup", "warmup", cancellationToken);

        using var flushCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        Task? survivingFlush = null;
        await kafka.SetPausedAsync(true);
        try
        {
            // Cached metadata allows append while the paused broker cannot acknowledge delivery.
            for (var index = 0; index < 10; index++)
                await producer.FireAsync(topic, $"key-{index}", $"value-{index}");

            var cancelledFlush = producer.FlushAsync(flushCancellation.Token).AsTask();
            survivingFlush = producer.FlushAsync(cancellationToken).AsTask();
            await Assert.That(cancelledFlush.IsCompleted).IsFalse();
            await Assert.That(survivingFlush.IsCompleted).IsFalse();

            await flushCancellation.CancelAsync();
            await Assert.That(async () => await cancelledFlush.WaitAsync(cancellationToken))
                .Throws<OperationCanceledException>();
            await Assert.That(survivingFlush.IsCompleted).IsFalse();
        }
        finally
        {
            await kafka.SetPausedAsync(false);
            if (survivingFlush is not null)
                await survivingFlush.WaitAsync(cancellationToken);
        }

        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync(cancellationToken);
        consumer.Assign(new TopicPartition(topic, 0));
        var received = new Dictionary<string, string>();
        while (received.Count < 11)
        {
            var record = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(10), cancellationToken);
            await Assert.That(record).IsNotNull();
            received.Add(record!.Value.Key!, record.Value.Value);
        }
        for (var index = 0; index < 10; index++)
            await Assert.That(received[$"key-{index}"]).IsEqualTo($"value-{index}");
        var offsets = await consumer.QueryWatermarkOffsetsAsync(new TopicPartition(topic, 0), cancellationToken);
        await Assert.That(offsets.High).IsEqualTo(11L);
    }
}
