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
[Category("ConsumerGroup")]
public sealed class MultiMemberConsumerGroupTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    [Test]
    public async Task ThreeConsumers_SixPartitions_EvenDistribution()
    {
        // Arrange
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 6);
        var groupId = $"test-group-{Guid.NewGuid():N}";

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer-multi-group")
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        // Produce one message per partition so consumers have data to fetch
        for (var p = 0; p < 6; p++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{p}",
                Value = $"value-{p}",
                Partition = p
            }, CancellationToken.None);
        }

        // Act - start 3 consumers sequentially, waiting for each to get assignments
        var consumers = new List<IKafkaConsumer<string, string>>();
        try
        {
            for (var i = 0; i < 3; i++)
            {
                var consumer = await Kafka.CreateConsumer<string, string>()
                    .WithBootstrapServers(KafkaContainer.BootstrapServers)
                    .WithClientId($"test-consumer-{i}")
                    .WithGroupId(groupId)
                    .WithSessionTimeout(TimeSpan.FromMilliseconds(10000))
                    .WithAutoOffsetReset(AutoOffsetReset.Earliest)
                    .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory()).BuildAsync();

                consumer.Subscribe(topic);
                consumers.Add(consumer);

                // Consume one message to trigger group join and stabilize
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(20), cts.Token);
            }

            // Wait for cooperative rebalance to settle (may require multiple rounds)
            foreach (var consumer in consumers)
            {
                await Assert.That(() => consumer.Assignment.ToArray().Count(tp => tp.Topic == topic))
                    .Eventually(x => x.IsEqualTo(2), TimeSpan.FromSeconds(30));
            }

            // Assert - all 6 partitions should be covered with no overlap
            var allAssignedPartitions = new HashSet<int>();
            foreach (var consumer in consumers)
            {
                foreach (var tp in consumer.Assignment.ToArray().Where(tp => tp.Topic == topic))
                {
                    allAssignedPartitions.Add(tp.Partition);
                }
            }

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

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer-join-midstream")
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        for (var p = 0; p < 4; p++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{p}",
                Value = $"value-{p}",
                Partition = p
            }, CancellationToken.None);
        }

        // Start first consumer - should get all 4 partitions
        await using var consumer1 = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer-1")
            .WithGroupId(groupId)
            .WithSessionTimeout(TimeSpan.FromMilliseconds(10000))
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory()).BuildAsync();

        consumer1.Subscribe(topic);

        using var cts1 = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await consumer1.ConsumeOneAsync(TimeSpan.FromSeconds(20), cts1.Token);

        // Wait for group to stabilize and assignment to be fully reflected
        await Task.Delay(3000).ConfigureAwait(false);

        var initialAssignment = consumer1.Assignment.ToArray().Where(tp => tp.Topic == topic).ToList();
        await Assert.That(initialAssignment.Count).IsEqualTo(4);

        // Act - start second consumer, triggering rebalance
        await using var consumer2 = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer-2")
            .WithGroupId(groupId)
            .WithSessionTimeout(TimeSpan.FromMilliseconds(10000))
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory()).BuildAsync();

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
            }, CancellationToken.None);
        }

        // Consumer 2 needs to consume to trigger group join
        using var cts2 = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await consumer2.ConsumeOneAsync(TimeSpan.FromSeconds(20), cts2.Token);

        // Wait for cooperative rebalance to settle (may require two rounds)
        await Assert.That(() => consumer1.Assignment.ToArray().Count(tp => tp.Topic == topic))
            .Eventually(x => x.IsEqualTo(2), TimeSpan.FromSeconds(30));
        await Assert.That(() => consumer2.Assignment.ToArray().Count(tp => tp.Topic == topic))
            .Eventually(x => x.IsEqualTo(2), TimeSpan.FromSeconds(30));

        // No overlap
        var allPartitions = consumer1.Assignment.ToArray().Where(tp => tp.Topic == topic).Select(tp => tp.Partition)
            .Concat(consumer2.Assignment.ToArray().Where(tp => tp.Topic == topic).Select(tp => tp.Partition))
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

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer-leave")
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        for (var p = 0; p < 4; p++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{p}",
                Value = $"value-{p}",
                Partition = p
            }, CancellationToken.None);
        }

        // Start two consumers
        await using var consumer1 = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer-stay")
            .WithGroupId(groupId)
            .WithSessionTimeout(TimeSpan.FromMilliseconds(10000))
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory()).BuildAsync();

        consumer1.Subscribe(topic);

        using var cts1 = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await consumer1.ConsumeOneAsync(TimeSpan.FromSeconds(20), cts1.Token);

        var consumer2 = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer-leave")
            .WithGroupId(groupId)
            .WithSessionTimeout(TimeSpan.FromMilliseconds(10000))
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory()).BuildAsync();

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
            }, CancellationToken.None);
        }

        using var cts2 = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await consumer2.ConsumeOneAsync(TimeSpan.FromSeconds(20), cts2.Token);

        // Wait for cooperative rebalance to complete (may require two rounds)
        await Assert.That(() => consumer1.Assignment.ToArray().Count(tp => tp.Topic == topic))
            .Eventually(x => x.IsEqualTo(2), TimeSpan.FromSeconds(30));
        await Assert.That(() => consumer2.Assignment.ToArray().Count(tp => tp.Topic == topic))
            .Eventually(x => x.IsEqualTo(2), TimeSpan.FromSeconds(30));

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
            }, CancellationToken.None);
        }

        // Consumer1 needs to consume to discover the rebalance after consumer2 leaves
        using var cts3 = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try { await consumer1.ConsumeOneAsync(TimeSpan.FromSeconds(20), cts3.Token); }
        catch (OperationCanceledException) { }

        // Wait for consumer1 to pick up all 4 partitions after rebalance
        await Assert.That(() => consumer1.Assignment.ToArray().Count(tp => tp.Topic == topic))
            .Eventually(x => x.IsEqualTo(4), TimeSpan.FromSeconds(30));
    }

    [Test]
    public async Task RebalanceListener_ReceivesAssignedPartitions()
    {
        // Arrange
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 3);
        var groupId = $"test-group-{Guid.NewGuid():N}";
        var listener = new TrackingRebalanceListener();

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer-rebalance-listener")
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        for (var p = 0; p < 3; p++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{p}",
                Value = $"value-{p}",
                Partition = p
            }, CancellationToken.None);
        }

        // Act
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer-rebalance-listener")
            .WithGroupId(groupId)
            .WithSessionTimeout(TimeSpan.FromMilliseconds(10000))
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithRebalanceListener(listener)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory()).BuildAsync();

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

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer-revoke-listener")
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        for (var p = 0; p < 4; p++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{p}",
                Value = $"value-{p}",
                Partition = p
            }, CancellationToken.None);
        }

        // Consumer1 with rebalance listener - gets all 4 partitions initially
        await using var consumer1 = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer-revoke-listener-1")
            .WithGroupId(groupId)
            .WithSessionTimeout(TimeSpan.FromMilliseconds(10000))
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithRebalanceListener(listener)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory()).BuildAsync();

        consumer1.Subscribe(topic);

        using var cts1 = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await consumer1.ConsumeOneAsync(TimeSpan.FromSeconds(20), cts1.Token);

        // Verify assigned was called with all partitions
        await Assert.That(listener.AssignedPartitions.Count).IsGreaterThanOrEqualTo(1);

        // Act - second consumer joins, forcing a rebalance that revokes partitions from consumer1
        await using var consumer2 = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer-revoke-listener-2")
            .WithGroupId(groupId)
            .WithSessionTimeout(TimeSpan.FromMilliseconds(10000))
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory()).BuildAsync();

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
            }, CancellationToken.None);
        }

        // Consumer2 consuming triggers group join and rebalance
        using var cts2 = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await consumer2.ConsumeOneAsync(TimeSpan.FromSeconds(20), cts2.Token);

        // Consumer1 needs to consume to discover and handle the rebalance
        using var cts3 = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try { await consumer1.ConsumeOneAsync(TimeSpan.FromSeconds(20), cts3.Token); }
        catch (OperationCanceledException) { }

        // Wait for revoked callback to fire (cooperative rebalance may take two rounds)
        await Assert.That(() => listener.RevokedPartitions.Count + listener.LostPartitions.Count)
            .Eventually(x => x.IsGreaterThanOrEqualTo(1), TimeSpan.FromSeconds(30));
    }

    [Test]
    public async Task AllConsumersLeave_NewConsumerResumesFromCommitted()
    {
        // Arrange
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var groupId = $"test-group-{Guid.NewGuid():N}";

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer-resume-committed")
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        for (var i = 0; i < 10; i++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{i}",
                Value = $"value-{i}"
            }, CancellationToken.None);
        }

        // First consumer: consume 5 messages and commit
        await using (var consumer1 = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer-first")
            .WithGroupId(groupId)
            .WithSessionTimeout(TimeSpan.FromMilliseconds(10000))
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithOffsetCommitMode(OffsetCommitMode.Manual)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory()).BuildAsync())
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
        await using var consumer2 = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer-resume")
            .WithGroupId(groupId)
            .WithSessionTimeout(TimeSpan.FromMilliseconds(10000))
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithOffsetCommitMode(OffsetCommitMode.Manual)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory()).BuildAsync();

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

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer-no-loss")
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        for (var i = 0; i < messageCount; i++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{i}",
                Value = $"value-{i}"
            }, CancellationToken.None);
        }

        // Act - start 2 consumers, each consuming in background
        var allMessages = new ConcurrentBag<ConsumeResult<string, string>>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

        var consumer1 = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer-no-loss-1")
            .WithGroupId(groupId)
            .WithSessionTimeout(TimeSpan.FromMilliseconds(10000))
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory()).BuildAsync();

        var consumer2 = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer-no-loss-2")
            .WithGroupId(groupId)
            .WithSessionTimeout(TimeSpan.FromMilliseconds(10000))
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory()).BuildAsync();

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
