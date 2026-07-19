using Dekaf.Consumer;
using Dekaf.Producer;

namespace Dekaf.Tests.Integration;

[Category("ConsumerGroup")]
public sealed class ConsumerAwareRebalanceListenerTests(KafkaTestContainer kafka)
    : KafkaIntegrationTest(kafka)
{
    [Test]
    public async Task RevokedCallback_CanCommitOwnedOffsets()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 4);
        var groupId = $"consumer-aware-revoke-{Guid.NewGuid():N}";
        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .BuildAsync();
        for (var partition = 0; partition < 4; partition++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Partition = partition,
                Key = $"key-{partition}",
                Value = $"value-{partition}"
            });
        }

        var listener = new CommittingRebalanceListener();
        await using var consumer1 = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithOffsetCommitMode(OffsetCommitMode.Manual)
            .WithRebalanceListener(listener)
            .BuildAsync();
        consumer1.Subscribe(topic);
        var consumed = await ConsumeMessagesAsync(consumer1, count: 4);
        await Assert.That(consumed).Count().IsEqualTo(4);

        await using var consumer2 = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();
        consumer2.Subscribe(topic);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var consumer2Task = consumer2.ConsumeOneAsync(
            TimeSpan.FromSeconds(30),
            timeout.Token).AsTask();

        var committed = await listener.Committed.Task.WaitAsync(timeout.Token);

        await Assert.That(committed).IsNotEmpty();
        foreach (var offset in committed)
        {
            var actual = await consumer1.Positions.GetCommittedOffsetAsync(
                new TopicPartition(offset.Topic, offset.Partition),
                timeout.Token);
            await Assert.That(actual).IsEqualTo(offset.Offset);
        }

        await timeout.CancelAsync();
        try { await consumer2Task; }
        catch (OperationCanceledException) { }
    }

    [Test]
    public async Task AssignedCallback_CanSeekPauseResumeAndQueryOffsets()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var groupId = $"consumer-aware-{Guid.NewGuid():N}";
        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .BuildAsync();
        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "first",
            Value = "first"
        });
        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "second",
            Value = "second"
        });

        var listener = new SeekingRebalanceListener();
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithRebalanceListener(listener)
            .BuildAsync();
        consumer.Subscribe(topic);

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), timeout.Token);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value.Value).IsEqualTo("second");
        await Assert.That(listener.AssignmentContainedCallbackPartitions).IsTrue();
        await Assert.That(listener.PauseResumeObserved).IsTrue();
        await Assert.That(listener.WatermarksQueried).IsTrue();
        var retained = listener.RetainedView;
        await Assert.That(retained).IsNotNull();
        var exception = Assert.Throws<InvalidOperationException>(() => _ = retained!.Assignment);
        await Assert.That(exception.Message).Contains("callback has completed");
    }

    private sealed class SeekingRebalanceListener : IConsumerAwareRebalanceListener
    {
        public IRebalanceConsumer? RetainedView { get; private set; }
        public bool AssignmentContainedCallbackPartitions { get; private set; }
        public bool PauseResumeObserved { get; private set; }
        public bool WatermarksQueried { get; private set; }

        public async ValueTask OnPartitionsAssignedAsync(
            IRebalanceConsumer consumer,
            IEnumerable<TopicPartition> partitions,
            CancellationToken cancellationToken)
        {
            var assigned = partitions.ToArray();
            RetainedView = consumer;
            AssignmentContainedCallbackPartitions = assigned.All(consumer.Assignment.Contains);
            consumer.Pause(assigned);
            PauseResumeObserved = assigned.All(consumer.Paused.Contains);
            consumer.Resume(assigned);
            var watermarks = await consumer.QueryWatermarkOffsetsAsync(
                assigned[0],
                cancellationToken).ConfigureAwait(false);
            WatermarksQueried = watermarks.High >= 2;
            consumer.Seek(new TopicPartitionOffset(
                assigned[0].Topic,
                assigned[0].Partition,
                1));
        }

        public ValueTask OnPartitionsRevokedAsync(
            IRebalanceConsumer consumer,
            IEnumerable<TopicPartition> partitions,
            CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask OnPartitionsLostAsync(
            IRebalanceConsumer consumer,
            IEnumerable<TopicPartition> partitions,
            CancellationToken cancellationToken) => ValueTask.CompletedTask;
    }

    private sealed class CommittingRebalanceListener : IConsumerAwareRebalanceListener
    {
        public TaskCompletionSource<TopicPartitionOffset[]> Committed { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public ValueTask OnPartitionsAssignedAsync(
            IRebalanceConsumer consumer,
            IEnumerable<TopicPartition> partitions,
            CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public async ValueTask OnPartitionsRevokedAsync(
            IRebalanceConsumer consumer,
            IEnumerable<TopicPartition> partitions,
            CancellationToken cancellationToken)
        {
            var offsets = partitions
                .Select(static partition => new TopicPartitionOffset(
                    partition.Topic,
                    partition.Partition,
                    1))
                .ToArray();
            if (offsets.Length == 0)
                return;

            try
            {
                await consumer.CommitAsync(offsets, cancellationToken).ConfigureAwait(false);
                Committed.TrySetResult(offsets);
            }
            catch (Exception exception)
            {
                Committed.TrySetException(exception);
                throw;
            }
        }

        public ValueTask OnPartitionsLostAsync(
            IRebalanceConsumer consumer,
            IEnumerable<TopicPartition> partitions,
            CancellationToken cancellationToken) => ValueTask.CompletedTask;
    }
}
