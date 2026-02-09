using Dekaf.Consumer;
using Dekaf.Producer;

namespace Dekaf.Tests.Integration;

/// <summary>
/// Integration tests for partition EOF (end-of-file) events.
/// Tests verify that EOF events fire correctly when reaching the high watermark.
/// </summary>
public class PartitionEofTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    [Test]
    public async Task Consumer_EnablePartitionEof_FiresEofWhenReachingHighWatermark()
    {
        // Arrange - produce some messages first
        var topic = await KafkaContainer.CreateTestTopicAsync();

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer")
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

        // Act - create consumer with EnablePartitionEof and consume
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithPartitionEof(true)
            .WithQueuedMinMessages(1) // Disable prefetching for predictable EOF timing
            .BuildAsync();

        var tp = new TopicPartition(topic, 0);
        consumer.Assign(tp);

        var messages = new List<ConsumeResult<string, string>>();
        var eofEvents = new List<ConsumeResult<string, string>>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await foreach (var result in consumer.ConsumeAsync(cts.Token))
        {
            if (result.IsPartitionEof)
            {
                eofEvents.Add(result);
                break; // We got our EOF event
            }
            else
            {
                messages.Add(result);
            }
        }

        // Assert - should have 3 messages and at least 1 EOF event
        await Assert.That(messages.Count).IsEqualTo(3);
        await Assert.That(eofEvents.Count).IsGreaterThanOrEqualTo(1);

        // Verify EOF event properties
        var eofEvent = eofEvents[0];
        await Assert.That(eofEvent.Topic).IsEqualTo(topic);
        await Assert.That(eofEvent.Partition).IsEqualTo(0);
        await Assert.That(eofEvent.IsPartitionEof).IsTrue();
    }

    [Test]
    public async Task Consumer_DisabledPartitionEof_DoesNotFireEofEvents()
    {
        // Arrange
        var topic = await KafkaContainer.CreateTestTopicAsync();

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer")
            .BuildAsync();

        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "key",
            Value = "value"
        });

        // Act - consumer without EnablePartitionEof (default is false)
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithPartitionEof(false) // Explicitly disabled
            .WithQueuedMinMessages(1) // Disable prefetching for predictable behavior
            .BuildAsync();

        var tp = new TopicPartition(topic, 0);
        consumer.Assign(tp);

        var messages = new List<ConsumeResult<string, string>>();
        var eofEvents = new List<ConsumeResult<string, string>>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        try
        {
            await foreach (var result in consumer.ConsumeAsync(cts.Token))
            {
                if (result.IsPartitionEof)
                {
                    eofEvents.Add(result);
                }
                else
                {
                    messages.Add(result);
                    // After consuming the only message, wait a bit to ensure no EOF comes
                    if (messages.Count >= 1)
                    {
                        // Small delay to allow EOF to be emitted if it was going to be
                        await Task.Delay(2000, cts.Token);
                        break;
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected after timeout
        }

        // Assert - should have message but NO EOF events
        await Assert.That(messages.Count).IsEqualTo(1);
        await Assert.That(eofEvents.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Consumer_EofResetsAfterNewMessages()
    {
        // Arrange
        var topic = await KafkaContainer.CreateTestTopicAsync();

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer")
            .BuildAsync();

        // Produce initial message
        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "key-1",
            Value = "value-1"
        });

        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithPartitionEof(true)
            .WithQueuedMinMessages(1) // Disable prefetching for predictable EOF timing
            .BuildAsync();

        var tp = new TopicPartition(topic, 0);
        consumer.Assign(tp);

        var eofCount = 0;
        var messageCount = 0;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        // Start consuming in background
        var consumeTask = Task.Run(async () =>
        {
            await foreach (var result in consumer.ConsumeAsync(cts.Token))
            {
                if (result.IsPartitionEof)
                {
                    eofCount++;
                    if (eofCount >= 2)
                        break; // Got two EOF events
                }
                else
                {
                    messageCount++;
                }
            }
        });

        // Wait for first EOF (after first message)
        var startTime = DateTime.UtcNow;
        while (eofCount < 1 && DateTime.UtcNow - startTime < TimeSpan.FromSeconds(15))
        {
            await Task.Delay(100);
        }

        // Verify we got first EOF
        if (eofCount < 1)
        {
            cts.Cancel();
            throw new InvalidOperationException("Did not receive first EOF event");
        }

        // Produce another message - this should reset EOF state
        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "key-2",
            Value = "value-2"
        });

        // Wait for consume task to complete (will exit after second EOF)
        await consumeTask;

        // Assert - should have 2 messages and 2 EOF events (one after each batch)
        await Assert.That(messageCount).IsEqualTo(2);
        await Assert.That(eofCount).IsEqualTo(2);
    }

    [Test]
    public async Task Consumer_EofResetsOnSeek()
    {
        // Arrange
        var topic = await KafkaContainer.CreateTestTopicAsync();

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer")
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

        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithPartitionEof(true)
            .WithQueuedMinMessages(1) // Disable prefetching for predictable EOF timing
            .BuildAsync();

        var tp = new TopicPartition(topic, 0);
        consumer.Assign(tp);

        // Act - consume until EOF
        var eofCount = 0;
        var messages = new List<string>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await foreach (var result in consumer.ConsumeAsync(cts.Token))
        {
            if (result.IsPartitionEof)
            {
                eofCount++;
                if (eofCount == 1)
                {
                    // After first EOF, seek back to beginning
                    consumer.SeekToBeginning(tp);
                }
                else
                {
                    // Got second EOF after replay
                    break;
                }
            }
            else
            {
                messages.Add(result.Value);
            }
        }

        // Assert - should have 6 messages (3 original + 3 replayed) and 2 EOF events
        await Assert.That(messages.Count).IsEqualTo(6);
        await Assert.That(eofCount).IsEqualTo(2);

        // Verify first 3 and last 3 messages are the same (replayed)
        for (var i = 0; i < 3; i++)
        {
            await Assert.That(messages[i]).IsEqualTo(messages[i + 3]);
        }
    }

    [Test]
    public async Task Consumer_EofOnEmptyPartition()
    {
        // Arrange - create a topic with no messages
        var topic = await KafkaContainer.CreateTestTopicAsync();

        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithPartitionEof(true)
            .WithQueuedMinMessages(1) // Disable prefetching for predictable EOF timing
            .BuildAsync();

        var tp = new TopicPartition(topic, 0);
        consumer.Assign(tp);

        // Act - consume from empty partition
        var eofReceived = false;
        var messagesReceived = 0;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

        try
        {
            await foreach (var result in consumer.ConsumeAsync(cts.Token))
            {
                if (result.IsPartitionEof)
                {
                    eofReceived = true;
                    break;
                }
                else
                {
                    messagesReceived++;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected if EOF never came
        }

        // Assert - should have received EOF with no messages
        await Assert.That(eofReceived).IsTrue();
        await Assert.That(messagesReceived).IsEqualTo(0);
    }

    [Test]
    public async Task Consumer_EofWithMultiplePartitions()
    {
        // Arrange - create a topic with multiple partitions
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 3);

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer")
            .BuildAsync();

        // Produce to each partition
        for (var partition = 0; partition < 3; partition++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-p{partition}",
                Value = $"value-p{partition}",
                Partition = partition
            });
        }

        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithPartitionEof(true)
            .WithQueuedMinMessages(1) // Disable prefetching for predictable EOF timing
            .BuildAsync();

        // Assign all 3 partitions
        consumer.Assign(
            new TopicPartition(topic, 0),
            new TopicPartition(topic, 1),
            new TopicPartition(topic, 2));

        // Act - consume until we get EOF for all 3 partitions
        var eofPartitions = new HashSet<int>();
        var messageCount = 0;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await foreach (var result in consumer.ConsumeAsync(cts.Token))
        {
            if (result.IsPartitionEof)
            {
                eofPartitions.Add(result.Partition);
                if (eofPartitions.Count >= 3)
                    break; // Got EOF for all partitions
            }
            else
            {
                messageCount++;
            }
        }

        // Assert - should have 3 messages and EOF for each partition
        await Assert.That(messageCount).IsEqualTo(3);
        await Assert.That(eofPartitions.Count).IsEqualTo(3);
        await Assert.That(eofPartitions).Contains(0);
        await Assert.That(eofPartitions).Contains(1);
        await Assert.That(eofPartitions).Contains(2);
    }

    [Test]
    public async Task Consumer_EofWithPrefetchingEnabled_FiresEofCorrectly()
    {
        // This test verifies EOF events work correctly with the default prefetching behavior
        // (QueuedMinMessages = 100000). Unlike other EOF tests that disable prefetching,
        // this uses the default settings to ensure EOF works in the common case.

        // Arrange
        var topic = await KafkaContainer.CreateTestTopicAsync();

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer")
            .BuildAsync();

        // Produce a few messages
        const int messageCount = 5;
        for (var i = 0; i < messageCount; i++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{i}",
                Value = $"value-{i}"
            });
        }

        // Act - consumer with default prefetching (does NOT call WithQueuedMinMessages)
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithPartitionEof(true)
            // Intentionally NOT calling WithQueuedMinMessages to test default behavior
            .BuildAsync();

        var tp = new TopicPartition(topic, 0);
        consumer.Assign(tp);

        var messages = new List<string>();
        var eofReceived = false;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await foreach (var result in consumer.ConsumeAsync(cts.Token))
        {
            if (result.IsPartitionEof)
            {
                eofReceived = true;
                break;
            }
            else
            {
                messages.Add(result.Value);
            }
        }

        // Assert - should have all messages and received EOF
        await Assert.That(messages.Count).IsEqualTo(messageCount);
        await Assert.That(eofReceived).IsTrue();
    }
}
