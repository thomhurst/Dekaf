using Dekaf.Consumer;
using Dekaf.Producer;
using Dekaf.Serialization;

namespace Dekaf.Tests.Integration.RealWorld;

/// <summary>
/// Tests for different serializer type combinations commonly used in production.
/// Real applications use various key/value types beyond just string/string.
/// </summary>
public sealed class SerializerVarietyTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    [Test]
    public async Task IntKey_StringValue_RoundTrip()
    {
        // Common pattern: numeric IDs as keys
        var topic = await KafkaContainer.CreateTestTopicAsync();

        await using var producer = await Kafka.CreateProducer<int, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .BuildAsync();

        await producer.ProduceAsync(new ProducerMessage<int, string>
        {
            Topic = topic,
            Key = 42,
            Value = "answer-to-everything"
        });

        await using var consumer = await Kafka.CreateConsumer<int, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId($"int-key-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        consumer.Subscribe(topic);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value.Key).IsEqualTo(42);
        await Assert.That(result.Value.Value).IsEqualTo("answer-to-everything");
    }

    [Test]
    public async Task LongKey_StringValue_RoundTrip()
    {
        // Common for timestamp-based or snowflake IDs
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var key = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        await using var producer = await Kafka.CreateProducer<long, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .BuildAsync();

        await producer.ProduceAsync(new ProducerMessage<long, string>
        {
            Topic = topic,
            Key = key,
            Value = "timestamp-keyed-event"
        });

        await using var consumer = await Kafka.CreateConsumer<long, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId($"long-key-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        consumer.Subscribe(topic);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value.Key).IsEqualTo(key);
        await Assert.That(result.Value.Value).IsEqualTo("timestamp-keyed-event");
    }

    [Test]
    public async Task GuidKey_StringValue_RoundTrip()
    {
        // Common for correlation IDs and distributed tracing
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var key = Guid.NewGuid();

        await using var producer = await Kafka.CreateProducer<Guid, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .BuildAsync();

        await producer.ProduceAsync(new ProducerMessage<Guid, string>
        {
            Topic = topic,
            Key = key,
            Value = "guid-keyed-event"
        });

        await using var consumer = await Kafka.CreateConsumer<Guid, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId($"guid-key-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        consumer.Subscribe(topic);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value.Key).IsEqualTo(key);
        await Assert.That(result.Value.Value).IsEqualTo("guid-keyed-event");
    }

    [Test]
    public async Task StringKey_ByteArrayValue_RoundTrip()
    {
        // Common for binary payloads (protobuf, avro, custom binary formats)
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var payload = new byte[] { 0x00, 0x01, 0x02, 0xFF, 0xFE, 0xFD };

        await using var producer = await Kafka.CreateProducer<string, byte[]>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .BuildAsync();

        await producer.ProduceAsync(new ProducerMessage<string, byte[]>
        {
            Topic = topic,
            Key = "binary-event",
            Value = payload
        });

        await using var consumer = await Kafka.CreateConsumer<string, byte[]>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId($"bytes-value-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        consumer.Subscribe(topic);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value.Key).IsEqualTo("binary-event");
        await Assert.That(result.Value.Value).IsEquivalentTo(payload);
    }

    [Test]
    public async Task ByteArrayKey_ByteArrayValue_RoundTrip()
    {
        // Fully binary messages
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var key = new byte[] { 0xCA, 0xFE };
        var value = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF };

        await using var producer = await Kafka.CreateProducer<byte[], byte[]>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .BuildAsync();

        await producer.ProduceAsync(new ProducerMessage<byte[], byte[]>
        {
            Topic = topic,
            Key = key,
            Value = value
        });

        await using var consumer = await Kafka.CreateConsumer<byte[], byte[]>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId($"binary-binary-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        consumer.Subscribe(topic);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value.Key).IsEquivalentTo(key);
        await Assert.That(result.Value.Value).IsEquivalentTo(value);
    }

    [Test]
    public async Task IntKey_MultipleMessages_PartitionByKey()
    {
        // Verify that integer keys provide consistent partitioning
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 3);

        await using var producer = await Kafka.CreateProducer<int, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .BuildAsync();

        // Same key should always go to the same partition
        var results = new List<RecordMetadata>();
        for (var i = 0; i < 5; i++)
        {
            var result = await producer.ProduceAsync(new ProducerMessage<int, string>
            {
                Topic = topic,
                Key = 12345,
                Value = $"same-key-msg-{i}"
            });
            results.Add(result);
        }

        // All should be on the same partition
        var partitions = results.Select(r => r.Partition).Distinct().ToList();
        await Assert.That(partitions).Count().IsEqualTo(1);
    }

    [Test]
    public async Task GuidKey_MultipleMessages_ConsistentPartitioning()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 3);
        var fixedGuid = Guid.NewGuid();

        await using var producer = await Kafka.CreateProducer<Guid, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .BuildAsync();

        var results = new List<RecordMetadata>();
        for (var i = 0; i < 5; i++)
        {
            var result = await producer.ProduceAsync(new ProducerMessage<Guid, string>
            {
                Topic = topic,
                Key = fixedGuid,
                Value = $"guid-msg-{i}"
            });
            results.Add(result);
        }

        // Same GUID key should always route to the same partition
        var partitions = results.Select(r => r.Partition).Distinct().ToList();
        await Assert.That(partitions).Count().IsEqualTo(1);
    }

    [Test]
    public async Task RawBytesValue_ZeroCopy_RoundTrip()
    {
        // ReadOnlyMemory<byte> value type for zero-copy scenarios
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var payload = new byte[] { 1, 2, 3, 4, 5 };
        ReadOnlyMemory<byte> memoryPayload = payload;

        await using var producer = await Kafka.CreateProducer<string, ReadOnlyMemory<byte>>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .BuildAsync();

        await producer.ProduceAsync(new ProducerMessage<string, ReadOnlyMemory<byte>>
        {
            Topic = topic,
            Key = "raw-bytes",
            Value = memoryPayload
        });

        await using var consumer = await Kafka.CreateConsumer<string, ReadOnlyMemory<byte>>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId($"raw-bytes-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        consumer.Subscribe(topic);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value.Key).IsEqualTo("raw-bytes");
        await Assert.That(result.Value.Value.ToArray()).IsEquivalentTo(payload);
    }

    [Test]
    public async Task MixedSerializerTypes_SameCluster_IndependentTopics()
    {
        // Different applications using different serializer types on the same cluster
        var intTopic = await KafkaContainer.CreateTestTopicAsync();
        var guidTopic = await KafkaContainer.CreateTestTopicAsync();
        var bytesTopic = await KafkaContainer.CreateTestTopicAsync();

        // Int-keyed producer
        await using var intProducer = await Kafka.CreateProducer<int, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .BuildAsync();

        // Guid-keyed producer
        await using var guidProducer = await Kafka.CreateProducer<Guid, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .BuildAsync();

        // Bytes producer
        await using var bytesProducer = await Kafka.CreateProducer<string, byte[]>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .BuildAsync();

        // Produce to all topics concurrently
        var guidKey = Guid.NewGuid();
        var intTask = intProducer.ProduceAsync(new ProducerMessage<int, string>
        {
            Topic = intTopic, Key = 100, Value = "int-event"
        });
        var guidTask = guidProducer.ProduceAsync(new ProducerMessage<Guid, string>
        {
            Topic = guidTopic, Key = guidKey, Value = "guid-event"
        });
        var bytesTask = bytesProducer.ProduceAsync(new ProducerMessage<string, byte[]>
        {
            Topic = bytesTopic, Key = "bytes-key", Value = [0xAB, 0xCD]
        });

        await intTask;
        await guidTask;
        await bytesTask;

        // Consume and verify each independently
        await using var intConsumer = await Kafka.CreateConsumer<int, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId($"int-verify-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        intConsumer.Subscribe(intTopic);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var intResult = await intConsumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token);
        await Assert.That(intResult).IsNotNull();
        await Assert.That(intResult!.Value.Key).IsEqualTo(100);

        await using var guidConsumer = await Kafka.CreateConsumer<Guid, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId($"guid-verify-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        guidConsumer.Subscribe(guidTopic);

        var guidResult = await guidConsumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token);
        await Assert.That(guidResult).IsNotNull();
        await Assert.That(guidResult!.Value.Key).IsEqualTo(guidKey);

        await using var bytesConsumer = await Kafka.CreateConsumer<string, byte[]>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId($"bytes-verify-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        bytesConsumer.Subscribe(bytesTopic);

        var bytesResult = await bytesConsumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token);
        await Assert.That(bytesResult).IsNotNull();
        await Assert.That(bytesResult!.Value.Value).IsEquivalentTo(new byte[] { 0xAB, 0xCD });
    }
}
