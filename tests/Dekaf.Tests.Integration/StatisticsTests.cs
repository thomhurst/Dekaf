using System.Collections.Concurrent;
using Dekaf.Consumer;
using Dekaf.Producer;
using Dekaf.Statistics;

namespace Dekaf.Tests.Integration;

/// <summary>
/// Integration tests for producer and consumer statistics reporting.
/// </summary>
[Category("Messaging")]
[ParallelLimiter<MessagingTestLimit>]
public sealed class StatisticsTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    [Test]
    public async Task ProducerStats_AfterProducing_ReportsMessagesAndBytes()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var stats = new ConcurrentBag<ProducerStatistics>();
        const int messageCount = 10;

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithStatisticsInterval(TimeSpan.FromSeconds(1))
            .WithStatisticsHandler(s => stats.Add(s))
            .BuildAsync();

        // Warm up to ensure broker has initialized partition state
        await producer.ProduceAsync(new ProducerMessage<string, string>
            { Topic = topic, Key = "warmup", Value = "warmup" });

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

        // Poll until at least one stats callback fires
        await Assert.That(() => stats.Count)
            .Eventually(c => c.IsGreaterThanOrEqualTo(1), timeout: TimeSpan.FromSeconds(10));

        var latestStats = stats.OrderByDescending(s => s.Timestamp).First();
        await Assert.That(latestStats.MessagesProduced).IsGreaterThanOrEqualTo(messageCount + 1);
        await Assert.That(latestStats.BytesProduced).IsGreaterThan(0);
    }

    [Test]
    public async Task ProducerStats_PeriodicCallback_CalledMultipleTimes()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var stats = new ConcurrentBag<ProducerStatistics>();

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithStatisticsInterval(TimeSpan.FromSeconds(1))
            .WithStatisticsHandler(s => stats.Add(s))
            .BuildAsync();

        // Warm up to ensure broker has initialized partition state
        await producer.ProduceAsync(new ProducerMessage<string, string>
            { Topic = topic, Key = "warmup", Value = "warmup" });

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

        // Poll until multiple stats callbacks fire
        await Assert.That(() => stats.Count)
            .Eventually(c => c.IsGreaterThanOrEqualTo(2), timeout: TimeSpan.FromSeconds(10));
    }

    [Test]
    public async Task ConsumerStats_GroupInfo_ReportsGroupMetadata()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var groupId = $"stats-group-{Guid.NewGuid():N}";
        var stats = new ConcurrentBag<ConsumerStatistics>();

        // Produce a message
        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .BuildAsync();

        // Warm up to ensure broker has initialized partition state
        await producer.ProduceAsync(new ProducerMessage<string, string>
            { Topic = topic, Key = "warmup", Value = "warmup" });

        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "key",
            Value = "value"
        });

        // Consume with group to trigger rebalance and assignment
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithStatisticsInterval(TimeSpan.FromSeconds(1))
            .WithStatisticsHandler(s => stats.Add(s))
            .BuildAsync();

        consumer.Subscribe(topic);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token);

        // Poll until stats callback fires with group info
        await Assert.That(() => stats.Count)
            .Eventually(c => c.IsGreaterThanOrEqualTo(1), timeout: TimeSpan.FromSeconds(10));

        var latestStats = stats.OrderByDescending(s => s.Timestamp).First();
        await Assert.That(latestStats.GroupId).IsEqualTo(groupId);
        await Assert.That(latestStats.MemberId).IsNotNull();
        await Assert.That(latestStats.AssignedPartitions).IsGreaterThan(0);
    }
}
