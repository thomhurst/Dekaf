using System.Collections.Concurrent;
using Dekaf.Admin;
using Dekaf.Consumer;
using Dekaf.Producer;

namespace Dekaf.Tests.Integration;

/// <summary>
/// Integration tests for consumer behavior when partitions are added to a topic
/// while the consumer is actively consuming.
/// </summary>
[Category("ConsumerGroup")]
public sealed class PartitionExpansionTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    private IAdminClient CreateAdminClient()
    {
        return new AdminClientBuilder()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-admin-client")
            .Build();
    }

    /// <summary>
    /// Waits for a condition to become true with exponential backoff.
    /// Kafka operations have eventual consistency - changes may not be immediately visible.
    /// </summary>
    private static async Task<T> WaitForConditionAsync<T>(
        Func<Task<T>> check,
        Func<T, bool> condition,
        int maxRetries = 5,
        int initialDelayMs = 500)
    {
        T result = default!;
        for (var i = 0; i < maxRetries; i++)
        {
            await Task.Delay(initialDelayMs * (i + 1)).ConfigureAwait(false);
            result = await check().ConfigureAwait(false);
            if (condition(result))
                return result;
        }
        return result;
    }

    /// <summary>
    /// Waits for metadata propagation after partition expansion by polling DescribeTopicsAsync
    /// until the expected partition count is visible.
    /// </summary>
    private static async Task WaitForPartitionCountAsync(IAdminClient admin, string topic, int expectedPartitionCount)
    {
        await WaitForConditionAsync(
            async () =>
            {
                var descriptions = await admin.DescribeTopicsAsync([topic]).ConfigureAwait(false);
                return descriptions.TryGetValue(topic, out var desc) ? desc.Partitions.Count : 0;
            },
            count => count >= expectedPartitionCount).ConfigureAwait(false);
    }

    [Test]
    public async Task ConsumerGroup_DetectsNewPartitions_AfterExpansion()
    {
        // Arrange - create topic with 2 partitions
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 2).ConfigureAwait(false);
        var groupId = $"test-group-{Guid.NewGuid():N}";
        var listener = new PartitionTrackingRebalanceListener();

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer")
            .BuildAsync();

        // Produce messages to original partitions
        for (var p = 0; p < 2; p++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{p}",
                Value = $"value-{p}",
                Partition = p
            }).ConfigureAwait(false);
        }

        // First consumer: subscribe, consume, commit, then dispose with short session
        await using (var consumer1 = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer-1")
            .WithGroupId(groupId)
            .WithSessionTimeout(TimeSpan.FromMilliseconds(10000))
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithOffsetCommitMode(OffsetCommitMode.Manual)
            .WithRebalanceListener(listener)
            .BuildAsync())
        {
            consumer1.Subscribe(topic);

            using var cts1 = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var msg1 = await consumer1.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts1.Token).ConfigureAwait(false);
            await Assert.That(msg1).IsNotNull();
            var msg2 = await consumer1.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts1.Token).ConfigureAwait(false);
            await Assert.That(msg2).IsNotNull();

            await consumer1.CommitAsync([
                new TopicPartitionOffset(topic, 0, 1),
                new TopicPartitionOffset(topic, 1, 1)
            ]).ConfigureAwait(false);
        }

        // Act - expand from 2 to 4 partitions
        await using var admin = CreateAdminClient();
        await admin.CreatePartitionsAsync(new Dictionary<string, int> { [topic] = 4 }).ConfigureAwait(false);

        // Wait for metadata propagation by polling until partition count is visible
        await WaitForPartitionCountAsync(admin, topic, 4).ConfigureAwait(false);

        // Produce to a new partition
        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "key-new-2",
            Value = "value-new-2",
            Partition = 2
        }).ConfigureAwait(false);

        // Create a new consumer in the same group - it should discover the expanded partitions
        var listener2 = new PartitionTrackingRebalanceListener();
        await using var consumer2 = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer-2")
            .WithGroupId(groupId)
            .WithSessionTimeout(TimeSpan.FromMilliseconds(10000))
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithRebalanceListener(listener2)
            .BuildAsync();

        consumer2.Subscribe(topic);

        // Consume - the new consumer should get assigned all partitions including new ones
        using var cts2 = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result = await consumer2.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts2.Token).ConfigureAwait(false);
        await Assert.That(result).IsNotNull();

        // Assert - consumer should have detected the new partitions
        await Assert.That(listener2.AssignedCallCount).IsGreaterThanOrEqualTo(1);
        // After expansion, the consumer should be assigned more than the original 2 partitions
        await Assert.That(consumer2.Assignment.Count).IsGreaterThanOrEqualTo(3);
    }

    [Test]
    public async Task NewPartitions_AreAssigned_ToConsumersInGroup()
    {
        // Arrange - create topic with 2 partitions
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 2).ConfigureAwait(false);
        var groupId = $"test-group-{Guid.NewGuid():N}";

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer")
            .BuildAsync();

        // Produce messages to original partitions
        for (var p = 0; p < 2; p++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{p}",
                Value = $"value-{p}",
                Partition = p
            }).ConfigureAwait(false);
        }

        // First consumer establishes the group and commits
        await using (var consumer1 = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer-1")
            .WithGroupId(groupId)
            .WithSessionTimeout(TimeSpan.FromMilliseconds(10000))
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithOffsetCommitMode(OffsetCommitMode.Manual)
            .BuildAsync())
        {
            consumer1.Subscribe(topic);

            using var cts1 = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var msg1 = await consumer1.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts1.Token).ConfigureAwait(false);
            await Assert.That(msg1).IsNotNull();
            var msg2 = await consumer1.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts1.Token).ConfigureAwait(false);
            await Assert.That(msg2).IsNotNull();

            // Verify initial assignment was 2 partitions
            await Assert.That(consumer1.Assignment.Count).IsEqualTo(2);
        }

        // Act - expand from 2 to 4 partitions
        await using var admin = CreateAdminClient();
        await admin.CreatePartitionsAsync(new Dictionary<string, int> { [topic] = 4 }).ConfigureAwait(false);

        // Wait for metadata propagation by polling until partition count is visible
        await WaitForPartitionCountAsync(admin, topic, 4).ConfigureAwait(false);

        // Produce to new partitions
        for (var p = 2; p < 4; p++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-new-{p}",
                Value = $"value-new-{p}",
                Partition = p
            }).ConfigureAwait(false);
        }

        // New consumer joining the same group should get all 4 partitions
        var listener = new PartitionTrackingRebalanceListener();
        await using var consumer2 = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer-2")
            .WithGroupId(groupId)
            .WithSessionTimeout(TimeSpan.FromMilliseconds(10000))
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithRebalanceListener(listener)
            .BuildAsync();

        consumer2.Subscribe(topic);

        // Consume messages - should get messages from new partitions
        using var cts2 = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var newPartitionMessages = new List<ConsumeResult<string, string>>();

        try
        {
            while (!cts2.Token.IsCancellationRequested)
            {
                var result = await consumer2.ConsumeOneAsync(TimeSpan.FromSeconds(15), cts2.Token).ConfigureAwait(false);
                if (result is { } r)
                {
                    if (r.Partition >= 2)
                    {
                        newPartitionMessages.Add(r);
                    }
                }

                if (newPartitionMessages.Count >= 2) break;
            }
        }
        catch (OperationCanceledException)
        {
            // Timeout
        }

        // Assert - new partitions should be assigned and messages consumed
        await Assert.That(consumer2.Assignment.Count).IsEqualTo(4);
        await Assert.That(newPartitionMessages.Count).IsEqualTo(2);
    }

    [Test]
    public async Task MessagesToNewPartitions_AreConsumed_AfterRebalance()
    {
        // Arrange - create topic with 2 partitions
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 2).ConfigureAwait(false);
        var groupId = $"test-group-{Guid.NewGuid():N}";

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer")
            .BuildAsync();

        // Produce initial messages
        for (var p = 0; p < 2; p++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{p}",
                Value = $"value-original-{p}",
                Partition = p
            }).ConfigureAwait(false);
        }

        // First consumer consumes and commits original messages
        await using (var consumer1 = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer-1")
            .WithGroupId(groupId)
            .WithSessionTimeout(TimeSpan.FromMilliseconds(10000))
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithOffsetCommitMode(OffsetCommitMode.Manual)
            .BuildAsync())
        {
            consumer1.Subscribe(topic);

            using var cts1 = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var msg1 = await consumer1.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts1.Token).ConfigureAwait(false);
            await Assert.That(msg1).IsNotNull();
            var msg2 = await consumer1.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts1.Token).ConfigureAwait(false);
            await Assert.That(msg2).IsNotNull();

            await consumer1.CommitAsync([
                new TopicPartitionOffset(topic, 0, 1),
                new TopicPartitionOffset(topic, 1, 1)
            ]).ConfigureAwait(false);
        }

        // Act - expand from 2 to 4 partitions
        await using var admin = CreateAdminClient();
        await admin.CreatePartitionsAsync(new Dictionary<string, int> { [topic] = 4 }).ConfigureAwait(false);

        // Wait for metadata propagation by polling until partition count is visible
        await WaitForPartitionCountAsync(admin, topic, 4).ConfigureAwait(false);

        // Produce messages to new partitions with distinct values
        var expectedNewValues = new HashSet<string>();
        for (var p = 2; p < 4; p++)
        {
            var value = $"value-expanded-{p}";
            expectedNewValues.Add(value);
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-expanded-{p}",
                Value = value,
                Partition = p
            }).ConfigureAwait(false);
        }

        // New consumer should consume messages from new partitions
        await using var consumer2 = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer-2")
            .WithGroupId(groupId)
            .WithSessionTimeout(TimeSpan.FromMilliseconds(10000))
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        consumer2.Subscribe(topic);

        var newPartitionValues = new HashSet<string>();
        using var cts2 = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        try
        {
            while (!cts2.Token.IsCancellationRequested)
            {
                var result = await consumer2.ConsumeOneAsync(TimeSpan.FromSeconds(15), cts2.Token).ConfigureAwait(false);
                if (result is { } r && r.Partition >= 2)
                {
                    newPartitionValues.Add(r.Value);
                }

                if (newPartitionValues.Count >= 2) break;
            }
        }
        catch (OperationCanceledException)
        {
            // Timeout
        }

        // Assert - messages from new partitions should be consumed
        await Assert.That(newPartitionValues.Count).IsEqualTo(2);
        foreach (var expected in expectedNewValues)
        {
            await Assert.That(newPartitionValues).Contains(expected);
        }
    }

    [Test]
    public async Task ManualAssignment_DoesNotAutoDetect_NewPartitions()
    {
        // Arrange - create topic with 2 partitions
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 2).ConfigureAwait(false);

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer")
            .BuildAsync();

        // Produce to original partitions
        for (var p = 0; p < 2; p++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{p}",
                Value = $"value-{p}",
                Partition = p
            }).ConfigureAwait(false);
        }

        // Manually assign only partition 0 and 1 (no group ID, no subscription)
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        consumer.Assign(
            new TopicPartition(topic, 0),
            new TopicPartition(topic, 1));

        // Consume initial messages
        using var cts1 = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var msg1 = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts1.Token).ConfigureAwait(false);
        await Assert.That(msg1).IsNotNull();
        var msg2 = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts1.Token).ConfigureAwait(false);
        await Assert.That(msg2).IsNotNull();

        // Act - expand from 2 to 4 partitions
        await using var admin = CreateAdminClient();
        await admin.CreatePartitionsAsync(new Dictionary<string, int> { [topic] = 4 }).ConfigureAwait(false);

        // Wait for metadata propagation by polling until partition count is visible
        await WaitForPartitionCountAsync(admin, topic, 4).ConfigureAwait(false);

        // Produce to the new partition
        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "key-new",
            Value = "value-new",
            Partition = 2
        }).ConfigureAwait(false);

        // Wait briefly for any potential auto-detection
        await WaitForConditionAsync(
            () => Task.FromResult(consumer.Assignment.Count),
            count => count > 2,
            maxRetries: 3,
            initialDelayMs: 500).ConfigureAwait(false);

        // Assert - manual assignment should NOT auto-detect new partitions
        // The consumer should still only be assigned partitions 0 and 1
        var assignment = consumer.Assignment;
        await Assert.That(assignment).Count().IsEqualTo(2);
        await Assert.That(assignment).Contains(new TopicPartition(topic, 0));
        await Assert.That(assignment).Contains(new TopicPartition(topic, 1));

        // Try consuming - should not get messages from partition 2
        var partition2Messages = new List<ConsumeResult<string, string>>();
        using var cts2 = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        try
        {
            while (!cts2.Token.IsCancellationRequested)
            {
                var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(3), cts2.Token).ConfigureAwait(false);
                if (result is { } r && r.Partition == 2)
                {
                    partition2Messages.Add(r);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected - timeout because no messages from partition 2
        }

        await Assert.That(partition2Messages).Count().IsEqualTo(0);
    }

    [Test]
    public async Task PatternSubscription_PicksUpNewPartitions()
    {
        // Arrange - create topic with a unique prefix and 2 partitions
        var topicPrefix = $"pattern-expand-{Guid.NewGuid():N}";
        var topic = $"{topicPrefix}-main";
        await KafkaContainer.CreateTopicAsync(topic, partitions: 2).ConfigureAwait(false);

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer")
            .BuildAsync();

        // Produce to original partitions
        for (var p = 0; p < 2; p++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{p}",
                Value = $"value-{p}",
                Partition = p
            }).ConfigureAwait(false);
        }

        var groupId = $"test-group-{Guid.NewGuid():N}";

        // First consumer with pattern subscription consumes initial messages
        await using (var consumer1 = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer-1")
            .WithGroupId(groupId)
            .WithSessionTimeout(TimeSpan.FromMilliseconds(10000))
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithOffsetCommitMode(OffsetCommitMode.Manual)
            .BuildAsync())
        {
            consumer1.Subscribe(t => t.StartsWith(topicPrefix, StringComparison.Ordinal));

            using var cts1 = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var msg1 = await consumer1.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts1.Token).ConfigureAwait(false);
            await Assert.That(msg1).IsNotNull();
            var msg2 = await consumer1.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts1.Token).ConfigureAwait(false);
            await Assert.That(msg2).IsNotNull();

            await consumer1.CommitAsync([
                new TopicPartitionOffset(topic, 0, 1),
                new TopicPartitionOffset(topic, 1, 1)
            ]).ConfigureAwait(false);
        }

        // Act - expand the topic from 2 to 4 partitions
        await using var admin = CreateAdminClient();
        await admin.CreatePartitionsAsync(new Dictionary<string, int> { [topic] = 4 }).ConfigureAwait(false);

        // Wait for metadata propagation by polling until partition count is visible
        await WaitForPartitionCountAsync(admin, topic, 4).ConfigureAwait(false);

        // Produce to new partitions
        for (var p = 2; p < 4; p++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-expanded-{p}",
                Value = $"value-expanded-{p}",
                Partition = p
            }).ConfigureAwait(false);
        }

        // New consumer with pattern subscription should discover new partitions
        await using var consumer2 = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer-2")
            .WithGroupId(groupId)
            .WithSessionTimeout(TimeSpan.FromMilliseconds(10000))
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        consumer2.Subscribe(t => t.StartsWith(topicPrefix, StringComparison.Ordinal));

        var newPartitionMessages = new List<ConsumeResult<string, string>>();
        using var cts2 = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        try
        {
            while (!cts2.Token.IsCancellationRequested)
            {
                var result = await consumer2.ConsumeOneAsync(TimeSpan.FromSeconds(15), cts2.Token).ConfigureAwait(false);
                if (result is { } r && r.Partition >= 2)
                {
                    newPartitionMessages.Add(r);
                }

                if (newPartitionMessages.Count >= 2) break;
            }
        }
        catch (OperationCanceledException)
        {
            // Timeout
        }

        // Assert - pattern subscription should discover new partitions and consume messages from them
        await Assert.That(newPartitionMessages.Count).IsGreaterThanOrEqualTo(1);
    }

    private sealed class PartitionTrackingRebalanceListener : IRebalanceListener
    {
        private int _assignedCount;
        private int _revokedCount;
        private readonly ConcurrentBag<TopicPartition> _allAssignedPartitions = [];

        public int AssignedCallCount => _assignedCount;
        public int RevokedCallCount => _revokedCount;
        public IReadOnlyCollection<TopicPartition> AllAssignedPartitions => _allAssignedPartitions;

        public ValueTask OnPartitionsAssignedAsync(IEnumerable<TopicPartition> partitions, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _assignedCount);
            foreach (var partition in partitions)
            {
                _allAssignedPartitions.Add(partition);
            }
            return ValueTask.CompletedTask;
        }

        public ValueTask OnPartitionsRevokedAsync(IEnumerable<TopicPartition> partitions, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _revokedCount);
            return ValueTask.CompletedTask;
        }

        public ValueTask OnPartitionsLostAsync(IEnumerable<TopicPartition> partitions, CancellationToken cancellationToken)
        {
            return ValueTask.CompletedTask;
        }
    }
}
