using System.Collections.Concurrent;
using Dekaf.Consumer;
using Dekaf.Producer;

namespace Dekaf.Tests.Integration;

[Category("ConsumerGroup")]
public sealed class PartitionedProcessingIntegrationTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    [Test]
    public async Task RunPartitionedAsync_MultiPartition_ProcessesOrderedLanesConcurrentlyAndCommits()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 2);
        var groupId = $"partitioned-runtime-{Guid.NewGuid():N}";

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        for (var offset = 0; offset < 3; offset++)
        {
            for (var partition = 0; partition < 2; partition++)
            {
                await producer.ProduceAsync(new ProducerMessage<string, string>
                {
                    Topic = topic,
                    Partition = partition,
                    Key = $"key-{partition}-{offset}",
                    Value = $"value-{partition}-{offset}"
                }, CancellationToken.None);
            }
        }

        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithOffsetCommitMode(OffsetCommitMode.Manual)
            .WithQueuedMinMessages(1)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        consumer.Subscribe(topic);

        var processed = new ConcurrentDictionary<int, List<long>>();
        var firstPartitionStarted = NewCompletionSource();
        var releaseFirstPartition = NewCompletionSource();
        var secondPartitionProcessedWhileFirstBlocked = NewCompletionSource();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var runTask = consumer.RunPartitionedAsync(
            async (context, cancellationToken) =>
            {
                await foreach (var message in context.Messages.WithCancellation(cancellationToken))
                {
                    if (message.Partition == 0 && message.Offset == 0)
                    {
                        firstPartitionStarted.SetResult();
                        await releaseFirstPartition.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
                    }

                    var offsets = processed.GetOrAdd(message.Partition, static _ => []);
                    lock (offsets)
                        offsets.Add(message.Offset);

                    context.MarkProcessed(message);

                    if (message.Partition == 1 && !releaseFirstPartition.Task.IsCompleted)
                        secondPartitionProcessedWhileFirstBlocked.TrySetResult();

                    if (processed.Values.Sum(static values =>
                        {
                            lock (values)
                                return values.Count;
                        }) >= 6)
                    {
                        await cts.CancelAsync().ConfigureAwait(false);
                    }
                }
            },
            new PartitionedProcessingOptions
            {
                MaxBufferedRecordsPerPartition = 4,
                CommitPolicy = PartitionCommitPolicy.CommitCompletedOnRevoke,
                StopPolicy = PartitionStopPolicy.Drain
            },
            cts.Token).AsTask();

        await firstPartitionStarted.Task.WaitAsync(cts.Token).ConfigureAwait(false);
        await secondPartitionProcessedWhileFirstBlocked.Task.WaitAsync(cts.Token).ConfigureAwait(false);

        releaseFirstPartition.SetResult();

        try
        {
            await runTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
        }

        await Assert.That(Snapshot(processed, 0)).IsEquivalentTo([0L, 1L, 2L]);
        await Assert.That(Snapshot(processed, 1)).IsEquivalentTo([0L, 1L, 2L]);

        await Assert.That(await consumer.GetCommittedOffsetAsync(new TopicPartition(topic, 0), CancellationToken.None))
            .IsEqualTo(3);
        await Assert.That(await consumer.GetCommittedOffsetAsync(new TopicPartition(topic, 1), CancellationToken.None))
            .IsEqualTo(3);
    }

    [Test]
    public async Task RunPartitionedAsync_IgnoreRedeliversRecordsQueuedBehindTheFailedRecord()
    {
        const int recordCount = 20;
        const long failingOffset = 2;
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 1);
        var groupId = $"partitioned-ignore-{Guid.NewGuid():N}";

        await using (var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync())
        {
            for (var offset = 0; offset < recordCount; offset++)
            {
                await producer.ProduceAsync(new ProducerMessage<string, string>
                {
                    Topic = topic,
                    Partition = 0,
                    Key = $"key-{offset}",
                    Value = $"value-{offset}"
                }, CancellationToken.None);
            }
        }

        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithOffsetCommitMode(OffsetCommitMode.Manual)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        consumer.Subscribe(topic);

        var attempts = 0;
        var processed = new ConcurrentQueue<long>();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var runTask = consumer.RunPartitionedAsync(
            async (context, cancellationToken) =>
            {
                var firstAttempt = Interlocked.Increment(ref attempts) == 1;
                await foreach (var message in context.Messages.WithCancellation(cancellationToken))
                {
                    // The fetch delivers every record at once, so the failure leaves the
                    // records after it queued in the lane.
                    if (firstAttempt && message.Offset == failingOffset)
                        throw new InvalidOperationException("ignored failure");

                    processed.Enqueue(message.Offset);
                    context.MarkProcessed(message);
                    if (message.Offset == recordCount - 1)
                        await cts.CancelAsync().ConfigureAwait(false);
                }
            },
            new PartitionedProcessingOptions
            {
                ErrorPolicy = PartitionWorkerErrorPolicy.Ignore,
                IgnoreRestartBackoff = TimeSpan.FromMilliseconds(10),
                CommitPolicy = PartitionCommitPolicy.CommitCompletedOnRevoke
            },
            cts.Token).AsTask();

        try
        {
            await runTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
        }

        var expected = Enumerable.Range(0, recordCount)
            .Where(static offset => offset != failingOffset)
            .Select(static offset => (long)offset)
            .ToArray();
        await Assert.That(processed.ToArray()).IsEquivalentTo(expected);
        await Assert.That(await consumer.GetCommittedOffsetAsync(new TopicPartition(topic, 0), CancellationToken.None))
            .IsEqualTo(recordCount);
    }

    private static TaskCompletionSource NewCompletionSource()
        => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static long[] Snapshot(
        ConcurrentDictionary<int, List<long>> processed,
        int partition)
    {
        if (!processed.TryGetValue(partition, out var offsets))
            return [];

        lock (offsets)
            return offsets.ToArray();
    }
}
