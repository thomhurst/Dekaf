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
