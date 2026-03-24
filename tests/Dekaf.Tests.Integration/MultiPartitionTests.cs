using System.Diagnostics;
using Dekaf.Consumer;
using Dekaf.Producer;

namespace Dekaf.Tests.Integration;

/// <summary>
/// Integration tests for multi-partition scenarios.
/// </summary>
[Category("Messaging")]
[ParallelLimiter<MessagingTestLimit>]
public class MultiPartitionTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    /// <summary>
    /// Logs elapsed time for a test phase to help diagnose CI timeout root causes.
    /// Output goes to TUnit's captured stdout and appears in CI test reports.
    /// </summary>
    private static void LogPhase(string phase, Stopwatch sw)
    {
        Console.WriteLine($"  [{sw.Elapsed.TotalSeconds:F1}s] {phase}");
    }

    /// <summary>
    /// Produces a warmup message to each partition to ensure the broker has fully initialized
    /// the partition and its producer state tracking.
    /// Uses a per-message timeout to fail fast if a produce hangs (e.g., due to a transient
    /// connection death on CI), and retries once on failure.
    /// </summary>
    private static async Task WarmUpAllPartitions(
        IKafkaProducer<string, string> producer, string topic, int partitions)
    {
        for (var p = 0; p < partitions; p++)
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            try
            {
                await producer.ProduceAsync(new ProducerMessage<string, string>
                {
                    Topic = topic, Key = "warmup", Value = "warmup", Partition = p
                }, cts.Token);
            }
            catch (OperationCanceledException)
            {
                // First attempt timed out — connection may have died and self-healed.
                // Retry once with a fresh timeout.
                Console.WriteLine($"  [warmup] partition {p} timed out, retrying...");
                using var retryCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                await producer.ProduceAsync(new ProducerMessage<string, string>
                {
                    Topic = topic, Key = "warmup", Value = "warmup", Partition = p
                }, retryCts.Token);
                Console.WriteLine($"  [warmup] partition {p} retry succeeded");
            }
        }
    }

    [Test]
    public async Task MultiPartition_KeyBasedPartitioning_SameKeyGoesToSamePartition()
    {
        var sw = Stopwatch.StartNew();

        // Arrange
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 5);
        LogPhase("topic created", sw);

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer")
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();
        LogPhase("producer built", sw);

        await WarmUpAllPartitions(producer, topic, 5);
        LogPhase("warmup done", sw);

        // Act - produce multiple messages with same key
        var results = new List<RecordMetadata>();
        for (var i = 0; i < 10; i++)
        {
            var metadata = await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = "consistent-key",
                Value = $"value-{i}"
            });
            results.Add(metadata);
        }
        LogPhase("produce done", sw);

        // Assert - all should go to same partition
        var partitions = results.Select(r => r.Partition).Distinct().ToList();
        await Assert.That(partitions).Count().IsEqualTo(1);
    }

    [Test]
    public async Task MultiPartition_DifferentKeys_DistributeAcrossPartitions()
    {
        var sw = Stopwatch.StartNew();

        // Arrange
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 5);
        LogPhase("topic created", sw);

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer")
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();
        LogPhase("producer built", sw);

        await WarmUpAllPartitions(producer, topic, 5);
        LogPhase("warmup done", sw);

        // Act - produce messages with different keys.
        // 10 keys across 5 partitions is statistically sufficient to hit ≥2 partitions
        // (probability of all 10 landing on the same partition: (1/5)^9 ≈ 0.00005%).
        // Keeping the count low avoids timeouts on slow CI runners with thread pool starvation.
        var results = new List<RecordMetadata>();
        for (var i = 0; i < 10; i++)
        {
            var metadata = await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"different-key-{i}",
                Value = $"value-{i}"
            });
            results.Add(metadata);
        }
        LogPhase("produce done", sw);

        // Assert - should distribute across multiple partitions
        var partitions = results.Select(r => r.Partition).Distinct().ToList();
        await Assert.That(partitions.Count).IsGreaterThanOrEqualTo(2); // At least 2 different partitions
    }

    [Test]
    public async Task MultiPartition_ManualAssignment_ConsumesOnlyAssignedPartition()
    {
        var sw = Stopwatch.StartNew();

        // Arrange
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 3);
        LogPhase("topic created", sw);

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer")
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();
        LogPhase("producer built", sw);

        await WarmUpAllPartitions(producer, topic, 3);
        LogPhase("warmup done", sw);

        // Produce to all partitions
        for (var p = 0; p < 3; p++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{p}",
                Value = $"value-partition-{p}",
                Partition = p
            });
        }
        LogPhase("produce done", sw);

        // Act - manually assign only partition 1
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory()).BuildAsync();

        consumer.Assign(new TopicPartition(topic, 1));

        var messages = new List<ConsumeResult<string, string>>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(90));

        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            messages.Add(msg);
            if (messages.Any(m => m.Key != "warmup")) break;
        }

        // Assert - filter out warmup
        var actual = messages.Where(m => m.Key != "warmup").ToList();
        await Assert.That(actual).Count().IsGreaterThanOrEqualTo(1);
        var r = actual[0];
        await Assert.That(r.Partition).IsEqualTo(1);
        await Assert.That(r.Value).IsEqualTo("value-partition-1");
    }

    [Test]
    public async Task MultiPartition_AssignMultiplePartitions_ConsumesFromAll()
    {
        var sw = Stopwatch.StartNew();

        // Arrange
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 4);
        LogPhase("topic created", sw);

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer")
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();
        LogPhase("producer built", sw);

        await WarmUpAllPartitions(producer, topic, 4);
        LogPhase("warmup done", sw);

        // Produce to partitions 0, 1, and 2
        for (var p = 0; p < 3; p++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{p}",
                Value = $"value-p{p}",
                Partition = p
            });
        }
        LogPhase("produce done", sw);

        // Act - assign partitions 0 and 2 only
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory()).BuildAsync();

        consumer.Assign(
            new TopicPartition(topic, 0),
            new TopicPartition(topic, 2));

        var messages = new List<ConsumeResult<string, string>>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(90));

        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            messages.Add(msg);
            // Break when we have non-warmup messages from BOTH assigned partitions
            var coveredPartitions = messages
                .Where(m => m.Key != "warmup")
                .Select(m => m.Partition)
                .Distinct()
                .Count();
            if (coveredPartitions >= 2) break;
        }

        // Assert - should only get messages from partitions 0 and 2 (filter out warmup)
        var actual = messages.Where(m => m.Key != "warmup").ToList();
        await Assert.That(actual).Count().IsGreaterThanOrEqualTo(2);
        var partitions = actual.Select(m => m.Partition).Distinct().OrderBy(p => p).ToList();
        int[] expectedPartitions = [0, 2];
        await Assert.That(partitions).IsEquivalentTo(expectedPartitions);
    }

    [Test]
    public async Task MultiPartition_ConsumerGroupSubscription_GetsAllPartitions()
    {
        var sw = Stopwatch.StartNew();

        // Arrange
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 3);
        var groupId = $"test-group-{Guid.NewGuid():N}";
        LogPhase("topic created", sw);

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer")
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();
        LogPhase("producer built", sw);

        await WarmUpAllPartitions(producer, topic, 3);
        LogPhase("warmup done", sw);

        // Produce multiple messages per partition so there's data available even if
        // consumer group rebalance assigns partitions incrementally on slow CI.
        for (var round = 0; round < 3; round++)
        {
            for (var p = 0; p < 3; p++)
            {
                await producer.ProduceAsync(new ProducerMessage<string, string>
                {
                    Topic = topic,
                    Key = $"key-{p}",
                    Value = $"value-{p}-{round}",
                    Partition = p
                });
            }
        }
        LogPhase("produce done (9 messages)", sw);

        // Act - subscribe (not manual assign)
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer")
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory()).BuildAsync();
        LogPhase("consumer built", sw);

        consumer.Subscribe(topic);

        var messages = new List<ConsumeResult<string, string>>();
        var seenPartitions = new HashSet<int>();
        // Consumer group rebalance can take 60+ seconds on slow CI runners with
        // thread pool starvation. Use a generous timeout to avoid flaky failures.
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(90));

        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            messages.Add(msg);
            if (msg.Key != "warmup")
                seenPartitions.Add(msg.Partition);
            // Break only when we've seen messages from ALL 3 partitions
            if (seenPartitions.Count >= 3) break;
        }
        LogPhase("consume done", sw);

        // Assert - should have received messages from all 3 partitions
        var partitions = seenPartitions.OrderBy(p => p).ToList();
        int[] expectedPartitions = [0, 1, 2];
        await Assert.That(partitions).IsEquivalentTo(expectedPartitions);
        LogPhase("test complete (disposal next)", sw);
    }

    [Test]
    public async Task MultiPartition_OrderWithinPartition_IsPreserved()
    {
        var sw = Stopwatch.StartNew();

        // Arrange
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 2);
        LogPhase("topic created", sw);

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer")
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();
        LogPhase("producer built", sw);

        await WarmUpAllPartitions(producer, topic, 2);
        LogPhase("warmup done", sw);

        // Produce ordered messages to partition 0.
        // Use a per-call timeout so a single hung delivery fails fast (~30s) instead of
        // blocking until the 360s orphan sweep. Don't retry — cancellation after append
        // doesn't prevent delivery, so a retry would produce a duplicate and shift indices.
        // If a timeout fires, the message is still delivered in the background;
        // the consumer assertion below handles this gracefully.
        for (var i = 0; i < 10; i++)
        {
            using var perCallCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            try
            {
                await producer.ProduceAsync(new ProducerMessage<string, string>
                {
                    Topic = topic,
                    Key = $"key-{i}",
                    Value = $"value-{i:D2}",
                    Partition = 0
                }, perCallCts.Token);
            }
            catch (OperationCanceledException)
            {
                // Timeout — the message was likely appended and will be delivered in the
                // background. Continue producing the rest; the consumer assertion below
                // will validate ordering of whatever was delivered.
            }
        }

        // No FlushAsync here: ProduceAsync with a per-call CTS can leave timed-out batches
        // in-flight, and FlushAsync waits for ALL in-flight batches (up to DeliveryTimeoutMs).
        // The consumer below reads whatever was delivered — if some messages timed out, the
        // consumer assertion handles it gracefully.

        // Act
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory()).BuildAsync();

        consumer.Assign(new TopicPartition(topic, 0));

        var messages = new List<ConsumeResult<string, string>>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(90));

        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            messages.Add(msg);
            if (messages.Count(m => m.Key != "warmup") >= 10) break;
        }

        // Assert - all 10 messages received and in order (filter out warmup)
        var actual = messages.Where(m => m.Key != "warmup").ToList();
        await Assert.That(actual).Count().IsGreaterThanOrEqualTo(10);
        for (var i = 0; i < actual.Count; i++)
        {
            await Assert.That(actual[i].Value).IsEqualTo($"value-{i:D2}");
        }
    }

    [Test]
    public async Task MultiPartition_SeekOnSpecificPartition_WorksCorrectly()
    {
        var sw = Stopwatch.StartNew();

        // Arrange
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 2);
        LogPhase("topic created", sw);

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer")
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();
        LogPhase("producer built", sw);

        await WarmUpAllPartitions(producer, topic, 2);
        LogPhase("warmup done", sw);

        // Produce to both partitions
        for (var i = 0; i < 5; i++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-p0-{i}",
                Value = $"p0-value-{i}",
                Partition = 0
            });
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-p1-{i}",
                Value = $"p1-value-{i}",
                Partition = 1
            });
        }
        LogPhase("produce done (10 messages)", sw);

        // Act - assign both but seek partition 0 to offset 3
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory()).BuildAsync();
        LogPhase("consumer built", sw);

        consumer.Assign(
            new TopicPartition(topic, 0),
            new TopicPartition(topic, 1));
        consumer.Seek(new TopicPartitionOffset(topic, 0, 4)); // offset 0 = warmup, 1-5 = actual; seek past warmup+3

        // Consume from partition 0
        var messages = new List<ConsumeResult<string, string>>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(90));

        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            if (msg.Partition == 0)
            {
                messages.Add(msg);
                if (messages.Count >= 2) break;
            }
        }
        LogPhase("consume done", sw);

        // Assert - partition 0 should start from offset 4 (after warmup + 3 actual)
        await Assert.That(messages[0].Partition).IsEqualTo(0);
        await Assert.That(messages[0].Offset).IsEqualTo(4);
        LogPhase("test complete (disposal next)", sw);
    }

    [Test]
    public async Task MultiPartition_HighPartitionCount_WorksCorrectly()
    {
        var sw = Stopwatch.StartNew();

        // Arrange
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 10);

        // Let broker stabilize partition leaders before producing.
        // On slow CI runners, leader election for 10 partitions can take several seconds.
        await Task.Delay(3000);
        LogPhase("topic created + stabilize delay", sw);

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer")
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();
        LogPhase("producer built", sw);

        await WarmUpAllPartitions(producer, topic, 10);
        LogPhase("warmup done (10 partitions)", sw);

        // Produce to all 10 partitions
        for (var p = 0; p < 10; p++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{p}",
                Value = $"value-{p}",
                Partition = p
            });
        }
        LogPhase("produce done (10 messages)", sw);

        // Act — use manual Assign (not Subscribe) to skip consumer group rebalance.
        // Rebalance for 10 partitions can take 60+ seconds on slow CI runners with
        // thread pool starvation, causing flaky timeouts. Manual assignment is instant.
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory()).BuildAsync();
        LogPhase("consumer built", sw);

        consumer.Assign(Enumerable.Range(0, 10)
            .Select(p => new TopicPartition(topic, p)).ToArray());

        var messages = new List<ConsumeResult<string, string>>();
        var seenPartitions = new HashSet<int>();
        // 10 partitions need more time on slow CI runners with thread pool starvation.
        // Each partition requires a separate fetch request/response cycle.
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(90));

        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            messages.Add(msg);
            if (msg.Key != "warmup")
                seenPartitions.Add(msg.Partition);
            if (seenPartitions.Count >= 10) break;
        }
        LogPhase("consume done", sw);

        // Assert - should get messages from all 10 partitions (filter out warmup)
        var actual = messages.Where(m => m.Key != "warmup").ToList();
        await Assert.That(actual).Count().IsGreaterThanOrEqualTo(10);
        var partitions = actual.Select(m => m.Partition).Distinct().OrderBy(p => p).ToList();
        await Assert.That(partitions).IsEquivalentTo(Enumerable.Range(0, 10));
        LogPhase("test complete (disposal next)", sw);
    }

    [Test]
    public async Task MultiPartition_ProduceAndConsumeWithKeys_PartitioningIsConsistent()
    {
        var sw = Stopwatch.StartNew();

        // Arrange
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 5);
        var groupId = $"test-group-{Guid.NewGuid():N}";
        LogPhase("topic created", sw);

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer")
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();
        LogPhase("producer built", sw);

        await WarmUpAllPartitions(producer, topic, 5);
        LogPhase("warmup done", sw);

        // Produce same key multiple times
        var expectedPartition = -1;
        for (var i = 0; i < 5; i++)
        {
            var metadata = await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = "consistent-key",
                Value = $"value-{i}"
            });

            if (expectedPartition < 0)
            {
                expectedPartition = metadata.Partition;
            }
            else
            {
                // All should go to same partition
                await Assert.That(metadata.Partition).IsEqualTo(expectedPartition);
            }
        }
        LogPhase("produce done (5 messages)", sw);

        // Act - consume
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer")
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory()).BuildAsync();
        LogPhase("consumer built", sw);

        consumer.Subscribe(topic);

        var messages = new List<ConsumeResult<string, string>>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(90));

        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            messages.Add(msg);
            if (messages.Count(m => m.Key == "consistent-key") >= 5) break;
        }
        LogPhase("consume done", sw);

        // Assert - all consistent-key messages should be from same partition
        var actual = messages.Where(m => m.Key == "consistent-key").ToList();
        await Assert.That(actual).Count().IsGreaterThanOrEqualTo(5);
        foreach (var msg in actual)
        {
            await Assert.That(msg.Partition).IsEqualTo(expectedPartition);
            await Assert.That(msg.Key).IsEqualTo("consistent-key");
        }
        LogPhase("test complete (disposal next)", sw);
    }
}
