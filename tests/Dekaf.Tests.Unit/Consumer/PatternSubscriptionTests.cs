namespace Dekaf.Tests.Unit.Consumer;

/// <summary>
/// Tests for consumer pattern subscription behavior.
/// These tests verify the subscribe/unsubscribe state management without a Kafka broker.
/// </summary>
public sealed class PatternSubscriptionTests
{
    [Test]
    public async Task Subscribe_WithNullFilter_ThrowsArgumentNullException()
    {
        await using var consumer = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .Build();

        var act = () => consumer.Subscribe((Func<string, bool>)null!);
        await Assert.That(act).Throws<ArgumentNullException>();
    }

    [Test]
    public async Task Subscribe_WithFilter_ClearsExistingSubscription()
    {
        await using var consumer = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .Build();

        // Subscribe to explicit topics first
        consumer.Subscribe("topic-a", "topic-b");
        await Assert.That(consumer.Subscription.Count).IsEqualTo(2);

        // Subscribe with filter - should clear explicit subscription
        consumer.Subscribe(topic => topic.StartsWith("prefix-", StringComparison.Ordinal));
        await Assert.That(consumer.Subscription.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Subscribe_WithTopics_ClearsFilter()
    {
        await using var consumer = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .Build();

        // Subscribe with filter first
        consumer.Subscribe(topic => topic.StartsWith("prefix-", StringComparison.Ordinal));

        // Subscribe to explicit topics - should clear filter
        consumer.Subscribe("topic-a");
        await Assert.That(consumer.Subscription.Count).IsEqualTo(1);
        await Assert.That(consumer.Subscription).Contains("topic-a");
    }

    [Test]
    public async Task Unsubscribe_ClearsFilter()
    {
        await using var consumer = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .Build();

        // Subscribe with filter
        consumer.Subscribe(topic => topic.StartsWith("prefix-", StringComparison.Ordinal));

        // Unsubscribe - should clear everything including the filter
        consumer.Unsubscribe();
        await Assert.That(consumer.Subscription.Count).IsEqualTo(0);
        await Assert.That(consumer.Assignment.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Subscribe_WithFilter_ClearsAssignment()
    {
        await using var consumer = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .Build();

        // Assign partitions first
        consumer.Assign(new Dekaf.Producer.TopicPartition("topic-a", 0));
        await Assert.That(consumer.Assignment.Count).IsEqualTo(1);

        // Subscribe with filter - should clear assignment
        consumer.Subscribe(topic => topic.StartsWith("prefix-", StringComparison.Ordinal));
        await Assert.That(consumer.Assignment.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Subscribe_WithFilter_ReturnsSelf()
    {
        await using var consumer = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .Build();

        var result = consumer.Subscribe(topic => topic.Contains("test"));
        await Assert.That(result).IsEqualTo(consumer);
    }
}
