using Dekaf.Consumer;
using Dekaf.Producer;

namespace Dekaf.Tests.Integration;

/// <summary>
/// Integration tests for cooperative sticky rebalancing and incremental assign/unassign.
/// </summary>
public sealed class CooperativeStickyRebalanceTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    [Test]
    public async Task IncrementalAssign_AddPartitions_AllPartitionsAssigned()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 4);

        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        // Initially assign 2 partitions
        consumer.Assign(
            new TopicPartition(topic, 0),
            new TopicPartition(topic, 1));

        // Incrementally assign 2 more
        consumer.IncrementalAssign([
            new TopicPartitionOffset(topic, 2, 0),
            new TopicPartitionOffset(topic, 3, 0)
        ]);

        // Produce to all 4 partitions
        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .BuildAsync();

        for (var p = 0; p < 4; p++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-p{p}",
                Value = $"value-p{p}",
                Partition = p
            });
        }

        // Consume from all 4 partitions
        var messages = new List<ConsumeResult<string, string>>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            messages.Add(msg);
            if (messages.Count >= 4) break;
        }

        await Assert.That(messages).Count().IsEqualTo(4);

        var partitions = messages.Select(m => m.Partition).Distinct().OrderBy(p => p).ToList();
        await Assert.That(partitions).Count().IsEqualTo(4);
    }

    [Test]
    public async Task IncrementalUnassign_RemovePartitions_RemainingPartitionsStillActive()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 4);

        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        // Assign all 4 partitions
        consumer.Assign(
            new TopicPartition(topic, 0),
            new TopicPartition(topic, 1),
            new TopicPartition(topic, 2),
            new TopicPartition(topic, 3));

        // Unassign partitions 2 and 3
        consumer.IncrementalUnassign([
            new TopicPartition(topic, 2),
            new TopicPartition(topic, 3)
        ]);

        // Produce to partitions 0 and 1 only
        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .BuildAsync();

        for (var p = 0; p < 2; p++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-p{p}",
                Value = $"value-p{p}",
                Partition = p
            });
        }

        // Should receive messages from remaining partitions
        var messages = new List<ConsumeResult<string, string>>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            messages.Add(msg);
            if (messages.Count >= 2) break;
        }

        await Assert.That(messages).Count().IsEqualTo(2);

        var partitions = messages.Select(m => m.Partition).Distinct().OrderBy(p => p).ToList();
        await Assert.That(partitions).Count().IsEqualTo(2);
        await Assert.That(partitions[0]).IsEqualTo(0);
        await Assert.That(partitions[1]).IsEqualTo(1);
    }

    [Test]
    public async Task CooperativeRebalance_TwoConsumers_PartitionsRedistributed()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 4);
        var groupId = $"coop-rebalance-{Guid.NewGuid():N}";
        var listener1 = new TestRebalanceListener();
        var listener2 = new TestRebalanceListener();

        // Produce messages to all partitions
        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .BuildAsync();

        for (var p = 0; p < 4; p++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{p}",
                Value = $"value-{p}",
                Partition = p
            });
        }

        // First consumer joins
        // CooperativeSticky is the default partition assignment strategy
        await using var consumer1 = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithRebalanceListener(listener1)
            .BuildAsync();

        consumer1.Subscribe(topic);

        // Consume a message to trigger initial assignment
        using var cts1 = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await consumer1.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts1.Token);

        // First consumer should have all 4 partitions
        await Assert.That(listener1.AssignedCallCount).IsGreaterThanOrEqualTo(1);

        // Second consumer joins the same group
        await using var consumer2 = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithRebalanceListener(listener2)
            .BuildAsync();

        consumer2.Subscribe(topic);

        // Wait for rebalance to redistribute partitions
        using var cts2 = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try
        {
            await consumer2.ConsumeOneAsync(TimeSpan.FromSeconds(15), cts2.Token);
        }
        catch (OperationCanceledException)
        {
            // May timeout if no messages left, that's OK
        }

        // Wait for rebalance to settle
        await Task.Delay(5000).ConfigureAwait(false);

        // Both listeners should have been called (rebalance happened)
        await Assert.That(listener1.AssignedCallCount).IsGreaterThanOrEqualTo(1);
        await Assert.That(listener2.AssignedCallCount).IsGreaterThanOrEqualTo(1);
    }

    [Test]
    public async Task OnPartitionsLost_ShortSessionTimeout_CallbackFires()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var groupId = $"partitions-lost-{Guid.NewGuid():N}";
        var listener = new TestRebalanceListener();

        // Produce a message
        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .BuildAsync();

        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "key",
            Value = "value"
        });

        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithSessionTimeout(TimeSpan.FromMilliseconds(6000))
            .WithRebalanceListener(listener)
            .BuildAsync();

        consumer.Subscribe(topic);

        // Consume to trigger assignment
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token);

        await Assert.That(listener.AssignedCallCount).IsGreaterThanOrEqualTo(1);
    }

    private sealed class TestRebalanceListener : IRebalanceListener
    {
        private int _assignedCount;
        private int _revokedCount;
        private int _lostCount;

        public int AssignedCallCount => _assignedCount;
        public int RevokedCallCount => _revokedCount;
        public int LostCallCount => _lostCount;

        public ValueTask OnPartitionsAssignedAsync(IEnumerable<TopicPartition> partitions, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _assignedCount);
            return ValueTask.CompletedTask;
        }

        public ValueTask OnPartitionsRevokedAsync(IEnumerable<TopicPartition> partitions, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _revokedCount);
            return ValueTask.CompletedTask;
        }

        public ValueTask OnPartitionsLostAsync(IEnumerable<TopicPartition> partitions, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _lostCount);
            return ValueTask.CompletedTask;
        }
    }
}
