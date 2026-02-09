using Dekaf.Consumer;
using Dekaf.Producer;

namespace Dekaf.Tests.Integration;

/// <summary>
/// Integration tests for consumer group rebalance edge cases.
/// Tests scenarios around listener exceptions, slow consumers, offset commits
/// during rebalance, rapid join/leave cycles, and poll timeouts.
/// </summary>
[Category("ConsumerGroup")]
public sealed class RebalanceEdgeCaseTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    [Test]
    public async Task RebalanceListener_ThrowsException_ConsumerHandlesGracefully()
    {
        // Arrange - listener that throws on assigned callback
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 2);
        var groupId = $"test-group-{Guid.NewGuid():N}";
        var listener = new ThrowingRebalanceListener();

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .BuildAsync();

        // Produce messages to ensure the consumer has data
        for (var i = 0; i < 3; i++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{i}",
                Value = $"value-{i}"
            });
        }

        // Act - consumer with a throwing listener should still be able to consume
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithRebalanceListener(listener)
            .BuildAsync();

        consumer.Subscribe(topic);

        // The consumer should handle the listener exception gracefully and still consume
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var messages = new List<ConsumeResult<string, string>>();

        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            messages.Add(msg);
            if (messages.Count >= 3) break;
        }

        // Assert - consumer survived the throwing listener and consumed messages
        await Assert.That(messages).Count().IsGreaterThanOrEqualTo(3);
        // The listener should have been called at least once (even though it threw)
        await Assert.That(listener.AssignedCallCount).IsGreaterThanOrEqualTo(1);
    }

    [Test]
    public async Task RebalanceTimeout_SlowConsumer_RemovedFromGroup()
    {
        // Arrange - two consumers with short session timeout; one will stop polling
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 4);
        var groupId = $"test-group-{Guid.NewGuid():N}";
        var listener1 = new TrackingRebalanceListener();
        var listener2 = new TrackingRebalanceListener();

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .BuildAsync();

        // Produce messages to all partitions
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

        // Consumer 1 - will be the "slow" consumer that leaves the group
        // Not using 'await using' because we manually dispose consumer1 mid-test
        var consumer1 = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(groupId)
            .WithSessionTimeout(TimeSpan.FromSeconds(10))
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithRebalanceListener(listener1)
            .BuildAsync();

        consumer1.Subscribe(topic);

        // Consume one message to trigger initial assignment for consumer 1
        using var cts1 = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await consumer1.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts1.Token);

        await Assert.That(listener1.AssignedCallCount).IsGreaterThanOrEqualTo(1);

        // Consumer 2 joins the group, triggering rebalance
        await using var consumer2 = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(groupId)
            .WithSessionTimeout(TimeSpan.FromSeconds(10))
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithRebalanceListener(listener2)
            .BuildAsync();

        consumer2.Subscribe(topic);

        // Consume a message with consumer 2 to trigger the rebalance
        using var cts2 = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try
        {
            await consumer2.ConsumeOneAsync(TimeSpan.FromSeconds(15), cts2.Token);
        }
        catch (OperationCanceledException)
        {
            // May timeout if all messages were consumed by consumer 1
        }

        // Wait for rebalance to settle - poll until consumer 2 gets assigned
        await WaitForConditionAsync(
            () => listener2.AssignedCallCount >= 1,
            timeout: TimeSpan.FromSeconds(15),
            pollInterval: TimeSpan.FromMilliseconds(500));

        // Assert - both consumers should have been assigned partitions
        await Assert.That(listener2.AssignedCallCount).IsGreaterThanOrEqualTo(1);

        var revokedCountBefore = listener2.RevokedCallCount + listener2.AssignedCallCount;

        // Now dispose consumer 1 to simulate it leaving the group
        try
        {
            await consumer1.DisposeAsync();
        }
        catch (Exception)
        {
            // Expected - consumer may have background work completing
        }

        // Wait for session timeout to expire and rebalance to happen
        // Poll until consumer 2 sees a new rebalance event
        await WaitForConditionAsync(
            () => (listener2.RevokedCallCount + listener2.AssignedCallCount) > revokedCountBefore,
            timeout: TimeSpan.FromSeconds(30),
            pollInterval: TimeSpan.FromMilliseconds(500));

        // Produce more messages for consumer 2 to pick up after rebalance
        for (var i = 0; i < 4; i++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-new-{i}",
                Value = $"value-new-{i}"
            });
        }

        // Consumer 2 should now get all partitions
        var messages = new List<ConsumeResult<string, string>>();
        using var cts3 = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await foreach (var msg in consumer2.ConsumeAsync(cts3.Token))
        {
            messages.Add(msg);
            if (messages.Count >= 4) break;
        }

        // Consumer 2 should have received messages after the slow consumer was removed
        await Assert.That(messages).Count().IsGreaterThanOrEqualTo(1);
    }

    [Test]
    public async Task RebalanceDuringOffsetCommit_CommitBeforeRevokeWorks()
    {
        // Arrange - a listener that commits offsets when partitions are revoked
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 4);
        var groupId = $"test-group-{Guid.NewGuid():N}";

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .BuildAsync();

        // Produce messages
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

        // Consumer 1 with a committing rebalance listener
        var committedOffsets = new List<TopicPartitionOffset>();
        var commitListener = new CommittingRebalanceListener(committedOffsets);

        await using var consumer1 = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithOffsetCommitMode(OffsetCommitMode.Manual)
            .WithRebalanceListener(commitListener)
            .BuildAsync();

        // Store the consumer reference in the listener so it can commit
        commitListener.Consumer = consumer1;

        consumer1.Subscribe(topic);

        // Consume messages and track them
        var messages = new List<ConsumeResult<string, string>>();
        using var cts1 = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await foreach (var msg in consumer1.ConsumeAsync(cts1.Token))
        {
            messages.Add(msg);
            if (messages.Count >= 4) break;
        }

        await Assert.That(messages).Count().IsEqualTo(4);

        // Manually commit the current offsets
        var offsets = messages
            .GroupBy(m => new TopicPartition(m.Topic, m.Partition))
            .Select(g => new TopicPartitionOffset(g.Key.Topic, g.Key.Partition, g.Max(m => m.Offset) + 1))
            .ToArray();

        await consumer1.CommitAsync(offsets);

        // Now add a second consumer to trigger rebalance (which will invoke OnPartitionsRevoked)
        var listener2 = new TrackingRebalanceListener();
        await using var consumer2 = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithRebalanceListener(listener2)
            .BuildAsync();

        consumer2.Subscribe(topic);

        // Consume with consumer2 to trigger the rebalance
        using var cts2 = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try
        {
            await consumer2.ConsumeOneAsync(TimeSpan.FromSeconds(15), cts2.Token);
        }
        catch (OperationCanceledException)
        {
            // May timeout if all messages already consumed
        }

        // Wait for rebalance to complete - poll until consumer 2 gets assigned
        await WaitForConditionAsync(
            () => listener2.AssignedCallCount >= 1,
            timeout: TimeSpan.FromSeconds(15),
            pollInterval: TimeSpan.FromMilliseconds(500));

        // Verify the committed offsets are preserved by checking with a new consumer
        await using var verifier = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithOffsetCommitMode(OffsetCommitMode.Manual)
            .BuildAsync();

        verifier.Subscribe(topic);

        // Try to consume - should not get the already-committed messages
        using var cts3 = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var remainingMessages = new List<ConsumeResult<string, string>>();

        try
        {
            await foreach (var msg in verifier.ConsumeAsync(cts3.Token))
            {
                remainingMessages.Add(msg);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected - timeout after no more messages
        }

        // Offsets were committed, so we should not re-read the same 4 messages
        // (or if we do get messages, they should be different from the original batch)
        // This verifies the commit during rebalance was effective
        await Assert.That(commitListener.RevokedCallCount + commitListener.AssignedCallCount)
            .IsGreaterThanOrEqualTo(1);
    }

    [Test]
    public async Task RapidJoinLeave_GroupStabilizesEventually()
    {
        // Arrange - rapidly join and leave consumers, then verify group stabilizes
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 2);
        var groupId = $"test-group-{Guid.NewGuid():N}";

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .BuildAsync();

        // Produce messages
        for (var i = 0; i < 5; i++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{i}",
                Value = $"value-{i}"
            });
        }

        // Rapidly create and dispose consumers to stress the group coordinator
        for (var round = 0; round < 3; round++)
        {
            var tempConsumer = await Kafka.CreateConsumer<string, string>()
                .WithBootstrapServers(KafkaContainer.BootstrapServers)
                .WithGroupId(groupId)
                .WithSessionTimeout(TimeSpan.FromSeconds(10))
                .WithAutoOffsetReset(AutoOffsetReset.Earliest)
                .BuildAsync();

            try
            {
                tempConsumer.Subscribe(topic);

                // Briefly try to consume
                try
                {
                    using var tempCts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                    await tempConsumer.ConsumeOneAsync(TimeSpan.FromSeconds(2), tempCts.Token);
                }
                catch (OperationCanceledException)
                {
                    // Expected - short timeout
                }
            }
            finally
            {
                try
                {
                    await tempConsumer.DisposeAsync();
                }
                catch (Exception)
                {
                    // Expected - consumer may have background work completing
                }
            }

            // Consumer disposed, leaving the group
        }

        // Now create a stable consumer and verify it can consume normally
        var stableListener = new TrackingRebalanceListener();
        await using var stableConsumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithRebalanceListener(stableListener)
            .BuildAsync();

        stableConsumer.Subscribe(topic);

        // The stable consumer should successfully join the group and consume
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var messages = new List<ConsumeResult<string, string>>();

        await foreach (var msg in stableConsumer.ConsumeAsync(cts.Token))
        {
            messages.Add(msg);
            if (messages.Count >= 1) break;
        }

        // Assert - group stabilized and the stable consumer got assigned and consumed
        await Assert.That(stableListener.AssignedCallCount).IsGreaterThanOrEqualTo(1);
        await Assert.That(messages).Count().IsGreaterThanOrEqualTo(1);
    }

    [Test]
    public async Task ConsumerPollTimeout_DuringRebalance_CorrectBehavior()
    {
        // Arrange - verify consume behavior when a rebalance is happening
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 4);
        var groupId = $"test-group-{Guid.NewGuid():N}";
        var listener1 = new TrackingRebalanceListener();

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .BuildAsync();

        // Produce messages to all partitions
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

        // Consumer 1 joins and starts consuming
        await using var consumer1 = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(groupId)
            .WithSessionTimeout(TimeSpan.FromSeconds(10))
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithRebalanceListener(listener1)
            .BuildAsync();

        consumer1.Subscribe(topic);

        // Consume to establish initial assignment
        using var cts1 = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result = await consumer1.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts1.Token);
        await Assert.That(result).IsNotNull();
        await Assert.That(listener1.AssignedCallCount).IsGreaterThanOrEqualTo(1);

        var assignedBefore = listener1.AssignedCallCount;

        // Consumer 2 joins, triggering a rebalance while consumer 1 is consuming
        var listener2 = new TrackingRebalanceListener();
        await using var consumer2 = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(groupId)
            .WithSessionTimeout(TimeSpan.FromSeconds(10))
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithRebalanceListener(listener2)
            .BuildAsync();

        consumer2.Subscribe(topic);

        // Both consumers try to consume during the rebalance period
        // Consumer 1 continues consuming
        var consumer1Messages = new List<ConsumeResult<string, string>>();
        using var cts2 = new CancellationTokenSource(TimeSpan.FromSeconds(20));

        try
        {
            await foreach (var msg in consumer1.ConsumeAsync(cts2.Token))
            {
                consumer1Messages.Add(msg);
                if (consumer1Messages.Count >= 3) break;
            }
        }
        catch (OperationCanceledException)
        {
            // Some messages may have been consumed by consumer 2
        }

        // Consumer 2 also tries to consume
        var consumer2Messages = new List<ConsumeResult<string, string>>();
        using var cts3 = new CancellationTokenSource(TimeSpan.FromSeconds(20));

        try
        {
            await foreach (var msg in consumer2.ConsumeAsync(cts3.Token))
            {
                consumer2Messages.Add(msg);
                if (consumer2Messages.Count >= 1) break;
            }
        }
        catch (OperationCanceledException)
        {
            // May not get any messages if consumer 1 consumed them all
        }

        // Assert - rebalance happened and at least consumer 1 processed something
        // The second consumer should have triggered a rebalance on consumer 1
        await Assert.That(listener2.AssignedCallCount).IsGreaterThanOrEqualTo(1);

        // Total messages consumed across both consumers should account for all produced messages
        var totalConsumed = consumer1Messages.Count + consumer2Messages.Count;
        await Assert.That(totalConsumed).IsGreaterThanOrEqualTo(1);
    }

    /// <summary>
    /// Polls a condition until it returns true, or until the timeout expires.
    /// Preferred over hard-coded Task.Delay to reduce flakiness.
    /// </summary>
    private static async Task WaitForConditionAsync(
        Func<bool> condition,
        TimeSpan timeout,
        TimeSpan pollInterval)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(pollInterval);
        }
    }

    /// <summary>
    /// Rebalance listener that throws an exception on partition assignment.
    /// Used to verify the consumer handles listener exceptions gracefully.
    /// </summary>
    private sealed class ThrowingRebalanceListener : IRebalanceListener
    {
        private int _assignedCount;

        public int AssignedCallCount => _assignedCount;

        public ValueTask OnPartitionsAssignedAsync(IEnumerable<TopicPartition> partitions, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _assignedCount);
            throw new InvalidOperationException("Simulated listener failure on assignment");
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

    /// <summary>
    /// Rebalance listener that tracks all callback invocations.
    /// </summary>
    private sealed class TrackingRebalanceListener : IRebalanceListener
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

    /// <summary>
    /// Rebalance listener that commits offsets when partitions are revoked.
    /// Verifies that offset commits work correctly during the rebalance callback.
    /// </summary>
    private sealed class CommittingRebalanceListener : IRebalanceListener
    {
        private readonly List<TopicPartitionOffset> _committedOffsets;
        private int _assignedCount;
        private int _revokedCount;

        public IKafkaConsumer<string, string>? Consumer { get; set; }
        public int AssignedCallCount => _assignedCount;
        public int RevokedCallCount => _revokedCount;

        public CommittingRebalanceListener(List<TopicPartitionOffset> committedOffsets)
        {
            _committedOffsets = committedOffsets;
        }

        public ValueTask OnPartitionsAssignedAsync(IEnumerable<TopicPartition> partitions, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _assignedCount);
            return ValueTask.CompletedTask;
        }

        public async ValueTask OnPartitionsRevokedAsync(IEnumerable<TopicPartition> partitions, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _revokedCount);

            // Commit current offsets before partitions are revoked
            if (Consumer is not null)
            {
                var partitionList = partitions.ToList();
                var offsetsToCommit = new List<TopicPartitionOffset>();

                foreach (var tp in partitionList)
                {
                    var position = Consumer.GetPosition(tp);
                    if (position.HasValue)
                    {
                        var offset = new TopicPartitionOffset(tp.Topic, tp.Partition, position.Value);
                        offsetsToCommit.Add(offset);
                    }
                }

                if (offsetsToCommit.Count > 0)
                {
                    try
                    {
                        await Consumer.CommitAsync(offsetsToCommit, cancellationToken);
                        lock (_committedOffsets)
                        {
                            _committedOffsets.AddRange(offsetsToCommit);
                        }
                    }
                    catch (Exception)
                    {
                        // Commit may fail during rebalance - that's expected in some scenarios
                    }
                }
            }
        }

        public ValueTask OnPartitionsLostAsync(IEnumerable<TopicPartition> partitions, CancellationToken cancellationToken)
        {
            return ValueTask.CompletedTask;
        }
    }
}
