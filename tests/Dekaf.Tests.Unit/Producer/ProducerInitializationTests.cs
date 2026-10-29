using Dekaf.Producer;
using Dekaf.Serialization;

namespace Dekaf.Tests.Unit.Producer;

/// <summary>
/// Tests for producer initialization behavior.
/// Verifies that the producer requires explicit initialization before producing messages.
/// </summary>
public sealed class ProducerInitializationTests
{
    [Test]
    [Arguments("")]
    [Arguments(" ")]
    [Arguments("\t\r\n")]
    [Arguments("\u2003")]
    public async Task Constructor_BlankTransactionalId_ThrowsArgumentException(string transactionalId)
    {
        var options = new ProducerOptions
        {
            BootstrapServers = ["localhost:9092"],
            TransactionalId = transactionalId
        };

        var exception = await Assert.That(async () =>
        {
            await using var producer = new KafkaProducer<string, string>(
                options, Serializers.String, Serializers.String);
        }).Throws<ArgumentException>();

        await Assert.That(exception!.ParamName).IsEqualTo("options.TransactionalId");
    }

    [Test]
    [Arguments(null)]
    [Arguments("txn-1")]
    [Arguments(" txn-1 ")]
    public async Task Constructor_NullOrNonBlankTransactionalId_Succeeds(string? transactionalId)
    {
        var options = new ProducerOptions
        {
            BootstrapServers = ["localhost:9092"],
            TransactionalId = transactionalId
        };

        await using var producer = new KafkaProducer<string, string>(
            options, Serializers.String, Serializers.String);

        await Assert.That(producer).IsNotNull();
    }

    [Test]
    public async Task ProduceAsync_WithoutInitialize_ThrowsInvalidOperationException()
    {
        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .Build();

        await Assert.That(async () =>
        {
            await producer.FireAsync(new ProducerMessage<string, string>
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
            await producer.FireAsync("test", "key", "value");
        }).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task ProduceAsync_FireAndForget_WithoutInitialize_ThrowsInvalidOperationException()
    {
        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .Build();

        await Assert.That(() =>
        {
            producer.FireAsync(new ProducerMessage<string, string>
            {
                Topic = "test",
                Key = "key",
                Value = "value"
            });
        }).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task ProduceAsync_FireAndForget_TopicOverload_WithoutInitialize_ThrowsInvalidOperationException()
    {
        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .Build();

        await Assert.That(() =>
        {
            producer.FireAsync("test", "key", "value");
        }).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task ProduceAsync_WithCallback_WithoutInitialize_ThrowsInvalidOperationException()
    {
        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .Build();

        await Assert.That(() =>
        {
            producer.FireAsync(
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
    public async Task PurgeAsync_WithoutInitialize_ThrowsInvalidOperationException()
    {
        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .Build();

        await Assert.That(async () =>
        {
            await producer.PurgeAsync(PurgeOptions.All);
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

    [Test]
    public async Task PurgeAsync_AfterDispose_ThrowsObjectDisposedException()
    {
        var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .Build();

        await producer.DisposeAsync();

        await Assert.That(async () =>
        {
            await producer.PurgeAsync(PurgeOptions.All);
        }).Throws<ObjectDisposedException>();
    }
}
