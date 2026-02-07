using System.Collections.Concurrent;
using Dekaf.Consumer;
using Dekaf.Producer;
using Dekaf.Statistics;

namespace Dekaf.Tests.Integration;

/// <summary>
/// Integration tests for producer and consumer statistics reporting.
/// </summary>
public sealed class StatisticsTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    [Test]
    public async Task ProducerStats_AfterProducing_ReportsMessagesAndBytes()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var stats = new ConcurrentBag<ProducerStatistics>();
        const int messageCount = 10;

        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithStatisticsInterval(TimeSpan.FromSeconds(1))
            .WithStatisticsHandler(s => stats.Add(s))
            .Build();

        for (var i = 0; i < messageCount; i++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"stats-key-{i}",
                Value = $"stats-value-{i}"
            });
        }

        await producer.FlushAsync();

        // Wait for at least one stats callback
        await Task.Delay(2000).ConfigureAwait(false);

        await Assert.That(stats.Count).IsGreaterThanOrEqualTo(1);

        var latestStats = stats.OrderByDescending(s => s.Timestamp).First();
        await Assert.That(latestStats.MessagesProduced).IsGreaterThanOrEqualTo(messageCount);
        await Assert.That(latestStats.BytesProduced).IsGreaterThan(0);
    }

    [Test]
    public async Task ConsumerStats_AfterConsuming_ReportsMessagesAndBytes()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var stats = new ConcurrentBag<ConsumerStatistics>();

        // Produce messages first
        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .Build();

        for (var i = 0; i < 10; i++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{i}",
                Value = $"value-{i}"
            });
        }

        // Consume with statistics
        await using var consumer = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId($"stats-consumer-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithStatisticsInterval(TimeSpan.FromSeconds(1))
            .WithStatisticsHandler(s => stats.Add(s))
            .Build();

        consumer.Subscribe(topic);

        var messages = new List<ConsumeResult<string, string>>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            messages.Add(msg);
            if (messages.Count >= 10) break;
        }

        // Poll until stats report consumed messages (stats are collected asynchronously)
        ConsumerStatistics? latestStats = null;
        for (var i = 0; i < 10; i++)
        {
            await Task.Delay(1000).ConfigureAwait(false);
            latestStats = stats
                .OrderByDescending(s => s.Timestamp)
                .FirstOrDefault(s => s.MessagesConsumed > 0);
            if (latestStats is not null) break;
        }

        await Assert.That(latestStats).IsNotNull();
        await Assert.That(latestStats!.MessagesConsumed).IsGreaterThan(0);
        await Assert.That(latestStats.BytesConsumed).IsGreaterThan(0);
    }

    [Test]
    public async Task ProducerStats_PeriodicCallback_CalledMultipleTimes()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var stats = new ConcurrentBag<ProducerStatistics>();

        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithStatisticsInterval(TimeSpan.FromSeconds(1))
            .WithStatisticsHandler(s => stats.Add(s))
            .Build();

        // Produce some messages to generate activity
        for (var i = 0; i < 5; i++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{i}",
                Value = $"value-{i}"
            });
        }

        // Wait for multiple stats intervals
        await Task.Delay(3500).ConfigureAwait(false);

        await Assert.That(stats.Count).IsGreaterThanOrEqualTo(2);
    }

    [Test]
    public async Task ConsumerStats_GroupInfo_ReportsGroupMetadata()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var groupId = $"stats-group-{Guid.NewGuid():N}";
        var stats = new ConcurrentBag<ConsumerStatistics>();

        // Produce a message
        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .Build();

        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "key",
            Value = "value"
        });

        // Consume with group to trigger rebalance and assignment
        await using var consumer = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithStatisticsInterval(TimeSpan.FromSeconds(1))
            .WithStatisticsHandler(s => stats.Add(s))
            .Build();

        consumer.Subscribe(topic);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token);

        // Wait for stats to include group info
        await Task.Delay(2000).ConfigureAwait(false);

        await Assert.That(stats.Count).IsGreaterThanOrEqualTo(1);

        var latestStats = stats.OrderByDescending(s => s.Timestamp).First();
        await Assert.That(latestStats.GroupId).IsEqualTo(groupId);
        await Assert.That(latestStats.MemberId).IsNotNull();
        await Assert.That(latestStats.AssignedPartitions).IsGreaterThan(0);
    }
}
