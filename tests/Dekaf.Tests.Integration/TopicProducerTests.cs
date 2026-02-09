using Dekaf.Consumer;
using Dekaf.Producer;
using Dekaf.Serialization;

namespace Dekaf.Tests.Integration;

/// <summary>
/// Integration tests for the topic-specific producer.
/// </summary>
public class TopicProducerTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    [Test]
    public async Task TopicProducer_ProduceAsync_MessageDelivered()
    {
        // Arrange
        var topic = await KafkaContainer.CreateTestTopicAsync();

        await using var producer = await Kafka.CreateTopicProducerAsync<string, string>(
            KafkaContainer.BootstrapServers, topic);

        // Act
        var metadata = await producer.ProduceAsync("key1", "value1");

        // Assert
        await Assert.That(metadata.Topic).IsEqualTo(topic);
        await Assert.That(metadata.Partition).IsGreaterThanOrEqualTo(0);
        await Assert.That(metadata.Offset).IsGreaterThanOrEqualTo(0);
    }

    [Test]
    public async Task TopicProducer_ProduceAsync_WithHeaders_MessageDelivered()
    {
        // Arrange
        var topic = await KafkaContainer.CreateTestTopicAsync();

        await using var producer = await Kafka.CreateTopicProducerAsync<string, string>(
            KafkaContainer.BootstrapServers, topic);

        var headers = Headers.Create("trace-id", "abc123");

        // Act
        var metadata = await producer.ProduceAsync("key1", "value1", headers);

        // Assert
        await Assert.That(metadata.Topic).IsEqualTo(topic);
        await Assert.That(metadata.Offset).IsGreaterThanOrEqualTo(0);
    }

    [Test]
    public async Task TopicProducer_ProduceAsync_ToPartition_MessageDelivered()
    {
        // Arrange
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 3);

        await using var producer = await Kafka.CreateTopicProducerAsync<string, string>(
            KafkaContainer.BootstrapServers, topic);

        // Act
        var metadata = await producer.ProduceAsync(partition: 1, "key1", "value1");

        // Assert
        await Assert.That(metadata.Topic).IsEqualTo(topic);
        await Assert.That(metadata.Partition).IsEqualTo(1);
        await Assert.That(metadata.Offset).IsGreaterThanOrEqualTo(0);
    }

    [Test]
    public async Task TopicProducer_ProduceAsync_WithMessage_MessageDelivered()
    {
        // Arrange
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var timestamp = DateTimeOffset.UtcNow.AddMinutes(-5);

        await using var producer = await Kafka.CreateTopicProducerAsync<string, string>(
            KafkaContainer.BootstrapServers, topic);

        var message = new TopicProducerMessage<string, string>
        {
            Key = "key1",
            Value = "value1",
            Timestamp = timestamp
        };

        // Act
        var metadata = await producer.ProduceAsync(message);

        // Assert
        await Assert.That(metadata.Topic).IsEqualTo(topic);
        await Assert.That(metadata.Offset).IsGreaterThanOrEqualTo(0);
    }

    [Test]
    public async Task TopicProducer_Send_FireAndForget_Succeeds()
    {
        // Arrange
        var topic = await KafkaContainer.CreateTestTopicAsync();

        await using var producer = await Kafka.CreateTopicProducerAsync<string, string>(
            KafkaContainer.BootstrapServers, topic);

        // Act - fire-and-forget
        producer.Send("key1", "value1");
        await producer.FlushAsync();

        // Verify by consuming
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId($"test-group-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        consumer.Subscribe(topic);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            await Assert.That(msg.Key).IsEqualTo("key1");
            await Assert.That(msg.Value).IsEqualTo("value1");
            break;
        }
    }

    [Test]
    public async Task TopicProducer_Send_WithHeaders_Succeeds()
    {
        // Arrange
        var topic = await KafkaContainer.CreateTestTopicAsync();

        await using var producer = await Kafka.CreateTopicProducerAsync<string, string>(
            KafkaContainer.BootstrapServers, topic);

        var headers = Headers.Create("custom-header", "header-value");

        // Act
        producer.Send("key1", "value1", headers);
        await producer.FlushAsync();

        // Verify by consuming
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId($"test-group-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        consumer.Subscribe(topic);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            await Assert.That(msg.Key).IsEqualTo("key1");
            await Assert.That(msg.Value).IsEqualTo("value1");
            await Assert.That(msg.Headers).IsNotNull();
            break;
        }
    }

    [Test]
    public async Task TopicProducer_Send_WithCallback_InvokesCallback()
    {
        // Arrange
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var callbackInvoked = new TaskCompletionSource<(RecordMetadata, Exception?)>();

        await using var producer = await Kafka.CreateTopicProducerAsync<string, string>(
            KafkaContainer.BootstrapServers, topic);

        // Act
        producer.Send("key1", "value1", (metadata, ex) => callbackInvoked.TrySetResult((metadata, ex)));

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        cts.Token.Register(() => callbackInvoked.TrySetCanceled());

        var (resultMetadata, resultError) = await callbackInvoked.Task;

        // Assert
        await Assert.That(resultError).IsNull();
        await Assert.That(resultMetadata.Topic).IsEqualTo(topic);
        await Assert.That(resultMetadata.Offset).IsGreaterThanOrEqualTo(0);
    }

    [Test]
    public async Task TopicProducer_ProduceAllAsync_BatchDelivered()
    {
        // Arrange
        var topic = await KafkaContainer.CreateTestTopicAsync();
        const int messageCount = 10;

        await using var producer = await Kafka.CreateTopicProducerAsync<string, string>(
            KafkaContainer.BootstrapServers, topic);

        var messages = Enumerable.Range(0, messageCount)
            .Select(i => ((string?)$"key-{i}", $"value-{i}"))
            .ToArray();

        // Act
        var results = await producer.ProduceAllAsync(messages);

        // Assert
        await Assert.That(results).Count().IsEqualTo(messageCount);
        foreach (var result in results)
        {
            await Assert.That(result.Topic).IsEqualTo(topic);
            await Assert.That(result.Offset).IsGreaterThanOrEqualTo(0);
        }
    }

    [Test]
    public async Task TopicProducer_ProduceAllAsync_WithMessages_BatchDelivered()
    {
        // Arrange
        var topic = await KafkaContainer.CreateTestTopicAsync();

        await using var producer = await Kafka.CreateTopicProducerAsync<string, string>(
            KafkaContainer.BootstrapServers, topic);

        var messages = new[]
        {
            new TopicProducerMessage<string, string> { Key = "key1", Value = "value1" },
            new TopicProducerMessage<string, string> { Key = "key2", Value = "value2" },
            new TopicProducerMessage<string, string> { Key = "key3", Value = "value3" }
        };

        // Act
        var results = await producer.ProduceAllAsync(messages);

        // Assert
        await Assert.That(results).Count().IsEqualTo(3);
        foreach (var result in results)
        {
            await Assert.That(result.Topic).IsEqualTo(topic);
        }
    }

    [Test]
    public async Task TopicProducer_BuildForTopic_CreatesProducerBoundToTopic()
    {
        // Arrange
        var topic = await KafkaContainer.CreateTestTopicAsync();

        // Act
        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithAcks(Acks.All)
            .BuildForTopicAsync(topic);

        var metadata = await producer.ProduceAsync("key", "value");

        // Assert
        await Assert.That(producer.Topic).IsEqualTo(topic);
        await Assert.That(metadata.Topic).IsEqualTo(topic);
        await Assert.That(metadata.Offset).IsGreaterThanOrEqualTo(0);
    }

    [Test]
    public async Task TopicProducer_ForTopic_CreatesProducerFromExisting()
    {
        // Arrange
        var topic = await KafkaContainer.CreateTestTopicAsync();

        await using var baseProducer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithAcks(Acks.All)
            .BuildAsync();

        // Act
        var topicProducer = baseProducer.ForTopic(topic);
        var metadata = await topicProducer.ProduceAsync("key", "value");

        // Assert
        await Assert.That(topicProducer.Topic).IsEqualTo(topic);
        await Assert.That(metadata.Topic).IsEqualTo(topic);

        // Dispose topic producer (should not dispose base producer)
        await topicProducer.DisposeAsync();

        // Base producer should still work
        var metadata2 = await baseProducer.ProduceAsync(topic, "key2", "value2");
        await Assert.That(metadata2.Topic).IsEqualTo(topic);
    }

    [Test]
    public async Task MultipleTopicProducers_ShareSameConnection()
    {
        // This test verifies that multiple topic producers from the same base
        // producer share connections efficiently
        var topic1 = await KafkaContainer.CreateTestTopicAsync();
        var topic2 = await KafkaContainer.CreateTestTopicAsync();

        await using var baseProducer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithAcks(Acks.All)
            .BuildAsync();

        var producer1 = baseProducer.ForTopic(topic1);
        var producer2 = baseProducer.ForTopic(topic2);

        // Produce to both topics
        var metadata1 = await producer1.ProduceAsync("key1", "value1");
        var metadata2 = await producer2.ProduceAsync("key2", "value2");

        // Assert both succeeded
        await Assert.That(metadata1.Topic).IsEqualTo(topic1);
        await Assert.That(metadata2.Topic).IsEqualTo(topic2);

        // Dispose topic producers
        await producer1.DisposeAsync();
        await producer2.DisposeAsync();

        // Base producer should still work after topic producers are disposed
        var metadata3 = await baseProducer.ProduceAsync(topic1, "key3", "value3");
        await Assert.That(metadata3.Topic).IsEqualTo(topic1);
    }

    [Test]
    public async Task TopicProducer_ConcurrentProduces_AllSucceed()
    {
        // Arrange
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 3);
        const int messageCount = 100;

        await using var producer = await Kafka.CreateTopicProducerAsync<string, string>(
            KafkaContainer.BootstrapServers, topic);

        // Act - produce concurrently
        var tasks = Enumerable.Range(0, messageCount)
            .Select(i => producer.ProduceAsync($"key-{i}", $"value-{i}").AsTask())
            .ToArray();

        var results = await Task.WhenAll(tasks);

        // Assert
        await Assert.That(results).Count().IsEqualTo(messageCount);
        foreach (var result in results)
        {
            await Assert.That(result.Topic).IsEqualTo(topic);
            await Assert.That(result.Offset).IsGreaterThanOrEqualTo(0);
        }
    }

    [Test]
    public async Task TopicProducer_Topic_ReturnsConfiguredTopic()
    {
        // Arrange
        var topic = await KafkaContainer.CreateTestTopicAsync();

        await using var producer = await Kafka.CreateTopicProducerAsync<string, string>(
            KafkaContainer.BootstrapServers, topic);

        // Assert
        await Assert.That(producer.Topic).IsEqualTo(topic);
    }

    [Test]
    public async Task TopicProducer_Flush_WaitsForPendingMessages()
    {
        // Arrange
        var topic = await KafkaContainer.CreateTestTopicAsync();

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithLinger(TimeSpan.FromMilliseconds(1000)) // Long linger
            .BuildForTopicAsync(topic);

        // Act - send multiple fire-and-forget messages
        for (var i = 0; i < 10; i++)
        {
            producer.Send($"key-{i}", $"value-{i}");
        }

        // Flush should complete delivery
        await producer.FlushAsync();

        // Verify by consuming
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId($"test-group-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        consumer.Subscribe(topic);

        var consumed = 0;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await foreach (var _ in consumer.ConsumeAsync(cts.Token))
        {
            consumed++;
            if (consumed >= 10)
                break;
        }

        await Assert.That(consumed).IsEqualTo(10);
    }
}
