using System.Collections.Concurrent;
using System.Threading.Channels;
using Dekaf.Consumer;
using Dekaf.Producer;

namespace Dekaf.Tests.Integration.RealWorld;

/// <summary>
/// Integration tests for multi-member consumer group dynamics.
/// Verifies partition distribution, rebalancing on join/leave, rebalance listener callbacks,
/// and group stability across member restarts.
/// </summary>
public sealed class MultiMemberConsumerGroupTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    [Test]
    public async Task ThreeConsumers_SixPartitions_EvenDistribution()
    {
        // Arrange
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 6);
        var groupId = $"test-group-{Guid.NewGuid():N}";

        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer-multi-group")
            .Build();

        // Produce one message per partition so consumers have data to fetch
        for (var p = 0; p < 6; p++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{p}",
                Value = $"value-{p}",
                Partition = p
            });
        }

        // Act - start 3 consumers sequentially, waiting for each to get assignments
        var consumers = new List<IKafkaConsumer<string, string>>();
        try
        {
            for (var i = 0; i < 3; i++)
            {
                var consumer = Kafka.CreateConsumer<string, string>()
                    .WithBootstrapServers(KafkaContainer.BootstrapServers)
                    .WithClientId($"test-consumer-{i}")
                    .WithGroupId(groupId)
                    .WithSessionTimeout(TimeSpan.FromMilliseconds(10000))
                    .WithAutoOffsetReset(AutoOffsetReset.Earliest)
                    .Build();

                consumer.Subscribe(topic);
                consumers.Add(consumer);

                // Consume one message to trigger group join and stabilize
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(20), cts.Token);
            }

            // Wait for group to fully stabilize after all joins
            await Task.Delay(5000).ConfigureAwait(false);

            // Assert - collect all assignments
            var allAssignedPartitions = new HashSet<int>();
            foreach (var consumer in consumers)
            {
                var assignment = consumer.Assignment.Where(tp => tp.Topic == topic).ToList();
                // Each consumer should have exactly 2 partitions
                await Assert.That(assignment.Count).IsEqualTo(2);

                foreach (var tp in assignment)
                {
                    allAssignedPartitions.Add(tp.Partition);
                }
            }

            // All 6 partitions should be covered with no overlap
            await Assert.That(allAssignedPartitions.Count).IsEqualTo(6);
        }
        finally
        {
            foreach (var consumer in consumers)
            {
                await consumer.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    [Test]
    public async Task ConsumerJoinsMidStream_PartitionsRedistributed()
    {
        // Arrange
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 4);
        var groupId = $"test-group-{Guid.NewGuid():N}";

        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer-join-midstream")
            .Build();

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

        // Start first consumer - should get all 4 partitions
        await using var consumer1 = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer-1")
            .WithGroupId(groupId)
            .WithSessionTimeout(TimeSpan.FromMilliseconds(10000))
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .Build();

        consumer1.Subscribe(topic);

        using var cts1 = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await consumer1.ConsumeOneAsync(TimeSpan.FromSeconds(20), cts1.Token);

        // Wait for group to stabilize and assignment to be fully reflected
        await Task.Delay(3000).ConfigureAwait(false);

        var initialAssignment = consumer1.Assignment.Where(tp => tp.Topic == topic).ToList();
        await Assert.That(initialAssignment.Count).IsEqualTo(4);

        // Act - start second consumer, triggering rebalance
        await using var consumer2 = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer-2")
            .WithGroupId(groupId)
            .WithSessionTimeout(TimeSpan.FromMilliseconds(10000))
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .Build();

        consumer2.Subscribe(topic);

        // Produce more messages to trigger fetching and rebalance
        for (var p = 0; p < 4; p++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-extra-{p}",
                Value = $"value-extra-{p}",
                Partition = p
            });
        }

        // Consumer 2 needs to consume to trigger group join
        using var cts2 = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await consumer2.ConsumeOneAsync(TimeSpan.FromSeconds(20), cts2.Token);

        // Wait for rebalance to stabilize
        await Task.Delay(5000).ConfigureAwait(false);

        // Assert - partitions should be redistributed
        var c1Assignment = consumer1.Assignment.Where(tp => tp.Topic == topic).ToList();
        var c2Assignment = consumer2.Assignment.Where(tp => tp.Topic == topic).ToList();

        // Both consumers should have partitions (2 each for even distribution)
        await Assert.That(c1Assignment.Count).IsEqualTo(2);
        await Assert.That(c2Assignment.Count).IsEqualTo(2);

        // No overlap
        var allPartitions = c1Assignment.Select(tp => tp.Partition)
            .Concat(c2Assignment.Select(tp => tp.Partition))
            .Distinct()
            .ToList();
        await Assert.That(allPartitions.Count).IsEqualTo(4);
    }

    [Test]
    public async Task ConsumerLeaves_RemainingPicksUpPartitions()
    {
        // Arrange
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 4);
        var groupId = $"test-group-{Guid.NewGuid():N}";

        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer-leave")
            .Build();

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

        // Start two consumers
        await using var consumer1 = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer-stay")
            .WithGroupId(groupId)
            .WithSessionTimeout(TimeSpan.FromMilliseconds(10000))
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .Build();

        consumer1.Subscribe(topic);

        using var cts1 = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await consumer1.ConsumeOneAsync(TimeSpan.FromSeconds(20), cts1.Token);

        var consumer2 = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer-leave")
            .WithGroupId(groupId)
            .WithSessionTimeout(TimeSpan.FromMilliseconds(10000))
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .Build();

        consumer2.Subscribe(topic);

        // Produce more messages for consumer2 to fetch
        for (var p = 0; p < 4; p++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-more-{p}",
                Value = $"value-more-{p}",
                Partition = p
            });
        }

        using var cts2 = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await consumer2.ConsumeOneAsync(TimeSpan.FromSeconds(20), cts2.Token);

        await Task.Delay(3000).ConfigureAwait(false);

        // Both should have 2 partitions each
        var c1Before = consumer1.Assignment.Where(tp => tp.Topic == topic).Count();
        var c2Before = consumer2.Assignment.Where(tp => tp.Topic == topic).Count();
        await Assert.That(c1Before).IsEqualTo(2);
        await Assert.That(c2Before).IsEqualTo(2);

        // Act - consumer2 leaves
        await consumer2.DisposeAsync().ConfigureAwait(false);

        // Produce more messages to trigger consumer1 to rebalance
        for (var p = 0; p < 4; p++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-after-{p}",
                Value = $"value-after-{p}",
                Partition = p
            });
        }

        // Wait for session timeout to expire and rebalance to complete
        await Task.Delay(15000).ConfigureAwait(false);

        // Consumer1 needs to consume to discover the rebalance
        using var cts3 = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await consumer1.ConsumeOneAsync(TimeSpan.FromSeconds(20), cts3.Token);

        await Task.Delay(3000).ConfigureAwait(false);

        // Assert - consumer1 should now have all 4 partitions
        var c1After = consumer1.Assignment.Where(tp => tp.Topic == topic).Count();
        await Assert.That(c1After).IsEqualTo(4);
    }

    [Test]
    public async Task RebalanceListener_ReceivesAssignedPartitions()
    {
        // Arrange
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 3);
        var groupId = $"test-group-{Guid.NewGuid():N}";
        var listener = new TrackingRebalanceListener();

        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer-rebalance-listener")
            .Build();

        for (var p = 0; p < 3; p++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{p}",
                Value = $"value-{p}",
                Partition = p
            });
        }

        // Act
        await using var consumer = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer-rebalance-listener")
            .WithGroupId(groupId)
            .WithSessionTimeout(TimeSpan.FromMilliseconds(10000))
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithRebalanceListener(listener)
            .Build();

        consumer.Subscribe(topic);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(20), cts.Token);

        // Assert - listener should have been called with assigned partitions
        await Assert.That(listener.AssignedPartitions.Count).IsGreaterThanOrEqualTo(1);

        var assignedForTopic = listener.AssignedPartitions
            .SelectMany(batch => batch)
            .Where(tp => tp.Topic == topic)
            .Select(tp => tp.Partition)
            .Distinct()
            .OrderBy(p => p)
            .ToList();

        // Single consumer should be assigned all 3 partitions
        await Assert.That(assignedForTopic.Count).IsEqualTo(3);
    }

    [Test]
    public async Task RebalanceListener_ReceivesRevokedOnRebalance()
    {
        // Arrange - use 4 partitions so the second consumer forces revocation of some
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 4);
        var groupId = $"test-group-{Guid.NewGuid():N}";
        var listener = new TrackingRebalanceListener();

        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer-revoke-listener")
            .Build();

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

        // Consumer1 with rebalance listener - gets all 4 partitions initially
        await using var consumer1 = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer-revoke-listener-1")
            .WithGroupId(groupId)
            .WithSessionTimeout(TimeSpan.FromMilliseconds(10000))
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithRebalanceListener(listener)
            .Build();

        consumer1.Subscribe(topic);

        using var cts1 = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await consumer1.ConsumeOneAsync(TimeSpan.FromSeconds(20), cts1.Token);

        // Verify assigned was called with all partitions
        await Assert.That(listener.AssignedPartitions.Count).IsGreaterThanOrEqualTo(1);

        // Act - second consumer joins, forcing a rebalance that revokes partitions from consumer1
        await using var consumer2 = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer-revoke-listener-2")
            .WithGroupId(groupId)
            .WithSessionTimeout(TimeSpan.FromMilliseconds(10000))
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .Build();

        consumer2.Subscribe(topic);

        // Produce more messages so consumers have data to trigger rebalance handling
        for (var p = 0; p < 4; p++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-extra-{p}",
                Value = $"value-extra-{p}",
                Partition = p
            });
        }

        // Consumer2 consuming triggers group join and rebalance
        using var cts2 = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await consumer2.ConsumeOneAsync(TimeSpan.FromSeconds(20), cts2.Token);

        // Consumer1 needs to consume to discover and handle the rebalance
        using var cts3 = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await consumer1.ConsumeOneAsync(TimeSpan.FromSeconds(20), cts3.Token);

        // Wait for rebalance to fully stabilize
        await Task.Delay(5000).ConfigureAwait(false);

        // Assert - revoked should have been called as partitions were taken from consumer1
        // With CooperativeSticky, the rebalance revokes some partitions from consumer1
        var totalRevokedOrLost = listener.RevokedPartitions.Count + listener.LostPartitions.Count;
        await Assert.That(totalRevokedOrLost).IsGreaterThanOrEqualTo(1);
    }

    [Test]
    public async Task AllConsumersLeave_NewConsumerResumesFromCommitted()
    {
        // Arrange
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var groupId = $"test-group-{Guid.NewGuid():N}";

        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer-resume-committed")
            .Build();

        for (var i = 0; i < 10; i++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{i}",
                Value = $"value-{i}"
            });
        }

        // First consumer: consume 5 messages and commit
        await using (var consumer1 = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer-first")
            .WithGroupId(groupId)
            .WithSessionTimeout(TimeSpan.FromMilliseconds(10000))
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithOffsetCommitMode(OffsetCommitMode.Manual)
            .Build())
        {
            consumer1.Subscribe(topic);

            var consumed = new List<ConsumeResult<string, string>>();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

            await foreach (var msg in consumer1.ConsumeAsync(cts.Token))
            {
                consumed.Add(msg);
                if (consumed.Count >= 5) break;
            }

            await consumer1.CommitAsync([new TopicPartitionOffset(topic, 0, 5)]);
        }

        // All consumers have left - group is empty

        // Act - new consumer joins with same group ID
        await using var consumer2 = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer-resume")
            .WithGroupId(groupId)
            .WithSessionTimeout(TimeSpan.FromMilliseconds(10000))
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithOffsetCommitMode(OffsetCommitMode.Manual)
            .Build();

        consumer2.Subscribe(topic);

        using var cts2 = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result = await consumer2.ConsumeOneAsync(TimeSpan.FromSeconds(20), cts2.Token);

        // Assert - should resume from committed offset 5 (not from beginning)
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value.Offset).IsEqualTo(5);
        await Assert.That(result.Value.Value).IsEqualTo("value-5");
    }

    [Test]
    public async Task MultipleConsumers_ConsumeAllMessages_NoLoss()
    {
        // Arrange
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 4);
        var groupId = $"test-group-{Guid.NewGuid():N}";
        const int messageCount = 100;

        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer-no-loss")
            .Build();

        for (var i = 0; i < messageCount; i++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{i}",
                Value = $"value-{i}"
            });
        }

        // Act - start 2 consumers, each consuming in background
        var allMessages = new ConcurrentBag<ConsumeResult<string, string>>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

        var consumer1 = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer-no-loss-1")
            .WithGroupId(groupId)
            .WithSessionTimeout(TimeSpan.FromMilliseconds(10000))
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .Build();

        var consumer2 = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer-no-loss-2")
            .WithGroupId(groupId)
            .WithSessionTimeout(TimeSpan.FromMilliseconds(10000))
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .Build();

        Task task1 = Task.CompletedTask, task2 = Task.CompletedTask;
        try
        {
            consumer1.Subscribe(topic);
            consumer2.Subscribe(topic);

            task1 = ConsumeUntilCountReached(consumer1, allMessages, messageCount, cts.Token);
            task2 = ConsumeUntilCountReached(consumer2, allMessages, messageCount, cts.Token);

            await Task.WhenAny(
                Task.WhenAll(task1, task2),
                Task.Delay(TimeSpan.FromSeconds(55), cts.Token)
            ).ConfigureAwait(false);
        }
        finally
        {
            // Cancel first to signal consume loops to stop before disposing
            await cts.CancelAsync();

            await consumer1.DisposeAsync().ConfigureAwait(false);
            await consumer2.DisposeAsync().ConfigureAwait(false);

            // Observe any exceptions from the consume tasks to prevent unobserved task exceptions
            try { await Task.WhenAll(task1, task2); }
            catch { /* Already handled within ConsumeUntilCountReached */ }
        }

        // Assert - all messages should be consumed (no loss, no duplicates within same partition)
        await Assert.That(allMessages.Count).IsGreaterThanOrEqualTo(messageCount);

        var uniqueValues = allMessages.Select(m => m.Value).Distinct().ToList();
        await Assert.That(uniqueValues.Count).IsEqualTo(messageCount);
    }

    private static async Task ConsumeUntilCountReached(
        IKafkaConsumer<string, string> consumer,
        ConcurrentBag<ConsumeResult<string, string>> results,
        int targetCount,
        CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var msg in consumer.ConsumeAsync(cancellationToken).ConfigureAwait(false))
            {
                results.Add(msg);
                if (results.Count >= targetCount) break;
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when target reached by other consumer
        }
        catch (ChannelClosedException)
        {
            // Expected when consumer is disposed while still iterating
        }
    }

    private sealed class TrackingRebalanceListener : IRebalanceListener
    {
        public ConcurrentBag<List<TopicPartition>> AssignedPartitions { get; } = [];
        public ConcurrentBag<List<TopicPartition>> RevokedPartitions { get; } = [];
        public ConcurrentBag<List<TopicPartition>> LostPartitions { get; } = [];

        public ValueTask OnPartitionsAssignedAsync(IEnumerable<TopicPartition> partitions, CancellationToken cancellationToken)
        {
            AssignedPartitions.Add(partitions.ToList());
            return ValueTask.CompletedTask;
        }

        public ValueTask OnPartitionsRevokedAsync(IEnumerable<TopicPartition> partitions, CancellationToken cancellationToken)
        {
            RevokedPartitions.Add(partitions.ToList());
            return ValueTask.CompletedTask;
        }

        public ValueTask OnPartitionsLostAsync(IEnumerable<TopicPartition> partitions, CancellationToken cancellationToken)
        {
            LostPartitions.Add(partitions.ToList());
            return ValueTask.CompletedTask;
        }
    }
}
