using Dekaf.Consumer;
using Dekaf.Producer;

namespace Dekaf.Tests.Integration;

[Category("Consumer")]
public sealed class BufferedBatchDeliveryTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    [Timeout(120_000)]
    public async Task HandlerFailure_CloseCommitsOnlySuccessfulBatches_AndRestartsFailedBatch(
        bool completeFirstBatch, CancellationToken cancellationToken)
    {
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 2);
        var group = $"buffered-batch-{Guid.NewGuid():N}";
        var perPartition = completeFirstBatch ? 4 : 2;
        await using (var producer = await Kafka.CreateProducer<string, string>()
                         .WithBootstrapServers(KafkaContainer.BootstrapServers).BuildAsync(cancellationToken))
        {
            for (var partition = 0; partition < 2; partition++)
                for (var i = 0; i < perPartition; i++)
                    await producer.ProduceAsync(new ProducerMessage<string, string>
                    {
                        Topic = topic, Partition = partition, Key = "key", Value = $"{partition}-{i}"
                    }, cancellationToken);
        }

        var proven = new Dictionary<TopicPartition, long>();
        var failed = new HashSet<(int Partition, long Offset)>();
        await using (var consumer = await CreateConsumerAsync(group, cancellationToken))
        {
            consumer.Subscribe(topic);
            var batchIndex = 0;
            await Assert.That(async () =>
            {
                await foreach (var batch in consumer.ConsumeAsync(cancellationToken).Batch(4, cancellationToken))
                {
                    if (completeFirstBatch && batchIndex++ == 0)
                    {
                        foreach (var record in batch)
                            proven[new TopicPartition(topic, record.Partition)] = record.Offset + 1;
                        continue;
                    }
                    foreach (var record in batch)
                        failed.Add((record.Partition, record.Offset));
                    throw new InvalidOperationException("batch handler failed");
                }
            }).Throws<InvalidOperationException>();
            await consumer.CloseAsync(cancellationToken);
        }

        await using var admin = KafkaContainer.CreateAdminClient();
        var committed = await admin.ListConsumerGroupOffsetsAsync(group, cancellationToken);
        for (var partition = 0; partition < 2; partition++)
        {
            var key = new TopicPartition(topic, partition);
            if (proven.TryGetValue(key, out var offset))
                await Assert.That(committed[key]).IsEqualTo(offset);
            else
                await Assert.That(committed.ContainsKey(key)).IsFalse();
        }

        var replayed = new HashSet<(int Partition, long Offset)>();
        await using var restart = await CreateConsumerAsync(group, cancellationToken);
        restart.Subscribe(topic);
        await foreach (var record in restart.ConsumeAsync(cancellationToken).Take(4, cancellationToken))
            replayed.Add((record.Partition, record.Offset));
        await Assert.That(replayed).IsEquivalentTo(failed);
    }

    [Test]
    [Timeout(120_000)]
    public async Task RejectAllFilter_CommitsWithoutYieldingApplicationBatch(CancellationToken cancellationToken)
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var group = $"buffered-batch-filter-{Guid.NewGuid():N}";
        await using (var producer = await Kafka.CreateProducer<string, string>()
                         .WithBootstrapServers(KafkaContainer.BootstrapServers).BuildAsync(cancellationToken))
        {
            for (var i = 0; i < 3; i++)
                await producer.ProduceAsync(topic, "key", "value", cancellationToken);
        }

        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(group)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithQueuedMinMessages(1)
            .WithAutoCommitInterval(TimeSpan.FromMilliseconds(100))
            .WithRecordFilter(new RejectAllRecords())
            .BuildAsync(cancellationToken);
        consumer.Subscribe(topic);

        using var stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var batches = DrainBatchesAsync();
        try
        {
            var partition = new TopicPartition(topic, 0);
            while (await consumer.GetCommittedOffsetAsync(partition, cancellationToken) != 3)
                await Task.Delay(50, cancellationToken);
        }
        finally
        {
            await stop.CancelAsync();
            try { await batches; }
            catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
        }

        async Task DrainBatchesAsync()
        {
            await foreach (var _ in consumer.ConsumeAsync(stop.Token).Batch(8, stop.Token))
                throw new InvalidOperationException("An all-rejecting filter must not yield a batch.");
        }
    }

    private sealed class RejectAllRecords : IConsumerRecordFilter
    {
        public bool ShouldDeserialize(in ConsumerRecordFilterContext record) => false;
    }

    private ValueTask<IKafkaConsumer<string, string>> CreateConsumerAsync(string group, CancellationToken cancellationToken) =>
        Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(group)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithQueuedMinMessages(1)
            .WithAutoCommitInterval(TimeSpan.FromMilliseconds(100))
            .BuildAsync(cancellationToken);
}
