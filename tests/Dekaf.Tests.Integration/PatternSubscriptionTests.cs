using Dekaf.Producer;

namespace Dekaf.Tests.Integration;

/// <summary>
/// Integration tests for consumer pattern subscription (Subscribe with topic filter).
/// </summary>
public class PatternSubscriptionTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    [Test]
    public async Task PatternSubscription_MatchesExistingTopics()
    {
        var prefix = $"pattern-test-{Guid.NewGuid():N}";
        var topic1 = $"{prefix}-alpha";
        var topic2 = $"{prefix}-beta";
        var unrelatedTopic = $"other-{Guid.NewGuid():N}";

        await KafkaContainer.CreateTopicAsync(topic1);
        await KafkaContainer.CreateTopicAsync(topic2);
        await KafkaContainer.CreateTopicAsync(unrelatedTopic);

        // Produce a message to each matching topic
        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .Build();

        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic1,
            Key = "key1",
            Value = "value1"
        });

        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic2,
            Key = "key2",
            Value = "value2"
        });

        await producer.FlushAsync();

        // Subscribe with a filter matching the prefix
        await using var consumer = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId($"pattern-consumer-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(Consumer.AutoOffsetReset.Earliest)
            .Build();

        consumer.Subscribe(topic => topic.StartsWith(prefix, StringComparison.Ordinal));

        var messages = new List<string>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await foreach (var record in consumer.ConsumeAsync(cts.Token))
        {
            messages.Add(record.Value);
            if (messages.Count >= 2)
                break;
        }

        await Assert.That(messages).Count().IsEqualTo(2);
        await Assert.That(messages).Contains("value1");
        await Assert.That(messages).Contains("value2");
    }

    [Test]
    public async Task PatternSubscription_ExcludesNonMatchingTopics()
    {
        var prefix = $"include-{Guid.NewGuid():N}";
        var matchingTopic = $"{prefix}-match";
        var excludedTopic = $"exclude-{Guid.NewGuid():N}";

        await KafkaContainer.CreateTopicAsync(matchingTopic);
        await KafkaContainer.CreateTopicAsync(excludedTopic);

        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .Build();

        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = matchingTopic,
            Key = "k1",
            Value = "included"
        });

        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = excludedTopic,
            Key = "k2",
            Value = "excluded"
        });

        await producer.FlushAsync();

        await using var consumer = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId($"pattern-consumer-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(Consumer.AutoOffsetReset.Earliest)
            .Build();

        consumer.Subscribe(topic => topic.StartsWith(prefix, StringComparison.Ordinal));

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var consumed = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token);

        await Assert.That(consumed).IsNotNull();
        await Assert.That(consumed!.Value.Value).IsEqualTo("included");
    }

    [Test]
    public async Task PatternSubscription_SwitchToExplicitTopics_Works()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();

        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .Build();

        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "key",
            Value = "explicit-value"
        });

        await producer.FlushAsync();

        await using var consumer = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId($"pattern-consumer-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(Consumer.AutoOffsetReset.Earliest)
            .Build();

        // Start with pattern subscription
        consumer.Subscribe(t => t.Contains("nonexistent-pattern"));

        // Switch to explicit topic subscription
        consumer.Subscribe(topic);

        await Assert.That(consumer.Subscription).Contains(topic);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var consumed = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token);

        await Assert.That(consumed).IsNotNull();
        await Assert.That(consumed!.Value.Value).IsEqualTo("explicit-value");
    }

    [Test]
    public async Task PatternSubscription_Unsubscribe_StopsConsuming()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();

        await using var consumer = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId($"pattern-consumer-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(Consumer.AutoOffsetReset.Earliest)
            .Build();

        consumer.Subscribe(t => t == topic);

        // Unsubscribe should clear the filter
        consumer.Unsubscribe();

        await Assert.That(consumer.Subscription.Count).IsEqualTo(0);
        await Assert.That(consumer.Assignment.Count).IsEqualTo(0);
    }
}
