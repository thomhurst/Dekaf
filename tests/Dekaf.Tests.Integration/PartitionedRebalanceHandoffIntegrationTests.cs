using System.Collections.Concurrent;
using Dekaf.Consumer;
using Dekaf.Producer;

namespace Dekaf.Tests.Integration;

[Category("ConsumerGroup")]
public sealed class PartitionedRebalanceHandoffIntegrationTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    [Test]
    public async Task RunPartitionedAsync_PartitionHandedOffByHeartbeat_ProcessesHandedOffRecords()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 2);
        var groupId = $"partitioned-handoff-{Guid.NewGuid():N}";
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(90));

        // A long fetch wait keeps the runtime consumer's loop away from assignment
        // publication for well over the runtime's 100 ms idle sync after the hand-off callback.
        await using var runtimeConsumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithOffsetCommitMode(OffsetCommitMode.Manual)
            .WithQueuedMinMessages(1)
            .WithFetchMaxWait(TimeSpan.FromSeconds(2))
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();
        runtimeConsumer.Subscribe(topic);

        var lanesStarted = new ConcurrentDictionary<int, int>();
        var processed = new ConcurrentDictionary<int, ConcurrentQueue<long>>();
        var runTask = runtimeConsumer.RunPartitionedAsync(
            async (context, cancellationToken) =>
            {
                lanesStarted.AddOrUpdate(context.TopicPartition.Partition, 1, static (_, count) => count + 1);
                await foreach (var message in context.Messages.WithCancellation(cancellationToken))
                {
                    processed.GetOrAdd(message.Partition, static _ => new ConcurrentQueue<long>())
                        .Enqueue(message.Offset);
                    context.MarkProcessed(message);
                }
            },
            new PartitionedProcessingOptions
            {
                CommitPolicy = PartitionCommitPolicy.CommitCompletedOnRevoke
            },
            cts.Token).AsTask();

        await Assert.That(() => lanesStarted.Count)
            .Eventually(count => count.IsEqualTo(2), TimeSpan.FromSeconds(30));

        // A second member takes one partition, then leaves and hands it back.
        var departingConsumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithOffsetCommitMode(OffsetCommitMode.Manual)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();
        departingConsumer.Subscribe(topic);
        using var departingCancellation = CancellationTokenSource.CreateLinkedTokenSource(cts.Token);
        var departingLoop = ConsumeUntilCancelledAsync(departingConsumer, departingCancellation.Token);

        await Assert.That(() => departingConsumer.Assignment.Count)
            .Eventually(count => count.IsEqualTo(1), TimeSpan.FromSeconds(60));
        var handedOffPartition = departingConsumer.Assignment.Single().Partition;
        var startsBeforeHandOff = lanesStarted.GetValueOrDefault(handedOffPartition);

        await departingCancellation.CancelAsync();
        await departingLoop;
        await departingConsumer.CloseAsync(CancellationToken.None);
        await departingConsumer.DisposeAsync();

        await Assert.That(() => lanesStarted.GetValueOrDefault(handedOffPartition))
            .Eventually(count => count.IsGreaterThan(startsBeforeHandOff), TimeSpan.FromSeconds(60));

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        const int recordCount = 5;
        for (var i = 0; i < recordCount; i++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Partition = handedOffPartition,
                Key = $"key-{i}",
                Value = $"value-{i}"
            }, CancellationToken.None);
        }

        await Assert.That(() => processed.TryGetValue(handedOffPartition, out var offsets) ? offsets.Count : 0)
            .Eventually(count => count.IsEqualTo(recordCount), TimeSpan.FromSeconds(30));
        await Assert.That(processed[handedOffPartition].ToArray()).IsEquivalentTo([0L, 1L, 2L, 3L, 4L]);

        await cts.CancelAsync();
        try
        {
            await runTask;
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
        }
    }

    [Test]
    public async Task RunPartitionedAsync_RevokedPartitionIsReleasedOnlyAfterDrainAndCommit()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 2);
        var groupId = $"partitioned-revoke-drain-{Guid.NewGuid():N}";
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        const int recordsPerPartition = 3;
        for (var partition = 0; partition < 2; partition++)
        {
            for (var i = 0; i < recordsPerPartition; i++)
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

        await using var runtimeConsumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithOffsetCommitMode(OffsetCommitMode.Manual)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();
        runtimeConsumer.Subscribe(topic);

        // Every lane holds its first record until released, so a revoked lane has queued records to drain.
        var releaseHandlers = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var blockedLanes = new ConcurrentDictionary<int, byte>();
        var runTask = runtimeConsumer.RunPartitionedAsync(
            async (context, cancellationToken) =>
            {
                await foreach (var message in context.Messages.WithCancellation(cancellationToken))
                {
                    blockedLanes.TryAdd(message.Partition, 0);
                    await releaseHandlers.Task.WaitAsync(cancellationToken);
                    context.MarkProcessed(message);
                }
            },
            new PartitionedProcessingOptions
            {
                StopPolicy = PartitionStopPolicy.Drain,
                CommitPolicy = PartitionCommitPolicy.CommitCompletedOnRevoke,
                StopTimeout = TimeSpan.FromSeconds(30)
            },
            cts.Token).AsTask();

        await Assert.That(() => blockedLanes.Count)
            .Eventually(count => count.IsEqualTo(2), TimeSpan.FromSeconds(30));

        await using var joiningConsumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithOffsetCommitMode(OffsetCommitMode.Manual)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();
        joiningConsumer.Subscribe(topic);
        var joinerRecords = new ConcurrentQueue<(int Partition, long Offset)>();
        using var joiningCancellation = CancellationTokenSource.CreateLinkedTokenSource(cts.Token);
        var joiningLoop = CollectUntilCancelledAsync(joiningConsumer, joinerRecords, joiningCancellation.Token);

        // While the revoked lane still drains, the runtime member must not release the partition.
        // Several heartbeat intervals pass; before the fix the joiner was assigned within one.
        await Task.Delay(TimeSpan.FromSeconds(12), cts.Token);
        await Assert.That(joiningConsumer.Assignment.Count).IsEqualTo(0);
        await Assert.That(joinerRecords).IsEmpty();

        releaseHandlers.SetResult();

        await Assert.That(() => joiningConsumer.Assignment.Count)
            .Eventually(count => count.IsEqualTo(1), TimeSpan.FromSeconds(60));
        var handedOffPartition = joiningConsumer.Assignment.Single().Partition;

        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Partition = handedOffPartition,
            Key = "after-handoff",
            Value = "after-handoff"
        }, CancellationToken.None);

        // The joiner starts from the offset the runtime committed after draining, not from the
        // records the runtime member already processed.
        await Assert.That(() => joinerRecords.Count)
            .Eventually(count => count.IsGreaterThanOrEqualTo(1), TimeSpan.FromSeconds(30));
        await Assert.That(joinerRecords.ToArray()).IsEquivalentTo(new[] { (handedOffPartition, (long)recordsPerPartition) });

        await joiningCancellation.CancelAsync();
        await joiningLoop;
        await cts.CancelAsync();
        try
        {
            await runTask;
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
        }
    }

    private static async Task CollectUntilCancelledAsync(
        IKafkaConsumer<string, string> consumer,
        ConcurrentQueue<(int Partition, long Offset)> records,
        CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var record in consumer.ConsumeAsync(cancellationToken))
                records.Enqueue((record.Partition, record.Offset));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private static async Task ConsumeUntilCancelledAsync(
        IKafkaConsumer<string, string> consumer,
        CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var _ in consumer.ConsumeAsync(cancellationToken))
            {
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }
}
