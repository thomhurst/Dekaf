namespace Dekaf.Tests.Unit.Consumer;

/// <summary>
/// Tests for consumer initialization behavior.
/// Verifies that the consumer requires explicit initialization before consuming messages.
/// </summary>
public sealed class ConsumerInitializationTests
{
    [Test]
    public async Task ConsumeAsync_WithoutInitialize_ThrowsInvalidOperationException()
    {
        await using var consumer = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithGroupId("test-group")
            .Build();

        consumer.Subscribe("test-topic");

        await Assert.That(async () =>
        {
            await foreach (var _ in consumer.ConsumeAsync())
            {
                break;
            }
        }).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task QueryWatermarkOffsetsAsync_WithoutInitialize_ThrowsInvalidOperationException()
    {
        await using var consumer = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithGroupId("test-group")
            .Build();

        await Assert.That(async () =>
        {
            await consumer.QueryWatermarkOffsetsAsync(
                new Dekaf.Producer.TopicPartition("test-topic", 0));
        }).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task GetOffsetsForTimesAsync_WithoutInitialize_ThrowsInvalidOperationException()
    {
        await using var consumer = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithGroupId("test-group")
            .Build();

        await Assert.That(async () =>
        {
            await consumer.GetOffsetsForTimesAsync([
                new Dekaf.Producer.TopicPartitionTimestamp("test-topic", 0, -2)
            ]);
        }).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task InitializeAsync_AfterDispose_ThrowsObjectDisposedException()
    {
        var consumer = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers("localhost:9092")
            .WithGroupId("test-group")
            .Build();

        await consumer.DisposeAsync();

        await Assert.That(async () =>
        {
            await consumer.InitializeAsync();
        }).Throws<ObjectDisposedException>();
    }
}
