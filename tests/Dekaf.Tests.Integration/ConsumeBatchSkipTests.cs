using Dekaf.Consumer;
using Dekaf.Producer;

namespace Dekaf.Tests.Integration;

/// <summary>
/// A batch the caller resumes past without enumerating is redelivered (it proved nothing),
/// but it must not block other partitions, new data or a rebalance while it is skipped.
/// </summary>
[Category("Consumer")]
public sealed class ConsumeBatchSkipTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    [Test]
    [Timeout(120_000)]
    [Arguments(false, false)]
    [Arguments(false, true)]
    [Arguments(true, false)]
    [Arguments(true, true)]
    public async Task SkippingOnePartition_StillDeliversOtherPartition(
        bool raw, bool prefetch, CancellationToken cancellationToken)
    {
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 2);
        await using var producer = await CreateProducerAsync();
        await ProduceAsync(producer, topic, partition: 0, start: 0, count: 3);
        await ProduceAsync(producer, topic, partition: 1, start: 0, count: 3);

        await using var consumer = await CreateConsumerAsync(prefetch);
        consumer.Assign(new TopicPartition(topic, 0), new TopicPartition(topic, 1));
        consumer.Seek(new TopicPartitionOffset(topic, 0, 0));
        consumer.Seek(new TopicPartitionOffset(topic, 1, 0));

        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cancellation.CancelAfter(TimeSpan.FromSeconds(60));
        var partition1Offsets = new List<long>();
        var partition0Skips = 0;
        var producedMore = false;
        await foreach (var batch in Batches(consumer, raw, cancellation.Token))
        {
            if (batch.Partition == 0)
            {
                partition0Skips++;
                continue;
            }

            partition1Offsets.AddRange(batch.Offsets());
            if (!producedMore && partition1Offsets.Count >= 3)
            {
                // Data that arrives while partition 0 is still being skipped must flow too.
                producedMore = true;
                await ProduceAsync(producer, topic, partition: 1, start: 3, count: 3);
            }

            if (partition1Offsets.Count >= 6)
                break;
        }

        await Assert.That(partition1Offsets).IsEquivalentTo([0L, 1L, 2L, 3L, 4L, 5L]);
        await Assert.That(partition0Skips).IsGreaterThan(0);
    }

    [Test]
    [Timeout(120_000)]
    [Arguments(false, false)]
    [Arguments(false, true)]
    [Arguments(true, false)]
    [Arguments(true, true)]
    public async Task SkippedRecords_AreRedeliveredExactlyOnceInOrder(
        bool raw, bool prefetch, CancellationToken cancellationToken)
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();
        await using var producer = await CreateProducerAsync();
        await ProduceAsync(producer, topic, partition: 0, start: 0, count: 6);

        await using var consumer = await CreateConsumerAsync(prefetch, maxPollRecords: 2);
        consumer.Assign(new TopicPartition(topic, 0));
        consumer.Seek(new TopicPartitionOffset(topic, 0, 0));

        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cancellation.CancelAfter(TimeSpan.FromSeconds(60));
        var offsets = new List<long>();
        var yields = 0;
        await foreach (var batch in Batches(consumer, raw, cancellation.Token))
        {
            // Skip the first window, process the next one, then skip one more mid-stream.
            yields++;
            if (yields == 1 || yields == 3)
                continue;

            offsets.AddRange(batch.Offsets());
            if (offsets.Count >= 6)
                break;
        }

        await Assert.That(offsets).IsEquivalentTo([0L, 1L, 2L, 3L, 4L, 5L]);
    }

    [Test]
    [Timeout(120_000)]
    [Arguments(false)]
    [Arguments(true)]
    public async Task SkippingEveryBatch_DoesNotBlockRebalance(
        bool prefetch, CancellationToken cancellationToken)
    {
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 2);
        var groupId = $"skip-rebalance-{Guid.NewGuid():N}";
        await using var producer = await CreateProducerAsync();
        await ProduceAsync(producer, topic, partition: 0, start: 0, count: 3);
        await ProduceAsync(producer, topic, partition: 1, start: 0, count: 3);

        await using var skipper = await CreateGroupConsumerAsync(groupId, prefetch);
        skipper.Subscribe(topic);

        using var skipperStop = new CancellationTokenSource();
        var bothSkipped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var skippedPartitions = new HashSet<int>();
        var skipperTask = Task.Run(async () =>
        {
            try
            {
                await foreach (var batch in skipper.ConsumeBatchAsync(skipperStop.Token))
                {
                    // Never enumerate: every batch is skipped.
                    lock (skippedPartitions)
                    {
                        if (skippedPartitions.Add(batch.Partition) && skippedPartitions.Count == 2)
                            bothSkipped.TrySetResult();
                    }
                }
            }
            catch (OperationCanceledException) when (skipperStop.IsCancellationRequested)
            {
            }
        }, CancellationToken.None);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(60));
        await bothSkipped.Task.WaitAsync(timeout.Token);

        // A second member joins. The skipper must release a partition promptly rather than
        // only after being fenced at the rebalance timeout.
        await using var joiner = await CreateGroupConsumerAsync(groupId, prefetch);
        joiner.Subscribe(topic);
        var received = await joiner.ConsumeOneAsync(TimeSpan.FromSeconds(45), timeout.Token);

        await Assert.That(received).IsNotNull();
        await Assert.That(skipper.Assignment.Count).IsEqualTo(1);

        await skipperStop.CancelAsync();
        await skipperTask;
    }

    private sealed class BatchView(int partition, Func<long[]> offsets)
    {
        public int Partition { get; } = partition;

        // Enumerates the batch on demand. A skipped batch is never enumerated.
        public long[] Offsets() => offsets();
    }

    private static async IAsyncEnumerable<BatchView> Batches(
        IKafkaConsumer<string, string> consumer,
        bool raw,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (raw)
        {
            await foreach (var batch in consumer.ConsumeRawBatchAsync(cancellationToken))
                yield return new BatchView(batch.Partition, () => batch.Select(static r => r.Offset).ToArray());
        }
        else
        {
            await foreach (var batch in consumer.ConsumeBatchAsync(cancellationToken))
                yield return new BatchView(batch.Partition, () => batch.Select(static r => r.Offset).ToArray());
        }
    }

    private ValueTask<IKafkaProducer<string, string>> CreateProducerAsync() =>
        Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("batch-skip-producer")
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

    private ValueTask<IKafkaConsumer<string, string>> CreateConsumerAsync(
        bool prefetch,
        int maxPollRecords = 500) =>
        Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("batch-skip-consumer")
            .WithQueuedMinMessages(prefetch ? 100 : 1)
            .WithMaxPollRecords(maxPollRecords)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

    private ValueTask<IKafkaConsumer<string, string>> CreateGroupConsumerAsync(string groupId, bool prefetch) =>
        Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithQueuedMinMessages(prefetch ? 100 : 1)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

    private static async Task ProduceAsync(
        IKafkaProducer<string, string> producer,
        string topic,
        int partition,
        int start,
        int count)
    {
        for (var i = start; i < start + count; i++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Partition = partition,
                Key = $"key-{i}",
                Value = $"value-{i}"
            }, CancellationToken.None);
        }
    }
}
