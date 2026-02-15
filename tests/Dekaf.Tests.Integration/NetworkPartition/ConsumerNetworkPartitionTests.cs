using Dekaf.Consumer;
using Dekaf.Producer;

namespace Dekaf.Tests.Integration.NetworkPartition;

/// <summary>
/// Tests verifying consumer behavior during network partitions.
/// Uses Docker pause/unpause to simulate network failures.
/// </summary>
[Category("NetworkPartition")]
[ClassDataSource<NetworkPartitionKafkaContainer>(Shared = SharedType.PerClass)]
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
            .BuildAsync();

        // Produce initial messages
        for (var i = 0; i < 5; i++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"pre-key-{i}",
                Value = $"pre-value-{i}"
            });
        }

        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-consumer-partition")
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithSessionTimeout(TimeSpan.FromSeconds(6))
            .WithHeartbeatInterval(TimeSpan.FromSeconds(1))
            .SubscribeTo(topic)
            .BuildAsync();

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
            await Task.Delay(TimeSpan.FromSeconds(8));
            await kafka.UnpauseAsync();

            // Wait for reconnection
            await Task.Delay(TimeSpan.FromSeconds(3));

            // Produce new messages after recovery
            for (var i = 0; i < 3; i++)
            {
                await producer.ProduceAsync(new ProducerMessage<string, string>
                {
                    Topic = topic,
                    Key = $"post-key-{i}",
                    Value = $"post-value-{i}"
                });
            }

            // Assert: consumer should rejoin and consume new messages
            var postPartitionMessages = new List<string>();
            using var postCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await foreach (var msg in consumer.ConsumeAsync(postCts.Token))
            {
                if (msg.Value!.StartsWith("post-", StringComparison.Ordinal))
                {
                    postPartitionMessages.Add(msg.Value);
                }

                if (postPartitionMessages.Count >= 3)
                    break;
            }

            await Assert.That(postPartitionMessages).Count().IsEqualTo(3);
        }
        finally
        {
            try { await kafka.UnpauseAsync(); } catch { /* may already be unpaused */ }
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
            });
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
            .BuildAsync();

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
                });
            }

            // Assert: consumer should rejoin, get partition assignments, and consume new messages
            var postMessages = new List<string>();
            using var postCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await foreach (var msg in consumer.ConsumeAsync(postCts.Token))
            {
                if (msg.Value!.StartsWith("post-", StringComparison.Ordinal))
                {
                    postMessages.Add(msg.Value);
                }

                if (postMessages.Count >= 4)
                    break;
            }

            await Assert.That(postMessages).Count().IsEqualTo(4);

            // Verify partitions were assigned (at least initial + rejoin)
            await Assert.That(assignedPartitions.Count).IsGreaterThanOrEqualTo(2)
                .Because("Consumer should have received partition assignments after rejoin");
        }
        finally
        {
            try { await kafka.UnpauseAsync(); } catch { /* may already be unpaused */ }
        }
    }

    private sealed class TestRebalanceListener(
        List<int> assignedPartitions,
        List<int> revokedPartitions) : IRebalanceListener
    {
        public ValueTask OnPartitionsAssignedAsync(
            IEnumerable<Producer.TopicPartition> partitions,
            CancellationToken cancellationToken)
        {
            lock (assignedPartitions)
            {
                assignedPartitions.AddRange(partitions.Select(p => p.Partition));
            }
            return ValueTask.CompletedTask;
        }

        public ValueTask OnPartitionsRevokedAsync(
            IEnumerable<Producer.TopicPartition> partitions,
            CancellationToken cancellationToken)
        {
            lock (revokedPartitions)
            {
                revokedPartitions.AddRange(partitions.Select(p => p.Partition));
            }
            return ValueTask.CompletedTask;
        }

        public ValueTask OnPartitionsLostAsync(
            IEnumerable<Producer.TopicPartition> partitions,
            CancellationToken cancellationToken)
        {
            return ValueTask.CompletedTask;
        }
    }
}
