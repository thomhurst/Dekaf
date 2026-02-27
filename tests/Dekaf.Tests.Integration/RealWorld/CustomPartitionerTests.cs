using Dekaf.Consumer;
using Dekaf.Producer;

namespace Dekaf.Tests.Integration.RealWorld;

/// <summary>
/// Integration tests for custom partitioner implementations.
/// Verifies that custom IPartitioner instances correctly route messages
/// to the expected partitions when used with the producer builder API.
/// </summary>
[Category("Messaging")]
[ParallelLimiter<MessagingTestLimit>]
public sealed class CustomPartitionerTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    /// <summary>
    /// Partitioner that always routes to partition 0.
    /// </summary>
    private sealed class AlwaysPartitionZeroPartitioner : IPartitioner
    {
        public int Partition(string topic, ReadOnlySpan<byte> key, bool keyIsNull, int partitionCount)
        {
            return 0;
        }
    }

    /// <summary>
    /// Partitioner that uses a simple custom hash: sum of key bytes modulo partition count.
    /// This provides deterministic but different partitioning from the default Murmur2 hash.
    /// </summary>
    private sealed class SumHashPartitioner : IPartitioner
    {
        public int Partition(string topic, ReadOnlySpan<byte> key, bool keyIsNull, int partitionCount)
        {
            if (keyIsNull || key.Length == 0)
            {
                return 0;
            }

            var sum = 0;
            for (var i = 0; i < key.Length; i++)
            {
                sum += key[i];
            }

            return sum % partitionCount;
        }
    }

    /// <summary>
    /// Partitioner that records the parameters it receives for verification.
    /// Always routes to partition 0 for simplicity.
    /// </summary>
    private sealed class RecordingPartitioner : IPartitioner
    {
        public List<(string Topic, bool KeyIsNull, int PartitionCount)> Invocations { get; } = [];

        public int Partition(string topic, ReadOnlySpan<byte> key, bool keyIsNull, int partitionCount)
        {
            lock (Invocations)
            {
                Invocations.Add((topic, keyIsNull, partitionCount));
            }

            return 0;
        }
    }

    /// <summary>
    /// Partitioner that counts invocations to verify state persistence across calls.
    /// Routes messages to partitions based on cumulative call count.
    /// </summary>
    private sealed class StatefulCountingPartitioner : IPartitioner
    {
        private int _callCount;

        public int CallCount => _callCount;

        public int Partition(string topic, ReadOnlySpan<byte> key, bool keyIsNull, int partitionCount)
        {
            var count = Interlocked.Increment(ref _callCount);
            return (count - 1) % partitionCount;
        }
    }

    [Test]
    public async Task CustomPartitioner_RoutesAllMessagesToSpecificPartition()
    {
        // Arrange
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 3);
        var partitioner = new AlwaysPartitionZeroPartitioner();

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithCustomPartitioner(partitioner)
            .BuildAsync();

        // Act - produce multiple messages with different keys
        var results = new List<RecordMetadata>();
        for (var i = 0; i < 10; i++)
        {
            var metadata = await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{i}",
                Value = $"value-{i}"
            });
            results.Add(metadata);
        }

        // Assert - all messages should be on partition 0
        foreach (var result in results)
        {
            await Assert.That(result.Partition).IsEqualTo(0);
        }
    }

    [Test]
    public async Task CustomPartitioner_DistributesByCustomKeyHashing()
    {
        // Arrange
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 3);
        var partitioner = new SumHashPartitioner();

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithCustomPartitioner(partitioner)
            .BuildAsync();

        // Act - produce messages and consume them to verify partition assignment
        const int messageCount = 30;
        var producedResults = new List<RecordMetadata>();
        for (var i = 0; i < messageCount; i++)
        {
            var metadata = await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"hash-key-{i}",
                Value = $"value-{i}"
            });
            producedResults.Add(metadata);
        }

        // Assert - with 30 different keys and sum-based hashing, messages should hit multiple partitions
        var distinctPartitions = producedResults.Select(r => r.Partition).Distinct().ToList();
        await Assert.That(distinctPartitions.Count).IsGreaterThan(1);

        // Verify all partition values are valid (0, 1, or 2 for 3 partitions)
        foreach (var result in producedResults)
        {
            await Assert.That(result.Partition).IsGreaterThanOrEqualTo(0);
            await Assert.That(result.Partition).IsLessThan(3);
        }
    }

    [Test]
    public async Task CustomPartitioner_ReceivesCorrectTopicAndPartitionCount()
    {
        // Arrange
        const int partitionCount = 3;
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: partitionCount);
        var partitioner = new RecordingPartitioner();

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithCustomPartitioner(partitioner)
            .BuildAsync();

        // Act - produce a message with a key
        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "test-key",
            Value = "test-value"
        });

        // Assert - the partitioner should have been invoked with the correct topic and partition count
        List<(string Topic, bool KeyIsNull, int PartitionCount)> invocations;
        lock (partitioner.Invocations)
        {
            invocations = partitioner.Invocations.ToList();
        }

        await Assert.That(invocations.Count).IsGreaterThanOrEqualTo(1);

        var invocation = invocations[0];
        await Assert.That(invocation.Topic).IsEqualTo(topic);
        await Assert.That(invocation.KeyIsNull).IsFalse();
        await Assert.That(invocation.PartitionCount).IsEqualTo(partitionCount);
    }

    [Test]
    public async Task CustomPartitioner_WithNullKey_CustomLogicInvoked()
    {
        // Arrange
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 3);
        var partitioner = new RecordingPartitioner();

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithCustomPartitioner(partitioner)
            .BuildAsync();

        // Act - produce a message with null key
        var metadata = await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = null,
            Value = "null-key-value"
        });

        // Assert - the partitioner should have been called with keyIsNull=true
        List<(string Topic, bool KeyIsNull, int PartitionCount)> invocations;
        lock (partitioner.Invocations)
        {
            invocations = partitioner.Invocations.ToList();
        }

        await Assert.That(invocations.Count).IsGreaterThanOrEqualTo(1);

        var invocation = invocations[0];
        await Assert.That(invocation.KeyIsNull).IsTrue();

        // The message should have been produced to partition 0 (RecordingPartitioner always returns 0)
        await Assert.That(metadata.Partition).IsEqualTo(0);

        // Verify the message was actually delivered
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId($"null-key-test-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        consumer.Subscribe(topic);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value.Key).IsNull();
        await Assert.That(result.Value.Value).IsEqualTo("null-key-value");
        await Assert.That(result.Value.Partition).IsEqualTo(0);
    }

    [Test]
    public async Task CustomPartitioner_StatePersistsAcrossMultipleCalls()
    {
        // Arrange
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 3);
        var partitioner = new StatefulCountingPartitioner();

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithCustomPartitioner(partitioner)
            .BuildAsync();

        // Act - produce multiple messages; the stateful partitioner cycles through partitions
        const int messageCount = 9;
        var results = new List<RecordMetadata>();
        for (var i = 0; i < messageCount; i++)
        {
            var metadata = await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"stateful-key-{i}",
                Value = $"value-{i}"
            });
            results.Add(metadata);
        }

        // Assert - partitioner state should persist: call count matches message count
        await Assert.That(partitioner.CallCount).IsEqualTo(messageCount);

        // The stateful partitioner distributes round-robin: 0, 1, 2, 0, 1, 2, 0, 1, 2
        var partitionCounts = results.GroupBy(r => r.Partition).ToDictionary(g => g.Key, g => g.Count());
        await Assert.That(partitionCounts.Count).IsEqualTo(3);
        foreach (var count in partitionCounts.Values)
        {
            await Assert.That(count).IsEqualTo(3);
        }
    }

    [Test]
    public async Task BuiltInPartitioners_RoundRobin_DistributesEvenly()
    {
        // Arrange
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 3);

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithPartitioner(PartitionerType.RoundRobin)
            .BuildAsync();

        // Act - produce 9 messages (evenly divisible by 3 partitions)
        var results = new List<RecordMetadata>();
        for (var i = 0; i < 9; i++)
        {
            var metadata = await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = null,
                Value = $"rr-value-{i}"
            });
            results.Add(metadata);
        }

        // Assert - round-robin should distribute evenly across all 3 partitions
        var partitionCounts = results.GroupBy(r => r.Partition).ToDictionary(g => g.Key, g => g.Count());
        await Assert.That(partitionCounts.Count).IsEqualTo(3);
        foreach (var count in partitionCounts.Values)
        {
            await Assert.That(count).IsEqualTo(3);
        }
    }
}
