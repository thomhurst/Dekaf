using System.Buffers;
using Dekaf.Consumer;
using Dekaf.Producer;
using Dekaf.Serialization;

namespace Dekaf.Tests.Integration;

/// <summary>
/// Integration tests for custom serializer/deserializer error handling.
/// Verifies that exceptions from user-provided serializers/deserializers are
/// properly propagated and do not corrupt internal producer/consumer state.
/// Closes #210
/// </summary>
public class CustomSerializerErrorTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    /// <summary>
    /// A value serializer that throws on a specific trigger value.
    /// For all other values, delegates to the built-in string serializer.
    /// </summary>
    private sealed class ThrowingValueSerializer : ISerializer<string>
    {
        public const string TriggerValue = "__THROW__";

        public void Serialize<TWriter>(string value, ref TWriter destination, SerializationContext context)
            where TWriter : IBufferWriter<byte>, allows ref struct
        {
            if (value == TriggerValue)
            {
                throw new InvalidOperationException("Serializer intentionally threw for test");
            }

            Serializers.String.Serialize(value, ref destination, context);
        }
    }

    /// <summary>
    /// A value deserializer that throws on a specific trigger value.
    /// For all other values, delegates to the built-in string deserializer.
    /// </summary>
    private sealed class ThrowingValueDeserializer : IDeserializer<string>
    {
        public const string TriggerValue = "__THROW__";

        public string Deserialize(ReadOnlySequence<byte> data, SerializationContext context)
        {
            // Peek at the value first using the built-in deserializer
            var result = Serializers.String.Deserialize(data, context);

            if (result == TriggerValue)
            {
                throw new InvalidOperationException("Deserializer intentionally threw for test");
            }

            return result;
        }
    }

    [Test]
    public async Task Producer_WithThrowingSerializer_ProduceAsyncPropagatesException()
    {
        // Arrange
        var topic = await KafkaContainer.CreateTestTopicAsync();

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer-throwing-serializer")
            .WithValueSerializer(new ThrowingValueSerializer())
            .BuildAsync();

        // Act & Assert - ProduceAsync should propagate the serializer exception
        await Assert.That(async () =>
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = "key",
                Value = ThrowingValueSerializer.TriggerValue
            });
        }).Throws<InvalidOperationException>()
          .WithMessage("Serializer intentionally threw for test");
    }

    [Test]
    public async Task Producer_AfterSerializerThrows_SubsequentMessageSucceeds()
    {
        // Arrange
        var topic = await KafkaContainer.CreateTestTopicAsync();

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer-serializer-recovery")
            .WithValueSerializer(new ThrowingValueSerializer())
            .BuildAsync();

        // Act - first message triggers serializer exception
        try
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = "key-bad",
                Value = ThrowingValueSerializer.TriggerValue
            });
        }
        catch (InvalidOperationException)
        {
            // Expected - serializer threw
        }

        // Act - second message with a normal value should succeed
        var metadata = await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "key-good",
            Value = "normal-value"
        });

        // Assert - producer internal state was not corrupted
        await Assert.That(metadata.Topic).IsEqualTo(topic);
        await Assert.That(metadata.Offset).IsGreaterThanOrEqualTo(0);

        // Verify by consuming the successfully produced message
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer-serializer-recovery")
            .WithGroupId($"test-group-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        consumer.Subscribe(topic);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var consumed = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token);

        await Assert.That(consumed).IsNotNull();
        await Assert.That(consumed!.Value.Key).IsEqualTo("key-good");
        await Assert.That(consumed.Value.Value).IsEqualTo("normal-value");
    }

    [Test]
    public async Task Consumer_WithThrowingDeserializer_ConsumeLoopReceivesError()
    {
        // Arrange - produce a message with the trigger value using default serializers
        var topic = await KafkaContainer.CreateTestTopicAsync();

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer-for-deser-error")
            .BuildAsync();

        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "key",
            Value = ThrowingValueDeserializer.TriggerValue
        });

        // Act - consume with a throwing deserializer
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer-throwing-deser")
            .WithGroupId($"test-group-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithValueDeserializer(new ThrowingValueDeserializer())
            .BuildAsync();

        consumer.Subscribe(topic);

        // Assert - the consume loop should surface the deserializer exception
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await Assert.That(async () =>
        {
            await foreach (var _ in consumer.ConsumeAsync(cts.Token))
            {
                // Should not reach here - exception expected before yield
            }
        }).Throws<InvalidOperationException>()
          .WithMessage("Deserializer intentionally threw for test");
    }

    [Test]
    public async Task Consumer_AfterDeserializerThrows_SubsequentMessagesCanBeConsumed()
    {
        // Arrange - produce two messages: one that triggers the error and one normal
        var topic = await KafkaContainer.CreateTestTopicAsync();

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer-for-deser-recovery")
            .BuildAsync();

        // Message 1: will trigger deserializer error
        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "key-bad",
            Value = ThrowingValueDeserializer.TriggerValue
        });

        // Message 2: normal message
        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "key-good",
            Value = "normal-value"
        });

        // Act - consume with a throwing deserializer, first attempt will fail
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer-deser-recovery")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithValueDeserializer(new ThrowingValueDeserializer())
            .BuildAsync();

        var tp = new TopicPartition(topic, 0);
        consumer.Assign(tp);

        // First consume attempt - should throw on the bad message
        using var cts1 = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var caughtException = false;

        try
        {
            await foreach (var _ in consumer.ConsumeAsync(cts1.Token))
            {
                // Should throw before yielding
            }
        }
        catch (InvalidOperationException)
        {
            caughtException = true;
        }

        await Assert.That(caughtException).IsTrue();

        // Seek past the bad message to offset 1 (the normal message)
        consumer.Seek(new TopicPartitionOffset(topic, 0, 1));

        // Second consume attempt - should succeed for the normal message
        using var cts2 = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var consumed = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts2.Token);

        // Assert - the consumer recovered and consumed the next message
        await Assert.That(consumed).IsNotNull();
        await Assert.That(consumed!.Value.Key).IsEqualTo("key-good");
        await Assert.That(consumed.Value.Value).IsEqualTo("normal-value");
    }
}
