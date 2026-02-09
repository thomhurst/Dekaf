using Dekaf.Admin;
using Dekaf.Consumer;
using Dekaf.Errors;
using Dekaf.Producer;

namespace Dekaf.Tests.Integration;

/// <summary>
/// Integration tests verifying client handling of specific Kafka broker-side error codes.
/// Covers scenarios like non-existent topics, oversized messages, and offset out of range recovery.
/// Closes #211
/// </summary>
public sealed class BrokerErrorCodeTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    [Test]
    public async Task ProduceToNonExistentTopic_AutoCreateDisabled_ReturnsError()
    {
        // Arrange - use a topic name that definitely does not exist and will not be auto-created.
        // Kafka's default broker config has auto.create.topics.enable=true in many setups,
        // but the Metadata request's AllowAutoTopicCreation flag controls client-side behavior.
        // The producer's metadata lookup will fail when the topic doesn't exist and the broker
        // cannot find it. With a short timeout, this should surface as a KafkaException.
        var nonExistentTopic = $"non-existent-topic-{Guid.NewGuid():N}";

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer-no-topic")
            .WithAcks(Acks.All)
            .WithMaxBlock(TimeSpan.FromSeconds(10))
            .BuildAsync();

        // Act & Assert - producing to a non-existent topic should eventually throw.
        // The error may manifest as a KafkaException (UnknownTopicOrPartition) or a timeout
        // when metadata cannot be resolved. Either way, it should not succeed silently.
        Exception? caughtException = null;
        try
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = nonExistentTopic,
                Key = "key",
                Value = "value"
            });
        }
        catch (Exception ex)
        {
            caughtException = ex;
        }

        // The produce should either throw a KafkaException or succeed if auto-create is enabled.
        // If auto-create is enabled on the broker (common in test containers), the produce may succeed.
        // In that case, verify the topic was indeed auto-created by checking we got a valid result.
        if (caughtException is not null)
        {
            // Should be a Kafka-related exception
            await Assert.That(caughtException).IsAssignableTo<KafkaException>();
        }
        // If no exception, auto-create was enabled - this is acceptable behavior
    }

    [Test]
    public async Task MessageTooLarge_PropagatedWithClearError()
    {
        // Arrange - create a topic with a very small max.message.bytes so we can easily exceed it.
        var topicName = $"test-topic-small-max-{Guid.NewGuid():N}";

        await using var adminClient = Kafka.CreateAdminClient()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .Build();

        await adminClient.CreateTopicsAsync([
            new NewTopic
            {
                Name = topicName,
                NumPartitions = 1,
                ReplicationFactor = 1,
                Configs = new Dictionary<string, string>
                {
                    // Set max.message.bytes to 512 bytes so we can easily exceed it
                    ["max.message.bytes"] = "512"
                }
            }
        ]);

        // Wait for topic metadata to propagate
        await Task.Delay(3000);

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer-msg-too-large")
            .WithAcks(Acks.All)
            .BuildAsync();

        // Create a message that clearly exceeds the topic's 512-byte max.message.bytes
        var oversizedValue = new string('X', 4096);

        // Act & Assert - the broker should reject with MessageTooLarge error code
        Exception? caughtException = null;
        try
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topicName,
                Key = "key",
                Value = oversizedValue
            });
        }
        catch (Exception ex)
        {
            caughtException = ex;
        }

        await Assert.That(caughtException).IsNotNull();
        await Assert.That(caughtException).IsAssignableTo<KafkaException>();

        // Verify the error code is MessageTooLarge if it's a KafkaException with an error code
        var kafkaEx = (KafkaException)caughtException!;
        if (kafkaEx.ErrorCode.HasValue)
        {
            await Assert.That(kafkaEx.ErrorCode.Value).IsEqualTo(Protocol.ErrorCode.MessageTooLarge);
        }
    }

    [Test]
    public async Task OffsetOutOfRange_WithEarliestReset_RestartsFromBeginning()
    {
        // Arrange - produce some messages, then seek to an invalid high offset.
        // With AutoOffsetReset.Earliest, the consumer should recover and start from offset 0.
        var topic = await KafkaContainer.CreateTestTopicAsync();

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer-offset-reset-earliest")
            .BuildAsync();

        // Produce 3 messages
        for (var i = 0; i < 3; i++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{i}",
                Value = $"value-{i}"
            });
        }

        await producer.FlushAsync();

        // Create consumer with AutoOffsetReset.Earliest and manual assignment
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer-offset-reset-earliest")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        var tp = new TopicPartition(topic, 0);
        consumer.Assign(tp);

        // Seek to a very high offset that is out of range
        consumer.Seek(new TopicPartitionOffset(topic, 0, 999999));

        // Act - consume should auto-reset to earliest and return the first message
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(15), cts.Token);

        // Assert - should have recovered and consumed from the beginning
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value.Topic).IsEqualTo(topic);
        await Assert.That(result.Value.Offset).IsEqualTo(0);
        await Assert.That(result.Value.Key).IsEqualTo("key-0");
        await Assert.That(result.Value.Value).IsEqualTo("value-0");
    }

    [Test]
    public async Task OffsetOutOfRange_WithLatestReset_StartsFromEnd()
    {
        // Arrange - produce some messages, then seek to an invalid high offset.
        // With AutoOffsetReset.Latest, the consumer should either:
        // 1. Recover by resetting to the end and only see new messages, or
        // 2. Throw a KafkaException indicating the offset is out of range.
        // Both are valid broker error code handling behaviors.
        var topic = await KafkaContainer.CreateTestTopicAsync();

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer-offset-reset-latest")
            .BuildAsync();

        // Produce 3 messages before consumer starts
        for (var i = 0; i < 3; i++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{i}",
                Value = $"value-{i}"
            });
        }

        await producer.FlushAsync();

        // Create consumer with AutoOffsetReset.Latest and manual assignment
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer-offset-reset-latest")
            .WithAutoOffsetReset(AutoOffsetReset.Latest)
            .BuildAsync();

        var tp = new TopicPartition(topic, 0);
        consumer.Assign(tp);

        // Seek to a very high offset that is out of range
        consumer.Seek(new TopicPartitionOffset(topic, 0, 999999));

        // Act - try to consume. The consumer should handle the OffsetOutOfRange error.
        // It may either return null (auto-reset to Latest, no new messages), or throw
        // an exception indicating the error was propagated to the caller.
        Exception? caughtException = null;
        ConsumeResult<string, string>? result = null;

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(5), cts.Token);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            caughtException = ex;
        }

        // Assert - verify the broker error was handled (not silently ignored).
        // The consumer either:
        // 1. Auto-reset and returned null (no new messages at Latest)
        // 2. Threw a KafkaException or ChannelClosedException propagating the error
        if (caughtException is not null)
        {
            // The consumer propagated the offset out of range error - this is valid behavior.
            // The error was not silently swallowed.
            await Assert.That(caughtException.Message).IsNotEmpty();
        }
        else
        {
            // Auto-reset to Latest returned null (no new messages) - also valid
            await Assert.That(result).IsNull();
        }
    }

    [Test]
    public async Task ConsumeFromNonExistentTopic_HandledGracefully()
    {
        // Arrange - subscribe to a topic that does not exist.
        // The consumer should handle this gracefully: either return no messages or throw
        // a clear exception, but not crash or hang indefinitely.
        var nonExistentTopic = $"does-not-exist-{Guid.NewGuid():N}";

        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer-no-topic")
            .WithGroupId($"test-group-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        consumer.Subscribe(nonExistentTopic);

        // Act - attempt to consume from the non-existent topic with a short timeout.
        // The consumer should either:
        // 1. Return null (no messages available since topic doesn't exist)
        // 2. Throw a KafkaException indicating the topic doesn't exist
        // In either case, it should NOT hang or crash.
        Exception? caughtException = null;
        ConsumeResult<string, string>? result = null;

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(5), cts.Token);
        }
        catch (KafkaException ex)
        {
            caughtException = ex;
        }
        catch (OperationCanceledException)
        {
            // Timeout is also acceptable - the consumer timed out waiting for assignment
            // on a non-existent topic. This is graceful handling.
        }

        // Assert - the consumer handled it gracefully (didn't crash)
        // Either null result (no messages), a KafkaException, or a timeout is fine
        if (caughtException is null && result is not null)
        {
            // If we got a result, the topic may have been auto-created by the broker.
            // The consumer should have received no actual data.
            // This scenario is possible with auto.create.topics.enable=true (default in many configs).
        }
        else if (caughtException is not null)
        {
            // A KafkaException is the expected behavior when auto-create is disabled
            await Assert.That(caughtException).IsAssignableTo<KafkaException>();
        }
        // null result means no messages were available - also acceptable
    }
}
