namespace Dekaf.Tests.Unit.Producer;

/// <summary>
/// Tests for topic producer initialization behavior.
/// </summary>
public sealed class TopicProducerInitializationTests
{
    [Test]
    public async Task ProduceAsync_WithoutInitialize_ThrowsInvalidOperationException()
    {
        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .BuildForTopic("test-topic");

        await Assert.That(async () =>
        {
            await producer.ProduceAsync("key", "value");
        }).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task Send_WithoutInitialize_ThrowsInvalidOperationException()
    {
        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .BuildForTopic("test-topic");

        await Assert.That(() =>
        {
            producer.Send("key", "value");
        }).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task FlushAsync_WithoutInitialize_ThrowsInvalidOperationException()
    {
        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .BuildForTopic("test-topic");

        await Assert.That(async () =>
        {
            await producer.FlushAsync();
        }).Throws<InvalidOperationException>();
    }
}
