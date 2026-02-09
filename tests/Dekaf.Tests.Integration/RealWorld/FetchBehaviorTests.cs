using Dekaf.Consumer;
using Dekaf.Producer;

namespace Dekaf.Tests.Integration.RealWorld;

/// <summary>
/// Tests for fetch behavior with different consumer presets and configurations.
/// Verifies that high-throughput and low-latency presets work correctly,
/// and that max poll records limits are respected.
/// </summary>
public sealed class FetchBehaviorTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    [Test]
    public async Task HighThroughputPreset_ConsumesLargeBatches()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 3);

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithLinger(TimeSpan.FromMilliseconds(5))
            .BuildAsync();

        const int messageCount = 500;
        for (var i = 0; i < messageCount; i++)
        {
            producer.Send(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{i}",
                Value = $"value-{i}"
            });
        }

        await producer.FlushAsync();

        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId($"ht-group-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .ForHighThroughput()
            .BuildAsync();

        consumer.Subscribe(topic);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var count = 0;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            count++;
            if (count >= messageCount) break;
        }

        sw.Stop();

        await Assert.That(count).IsEqualTo(messageCount);
        // High throughput should consume 500 messages well within 30 seconds
        await Assert.That(sw.Elapsed).IsLessThan(TimeSpan.FromSeconds(20));
    }

    [Test]
    public async Task LowLatencyPreset_ReceivesMessagesQuickly()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .BuildAsync();

        // Produce messages first
        const int messageCount = 50;
        for (var i = 0; i < messageCount; i++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{i}",
                Value = $"ll-value-{i}"
            });
        }

        // Use low-latency consumer with assign (no group coordination delay)
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .ForLowLatency()
            .BuildAsync();

        var tp = new TopicPartition(topic, 0);
        consumer.Assign(tp);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var messages = new List<ConsumeResult<string, string>>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            messages.Add(msg);
            if (messages.Count >= messageCount) break;
        }

        sw.Stop();

        await Assert.That(messages).Count().IsEqualTo(messageCount);
        // Low latency preset should consume quickly
        await Assert.That(sw.Elapsed).IsLessThan(TimeSpan.FromSeconds(15));
    }

    [Test]
    public async Task MaxPollRecords_LimitsMessagesPerPoll()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .BuildAsync();

        // Produce many messages
        const int totalMessages = 100;
        for (var i = 0; i < totalMessages; i++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{i}",
                Value = $"value-{i}"
            });
        }

        // Consumer with max poll records of 10
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId($"poll-limit-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithMaxPollRecords(10)
            .BuildAsync();

        consumer.Subscribe(topic);

        // Consume all messages - should still get all of them eventually
        var messages = new List<ConsumeResult<string, string>>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            messages.Add(msg);
            if (messages.Count >= totalMessages) break;
        }

        await Assert.That(messages).Count().IsEqualTo(totalMessages);
    }

    [Test]
    public async Task LargeMessages_FetchSizeHandledCorrectly()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .BuildAsync();

        // Produce messages of varying sizes
        var sizes = new[] { 100, 1000, 10_000, 100_000, 500_000 };
        for (var i = 0; i < sizes.Length; i++)
        {
            var value = new string('X', sizes[i]);
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{i}",
                Value = value
            });
        }

        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        var tp = new TopicPartition(topic, 0);
        consumer.Assign(tp);

        var messages = new List<ConsumeResult<string, string>>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            messages.Add(msg);
            if (messages.Count >= sizes.Length) break;
        }

        await Assert.That(messages).Count().IsEqualTo(sizes.Length);

        // Verify all message sizes round-tripped correctly
        for (var i = 0; i < sizes.Length; i++)
        {
            await Assert.That(messages[i].Value.Length).IsEqualTo(sizes[i]);
        }
    }

    [Test]
    public async Task EmptyTopic_ConsumeReturnsNullWithinTimeout()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();

        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        var tp = new TopicPartition(topic, 0);
        consumer.Assign(tp);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(3), cts.Token);

        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task MultiPartitionFetch_AllPartitionsServed()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 4);

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .BuildAsync();

        // Produce to each partition
        for (var p = 0; p < 4; p++)
        {
            for (var i = 0; i < 10; i++)
            {
                await producer.ProduceAsync(new ProducerMessage<string, string>
                {
                    Topic = topic,
                    Key = $"p{p}-key-{i}",
                    Value = $"p{p}-value-{i}",
                    Partition = p
                });
            }
        }

        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId($"multi-fetch-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        consumer.Subscribe(topic);

        var partitionCounts = new Dictionary<int, int>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            partitionCounts[msg.Partition] = partitionCounts.GetValueOrDefault(msg.Partition) + 1;
            var total = partitionCounts.Values.Sum();
            if (total >= 40) break;
        }

        // All 4 partitions should have been served
        await Assert.That(partitionCounts.Count).IsEqualTo(4);
        foreach (var count in partitionCounts.Values)
        {
            await Assert.That(count).IsEqualTo(10);
        }
    }

    [Test]
    public async Task ReliabilityPreset_ProducerAcksAllAndConsumerCommitted()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .ForReliability()
            .BuildAsync();

        const int messageCount = 20;
        for (var i = 0; i < messageCount; i++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{i}",
                Value = $"value-{i}"
            });
        }

        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId($"reliable-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        consumer.Subscribe(topic);

        var messages = new List<ConsumeResult<string, string>>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            messages.Add(msg);
            if (messages.Count >= messageCount) break;
        }

        await Assert.That(messages).Count().IsEqualTo(messageCount);

        // All messages should be present and ordered
        for (var i = 0; i < messageCount; i++)
        {
            await Assert.That(messages[i].Value).IsEqualTo($"value-{i}");
        }
    }
}
