using System.Collections.Concurrent;
using System.Reflection;
using Dekaf.Consumer;
using Dekaf.Producer;

namespace Dekaf.Tests.Integration;

/// <summary>
/// Seeks and pauses a rebalance callback makes run before the consumer synchronizes the
/// assignment the callback announced. These pin that the synchronization keeps them.
/// </summary>
[Category("ConsumerGroup")]
public sealed class RebalanceCallbackConsumerStateTests(KafkaTestContainer kafka)
    : KafkaIntegrationTest(kafka)
{
    [Test]
    public async Task LegacyListener_SeekInAssigned_WinsOverCommittedOffset()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var groupId = $"callback-seek-{Guid.NewGuid():N}";
        await ProduceAsync(topic, partitions: 1, perPartition: 4);
        var partition = new TopicPartition(topic, 0);

        await using (var committer = await CreateConsumerAsync(groupId, listener: null))
        {
            committer.Subscribe(topic);
            await ConsumeMessagesAsync(committer, count: 1);
            await committer.CommitAsync([new TopicPartitionOffset(topic, 0, 1)]);
        }

        var listener = new LegacyListener();
        await using var consumer = await CreateConsumerAsync(groupId, listener);
        listener.OnAssigned = (c, partitions) =>
        {
            if (partitions.Contains(partition))
                c.Seek(new TopicPartitionOffset(topic, 0, 3));
        };
        listener.Consumer = consumer;
        consumer.Subscribe(topic);

        var first = (await ConsumeMessagesAsync(consumer, count: 1)).Single();

        await Assert.That(first.Offset).IsEqualTo(3L);
    }

    [Test]
    public async Task LegacyListener_SeekThenUnsubscribeBeforeSync_DoesNotLeakIntoNextSubscription()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var groupId = $"callback-seek-abandon-{Guid.NewGuid():N}";
        await ProduceAsync(topic, partitions: 1, perPartition: 4);
        var partition = new TopicPartition(topic, 0);

        await using (var committer = await CreateConsumerAsync(groupId, listener: null))
        {
            committer.Subscribe(topic);
            await ConsumeMessagesAsync(committer, count: 1);
            await committer.CommitAsync([new TopicPartitionOffset(topic, 0, 1)]);
        }

        var listener = new LegacyListener();
        await using var consumer = await CreateConsumerAsync(groupId, listener);
        var seeked = false;
        listener.OnAssigned = (c, partitions) =>
        {
            if (seeked || !partitions.Contains(partition))
                return;

            // The callback's seek is staged for the assignment it announced, which the consumer
            // abandons before ever synchronizing it.
            seeked = true;
            c.Seek(new TopicPartitionOffset(topic, 0, 3));
            c.Unsubscribe();
        };
        listener.Consumer = consumer;
        consumer.Subscribe(topic);

        // Joins; the assignment and its callback may follow on the heartbeat.
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        await ((KafkaConsumer<string, string>)consumer).EnsureAssignmentAsync(timeout.Token);
        await WaitForConditionAsync(() => seeked, TimeSpan.FromSeconds(60), description: "assigned callback");

        consumer.Subscribe(topic);
        var first = (await ConsumeMessagesAsync(consumer, count: 1)).Single();

        await Assert.That(first.Offset).IsEqualTo(1L);
    }

    [Test]
    public async Task TwoListeners_SeekAndPauseInAssigned_BothSurviveSync()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var groupId = $"callback-two-listeners-{Guid.NewGuid():N}";
        await ProduceAsync(topic, partitions: 1, perPartition: 4);
        var partition = new TopicPartition(topic, 0);

        var pausing = new ConsumerAwareListener
        {
            OnReturn = static (target, partitions) => target.Pause([.. partitions])
        };
        var seeking = new LegacyListener();
        seeking.OnAssigned = (c, partitions) =>
        {
            if (partitions.Contains(partition))
                c.Seek(new TopicPartitionOffset(topic, 0, 2));
        };
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithOffsetCommitMode(OffsetCommitMode.Manual)
            .WithRebalanceListener(pausing)
            .AddRebalanceListener(seeking)
            .BuildAsync();
        seeking.Consumer = consumer;
        consumer.Subscribe(topic);

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        await ((KafkaConsumer<string, string>)consumer).EnsureAssignmentAsync(timeout.Token);

        await Assert.That(consumer.Assignment).Contains(partition);
        await Assert.That(consumer.Paused).Contains(partition);
        await Assert.That(consumer.Positions.GetPosition(partition)).IsEqualTo(2L);

        consumer.Resume(partition);
        var first = (await ConsumeMessagesAsync(consumer, count: 1)).Single();

        await Assert.That(first.Offset).IsEqualTo(2L);
    }

    [Test]
    public async Task LegacyListener_SeekInAssignedForReturnedPartition_WinsOverResetOffset()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 2);
        var groupId = $"callback-handoff-seek-{Guid.NewGuid():N}";
        await ProduceAsync(topic, partitions: 2, perPartition: 4);

        var listener = new LegacyListener();
        await using var consumer1 = await CreateConsumerAsync(groupId, listener);
        listener.Consumer = consumer1;
        consumer1.Subscribe(topic);
        await ConsumeMessagesAsync(consumer1, count: 8);

        var handedOff = await HandOffAndReturnAsync(
            topic,
            groupId,
            listener,
            onReturn: (c, partition) => c.Seek(new TopicPartitionOffset(partition.Topic, partition.Partition, 2)));

        var records = await ConsumeUntilPartitionAsync(consumer1, handedOff);

        await Assert.That(records.First(r => r.Partition == handedOff.Partition).Offset).IsEqualTo(2L);
    }

    [Test]
    public async Task ConsumerAwareListener_PauseInAssignedAfterHandOffAndReturn_SurvivesSync()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 2);
        var groupId = $"callback-handoff-pause-{Guid.NewGuid():N}";
        await ProduceAsync(topic, partitions: 2, perPartition: 4);

        var listener = new ConsumerAwareListener();
        await using var consumer1 = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithOffsetCommitMode(OffsetCommitMode.Manual)
            .WithRebalanceListener(listener)
            .BuildAsync();
        consumer1.Subscribe(topic);
        await ConsumeMessagesAsync(consumer1, count: 8);

        var handedOff = await HandOffAndReturnAsync(
            topic,
            groupId,
            listener,
            onReturn: static (c, partition) => c.Pause(partition),
            holdAssignmentSyncOf: (KafkaConsumer<string, string>)consumer1);

        // Records arrive for the other partition only; the returned one stays paused.
        await ProduceAsync(topic, partitions: 2, perPartition: 2);
        var other = new TopicPartition(topic, 1 - handedOff.Partition);
        var records = await ConsumeMessagesAsync(consumer1, count: 2);

        await Assert.That(consumer1.Paused).Contains(handedOff);
        await Assert.That(records.All(r => r.Partition == other.Partition)).IsTrue();
    }

    /// <summary>
    /// A second member joins and takes one partition from <paramref name="listener"/>'s consumer,
    /// then leaves, and the partition returns. With <paramref name="holdAssignmentSyncOf"/>, that
    /// consumer cannot synchronize its assignment meanwhile (its background prefetch would
    /// otherwise), so the partition is revoked and assigned again between two synchronizations.
    /// </summary>
    private async Task<TopicPartition> HandOffAndReturnAsync(
        string topic,
        string groupId,
        IRecordingListener listener,
        Action<IConsumerCallbackTarget, TopicPartition> onReturn,
        KafkaConsumer<string, string>? holdAssignmentSyncOf = null)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        var revoked = listener.NextRevocation();
        var assignmentLock = holdAssignmentSyncOf is null ? null : GetAssignmentLock(holdAssignmentSyncOf);
        if (assignmentLock is not null)
            await assignmentLock.WaitAsync(timeout.Token);
        try
        {
            return await HandOffAndReturnCoreAsync(topic, groupId, listener, onReturn, revoked, timeout.Token);
        }
        finally
        {
            assignmentLock?.Release();
        }
    }

    private async Task<TopicPartition> HandOffAndReturnCoreAsync(
        string topic,
        string groupId,
        IRecordingListener listener,
        Action<IConsumerCallbackTarget, TopicPartition> onReturn,
        Task<IReadOnlyList<TopicPartition>> revoked,
        CancellationToken cancellationToken)
    {

        await using (var consumer2 = await CreateConsumerAsync(groupId, listener: null))
        {
            consumer2.Subscribe(topic);
            var consumer2Poll = ConsumeMessagesAsync(consumer2, count: 1);
            var handedOff = (await revoked.WaitAsync(cancellationToken)).Single();
            await consumer2Poll.WaitAsync(cancellationToken);

            listener.OnReturn = (target, partitions) =>
            {
                if (partitions.Contains(handedOff))
                    onReturn(target, handedOff);
            };
            var returned = listener.NextAssignment();
            await consumer2.CloseAsync(cancellationToken);
            var reassigned = await returned.WaitAsync(cancellationToken);
            await Assert.That(reassigned).Contains(handedOff);
            return handedOff;
        }
    }

    private static SemaphoreSlim GetAssignmentLock(KafkaConsumer<string, string> consumer) =>
        (SemaphoreSlim)(typeof(KafkaConsumer<string, string>).GetField(
                "_assignmentLock",
                BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("_assignmentLock field not found."))
        .GetValue(consumer)!;

    private static async Task<List<ConsumeResult<string, string>>> ConsumeUntilPartitionAsync(
        IKafkaConsumer<string, string> consumer,
        TopicPartition partition)
    {
        var records = new List<ConsumeResult<string, string>>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await foreach (var record in consumer.ConsumeAsync(cts.Token))
        {
            records.Add(record);
            if (record.Partition == partition.Partition)
                break;
        }

        return records;
    }

    private async Task ProduceAsync(string topic, int partitions, int perPartition)
    {
        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .BuildAsync();
        for (var partition = 0; partition < partitions; partition++)
        {
            for (var i = 0; i < perPartition; i++)
            {
                await producer.ProduceAsync(new ProducerMessage<string, string>
                {
                    Topic = topic,
                    Partition = partition,
                    Key = $"key-{partition}-{i}",
                    Value = $"value-{partition}-{i}"
                });
            }
        }
    }

    private Task<IKafkaConsumer<string, string>> CreateConsumerAsync(string groupId, IRebalanceListener? listener)
    {
        var builder = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithOffsetCommitMode(OffsetCommitMode.Manual);
        if (listener is not null)
            builder = builder.WithRebalanceListener(listener);
        return builder.BuildAsync().AsTask();
    }

    /// <summary>The calls a callback makes, on the consumer itself or on the consumer-aware view.</summary>
    private interface IConsumerCallbackTarget
    {
        void Seek(TopicPartitionOffset offset);

        void Pause(params TopicPartition[] partitions);
    }

    private interface IRecordingListener
    {
        Action<IConsumerCallbackTarget, IReadOnlyList<TopicPartition>>? OnReturn { get; set; }

        Task<IReadOnlyList<TopicPartition>> NextRevocation();

        Task<IReadOnlyList<TopicPartition>> NextAssignment();
    }

    private abstract class RecordingListenerBase : IRecordingListener
    {
        private readonly ConcurrentQueue<TaskCompletionSource<IReadOnlyList<TopicPartition>>> _revocations = new();
        private readonly ConcurrentQueue<TaskCompletionSource<IReadOnlyList<TopicPartition>>> _assignments = new();

        public Action<IConsumerCallbackTarget, IReadOnlyList<TopicPartition>>? OnReturn { get; set; }

        public Task<IReadOnlyList<TopicPartition>> NextRevocation() => Enqueue(_revocations);

        public Task<IReadOnlyList<TopicPartition>> NextAssignment() => Enqueue(_assignments);

        protected void Assigned(IConsumerCallbackTarget target, IEnumerable<TopicPartition> partitions)
        {
            var list = partitions.ToList();
            OnReturn?.Invoke(target, list);
            if (_assignments.TryDequeue(out var waiter))
                waiter.TrySetResult(list);
        }

        protected void Revoked(IEnumerable<TopicPartition> partitions)
        {
            var list = partitions.ToList();
            if (list.Count != 0 && _revocations.TryDequeue(out var waiter))
                waiter.TrySetResult(list);
        }

        private static Task<IReadOnlyList<TopicPartition>> Enqueue(
            ConcurrentQueue<TaskCompletionSource<IReadOnlyList<TopicPartition>>> queue)
        {
            var waiter = new TaskCompletionSource<IReadOnlyList<TopicPartition>>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            queue.Enqueue(waiter);
            return waiter.Task;
        }
    }

    /// <summary>A plain listener that captures the consumer, as Confluent-style code does.</summary>
    private sealed class LegacyListener : RecordingListenerBase, IRebalanceListener, IConsumerCallbackTarget
    {
        public IKafkaConsumer<string, string>? Consumer { get; set; }

        public Action<IKafkaConsumer<string, string>, IReadOnlyList<TopicPartition>>? OnAssigned { get; set; }

        public ValueTask OnPartitionsAssignedAsync(IEnumerable<TopicPartition> partitions, CancellationToken cancellationToken)
        {
            var list = partitions.ToList();
            if (Consumer is { } consumer)
                OnAssigned?.Invoke(consumer, list);
            Assigned(this, list);
            return ValueTask.CompletedTask;
        }

        public ValueTask OnPartitionsRevokedAsync(IEnumerable<TopicPartition> partitions, CancellationToken cancellationToken)
        {
            Revoked(partitions);
            return ValueTask.CompletedTask;
        }

        public ValueTask OnPartitionsLostAsync(IEnumerable<TopicPartition> partitions, CancellationToken cancellationToken) =>
            ValueTask.CompletedTask;

        public void Seek(TopicPartitionOffset offset) => Consumer!.Seek(offset);

        public void Pause(params TopicPartition[] partitions) => Consumer!.Pause(partitions);
    }

    private sealed class ConsumerAwareListener : RecordingListenerBase, IConsumerAwareRebalanceListener
    {
        public ValueTask OnPartitionsAssignedAsync(
            IRebalanceConsumer consumer,
            IEnumerable<TopicPartition> partitions,
            CancellationToken cancellationToken)
        {
            Assigned(new ViewTarget(consumer), partitions);
            return ValueTask.CompletedTask;
        }

        public ValueTask OnPartitionsRevokedAsync(
            IRebalanceConsumer consumer,
            IEnumerable<TopicPartition> partitions,
            CancellationToken cancellationToken)
        {
            Revoked(partitions);
            return ValueTask.CompletedTask;
        }

        public ValueTask OnPartitionsLostAsync(
            IRebalanceConsumer consumer,
            IEnumerable<TopicPartition> partitions,
            CancellationToken cancellationToken) =>
            ValueTask.CompletedTask;

        private sealed class ViewTarget(IRebalanceConsumer consumer) : IConsumerCallbackTarget
        {
            public void Seek(TopicPartitionOffset offset) => consumer.Seek(offset);

            public void Pause(params TopicPartition[] partitions) => consumer.Pause(partitions);
        }
    }
}
