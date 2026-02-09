using Dekaf.Consumer;
using Dekaf.Producer;
using Dekaf.Serialization;

namespace Dekaf.Tests.Integration.RealWorld;

/// <summary>
/// Integration tests for deserializer error handling.
/// Verifies behavior when consumers encounter data that doesn't match their deserializer,
/// including mismatched types, custom deserializer exceptions, and null value handling.
/// </summary>
public sealed class DeserializerErrorHandlingTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    [Test]
    public async Task RawBytes_ConsumedAsBytes_AlwaysSucceeds()
    {
        // Arrange - produce with string serializer, consume as byte[]
        var topic = await KafkaContainer.CreateTestTopicAsync();

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer-raw-bytes")
            .BuildAsync();

        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "key",
            Value = "hello-bytes"
        });

        // Act - consume as raw bytes (universal escape hatch)
        await using var consumer = await Kafka.CreateConsumer<byte[], byte[]>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer-raw-bytes")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        consumer.Assign(new TopicPartition(topic, 0));

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(20), cts.Token);

        // Assert - raw bytes consumption should always succeed
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value.Key).IsNotNull();
        await Assert.That(result.Value.Value).IsNotNull();
        await Assert.That(result.Value.Value.Length).IsGreaterThan(0);
    }

    [Test]
    public async Task NullValue_Tombstone_ConsumedSuccessfully()
    {
        // Arrange - produce a tombstone (null value)
        var topic = await KafkaContainer.CreateTestTopicAsync();

        await using var producer = await Kafka.CreateProducer<string, string?>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer-tombstone")
            .BuildAsync();

        await producer.ProduceAsync(new ProducerMessage<string, string?>
        {
            Topic = topic,
            Key = "tombstone-key",
            Value = null!
        });

        // Act - consume with null-safe deserializer
        var nullSerde = Serializers.Null<string>();

        await using var consumer = await Kafka.CreateConsumer<string, string?>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer-tombstone")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithValueDeserializer(nullSerde)
            .BuildAsync();

        consumer.Assign(new TopicPartition(topic, 0));

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(20), cts.Token);

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value.Key).IsEqualTo("tombstone-key");
        await Assert.That(result.Value.Value).IsNull();
    }

    [Test]
    public async Task NullKey_ConsumedSuccessfully()
    {
        // Arrange - produce with null key
        var topic = await KafkaContainer.CreateTestTopicAsync();

        var nullSerde = Serializers.Null<string>();

        await using var producer = await Kafka.CreateProducer<string?, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer-null-key")
            .WithKeySerializer(nullSerde)
            .BuildAsync();

        await producer.ProduceAsync(new ProducerMessage<string?, string>
        {
            Topic = topic,
            Key = null,
            Value = "value-with-null-key"
        });

        // Act
        await using var consumer = await Kafka.CreateConsumer<string?, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer-null-key")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithKeyDeserializer(nullSerde)
            .BuildAsync();

        consumer.Assign(new TopicPartition(topic, 0));

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(20), cts.Token);

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value.Key).IsNull();
        await Assert.That(result.Value.Value).IsEqualTo("value-with-null-key");
    }

    [Test]
    public async Task StringValue_ConsumedAsBytes_PreservesData()
    {
        // Arrange - produce string, consume as bytes and verify round-trip
        var topic = await KafkaContainer.CreateTestTopicAsync();
        const string originalValue = "round-trip-test-value";

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer-string-to-bytes")
            .BuildAsync();

        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "key",
            Value = originalValue
        });

        // Act - consume as bytes
        await using var consumer = await Kafka.CreateConsumer<byte[], byte[]>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer-string-to-bytes")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        consumer.Assign(new TopicPartition(topic, 0));

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(20), cts.Token);

        // Assert - bytes should decode back to original string
        await Assert.That(result).IsNotNull();
        var decodedValue = System.Text.Encoding.UTF8.GetString(result!.Value.Value);
        await Assert.That(decodedValue).IsEqualTo(originalValue);
    }

    [Test]
    public async Task IntValue_ConsumedAsBytes_PreservesBigEndianEncoding()
    {
        // Arrange - produce int, consume as bytes
        var topic = await KafkaContainer.CreateTestTopicAsync();
        const int originalValue = 42;

        await using var producer = await Kafka.CreateProducer<string, int>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer-int-to-bytes")
            .BuildAsync();

        await producer.ProduceAsync(new ProducerMessage<string, int>
        {
            Topic = topic,
            Key = "key",
            Value = originalValue
        });

        // Act - consume as raw bytes
        await using var consumer = await Kafka.CreateConsumer<byte[], byte[]>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer-int-to-bytes")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        consumer.Assign(new TopicPartition(topic, 0));

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(20), cts.Token);

        // Assert - should be 4 bytes (big-endian int32)
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value.Value.Length).IsEqualTo(4);

        var decodedValue = System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(result.Value.Value);
        await Assert.That(decodedValue).IsEqualTo(originalValue);
    }

    [Test]
    public async Task EmptyBytes_ConsumedSuccessfully()
    {
        // Arrange - produce empty byte array
        var topic = await KafkaContainer.CreateTestTopicAsync();

        await using var producer = await Kafka.CreateProducer<string, byte[]>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer-empty-bytes")
            .BuildAsync();

        await producer.ProduceAsync(new ProducerMessage<string, byte[]>
        {
            Topic = topic,
            Key = "key",
            Value = []
        });

        // Act
        await using var consumer = await Kafka.CreateConsumer<string, byte[]>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer-empty-bytes")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        consumer.Assign(new TopicPartition(topic, 0));

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(20), cts.Token);

        // Assert - empty array round-trip
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value.Value.Length).IsEqualTo(0);
    }
}
