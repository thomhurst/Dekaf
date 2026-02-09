using Dekaf.Producer;

namespace Dekaf.Tests.Unit.Producer;

/// <summary>
/// Tests for producer initialization behavior.
/// Verifies that the producer requires explicit initialization before producing messages.
/// </summary>
public sealed class ProducerInitializationTests
{
    [Test]
    public async Task ProduceAsync_WithoutInitialize_ThrowsInvalidOperationException()
    {
        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .Build();

        await Assert.That(async () =>
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = "test",
                Key = "key",
                Value = "value"
            });
        }).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task ProduceAsync_TopicOverload_WithoutInitialize_ThrowsInvalidOperationException()
    {
        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .Build();

        await Assert.That(async () =>
        {
            await producer.ProduceAsync("test", "key", "value");
        }).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task Send_WithoutInitialize_ThrowsInvalidOperationException()
    {
        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .Build();

        await Assert.That(() =>
        {
            producer.Send(new ProducerMessage<string, string>
            {
                Topic = "test",
                Key = "key",
                Value = "value"
            });
        }).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task Send_TopicOverload_WithoutInitialize_ThrowsInvalidOperationException()
    {
        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .Build();

        await Assert.That(() =>
        {
            producer.Send("test", "key", "value");
        }).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task Send_WithCallback_WithoutInitialize_ThrowsInvalidOperationException()
    {
        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .Build();

        await Assert.That(() =>
        {
            producer.Send(
                new ProducerMessage<string, string> { Topic = "test", Key = "key", Value = "value" },
                (_, _) => { });
        }).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task FlushAsync_WithoutInitialize_ThrowsInvalidOperationException()
    {
        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .Build();

        await Assert.That(async () =>
        {
            await producer.FlushAsync();
        }).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task InitializeAsync_AfterDispose_ThrowsObjectDisposedException()
    {
        var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .Build();

        await producer.DisposeAsync();

        await Assert.That(async () =>
        {
            await producer.InitializeAsync();
        }).Throws<ObjectDisposedException>();
    }
}
