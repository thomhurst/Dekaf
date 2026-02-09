using Dekaf.Consumer;
using Dekaf.Errors;
using Dekaf.Producer;
using Dekaf.Serialization;

namespace Dekaf.Tests.Integration.RealWorld;

/// <summary>
/// Integration tests for producer error handling and edge cases.
/// Verifies behavior with null values (tombstones), empty strings, large messages,
/// use-after-dispose, and fire-and-forget callbacks.
/// </summary>
public sealed class ProducerErrorHandlingTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    [Test]
    public async Task Producer_NullValue_ProducesSuccessfully()
    {
        // Null values represent tombstones in Kafka (used for log compaction deletion)
        // The producer skips serialization for null values, producing a proper null record
        var topic = await KafkaContainer.CreateTestTopicAsync().ConfigureAwait(false);

        await using var producer = await Kafka.CreateProducer<string, string?>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer-null-value")
            .BuildAsync();

        // Act
        var metadata = await producer.ProduceAsync(new ProducerMessage<string, string?>
        {
            Topic = topic,
            Key = "tombstone-key",
            Value = null!
        }).ConfigureAwait(false);

        // Assert
        await Assert.That(metadata.Topic).IsEqualTo(topic);
        await Assert.That(metadata.Offset).IsGreaterThanOrEqualTo(0);

        // Verify by consuming - use Serializers.Null for deserialization since the value is null
        var nullSerde = Serializers.Null<string>();
        await using var consumer = await Kafka.CreateConsumer<string, string?>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer-null-value")
            .WithGroupId($"test-group-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithValueDeserializer(nullSerde)
            .BuildAsync();

        consumer.Subscribe(topic);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token).ConfigureAwait(false);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value.Key).IsEqualTo("tombstone-key");
        await Assert.That(result.Value.Value).IsNull();
    }

    [Test]
    public async Task Producer_EmptyStringKeyAndValue_ProducesSuccessfully()
    {
        // Arrange
        var topic = await KafkaContainer.CreateTestTopicAsync().ConfigureAwait(false);

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer-empty")
            .BuildAsync();

        // Act
        var metadata = await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = string.Empty,
            Value = string.Empty
        }).ConfigureAwait(false);

        // Assert
        await Assert.That(metadata.Topic).IsEqualTo(topic);
        await Assert.That(metadata.Offset).IsGreaterThanOrEqualTo(0);

        // Verify round-trip
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer-empty")
            .WithGroupId($"test-group-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        consumer.Subscribe(topic);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token).ConfigureAwait(false);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value.Key).IsEqualTo(string.Empty);
        await Assert.That(result.Value.Value).IsEqualTo(string.Empty);
    }

    [Test]
    public async Task Producer_VeryLargeMessage_1MB_ProducesSuccessfully()
    {
        // Arrange - 1MB message body (default max.request.size is 1MB)
        var topic = await KafkaContainer.CreateTestTopicAsync().ConfigureAwait(false);
        var largeValue = new string('x', 900_000); // ~900KB to stay under 1MB with overhead

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer-large-1mb")
            .BuildAsync();

        // Act
        var metadata = await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "large-key",
            Value = largeValue
        }).ConfigureAwait(false);

        // Assert
        await Assert.That(metadata.Topic).IsEqualTo(topic);
        await Assert.That(metadata.Offset).IsGreaterThanOrEqualTo(0);

        // Verify by consuming
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer-large-1mb")
            .WithGroupId($"test-group-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        consumer.Subscribe(topic);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token).ConfigureAwait(false);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value.Value).IsEqualTo(largeValue);
    }

    [Test]
    public async Task Producer_ProduceAfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        var topic = await KafkaContainer.CreateTestTopicAsync().ConfigureAwait(false);

        var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer-disposed")
            .BuildAsync();

        await producer.DisposeAsync().ConfigureAwait(false);

        // Act & Assert
        await Assert.That(async () =>
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = "key",
                Value = "value"
            }).ConfigureAwait(false);
        }).Throws<ObjectDisposedException>();
    }

    [Test]
    public async Task Producer_SendWithCallback_ReceivesMetadata()
    {
        // Arrange
        var topic = await KafkaContainer.CreateTestTopicAsync().ConfigureAwait(false);
        var callbackInvoked = new TaskCompletionSource<(RecordMetadata Metadata, Exception? Error)>();

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer-callback")
            .WithAcks(Acks.Leader)
            .BuildAsync();

        // Act - send with delivery callback
        producer.Send(
            new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = "callback-key",
                Value = "callback-value"
            },
            (metadata, error) => callbackInvoked.TrySetResult((metadata, error)));

        // Wait for callback with timeout
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        cts.Token.Register(() => callbackInvoked.TrySetCanceled());

        var (resultMetadata, resultError) = await callbackInvoked.Task.ConfigureAwait(false);

        // Assert
        await Assert.That(resultError).IsNull();
        await Assert.That(resultMetadata.Topic).IsEqualTo(topic);
        await Assert.That(resultMetadata.Partition).IsGreaterThanOrEqualTo(0);
        await Assert.That(resultMetadata.Offset).IsGreaterThanOrEqualTo(0);
    }
}
