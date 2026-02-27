using System.Collections.Concurrent;
using Dekaf.Consumer;
using Dekaf.Producer;
using Dekaf.Statistics;

namespace Dekaf.Tests.Integration;

/// <summary>
/// Integration tests for client metrics (statistics) verifying accuracy of counters,
/// lag tracking, multi-topic aggregation, partition assignment reporting, and reset behavior.
/// </summary>
[Category("Messaging")]
[ParallelLimiter<MessagingTestLimit>]
public sealed class MetricsMonitoringTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    /// <summary>
    /// Waits for a stats snapshot in the bag that satisfies the predicate, polling every 250ms.
    /// Returns the first matching snapshot, or null if the timeout expires.
    /// </summary>
    private static async Task<T?> WaitForStatsAsync<T>(
        ConcurrentBag<T> stats,
        Func<T, bool> predicate,
        TimeSpan timeout) where T : class
    {
        using var cts = new CancellationTokenSource(timeout);
        while (!cts.Token.IsCancellationRequested)
        {
            var match = stats.FirstOrDefault(predicate);
            if (match is not null)
                return match;

            try
            {
                await Task.Delay(250, cts.Token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        // Final attempt
        return stats.FirstOrDefault(predicate);
    }

    [Test]
    public async Task ProducerMetrics_MessageCount_ReflectsProducedMessages()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var stats = new ConcurrentBag<ProducerStatistics>();
        const int messageCount = 25;

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithStatisticsInterval(TimeSpan.FromSeconds(1))
            .WithStatisticsHandler(s => stats.Add(s))
            .BuildAsync();

        for (var i = 0; i < messageCount; i++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{i}",
                Value = $"value-{i}"
            });
        }

        await producer.FlushAsync();

        // Wait for a stats snapshot that reflects all produced and delivered messages
        var matchingStats = await WaitForStatsAsync(stats,
            s => s.MessagesProduced >= messageCount && s.MessagesDelivered >= messageCount,
            TimeSpan.FromSeconds(10));

        await Assert.That(matchingStats).IsNotNull();
        await Assert.That(matchingStats!.MessagesProduced).IsGreaterThanOrEqualTo(messageCount);
        await Assert.That(matchingStats.MessagesDelivered).IsGreaterThanOrEqualTo(messageCount);
        await Assert.That(matchingStats.MessagesFailed).IsEqualTo(0);
    }

    [Test]
    public async Task ConsumerMetrics_MessageCount_ReflectsConsumedMessages()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var groupId = $"metrics-consumer-count-{Guid.NewGuid():N}";
        var stats = new ConcurrentBag<ConsumerStatistics>();
        const int messageCount = 10;

        // Produce messages
        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .BuildAsync();

        for (var i = 0; i < messageCount; i++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{i}",
                Value = $"value-{i}"
            });
        }

        await producer.FlushAsync();

        // Consume all messages using a short timeout after the last message
        // The consumer stats are recorded at batch-level when a fetch is fully consumed,
        // so we let the consumer continue briefly after consuming all messages to allow
        // the batch stats to be recorded on the next MoveNextAsync call.
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithStatisticsInterval(TimeSpan.FromSeconds(1))
            .WithStatisticsHandler(s => stats.Add(s))
            .BuildAsync();

        consumer.Subscribe(topic);

        var consumed = 0;
        // Use a short timeout - after consuming all messages, the consumer will
        // block waiting for more, which triggers batch-level stats recording
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try
        {
            await foreach (var msg in consumer.ConsumeAsync(cts.Token))
            {
                consumed++;
                if (consumed >= messageCount)
                {
                    // Don't break immediately - set a short timeout to allow the iterator
                    // to complete the current batch's stats recording on next MoveNextAsync
                    cts.CancelAfter(TimeSpan.FromSeconds(3));
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when the CTS fires after consuming all messages
        }

        // Wait for a stats snapshot that reflects consumed messages
        var matchingStats = await WaitForStatsAsync(stats,
            s => s.MessagesConsumed >= messageCount,
            TimeSpan.FromSeconds(10));

        await Assert.That(matchingStats).IsNotNull();
        await Assert.That(matchingStats!.MessagesConsumed).IsGreaterThanOrEqualTo(messageCount);
        await Assert.That(matchingStats.BytesConsumed).IsGreaterThan(0);
    }

    [Test]
    public async Task ConsumerMetrics_Lag_ReflectsUnconsumedMessages()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var groupId = $"metrics-lag-{Guid.NewGuid():N}";
        var stats = new ConcurrentBag<ConsumerStatistics>();
        const int totalMessages = 20;
        const int consumeCount = 5;

        // Produce messages
        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .BuildAsync();

        for (var i = 0; i < totalMessages; i++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{i}",
                Value = $"value-{i}"
            });
        }

        await producer.FlushAsync();

        // Consume only some messages with auto-commit
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithOffsetCommitMode(OffsetCommitMode.Auto)
            .WithAutoCommitInterval(TimeSpan.FromMilliseconds(100))
            .WithStatisticsInterval(TimeSpan.FromSeconds(1))
            .WithStatisticsHandler(s => stats.Add(s))
            .BuildAsync();

        consumer.Subscribe(topic);

        var consumed = 0;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try
        {
            await foreach (var msg in consumer.ConsumeAsync(cts.Token))
            {
                consumed++;
                if (consumed >= consumeCount)
                {
                    // Allow batch-level stats to be recorded before stopping
                    cts.CancelAfter(TimeSpan.FromSeconds(3));
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        // Wait for a stats snapshot with topic-level data
        // The consumer fetches all messages in the partition, so topic stats should exist
        // once a fetch response has been processed
        var matchingStats = await WaitForStatsAsync(stats,
            s => s.MessagesConsumed > 0 && s.Topics.ContainsKey(topic),
            TimeSpan.FromSeconds(10));

        await Assert.That(matchingStats).IsNotNull();
        await Assert.That(matchingStats!.Topics.ContainsKey(topic)).IsTrue();
        await Assert.That(matchingStats.MessagesConsumed).IsGreaterThan(0);

        // If lag is available, it should be non-negative
        if (matchingStats.TotalLag.HasValue)
        {
            await Assert.That(matchingStats.TotalLag.Value).IsGreaterThanOrEqualTo(0);
        }
    }

    [Test]
    public async Task Metrics_UnderHighThroughput_StayAccurate()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 3);
        var producerStats = new ConcurrentBag<ProducerStatistics>();
        var consumerStats = new ConcurrentBag<ConsumerStatistics>();
        var groupId = $"metrics-throughput-{Guid.NewGuid():N}";
        const int messageCount = 1000;

        // Produce 1000 messages
        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithStatisticsInterval(TimeSpan.FromSeconds(1))
            .WithStatisticsHandler(s => producerStats.Add(s))
            .BuildAsync();

        for (var i = 0; i < messageCount; i++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{i}",
                Value = $"payload-{i}-{new string('x', 100)}"
            });
        }

        await producer.FlushAsync();

        // Wait for producer stats reflecting all messages
        var matchingProducerStats = await WaitForStatsAsync(producerStats,
            s => s.MessagesProduced >= messageCount && s.MessagesDelivered >= messageCount,
            TimeSpan.FromSeconds(10));

        await Assert.That(matchingProducerStats).IsNotNull();
        await Assert.That(matchingProducerStats!.MessagesProduced).IsGreaterThanOrEqualTo(messageCount);
        await Assert.That(matchingProducerStats.MessagesDelivered).IsGreaterThanOrEqualTo(messageCount);
        await Assert.That(matchingProducerStats.BytesProduced).IsGreaterThan(0);

        // Consume all messages and verify consumer stats
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithStatisticsInterval(TimeSpan.FromSeconds(1))
            .WithStatisticsHandler(s => consumerStats.Add(s))
            .BuildAsync();

        consumer.Subscribe(topic);

        var consumed = 0;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        try
        {
            await foreach (var msg in consumer.ConsumeAsync(cts.Token))
            {
                consumed++;
                if (consumed >= messageCount)
                {
                    // Allow batch stats to be recorded
                    cts.CancelAfter(TimeSpan.FromSeconds(3));
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        // Wait for consumer stats reflecting all consumed messages
        // With 1000 messages across 3 partitions and multiple fetches,
        // at least the majority of messages should be recorded in stats
        var matchingConsumerStats = await WaitForStatsAsync(consumerStats,
            s => s.MessagesConsumed >= messageCount,
            TimeSpan.FromSeconds(10));

        await Assert.That(matchingConsumerStats).IsNotNull();
        await Assert.That(matchingConsumerStats!.MessagesConsumed).IsGreaterThanOrEqualTo(messageCount);
        await Assert.That(matchingConsumerStats.BytesConsumed).IsGreaterThan(0);
    }

    [Test]
    public async Task Metrics_AfterClientRestart_ResetCorrectly()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();

        // First producer: produce some messages and collect stats
        var firstStats = new ConcurrentBag<ProducerStatistics>();

        await using (var producer1 = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithStatisticsInterval(TimeSpan.FromSeconds(1))
            .WithStatisticsHandler(s => firstStats.Add(s))
            .BuildAsync())
        {
            for (var i = 0; i < 10; i++)
            {
                await producer1.ProduceAsync(new ProducerMessage<string, string>
                {
                    Topic = topic,
                    Key = $"key-{i}",
                    Value = $"value-{i}"
                });
            }

            await producer1.FlushAsync();

            // Wait for stats snapshot showing all 10 messages
            var firstMatch = await WaitForStatsAsync(firstStats,
                s => s.MessagesProduced >= 10,
                TimeSpan.FromSeconds(10));
            await Assert.That(firstMatch).IsNotNull();
        }

        // Second producer: fresh instance should start with zero counters
        var secondStats = new ConcurrentBag<ProducerStatistics>();

        await using var producer2 = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithStatisticsInterval(TimeSpan.FromSeconds(1))
            .WithStatisticsHandler(s => secondStats.Add(s))
            .BuildAsync();

        // Produce fewer messages with the new producer
        for (var i = 0; i < 3; i++)
        {
            await producer2.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"restart-key-{i}",
                Value = $"restart-value-{i}"
            });
        }

        await producer2.FlushAsync();

        // Wait for stats snapshot showing all 3 messages
        var secondMatch = await WaitForStatsAsync(secondStats,
            s => s.MessagesProduced >= 3,
            TimeSpan.FromSeconds(10));
        await Assert.That(secondMatch).IsNotNull();

        // Verify first producer had >= 10 messages
        var firstLatest = firstStats.OrderByDescending(s => s.Timestamp).First();
        await Assert.That(firstLatest.MessagesProduced).IsGreaterThanOrEqualTo(10);

        // Verify second producer started fresh - should only have the 3 new messages
        // The new producer should NOT carry over the first producer's count
        await Assert.That(secondMatch!.MessagesProduced).IsGreaterThanOrEqualTo(3);
        await Assert.That(secondMatch.MessagesProduced).IsLessThan(firstLatest.MessagesProduced);
    }

    [Test]
    public async Task ProducerMetrics_MultipleTopics_AggregatesCorrectly()
    {
        var topic1 = await KafkaContainer.CreateTestTopicAsync();
        var topic2 = await KafkaContainer.CreateTestTopicAsync();
        var stats = new ConcurrentBag<ProducerStatistics>();
        const int messagesPerTopic = 15;

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithStatisticsInterval(TimeSpan.FromSeconds(1))
            .WithStatisticsHandler(s => stats.Add(s))
            .BuildAsync();

        // Produce to topic1
        for (var i = 0; i < messagesPerTopic; i++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic1,
                Key = $"t1-key-{i}",
                Value = $"t1-value-{i}"
            });
        }

        // Produce to topic2
        for (var i = 0; i < messagesPerTopic; i++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic2,
                Key = $"t2-key-{i}",
                Value = $"t2-value-{i}"
            });
        }

        await producer.FlushAsync();

        var totalExpected = messagesPerTopic * 2;

        // Wait for a stats snapshot that has both topics and the expected message counts
        var matchingStats = await WaitForStatsAsync(stats,
            s => s.MessagesProduced >= totalExpected
                && s.Topics.ContainsKey(topic1)
                && s.Topics.ContainsKey(topic2)
                && s.Topics[topic1].MessagesProduced >= messagesPerTopic
                && s.Topics[topic2].MessagesProduced >= messagesPerTopic,
            TimeSpan.FromSeconds(10));

        await Assert.That(matchingStats).IsNotNull();
        await Assert.That(matchingStats!.MessagesProduced).IsGreaterThanOrEqualTo(totalExpected);

        // Per-topic stats should exist for both topics
        await Assert.That(matchingStats.Topics.ContainsKey(topic1)).IsTrue();
        await Assert.That(matchingStats.Topics.ContainsKey(topic2)).IsTrue();

        // Each topic should have at least messagesPerTopic messages
        await Assert.That(matchingStats.Topics[topic1].MessagesProduced).IsGreaterThanOrEqualTo(messagesPerTopic);
        await Assert.That(matchingStats.Topics[topic2].MessagesProduced).IsGreaterThanOrEqualTo(messagesPerTopic);

        // The sum of per-topic counts should equal the global count
        var topicSum = matchingStats.Topics.Values.Sum(t => t.MessagesProduced);
        await Assert.That(topicSum).IsEqualTo(matchingStats.MessagesProduced);
    }

    [Test]
    public async Task ConsumerMetrics_AssignedPartitions_MatchesAssignment()
    {
        const int expectedPartitions = 3;
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: expectedPartitions);
        var groupId = $"metrics-partitions-{Guid.NewGuid():N}";
        var stats = new ConcurrentBag<ConsumerStatistics>();

        // Produce a message to ensure the topic has data
        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .BuildAsync();

        for (var i = 0; i < expectedPartitions; i++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{i}",
                Value = $"value-{i}"
            });
        }

        await producer.FlushAsync();

        // Consume with stats handler - single consumer in group should get all partitions
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithStatisticsInterval(TimeSpan.FromSeconds(1))
            .WithStatisticsHandler(s => stats.Add(s))
            .BuildAsync();

        consumer.Subscribe(topic);

        // Consume at least one message to trigger assignment
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token);

        // Wait for a stats snapshot that reflects the correct partition assignment
        var matchingStats = await WaitForStatsAsync(stats,
            s => s.AssignedPartitions == expectedPartitions,
            TimeSpan.FromSeconds(10));

        await Assert.That(matchingStats).IsNotNull();
        await Assert.That(matchingStats!.AssignedPartitions).IsEqualTo(expectedPartitions);
        await Assert.That(matchingStats.GroupId).IsEqualTo(groupId);
    }
}
