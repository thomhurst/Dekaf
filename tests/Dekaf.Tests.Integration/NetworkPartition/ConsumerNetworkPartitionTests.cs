using Dekaf.Consumer;
using Dekaf.Errors;
using Dekaf.Producer;

namespace Dekaf.Tests.Integration.NetworkPartition;

/// <summary>
/// Tests verifying consumer behavior during network partitions.
/// Uses Docker pause/unpause to simulate network failures.
/// </summary>
[Category("NetworkPartition")]
[NotInParallel("NetworkPartitionKafkaContainer")]
[ClassDataSource<NetworkPartitionKafkaContainer>(Shared = SharedType.PerTestSession)]
public class ConsumerNetworkPartitionTests(NetworkPartitionKafkaContainer kafka)
{
    [Test]
    [Timeout(60_000)]
    public async Task Consumer_LatestOffsetResolutionFailure_DoesNotReplayFromZero(
        CancellationToken cancellationToken)
    {
        var topic = await kafka.CreateTestTopicAsync();

        await using (var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("offset-resolution-setup-producer")
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync(cancellationToken))
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Partition = 0,
                Key = "before",
                Value = "before"
            }, cancellationToken);
        }

        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("offset-resolution-consumer")
            .WithAutoOffsetReset(AutoOffsetReset.Latest)
            .WithQueuedMinMessages(1)
            .WithRequestTimeout(TimeSpan.FromSeconds(1))
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync(cancellationToken);
        var partition = new TopicPartition(topic, 0);

        await kafka.PauseAsync();
        try
        {
            consumer.Assign(partition);
            using var unavailableCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            unavailableCts.CancelAfter(TimeSpan.FromSeconds(15));

            Exception? offsetResolutionFailure = null;
            try
            {
                _ = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(10), unavailableCts.Token)
                    .ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is KafkaException or TimeoutException)
            {
                offsetResolutionFailure = ex;
            }

            await Assert.That(offsetResolutionFailure).IsNotNull();
        }
        finally
        {
            await kafka.TryUnpauseAsync();
        }

        using var recoveryCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        recoveryCts.CancelAfter(TimeSpan.FromSeconds(20));
        var consumeTask = consumer.ConsumeOneAsync(TimeSpan.FromSeconds(15), recoveryCts.Token).AsTask();

        while (consumer.GetPosition(partition) != 1)
        {
            if (consumeTask.IsCompleted)
                _ = await consumeTask.ConfigureAwait(false);

            await Task.Delay(TimeSpan.FromMilliseconds(50), recoveryCts.Token).ConfigureAwait(false);
        }

        await using (var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("offset-resolution-recovery-producer")
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync(recoveryCts.Token))
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Partition = 0,
                Key = "after",
                Value = "after"
            }, recoveryCts.Token).ConfigureAwait(false);
        }

        var result = await consumeTask.ConfigureAwait(false);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value.Key).IsEqualTo("after");
        await Assert.That(result.Value.Offset).IsEqualTo(1);
    }

    [Test]
    public async Task Consumer_RejoinsGroup_AfterNetworkPartition()
    {
        // Arrange: short session timeout and heartbeat interval for fast detection
        var topic = await kafka.CreateTestTopicAsync();
        var groupId = $"partition-test-{Guid.NewGuid():N}";

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-consumer-partition-producer")
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        // Produce initial messages
        for (var i = 0; i < 5; i++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"pre-key-{i}",
                Value = $"pre-value-{i}"
            }, CancellationToken.None);
        }

        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-consumer-partition")
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithSessionTimeout(TimeSpan.FromSeconds(6))
            .WithHeartbeatInterval(TimeSpan.FromSeconds(1))
            .SubscribeTo(topic)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory()).BuildAsync();

        // Consume all initial messages
        var prePartitionMessages = new List<string>();
        using var preCts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await foreach (var msg in consumer.ConsumeAsync(preCts.Token))
        {
            prePartitionMessages.Add(msg.Value!);
            if (prePartitionMessages.Count >= 5)
                break;
        }

        await Assert.That(prePartitionMessages).Count().IsEqualTo(5);

        // Act: pause container to simulate network partition (exceeds session timeout)
        await kafka.PauseAsync();

        try
        {
            // Wait 8s to exceed the 6s session timeout, causing group expiry
            await Task.Delay(TimeSpan.FromSeconds(8));
            await kafka.UnpauseAsync();

            // Allow time for TCP recovery and consumer group rejoin
            await Task.Delay(TimeSpan.FromSeconds(3));

            // Produce new messages after recovery
            for (var i = 0; i < 3; i++)
            {
                await producer.ProduceAsync(new ProducerMessage<string, string>
                {
                    Topic = topic,
                    Key = $"post-key-{i}",
                    Value = $"post-value-{i}"
                }, CancellationToken.None);
            }

            // Assert: consumer should rejoin and consume new messages
            var postPartitionMessages = await ConsumeMatchingValuesAsync(
                consumer,
                expectedCount: 3,
                value => value.StartsWith("post-", StringComparison.Ordinal),
                TimeSpan.FromSeconds(30));

            await Assert.That(postPartitionMessages).Count().IsEqualTo(3);
        }
        finally
        {
            await kafka.TryUnpauseAsync();
        }
    }

    [Test]
    public async Task Consumer_RebalanceStabilizes_AfterNetworkPartition()
    {
        // Arrange: topic with 2 partitions
        var topic = await kafka.CreateTestTopicAsync(partitions: 2);
        var groupId = $"rebalance-test-{Guid.NewGuid():N}";

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-rebalance-producer")
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        // Produce initial messages across partitions
        for (var i = 0; i < 6; i++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{i}",
                Value = $"initial-{i}",
                Partition = i % 2
            }, CancellationToken.None);
        }

        // Track partition assignments via rebalance listener
        var assignedPartitions = new List<int>();
        var revokedPartitions = new List<int>();
        var rebalanceListener = new TestRebalanceListener(assignedPartitions, revokedPartitions);

        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-rebalance-consumer")
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithSessionTimeout(TimeSpan.FromSeconds(6))
            .WithHeartbeatInterval(TimeSpan.FromSeconds(1))
            .WithRebalanceListener(rebalanceListener)
            .SubscribeTo(topic)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory()).BuildAsync();

        // Consume initial messages
        var initialMessages = new List<string>();
        using var initCts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await foreach (var msg in consumer.ConsumeAsync(initCts.Token))
        {
            initialMessages.Add(msg.Value!);
            if (initialMessages.Count >= 6)
                break;
        }

        await Assert.That(initialMessages).Count().IsEqualTo(6);

        // Act: pause container to trigger session timeout and rebalance
        await kafka.PauseAsync();

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(8));
            await kafka.UnpauseAsync();

            // Wait for consumer to rejoin
            await Task.Delay(TimeSpan.FromSeconds(3));

            // Produce more messages after recovery
            for (var i = 0; i < 4; i++)
            {
                await producer.ProduceAsync(new ProducerMessage<string, string>
                {
                    Topic = topic,
                    Key = $"post-key-{i}",
                    Value = $"post-{i}",
                    Partition = i % 2
                }, CancellationToken.None);
            }

            // Assert: consumer should rejoin, get partition assignments, and consume new messages
            var postMessages = await ConsumeMatchingValuesAsync(
                consumer,
                expectedCount: 4,
                value => value.StartsWith("post-", StringComparison.Ordinal),
                TimeSpan.FromSeconds(30));

            await Assert.That(postMessages).Count().IsEqualTo(4);

            // Verify partitions were assigned (at least initial + rejoin)
            await Assert.That(assignedPartitions.Count).IsGreaterThanOrEqualTo(2)
                .Because("Consumer should have received partition assignments after rejoin");
        }
        finally
        {
            await kafka.TryUnpauseAsync();
        }
    }

    private sealed class TestRebalanceListener(
        List<int> assignedPartitions,
        List<int> revokedPartitions) : IRebalanceListener
    {
        public ValueTask OnPartitionsAssignedAsync(
            IEnumerable<TopicPartition> partitions,
            CancellationToken cancellationToken)
        {
            lock (assignedPartitions)
            {
                assignedPartitions.AddRange(partitions.Select(p => p.Partition));
            }
            return ValueTask.CompletedTask;
        }

        public ValueTask OnPartitionsRevokedAsync(
            IEnumerable<TopicPartition> partitions,
            CancellationToken cancellationToken)
        {
            lock (revokedPartitions)
            {
                revokedPartitions.AddRange(partitions.Select(p => p.Partition));
            }
            return ValueTask.CompletedTask;
        }

        public ValueTask OnPartitionsLostAsync(
            IEnumerable<TopicPartition> partitions,
            CancellationToken cancellationToken)
        {
            return ValueTask.CompletedTask;
        }
    }

    private static async Task<List<string>> ConsumeMatchingValuesAsync(
        IKafkaConsumer<string, string> consumer,
        int expectedCount,
        Func<string, bool> predicate,
        TimeSpan timeout)
    {
        var values = new List<string>();
        using var cts = new CancellationTokenSource(timeout);

        while (values.Count < expectedCount)
        {
            try
            {
                await foreach (var msg in consumer.ConsumeAsync(cts.Token))
                {
                    if (msg.Value is { } value && predicate(value))
                    {
                        values.Add(value);
                    }

                    if (values.Count >= expectedCount)
                        return values;
                }
            }
            catch (ConsumeException ex) when (ex.IsRetriable)
            {
                // Broker pause/unpause can surface one retriable fetch reset before consumption resumes.
            }
            catch (OperationCanceledException) when (cts.IsCancellationRequested)
            {
                break;
            }
        }

        return values;
    }
}
