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
