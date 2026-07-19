using System.Buffers;
using System.Text;
using Dekaf.Consumer;
using Dekaf.Producer;
using Dekaf.Serialization;

namespace Dekaf.Tests.Integration;

/// <summary>
/// End-to-end coverage for <see cref="IAsyncSerializer{T}"/>/<see cref="IAsyncDeserializer{T}"/>
/// (issue #2309): serdes that perform per-message asynchronous work, e.g. an encryption stack
/// fetching short-lived keys. The test serde XORs the payload with a "key" that is only obtainable
/// asynchronously, so a sync-only path would either fail or produce unreadable bytes.
/// </summary>
[Category("Serialization")]
public class AsyncSerdeTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    /// <summary>
    /// Simulates an IO-dependent envelope-encryption serde: every operation awaits an async "key
    /// fetch" before transforming the payload. Also counts invocations so tests can prove the
    /// async path (not a sync fallback) ran.
    /// </summary>
    private sealed class XorEncryptionStringSerde : IAsyncSerde<string>
    {
        private const byte Key = 0x5A;
        private int _serializeCalls;
        private int _deserializeCalls;

        public int SerializeCalls => Volatile.Read(ref _serializeCalls);
        public int DeserializeCalls => Volatile.Read(ref _deserializeCalls);

        private static async ValueTask<byte> FetchKeyAsync(CancellationToken cancellationToken)
        {
            // Force a real asynchronous hop, as a short-lived-key fetch would.
            await Task.Delay(1, cancellationToken);
            return Key;
        }

        public async ValueTask SerializeAsync(
            string value,
            IBufferWriter<byte> destination,
            SerializationContext context,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _serializeCalls);
            var key = await FetchKeyAsync(cancellationToken);
            var bytes = Encoding.UTF8.GetBytes(value);
            for (var i = 0; i < bytes.Length; i++)
                bytes[i] ^= key;
            destination.Write(bytes);
        }

        public async ValueTask<string> DeserializeAsync(
            ReadOnlyMemory<byte> data,
            SerializationContext context,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _deserializeCalls);
            var key = await FetchKeyAsync(cancellationToken);
            var bytes = data.ToArray();
            for (var i = 0; i < bytes.Length; i++)
                bytes[i] ^= key;
            return Encoding.UTF8.GetString(bytes);
        }
    }

    [Test]
    public async Task AsyncSerde_ProduceAsyncAndConsumeAsync_RoundTrips()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var groupId = $"test-group-{Guid.NewGuid():N}";
        var producerSerde = new XorEncryptionStringSerde();
        var consumerSerde = new XorEncryptionStringSerde();

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithKeySerializer(producerSerde)
            .WithValueSerializer(producerSerde)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        var metadata = await producer.ProduceAsync(topic, "secret-key", "secret-value");
        await Assert.That(metadata.Offset).IsGreaterThanOrEqualTo(0);
        await Assert.That(producerSerde.SerializeCalls).IsEqualTo(2);

        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithKeyDeserializer(consumerSerde)
            .WithValueDeserializer(consumerSerde)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        consumer.Subscribe(topic);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await foreach (var result in consumer.ConsumeAsync(cts.Token))
        {
            await Assert.That(result.Key).IsEqualTo("secret-key");
            await Assert.That(result.Value).IsEqualTo("secret-value");
            break;
        }

        await Assert.That(consumerSerde.DeserializeCalls).IsEqualTo(2);
    }

    [Test]
    public async Task AsyncSerde_ConsumeOneAsync_RoundTrips()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var groupId = $"test-group-{Guid.NewGuid():N}";

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithValueSerializer(new XorEncryptionStringSerde())
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        await producer.ProduceAsync(topic, "k1", "consume-one-value");

        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithValueDeserializer(new XorEncryptionStringSerde())
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        consumer.Subscribe(topic);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token);

        await Assert.That(result).IsNotNull();
        // Mixed configuration: key uses the built-in sync string deserializer, value the async serde.
        await Assert.That(result!.Value.Key).IsEqualTo("k1");
        await Assert.That(result.Value.Value).IsEqualTo("consume-one-value");
    }

    [Test]
    public async Task AsyncSerde_ProduceAllAsync_DeliversAllMessages()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var groupId = $"test-group-{Guid.NewGuid():N}";
        const int messageCount = 50;

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithValueSerializer(new XorEncryptionStringSerde())
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        var messages = Enumerable.Range(0, messageCount)
            .Select(i => new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{i}",
                Value = $"value-{i}"
            })
            .ToList();

        var results = await producer.ProduceAllAsync(messages);
        await Assert.That(results.Length).IsEqualTo(messageCount);

        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithValueDeserializer(new XorEncryptionStringSerde())
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        consumer.Subscribe(topic);

        var seen = new HashSet<string>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        await foreach (var result in consumer.ConsumeAsync(cts.Token))
        {
            seen.Add(result.Value);
            if (seen.Count == messageCount)
                break;
        }

        await Assert.That(seen.Count).IsEqualTo(messageCount);
        for (var i = 0; i < messageCount; i++)
        {
            await Assert.That(seen.Contains($"value-{i}")).IsTrue();
        }
    }

    [Test]
    public async Task AsyncSerde_FireAsync_DeliversMessage()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var groupId = $"test-group-{Guid.NewGuid():N}";

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithValueSerializer(new XorEncryptionStringSerde())
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        await producer.FireAsync(topic, "fire-key", "fire-value");
        await producer.FlushAsync();

        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithValueDeserializer(new XorEncryptionStringSerde())
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        consumer.Subscribe(topic);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value.Value).IsEqualTo("fire-value");
    }

    [Test]
    public async Task AsyncSerde_FireAsyncWithDeliveryHandler_InvokesHandler()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithValueSerializer(new XorEncryptionStringSerde())
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        var delivered = new TaskCompletionSource<(RecordMetadata Metadata, Exception? Error)>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        await producer.FireAsync(
            new ProducerMessage<string, string> { Topic = topic, Key = "k", Value = "handler-value" },
            (metadata, error) => delivered.TrySetResult((metadata, error)));
        await producer.FlushAsync();

        var (recordMetadata, exception) = await delivered.Task.WaitAsync(TimeSpan.FromSeconds(30));
        await Assert.That(exception).IsNull();
        await Assert.That(recordMetadata.Offset).IsGreaterThanOrEqualTo(0);
        await Assert.That(recordMetadata.Topic).IsEqualTo(topic);
    }

    [Test]
    public async Task AsyncSerde_NullValue_RoundTripsAsTombstone()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var groupId = $"test-group-{Guid.NewGuid():N}";

        await using var producer = await Kafka.CreateProducer<string, string?>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithValueSerializer(new XorEncryptionStringSerde()!)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        await producer.ProduceAsync(topic, "tombstone-key", null);

        await using var consumer = await Kafka.CreateConsumer<string, string?>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithValueDeserializer(new NullTolerantAsyncStringDeserializer())
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        consumer.Subscribe(topic);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value.Key).IsEqualTo("tombstone-key");
        await Assert.That(result.Value.Value).IsNull();
    }

    private sealed class NullTolerantAsyncStringDeserializer : IAsyncDeserializer<string?>
    {
        public async ValueTask<string?> DeserializeAsync(
            ReadOnlyMemory<byte> data,
            SerializationContext context,
            CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            return context.IsNull ? null : Encoding.UTF8.GetString(data.Span);
        }
    }
}
