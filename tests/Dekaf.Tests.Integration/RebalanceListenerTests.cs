using System.Collections.Concurrent;
using Dekaf.Admin;
using Dekaf.Consumer;
using Dekaf.Producer;

namespace Dekaf.Tests.Integration;

[Category("ConsumerGroup")]
public class RebalanceListenerTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    [Test]
    public async Task OnPartitionsAssigned_CalledWhenConsumerSubscribes()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var groupId = $"test-group-{Guid.NewGuid():N}";
        var listener = new TestRebalanceListener();

        // Produce a message first so the consumer has something to join for
        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "key",
            Value = "value"
        }, CancellationToken.None);

        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithRebalanceListener(listener)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory()).BuildAsync();

        consumer.Subscribe(topic);

        // Consume one message to trigger the rebalance
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token);

        await Assert.That(listener.AssignedCallCount).IsGreaterThanOrEqualTo(1);
    }

    [Test]
    public async Task AddRebalanceListener_InvokesInOrder_AndIsolatesExceptions()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var groupId = $"test-group-{Guid.NewGuid():N}";
        var callbacks = new ConcurrentQueue<string>();

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "key",
            Value = "value"
        }, CancellationToken.None);

        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithRebalanceListener(new OrderedRebalanceListener("configured", callbacks))
            .AddRebalanceListener(new OrderedRebalanceListener("failing", callbacks, throwOnAssigned: true))
            .AddRebalanceListener(new OrderedRebalanceListener("trailing", callbacks))
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        consumer.Subscribe(topic);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token);
        var callbackSnapshot = callbacks.ToArray();

        await Assert.That(result).IsNotNull();
        await Assert.That(callbackSnapshot).Count().IsGreaterThanOrEqualTo(3);
        await Assert.That(callbackSnapshot[0]).IsEqualTo("configured");
        await Assert.That(callbackSnapshot[1]).IsEqualTo("failing");
        await Assert.That(callbackSnapshot[2]).IsEqualTo("trailing");
    }

    [Test]
    public async Task EvictedMember_ReportsItsPartitionsLostBeforeTheRejoinAssignment()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var groupId = $"test-group-{Guid.NewGuid():N}";
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        var listener = new SequenceRebalanceListener();

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();
        await producer.ProduceAsync(
            new ProducerMessage<string, string> { Topic = topic, Key = "key", Value = "before" },
            deadline.Token);

        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithRebalanceListener(listener)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();
        consumer.Subscribe(topic);

        await Assert.That(await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), deadline.Token)).IsNotNull();
        var evictedMemberId = consumer.MemberId;
        await Assert.That(evictedMemberId).IsNotNull();
        var callbacksBeforeEviction = listener.Callbacks.Count;

        // The coordinator forgets the member; its next heartbeat is fenced.
        await using (var admin = new AdminClientBuilder()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .Build())
        {
            var removal = await admin.RemoveMembersFromConsumerGroupAsync(groupId, new ConsumerGroupMemberRemovalOptions
            {
                Members = [new ConsumerGroupMemberIdentity { MemberId = evictedMemberId }],
                Reason = "membership loss integration test"
            }, deadline.Token);
            await Assert.That(removal.Succeeded).IsTrue();
        }

        // The heartbeat loop reports the loss; the next polls rejoin the group.
        while (!listener.ReassignedAfterLoss.IsCompleted)
            await consumer.ConsumeOneAsync(TimeSpan.FromMilliseconds(500), deadline.Token);

        // Lost comes first, and nothing is assigned or revoked before it.
        var afterEviction = listener.Callbacks.Skip(callbacksBeforeEviction).ToArray();
        await Assert.That(afterEviction[0]).IsEqualTo($"lost:{topic}-0");
        await Assert.That(afterEviction).Contains($"assigned:{topic}-0");
        await Assert.That(consumer.MemberId).IsNotEqualTo(evictedMemberId);

        // The rejoined member consumes and commits again.
        await producer.ProduceAsync(
            new ProducerMessage<string, string> { Topic = topic, Key = "key", Value = "after" },
            deadline.Token);
        while (true)
        {
            var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), deadline.Token);
            await Assert.That(result).IsNotNull();
            if (result!.Value.Value == "after")
                break;
        }

        await consumer.CommitAsync(deadline.Token);
    }

    [Test]
    public async Task MemberRemovedDuringAssignment_RejoinCallbackSeekIsApplied()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var groupId = $"test-group-{Guid.NewGuid():N}";
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(90));

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();
        for (var i = 0; i < 5; i++)
        {
            await producer.ProduceAsync(
                new ProducerMessage<string, string> { Topic = topic, Key = "key", Value = $"value-{i}" },
                deadline.Token);
        }

        await using var admin = new AdminClientBuilder()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .Build();
        var listener = new RemoveThenSeekListener(admin, groupId, new TopicPartitionOffset(topic, 0, 3));

        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithRebalanceListener(listener)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();
        consumer.Subscribe(topic);

        // The first assignment's callback removes the member, so the committed-offset fetch that
        // initializes its position is fenced (UNKNOWN_MEMBER_ID). The consumer rejoins, reports the
        // loss, and the rejoin's callback seeks, which takes the assignment lock. A real broker may
        // deliver the rejoin assignment on a later heartbeat, so the lock-order deadlock itself is
        // pinned by the unit test; this covers the broker protocol, callback order and position.
        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(60), deadline.Token);

        await Assert.That(result).IsNotNull();
        await Assert.That(listener.Lost).IsTrue();
        await Assert.That(listener.AssignedCalls).IsGreaterThanOrEqualTo(2);
        await Assert.That(result!.Value.Offset).IsEqualTo(3);
        await Assert.That(result.Value.Value).IsEqualTo("value-3");
    }

    private sealed class RemoveThenSeekListener(
        IAdminClient admin,
        string groupId,
        TopicPartitionOffset seek) : IConsumerAwareRebalanceListener
    {
        private int _assignedCalls;
        private int _lost;

        public int AssignedCalls => Volatile.Read(ref _assignedCalls);
        public bool Lost => Volatile.Read(ref _lost) != 0;

        public async ValueTask OnPartitionsAssignedAsync(
            IRebalanceConsumer consumer,
            IEnumerable<TopicPartition> partitions,
            CancellationToken cancellationToken)
        {
            if (!partitions.Any())
                return;
            if (Interlocked.Increment(ref _assignedCalls) == 1)
            {
                var removal = await admin.RemoveMembersFromConsumerGroupAsync(groupId, new ConsumerGroupMemberRemovalOptions
                {
                    Members = [new ConsumerGroupMemberIdentity { MemberId = consumer.MemberId! }],
                    Reason = "fenced position initialization integration test"
                }, cancellationToken);
                if (!removal.Succeeded)
                    throw new InvalidOperationException("Member removal failed.");
                return;
            }

            if (Lost)
                consumer.Seek(seek);
        }

        public ValueTask OnPartitionsRevokedAsync(
            IRebalanceConsumer consumer,
            IEnumerable<TopicPartition> partitions,
            CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask OnPartitionsLostAsync(
            IRebalanceConsumer consumer,
            IEnumerable<TopicPartition> partitions,
            CancellationToken cancellationToken)
        {
            Volatile.Write(ref _lost, 1);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class SequenceRebalanceListener : IRebalanceListener
    {
        private readonly TaskCompletionSource _reassignedAfterLoss =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private bool _lost;

        public ConcurrentQueue<string> Callbacks { get; } = new();

        public Task ReassignedAfterLoss => _reassignedAfterLoss.Task;

        public ValueTask OnPartitionsAssignedAsync(IEnumerable<TopicPartition> partitions, CancellationToken cancellationToken)
        {
            Record("assigned", partitions);
            if (Volatile.Read(ref _lost) && partitions.Any())
                _reassignedAfterLoss.TrySetResult();
            return ValueTask.CompletedTask;
        }

        public ValueTask OnPartitionsRevokedAsync(IEnumerable<TopicPartition> partitions, CancellationToken cancellationToken)
        {
            Record("revoked", partitions);
            return ValueTask.CompletedTask;
        }

        public ValueTask OnPartitionsLostAsync(IEnumerable<TopicPartition> partitions, CancellationToken cancellationToken)
        {
            Record("lost", partitions);
            Volatile.Write(ref _lost, true);
            return ValueTask.CompletedTask;
        }

        private void Record(string callback, IEnumerable<TopicPartition> partitions) =>
            Callbacks.Enqueue($"{callback}:{string.Join(',', partitions.Select(static p => $"{p.Topic}-{p.Partition}"))}");
    }

    private sealed class TestRebalanceListener : IRebalanceListener
    {
        private int _assignedCount;

        public int AssignedCallCount => _assignedCount;

        public ValueTask OnPartitionsAssignedAsync(IEnumerable<TopicPartition> partitions, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _assignedCount);
            return ValueTask.CompletedTask;
        }

        public ValueTask OnPartitionsRevokedAsync(IEnumerable<TopicPartition> partitions, CancellationToken cancellationToken)
        {
            return ValueTask.CompletedTask;
        }

        public ValueTask OnPartitionsLostAsync(IEnumerable<TopicPartition> partitions, CancellationToken cancellationToken)
        {
            return ValueTask.CompletedTask;
        }
    }

    private sealed class OrderedRebalanceListener(
        string name,
        ConcurrentQueue<string> callbacks,
        bool throwOnAssigned = false) : IRebalanceListener
    {
        public ValueTask OnPartitionsAssignedAsync(
            IEnumerable<TopicPartition> partitions,
            CancellationToken cancellationToken)
        {
            callbacks.Enqueue(name);
            if (throwOnAssigned)
            {
                throw new InvalidOperationException($"{name} assignment failure");
            }

            return ValueTask.CompletedTask;
        }

        public ValueTask OnPartitionsRevokedAsync(
            IEnumerable<TopicPartition> partitions,
            CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask OnPartitionsLostAsync(
            IEnumerable<TopicPartition> partitions,
            CancellationToken cancellationToken) => ValueTask.CompletedTask;
    }
}
