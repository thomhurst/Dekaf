using Dekaf.Consumer;
using Dekaf.Producer;
using Dekaf.Serialization;

namespace Dekaf.Tests.Integration.RealWorld;

/// <summary>
/// Tests for tombstone (null value) handling and null key/value combinations.
/// Tombstones are critical for log compaction and delete semantics in Kafka.
/// </summary>
public sealed class TombstoneAndNullValueTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    [Test]
    public async Task NullValue_ProduceAndConsume_RoundTrips()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();

        await using var producer = Kafka.CreateProducer<string, string?>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithValueSerializer(Serializers.NullableString)
            .Build();

        await producer.ProduceAsync(new ProducerMessage<string, string?>
        {
            Topic = topic,
            Key = "tombstone-key",
            Value = null
        });

        await using var consumer = Kafka.CreateConsumer<string, string?>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithValueDeserializer(Serializers.NullableString)
            .Build();

        var tp = new TopicPartition(topic, 0);
        consumer.Assign(tp);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value.Key).IsEqualTo("tombstone-key");
        await Assert.That(result.Value.Value).IsNull();
    }

    [Test]
    public async Task NullKey_ProduceAndConsume_RoundTrips()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();

        await using var producer = Kafka.CreateProducer<string?, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithKeySerializer(Serializers.NullableString)
            .Build();

        await producer.ProduceAsync(new ProducerMessage<string?, string>
        {
            Topic = topic,
            Key = null,
            Value = "value-with-null-key"
        });

        await using var consumer = Kafka.CreateConsumer<string?, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithKeyDeserializer(Serializers.NullableString)
            .Build();

        var tp = new TopicPartition(topic, 0);
        consumer.Assign(tp);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value.Key).IsNull();
        await Assert.That(result.Value.Value).IsEqualTo("value-with-null-key");
    }

    [Test]
    public async Task MixedNullAndNonNull_AllDeliveredCorrectly()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();

        await using var producer = Kafka.CreateProducer<string, string?>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithValueSerializer(Serializers.NullableString)
            .Build();

        // Produce a mix of normal values and tombstones
        await producer.ProduceAsync(new ProducerMessage<string, string?>
        {
            Topic = topic,
            Key = "key-1",
            Value = "normal-value"
        });
        await producer.ProduceAsync(new ProducerMessage<string, string?>
        {
            Topic = topic,
            Key = "key-2",
            Value = null // tombstone
        });
        await producer.ProduceAsync(new ProducerMessage<string, string?>
        {
            Topic = topic,
            Key = "key-3",
            Value = "another-value"
        });
        await producer.ProduceAsync(new ProducerMessage<string, string?>
        {
            Topic = topic,
            Key = "key-4",
            Value = null // tombstone
        });
        await producer.ProduceAsync(new ProducerMessage<string, string?>
        {
            Topic = topic,
            Key = "key-5",
            Value = ""  // empty string (NOT a tombstone)
        });

        await using var consumer = Kafka.CreateConsumer<string, string?>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithValueDeserializer(Serializers.NullableString)
            .Build();

        var tp = new TopicPartition(topic, 0);
        consumer.Assign(tp);

        var messages = new List<ConsumeResult<string, string?>>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            messages.Add(msg);
            if (messages.Count >= 5) break;
        }

        await Assert.That(messages).Count().IsEqualTo(5);

        // Verify each message
        await Assert.That(messages[0].Value).IsEqualTo("normal-value");
        await Assert.That(messages[1].Value).IsNull();           // tombstone
        await Assert.That(messages[2].Value).IsEqualTo("another-value");
        await Assert.That(messages[3].Value).IsNull();           // tombstone
        await Assert.That(messages[4].Value).IsEqualTo("");      // empty string
    }

    [Test]
    public async Task Tombstone_WithHeaders_HeadersPreserved()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();

        await using var producer = Kafka.CreateProducer<string, string?>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithValueSerializer(Serializers.NullableString)
            .Build();

        var headers = new Headers
        {
            { "reason", "deleted"u8.ToArray() },
            { "deleted-by", "admin"u8.ToArray() }
        };

        await producer.ProduceAsync(new ProducerMessage<string, string?>
        {
            Topic = topic,
            Key = "deleted-record",
            Value = null,
            Headers = headers
        });

        await using var consumer = Kafka.CreateConsumer<string, string?>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithValueDeserializer(Serializers.NullableString)
            .Build();

        var tp = new TopicPartition(topic, 0);
        consumer.Assign(tp);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value.Value).IsNull();
        await Assert.That(result.Value.Headers).IsNotNull();
        await Assert.That(result.Value.Headers!.Count).IsEqualTo(2);

        var reason = result.Value.Headers.First(h => h.Key == "reason");
        await Assert.That(reason.GetValueAsString()).IsEqualTo("deleted");
    }

    [Test]
    public async Task TombstoneSequence_SetThenDelete_BothVisible()
    {
        // Simulates a key being set and then deleted
        var topic = await KafkaContainer.CreateTestTopicAsync();

        await using var producer = Kafka.CreateProducer<string, string?>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithValueSerializer(Serializers.NullableString)
            .Build();

        // Set the value
        await producer.ProduceAsync(new ProducerMessage<string, string?>
        {
            Topic = topic,
            Key = "user-123",
            Value = "{\"name\":\"Alice\",\"email\":\"alice@example.com\"}"
        });

        // Delete the value (tombstone)
        await producer.ProduceAsync(new ProducerMessage<string, string?>
        {
            Topic = topic,
            Key = "user-123",
            Value = null
        });

        await using var consumer = Kafka.CreateConsumer<string, string?>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithValueDeserializer(Serializers.NullableString)
            .Build();

        var tp = new TopicPartition(topic, 0);
        consumer.Assign(tp);

        var messages = new List<ConsumeResult<string, string?>>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            messages.Add(msg);
            if (messages.Count >= 2) break;
        }

        await Assert.That(messages).Count().IsEqualTo(2);
        await Assert.That(messages[0].Key).IsEqualTo("user-123");
        await Assert.That(messages[0].Value).IsNotNull();
        await Assert.That(messages[1].Key).IsEqualTo("user-123");
        await Assert.That(messages[1].Value).IsNull();
    }

    [Test]
    public async Task ByteArray_NullValue_ProduceAndConsume()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();

        // Produce a normal byte[] message
        await using var producer = Kafka.CreateProducer<string, byte[]>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .Build();

        await producer.ProduceAsync(new ProducerMessage<string, byte[]>
        {
            Topic = topic,
            Key = "bytes-key",
            Value = [1, 2, 3, 4, 5]
        });

        // Produce a null value (tombstone) using nullable string producer
        await using var nullProducer = Kafka.CreateProducer<string, string?>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithValueSerializer(Serializers.NullableString)
            .Build();

        await nullProducer.ProduceAsync(new ProducerMessage<string, string?>
        {
            Topic = topic,
            Key = "bytes-key",
            Value = null
        });

        await using var consumer = Kafka.CreateConsumer<string, byte[]>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .Build();

        var tp = new TopicPartition(topic, 0);
        consumer.Assign(tp);

        var messages = new List<ConsumeResult<string, byte[]>>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            messages.Add(msg);
            if (messages.Count >= 2) break;
        }

        await Assert.That(messages).Count().IsEqualTo(2);
        await Assert.That(messages[0].Value).IsNotNull();
        await Assert.That(messages[0].Value.Length).IsEqualTo(5);
    }
}
