using Dekaf.Consumer;
using Dekaf.Producer;

namespace Dekaf.Tests.Integration.RealWorld;

/// <summary>
/// Tests for fan-out patterns where multiple independent consumer groups
/// each receive a full copy of all messages from the same topic.
/// Common in event-driven architectures where different services need the same data.
/// </summary>
public sealed class FanOutPatternTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    [Test]
    public async Task FanOut_MultipleConsumerGroups_EachReceivesAllMessages()
    {
        // Simulate: order-events consumed by billing, shipping, and notification services
        var topic = await KafkaContainer.CreateTestTopicAsync();
        const int messageCount = 10;

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .BuildAsync();

        for (var i = 0; i < messageCount; i++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"order-{i}",
                Value = $"order-created-{i}"
            });
        }

        // Three independent consumer groups
        var billingGroup = $"billing-{Guid.NewGuid():N}";
        var shippingGroup = $"shipping-{Guid.NewGuid():N}";
        var notificationGroup = $"notification-{Guid.NewGuid():N}";

        var billingMessages = await ConsumeAllAsync(topic, billingGroup, messageCount);
        var shippingMessages = await ConsumeAllAsync(topic, shippingGroup, messageCount);
        var notificationMessages = await ConsumeAllAsync(topic, notificationGroup, messageCount);

        // Each group should get all messages independently
        await Assert.That(billingMessages).Count().IsEqualTo(messageCount);
        await Assert.That(shippingMessages).Count().IsEqualTo(messageCount);
        await Assert.That(notificationMessages).Count().IsEqualTo(messageCount);

        // Verify message content is the same across groups
        var billingValues = billingMessages.Select(m => m.Value).OrderBy(v => v).ToList();
        var shippingValues = shippingMessages.Select(m => m.Value).OrderBy(v => v).ToList();
        var notificationValues = notificationMessages.Select(m => m.Value).OrderBy(v => v).ToList();

        await Assert.That(billingValues).IsEquivalentTo(shippingValues);
        await Assert.That(shippingValues).IsEquivalentTo(notificationValues);
    }

    [Test]
    public async Task FanOut_ConsumerGroupsAtDifferentSpeeds_IndependentProgress()
    {
        // Fast consumer reads all, slow consumer reads partial - they don't affect each other
        var topic = await KafkaContainer.CreateTestTopicAsync();
        const int messageCount = 10;

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

        // Fast consumer reads all 10
        var fastGroup = $"fast-{Guid.NewGuid():N}";
        await using var fastConsumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(fastGroup)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithOffsetCommitMode(OffsetCommitMode.Manual)
            .BuildAsync();

        fastConsumer.Subscribe(topic);

        var fastMessages = new List<ConsumeResult<string, string>>();
        using var cts1 = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await foreach (var msg in fastConsumer.ConsumeAsync(cts1.Token))
        {
            fastMessages.Add(msg);
            if (fastMessages.Count >= messageCount) break;
        }

        await fastConsumer.CommitAsync();

        // Slow consumer reads only 3 and commits
        var slowGroup = $"slow-{Guid.NewGuid():N}";
        await using var slowConsumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(slowGroup)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithOffsetCommitMode(OffsetCommitMode.Manual)
            .BuildAsync();

        slowConsumer.Subscribe(topic);

        var slowMessages = new List<ConsumeResult<string, string>>();
        using var cts2 = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await foreach (var msg in slowConsumer.ConsumeAsync(cts2.Token))
        {
            slowMessages.Add(msg);
            if (slowMessages.Count >= 3) break;
        }

        await slowConsumer.CommitAsync();

        await Assert.That(fastMessages).Count().IsEqualTo(messageCount);
        await Assert.That(slowMessages).Count().IsEqualTo(3);

        // Verify fast consumer committed offset at 10
        var fastOffset = await fastConsumer.GetCommittedOffsetAsync(new TopicPartition(topic, 0));
        await Assert.That(fastOffset).IsEqualTo(messageCount);

        // Verify slow consumer committed offset at 3
        var slowOffset = await slowConsumer.GetCommittedOffsetAsync(new TopicPartition(topic, 0));
        await Assert.That(slowOffset).IsEqualTo(3);
    }

    [Test]
    public async Task FanOut_NewConsumerGroupJoinsLate_GetsAllHistoricalMessages()
    {
        // A new service joins after events have already been produced
        var topic = await KafkaContainer.CreateTestTopicAsync();

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .BuildAsync();

        // Produce messages before any consumer exists
        for (var i = 0; i < 5; i++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{i}",
                Value = $"historical-{i}"
            });
        }

        // Original consumer processes everything
        var originalGroup = $"original-{Guid.NewGuid():N}";
        var originalMessages = await ConsumeAllAsync(topic, originalGroup, 5);
        await Assert.That(originalMessages).Count().IsEqualTo(5);

        // Produce more messages
        for (var i = 5; i < 10; i++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{i}",
                Value = $"recent-{i}"
            });
        }

        // Late-joining consumer with Earliest offset reset gets ALL messages (0-9)
        var lateGroup = $"late-joiner-{Guid.NewGuid():N}";
        var lateMessages = await ConsumeAllAsync(topic, lateGroup, 10);

        await Assert.That(lateMessages).Count().IsEqualTo(10);
        await Assert.That(lateMessages[0].Value).IsEqualTo("historical-0");
        await Assert.That(lateMessages[9].Value).IsEqualTo("recent-9");
    }

    [Test]
    public async Task FanOut_ConcurrentConsumerGroups_AllConsumeSimultaneously()
    {
        // Multiple consumer groups consuming the same topic at the same time
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 3);
        const int messageCount = 30;

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .BuildAsync();

        for (var i = 0; i < messageCount; i++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{i}",
                Value = $"concurrent-value-{i}"
            });
        }

        // Launch 3 consumer groups consuming concurrently
        var groups = Enumerable.Range(0, 3)
            .Select(_ => $"concurrent-group-{Guid.NewGuid():N}")
            .ToArray();

        var consumeTasks = groups.Select(group => Task.Run(async () =>
        {
            await using var consumer = await Kafka.CreateConsumer<string, string>()
                .WithBootstrapServers(KafkaContainer.BootstrapServers)
                .WithGroupId(group)
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

            return messages;
        })).ToArray();

        var results = await Task.WhenAll(consumeTasks);

        // Each concurrent group should have received all messages
        foreach (var groupMessages in results)
        {
            await Assert.That(groupMessages).Count().IsEqualTo(messageCount);
        }
    }

    private async Task<List<ConsumeResult<string, string>>> ConsumeAllAsync(
        string topic, string groupId, int expectedCount)
    {
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        consumer.Subscribe(topic);

        var messages = new List<ConsumeResult<string, string>>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            messages.Add(msg);
            if (messages.Count >= expectedCount) break;
        }

        return messages;
    }
}
