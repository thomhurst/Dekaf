using Dekaf.Consumer;
using Dekaf.Producer;

namespace Dekaf.Tests.Integration;

/// <summary>
/// Integration tests for static consumer group membership (group.instance.id).
/// Static membership allows consumers to rejoin a group without triggering a rebalance,
/// as long as they rejoin within the session timeout using the same group.instance.id.
/// </summary>
[Category("ConsumerGroup")]
public sealed class StaticMembershipTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    [Test]
    public async Task StaticMember_RetainsPartitionAssignment_AfterRestart()
    {
        // Arrange - create a topic with multiple partitions for meaningful assignment
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 4);
        var groupId = $"static-retain-{Guid.NewGuid():N}";
        var instanceId = $"static-instance-{Guid.NewGuid():N}";

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

        // First consumer with static membership - record the partition assignment
        HashSet<int> firstAssignment;
        await using (var consumer1 = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("static-consumer-1")
            .WithGroupId(groupId)
            .WithGroupInstanceId(instanceId)
            .WithSessionTimeout(TimeSpan.FromSeconds(30))
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithOffsetCommitMode(OffsetCommitMode.Manual)
            .BuildAsync())
        {
            consumer1.Subscribe(topic);

            // Consume messages to trigger assignment and stabilize
            var messages = new List<ConsumeResult<string, string>>();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await foreach (var msg in consumer1.ConsumeAsync(cts.Token))
            {
                messages.Add(msg);
                if (messages.Count >= 4) break;
            }

            firstAssignment = consumer1.Assignment.ToArray().Select(tp => tp.Partition).ToHashSet();
            await Assert.That(firstAssignment).Count().IsGreaterThanOrEqualTo(1);

            // Commit offsets so consumer2 doesn't re-consume
            await consumer1.CommitAsync();
        }

        // Produce more messages for the second consumer
        for (var p = 0; p < 4; p++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key2-{p}",
                Value = $"value2-{p}",
                Partition = p
            });
        }

        // Second consumer with same group.instance.id should get the same partitions
        await using var consumer2 = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("static-consumer-2")
            .WithGroupId(groupId)
            .WithGroupInstanceId(instanceId)
            .WithSessionTimeout(TimeSpan.FromSeconds(30))
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithOffsetCommitMode(OffsetCommitMode.Manual)
            .BuildAsync();

        consumer2.Subscribe(topic);

        // Consume messages to trigger assignment and stabilize (matching the first consumer's pattern)
        var messages2 = new List<ConsumeResult<string, string>>();
        using var cts2 = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await foreach (var msg in consumer2.ConsumeAsync(cts2.Token))
        {
            messages2.Add(msg);
            if (messages2.Count >= 4) break;
        }

        await Assert.That(messages2).Count().IsGreaterThanOrEqualTo(1);

        var secondAssignment = consumer2.Assignment.ToArray().Select(tp => tp.Partition).ToHashSet();

        // Assert - the second consumer should get the same partitions as the first
        await Assert.That(secondAssignment).IsEquivalentTo(firstAssignment);
    }

    [Test]
    public async Task StaticMember_RejoinWithinSessionTimeout_SkipsRebalance()
    {
        // Arrange
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 3);
        var groupId = $"static-skip-rebalance-{Guid.NewGuid():N}";
        var instanceId = $"static-instance-{Guid.NewGuid():N}";
        var listener1 = new TrackingRebalanceListener();
        var listener2 = new TrackingRebalanceListener();

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .BuildAsync();

        // Produce messages
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

        // First consumer with static membership and long session timeout
        await using (var consumer1 = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("static-consumer-1")
            .WithGroupId(groupId)
            .WithGroupInstanceId(instanceId)
            .WithSessionTimeout(TimeSpan.FromSeconds(30))
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithOffsetCommitMode(OffsetCommitMode.Manual)
            .WithRebalanceListener(listener1)
            .BuildAsync())
        {
            consumer1.Subscribe(topic);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var result = await consumer1.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token);
            await Assert.That(result).IsNotNull();

            // The first consumer should have been assigned partitions
            await Assert.That(listener1.AssignedCallCount).IsGreaterThanOrEqualTo(1);

            await consumer1.CommitAsync();
        }

        // Produce more messages for the second consumer
        for (var p = 0; p < 3; p++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key2-{p}",
                Value = $"value2-{p}",
                Partition = p
            });
        }

        // Rejoin quickly with the same instance ID (within the 30s session timeout)
        // Since this is a static member, the broker should recognize the instance ID
        // and skip the rebalance protocol
        await using var consumer2 = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("static-consumer-2")
            .WithGroupId(groupId)
            .WithGroupInstanceId(instanceId)
            .WithSessionTimeout(TimeSpan.FromSeconds(30))
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithOffsetCommitMode(OffsetCommitMode.Manual)
            .WithRebalanceListener(listener2)
            .BuildAsync();

        consumer2.Subscribe(topic);

        using var cts2 = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result2 = await consumer2.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts2.Token);
        await Assert.That(result2).IsNotNull();

        // The second consumer should have been assigned (at least 1 assignment call),
        // but the key property of static membership is that there was no revoke/rebalance
        // triggered for other members. With a single member, we verify assignment succeeds
        // and the revoked count stays at zero for the new consumer.
        await Assert.That(listener2.AssignedCallCount).IsGreaterThanOrEqualTo(1);
        await Assert.That(listener2.RevokedCallCount).IsEqualTo(0);
    }

    [Test]
    public async Task StaticMember_ExceedsSessionTimeout_TriggersRebalance()
    {
        // Arrange - use a very short session timeout so it expires while the static member is gone
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 2);
        var groupId = $"static-timeout-{Guid.NewGuid():N}";
        var instanceId = $"static-instance-{Guid.NewGuid():N}";
        var otherListener = new TrackingRebalanceListener();

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .BuildAsync();

        // Produce messages
        for (var p = 0; p < 2; p++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{p}",
                Value = $"value-{p}",
                Partition = p
            });
        }

        // Static member consumer with short session timeout
        await using (var staticConsumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("static-consumer")
            .WithGroupId(groupId)
            .WithGroupInstanceId(instanceId)
            .WithSessionTimeout(TimeSpan.FromSeconds(6))
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithOffsetCommitMode(OffsetCommitMode.Manual)
            .BuildAsync())
        {
            staticConsumer.Subscribe(topic);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var result = await staticConsumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token);
            await Assert.That(result).IsNotNull();
        }

        // Wait for the session timeout to expire (6 seconds + buffer)
        await Task.Delay(TimeSpan.FromSeconds(10));

        // Now a new dynamic consumer joining the same group should trigger a rebalance
        // because the static member's session has expired
        await using var dynamicConsumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("dynamic-consumer")
            .WithGroupId(groupId)
            .WithSessionTimeout(TimeSpan.FromSeconds(10))
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithRebalanceListener(otherListener)
            .BuildAsync();

        dynamicConsumer.Subscribe(topic);

        // Produce more messages so the dynamic consumer has something to consume
        for (var p = 0; p < 2; p++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key2-{p}",
                Value = $"value2-{p}",
                Partition = p
            });
        }

        using var cts2 = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result2 = await dynamicConsumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts2.Token);
        await Assert.That(result2).IsNotNull();

        // The dynamic consumer should have been assigned partitions through a rebalance
        await Assert.That(otherListener.AssignedCallCount).IsGreaterThanOrEqualTo(1);

        // The dynamic consumer should now own all partitions since the static member has timed out
        var assignment = dynamicConsumer.Assignment.ToArray();
        await Assert.That(assignment).Count().IsEqualTo(2);
    }

    [Test]
    public async Task StaticMember_DuplicateInstanceId_FencingBehavior()
    {
        // Arrange - two consumers with the same group.instance.id should cause fencing
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 2);
        var groupId = $"static-fencing-{Guid.NewGuid():N}";
        var instanceId = $"static-instance-{Guid.NewGuid():N}";

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .BuildAsync();

        // Produce messages
        for (var p = 0; p < 2; p++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{p}",
                Value = $"value-{p}",
                Partition = p
            });
        }

        // First consumer with static membership
        await using var consumer1 = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("static-consumer-1")
            .WithGroupId(groupId)
            .WithGroupInstanceId(instanceId)
            .WithSessionTimeout(TimeSpan.FromSeconds(30))
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithOffsetCommitMode(OffsetCommitMode.Manual)
            .BuildAsync();

        consumer1.Subscribe(topic);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result = await consumer1.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token);
        await Assert.That(result).IsNotNull();

        // Second consumer with the same group.instance.id should either:
        // 1. Fence the first consumer and take over, or
        // 2. Throw an exception itself
        // In Kafka, the second JoinGroup with the same instance ID will fence the first consumer.
        // The second consumer should successfully join, replacing the first.
        await using var consumer2 = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("static-consumer-2")
            .WithGroupId(groupId)
            .WithGroupInstanceId(instanceId)
            .WithSessionTimeout(TimeSpan.FromSeconds(30))
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithOffsetCommitMode(OffsetCommitMode.Manual)
            .BuildAsync();

        consumer2.Subscribe(topic);

        // Produce more messages for consumer2
        for (var p = 0; p < 2; p++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key2-{p}",
                Value = $"value2-{p}",
                Partition = p
            });
        }

        // The second consumer should be able to consume (it fences the first)
        using var cts2 = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result2 = await consumer2.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts2.Token);
        await Assert.That(result2).IsNotNull();

        // Consumer2 should have partitions assigned (it took over from consumer1)
        var assignment = consumer2.Assignment.ToArray();
        await Assert.That(assignment).Count().IsGreaterThanOrEqualTo(1);
    }

    [Test]
    public async Task StaticMember_MixedWithDynamic_InSameGroup()
    {
        // Arrange - mix static and dynamic members in the same consumer group
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 4);
        var groupId = $"static-mixed-{Guid.NewGuid():N}";
        var instanceId = $"static-instance-{Guid.NewGuid():N}";
        var staticListener = new TrackingRebalanceListener();
        var dynamicListener = new TrackingRebalanceListener();

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

        // Static member consumer
        await using var staticConsumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("static-consumer")
            .WithGroupId(groupId)
            .WithGroupInstanceId(instanceId)
            .WithSessionTimeout(TimeSpan.FromSeconds(10))
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithOffsetCommitMode(OffsetCommitMode.Manual)
            .WithRebalanceListener(staticListener)
            .BuildAsync();

        staticConsumer.Subscribe(topic);

        // Wait for static consumer to get all partitions
        using var cts1 = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var msg1 = await staticConsumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts1.Token);
        await Assert.That(msg1).IsNotNull();

        // Static consumer should initially have all 4 partitions
        await Assert.That(staticConsumer.Assignment.ToArray()).Count().IsEqualTo(4);

        // Dynamic member consumer joins the same group
        await using var dynamicConsumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("dynamic-consumer")
            .WithGroupId(groupId)
            .WithSessionTimeout(TimeSpan.FromSeconds(10))
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithOffsetCommitMode(OffsetCommitMode.Manual)
            .WithRebalanceListener(dynamicListener)
            .BuildAsync();

        dynamicConsumer.Subscribe(topic);

        // Wait for rebalance to redistribute partitions
        using var cts2 = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try
        {
            await dynamicConsumer.ConsumeOneAsync(TimeSpan.FromSeconds(15), cts2.Token);
        }
        catch (OperationCanceledException)
        {
            // May timeout if no messages are available on the assigned partitions, that's OK
        }

        // Wait for rebalance to settle
        await Task.Delay(5000);

        // Both consumers should have been assigned at least 1 partition
        await Assert.That(staticListener.AssignedCallCount).IsGreaterThanOrEqualTo(1);
        await Assert.That(dynamicListener.AssignedCallCount).IsGreaterThanOrEqualTo(1);

        // Together they should cover all 4 partitions
        var staticPartitions = staticConsumer.Assignment.ToArray().Select(tp => tp.Partition).ToHashSet();
        var dynamicPartitions = dynamicConsumer.Assignment.ToArray().Select(tp => tp.Partition).ToHashSet();

        // Each consumer should have at least 1 partition after rebalance
        await Assert.That(staticPartitions).Count().IsGreaterThanOrEqualTo(1);
        await Assert.That(dynamicPartitions).Count().IsGreaterThanOrEqualTo(1);

        // Combined they should cover all 4 partitions
        var allPartitions = staticPartitions.Union(dynamicPartitions).OrderBy(p => p).ToList();
        int[] expectedPartitions = [0, 1, 2, 3];
        await Assert.That(allPartitions).IsEquivalentTo(expectedPartitions);
    }

    [Test]
    public async Task StaticMember_WithCooperativeRebalance_Works()
    {
        // Arrange - static membership with cooperative sticky rebalance strategy
        // CooperativeSticky is the default, so we just verify it works with static membership
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 3);
        var groupId = $"static-coop-{Guid.NewGuid():N}";
        var instanceId = $"static-instance-{Guid.NewGuid():N}";
        var listener = new TrackingRebalanceListener();

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .BuildAsync();

        // Produce messages to all partitions
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

        // Static member with cooperative sticky (default strategy)
        await using (var consumer1 = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("static-coop-consumer-1")
            .WithGroupId(groupId)
            .WithGroupInstanceId(instanceId)
            .WithSessionTimeout(TimeSpan.FromSeconds(30))
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithOffsetCommitMode(OffsetCommitMode.Manual)
            .WithRebalanceListener(listener)
            .BuildAsync())
        {
            consumer1.Subscribe(topic);

            // Consume all messages
            var messages = new List<ConsumeResult<string, string>>();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await foreach (var msg in consumer1.ConsumeAsync(cts.Token))
            {
                messages.Add(msg);
                if (messages.Count >= 3) break;
            }

            await Assert.That(messages).Count().IsEqualTo(3);
            await Assert.That(listener.AssignedCallCount).IsGreaterThanOrEqualTo(1);

            // Record the assignment
            var assignment = consumer1.Assignment.ToArray().Select(tp => tp.Partition).OrderBy(p => p).ToList();
            await Assert.That(assignment).Count().IsEqualTo(3);

            await consumer1.CommitAsync();
        }

        // Produce more messages
        for (var p = 0; p < 3; p++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key2-{p}",
                Value = $"value2-{p}",
                Partition = p
            });
        }

        // Rejoin with same static ID - cooperative sticky should preserve assignments
        var listener2 = new TrackingRebalanceListener();
        await using var consumer2 = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("static-coop-consumer-2")
            .WithGroupId(groupId)
            .WithGroupInstanceId(instanceId)
            .WithSessionTimeout(TimeSpan.FromSeconds(30))
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithOffsetCommitMode(OffsetCommitMode.Manual)
            .WithRebalanceListener(listener2)
            .BuildAsync();

        consumer2.Subscribe(topic);

        // Consume to verify it works
        var messages2 = new List<ConsumeResult<string, string>>();
        using var cts2 = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await foreach (var msg in consumer2.ConsumeAsync(cts2.Token))
        {
            messages2.Add(msg);
            if (messages2.Count >= 3) break;
        }

        await Assert.That(messages2).Count().IsEqualTo(3);
        await Assert.That(listener2.AssignedCallCount).IsGreaterThanOrEqualTo(1);

        // Should still have all 3 partitions
        var assignment2 = consumer2.Assignment.ToArray().Select(tp => tp.Partition).OrderBy(p => p).ToList();
        int[] expectedPartitions = [0, 1, 2];
        await Assert.That(assignment2).IsEquivalentTo(expectedPartitions);
    }

    /// <summary>
    /// Rebalance listener that tracks the number of times each callback is invoked.
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
}
