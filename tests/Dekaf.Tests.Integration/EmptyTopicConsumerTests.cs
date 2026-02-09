using Dekaf.Consumer;
using Dekaf.Producer;

namespace Dekaf.Tests.Integration;

/// <summary>
/// Integration tests for consumer behavior on empty topics with subsequent message arrival.
/// Closes #223
/// </summary>
public class EmptyTopicConsumerTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    [Test]
    public async Task Consumer_SubscribesToEmptyTopic_ReceivesMessageWhenProduced()
    {
        // Arrange - create an empty topic, subscribe a consumer, then produce a message
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var groupId = $"test-group-{Guid.NewGuid():N}";

        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer")
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithSessionTimeout(TimeSpan.FromMilliseconds(10000))
            .BuildAsync();

        consumer.Subscribe(topic);

        // Start consuming in background before any messages exist
        var receivedMessages = new List<ConsumeResult<string, string>>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        var consumeTask = Task.Run(async () =>
        {
            try
            {
                await foreach (var msg in consumer.ConsumeAsync(cts.Token))
                {
                    receivedMessages.Add(msg);
                    if (receivedMessages.Count >= 1) break;
                }
            }
            catch (OperationCanceledException) { }
        });

        // Wait for consumer to get partition assignment on the empty topic
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (consumer.Assignment.Count == 0 && sw.Elapsed < TimeSpan.FromSeconds(15))
        {
            await Task.Delay(100);
        }

        if (consumer.Assignment.Count == 0)
        {
            throw new InvalidOperationException("Consumer did not receive assignment within timeout");
        }

        // Now produce a message to the previously empty topic
        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer")
            .BuildAsync();

        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "first-key",
            Value = "first-value"
        });

        await consumeTask;

        // Assert - consumer should receive the message that arrived after subscription
        await Assert.That(receivedMessages).Count().IsEqualTo(1);
        await Assert.That(receivedMessages[0].Topic).IsEqualTo(topic);
        await Assert.That(receivedMessages[0].Key).IsEqualTo("first-key");
        await Assert.That(receivedMessages[0].Value).IsEqualTo("first-value");
    }

    [Test]
    public async Task Consumer_AutoOffsetResetLatest_OnEmptyTopic_ReceivesOnlyNewMessages()
    {
        // Arrange - subscribe with Latest on empty topic, then produce messages
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var groupId = $"test-group-{Guid.NewGuid():N}";

        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer")
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Latest)
            .WithSessionTimeout(TimeSpan.FromMilliseconds(10000))
            .BuildAsync();

        consumer.Subscribe(topic);

        // Start consuming in background
        var receivedMessages = new List<ConsumeResult<string, string>>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        var consumeTask = Task.Run(async () =>
        {
            try
            {
                await foreach (var msg in consumer.ConsumeAsync(cts.Token))
                {
                    receivedMessages.Add(msg);
                    if (receivedMessages.Count >= 2) break;
                }
            }
            catch (OperationCanceledException) { }
        });

        // Wait for consumer to get partition assignment
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (consumer.Assignment.Count == 0 && sw.Elapsed < TimeSpan.FromSeconds(15))
        {
            await Task.Delay(100);
        }

        if (consumer.Assignment.Count == 0)
        {
            throw new InvalidOperationException("Consumer did not receive assignment within timeout");
        }

        // Small additional delay to ensure positions are initialized after assignment
        await Task.Delay(500);

        // Now produce messages after consumer is positioned at Latest
        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer")
            .BuildAsync();

        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "new-key-1",
            Value = "new-value-1"
        });

        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "new-key-2",
            Value = "new-value-2"
        });

        await consumeTask;

        // Assert - with Latest on an empty topic, consumer should receive all produced messages
        // since the topic was empty at subscription time (Latest = end of log = offset 0 = same as Earliest)
        await Assert.That(receivedMessages).Count().IsEqualTo(2);
        await Assert.That(receivedMessages[0].Key).IsEqualTo("new-key-1");
        await Assert.That(receivedMessages[0].Value).IsEqualTo("new-value-1");
        await Assert.That(receivedMessages[1].Key).IsEqualTo("new-key-2");
        await Assert.That(receivedMessages[1].Value).IsEqualTo("new-value-2");
    }

    [Test]
    public async Task Consumer_AutoOffsetResetEarliest_OnEmptyTopic_ReceivesAllMessagesOnceProduced()
    {
        // Arrange - subscribe with Earliest on empty topic, then produce messages
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var groupId = $"test-group-{Guid.NewGuid():N}";

        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer")
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithSessionTimeout(TimeSpan.FromMilliseconds(10000))
            .BuildAsync();

        consumer.Subscribe(topic);

        // Start consuming in background
        var receivedMessages = new List<ConsumeResult<string, string>>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        var consumeTask = Task.Run(async () =>
        {
            try
            {
                await foreach (var msg in consumer.ConsumeAsync(cts.Token))
                {
                    receivedMessages.Add(msg);
                    if (receivedMessages.Count >= 3) break;
                }
            }
            catch (OperationCanceledException) { }
        });

        // Wait for consumer to get partition assignment
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (consumer.Assignment.Count == 0 && sw.Elapsed < TimeSpan.FromSeconds(15))
        {
            await Task.Delay(100);
        }

        if (consumer.Assignment.Count == 0)
        {
            throw new InvalidOperationException("Consumer did not receive assignment within timeout");
        }

        // Produce messages after consumer is subscribed and assigned
        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer")
            .BuildAsync();

        for (var i = 0; i < 3; i++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{i}",
                Value = $"value-{i}"
            });
        }

        await consumeTask;

        // Assert - Earliest on empty topic should receive all messages once produced
        await Assert.That(receivedMessages).Count().IsEqualTo(3);
        await Assert.That(receivedMessages[0].Offset).IsEqualTo(0);
        await Assert.That(receivedMessages[0].Key).IsEqualTo("key-0");
        await Assert.That(receivedMessages[0].Value).IsEqualTo("value-0");
        await Assert.That(receivedMessages[1].Key).IsEqualTo("key-1");
        await Assert.That(receivedMessages[2].Key).IsEqualTo("key-2");
    }

    [Test]
    public async Task Consumer_LongPollingOnEmptyTopic_RespectsTimeout()
    {
        // Arrange - create an empty topic and attempt to consume with a short timeout
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var groupId = $"test-group-{Guid.NewGuid():N}";

        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer")
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithSessionTimeout(TimeSpan.FromMilliseconds(10000))
            .BuildAsync();

        consumer.Subscribe(topic);

        // Wait for consumer to get assignment first so we know the group has stabilized
        var sw = System.Diagnostics.Stopwatch.StartNew();
        using var assignCts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

        // Trigger the group join by starting to consume; use a short timeout
        // ConsumeOneAsync with a short poll timeout on an empty topic should return null
        var pollTimeout = TimeSpan.FromSeconds(5);
        var result = await consumer.ConsumeOneAsync(pollTimeout, assignCts.Token);

        // Assert - no messages on empty topic, should return null within timeout
        await Assert.That(result).IsNull();

        // Verify the operation completed within a reasonable time (not hanging)
        await Assert.That(sw.Elapsed).IsLessThan(TimeSpan.FromSeconds(30));
    }

    [Test]
    public async Task Consumer_MultipleConsumersOnEmptyTopic_RebalanceBeforeMessages()
    {
        // Arrange - two consumers subscribe to the same empty topic before any messages exist
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 4);
        var groupId = $"test-group-{Guid.NewGuid():N}";

        // First consumer
        await using var consumer1 = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer-1")
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithSessionTimeout(TimeSpan.FromMilliseconds(10000))
            .BuildAsync();

        consumer1.Subscribe(topic);

        // Start consumer1 consuming in background to trigger group join
        var received1 = new List<ConsumeResult<string, string>>();
        using var cts1 = new CancellationTokenSource(TimeSpan.FromSeconds(45));

        var consumeTask1 = Task.Run(async () =>
        {
            try
            {
                await foreach (var msg in consumer1.ConsumeAsync(cts1.Token))
                {
                    received1.Add(msg);
                    if (received1.Count >= 2) break;
                }
            }
            catch (OperationCanceledException) { }
        });

        // Wait for consumer1 to get initial assignment on empty topic
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (consumer1.Assignment.Count == 0 && sw.Elapsed < TimeSpan.FromSeconds(15))
        {
            await Task.Delay(100);
        }

        if (consumer1.Assignment.Count == 0)
        {
            throw new InvalidOperationException("Consumer 1 did not receive assignment within timeout");
        }

        // Second consumer joins the same group on the empty topic
        await using var consumer2 = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer-2")
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithSessionTimeout(TimeSpan.FromMilliseconds(10000))
            .BuildAsync();

        consumer2.Subscribe(topic);

        // Start consumer2 consuming in background
        var received2 = new List<ConsumeResult<string, string>>();
        using var cts2 = new CancellationTokenSource(TimeSpan.FromSeconds(45));

        var consumeTask2 = Task.Run(async () =>
        {
            try
            {
                await foreach (var msg in consumer2.ConsumeAsync(cts2.Token))
                {
                    received2.Add(msg);
                    if (received2.Count >= 2) break;
                }
            }
            catch (OperationCanceledException) { }
        });

        // Wait for rebalance to complete - both consumers should have assignments
        sw.Restart();
        while ((consumer1.Assignment.Count == 0 || consumer2.Assignment.Count == 0)
               && sw.Elapsed < TimeSpan.FromSeconds(20))
        {
            await Task.Delay(200);
        }

        // Both consumers should have assignments (partitions split between them)
        await Assert.That(consumer1.Assignment.Count).IsGreaterThanOrEqualTo(1);
        await Assert.That(consumer2.Assignment.Count).IsGreaterThanOrEqualTo(1);

        // Total assignments should cover all partitions
        var totalAssigned = consumer1.Assignment.Count + consumer2.Assignment.Count;
        await Assert.That(totalAssigned).IsEqualTo(4);

        // Now produce messages - they should be distributed across consumers
        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer")
            .BuildAsync();

        for (var p = 0; p < 4; p++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{p}",
                Value = $"value-{p}",
                Partition = p
            });
        }

        // Wait for both consumers to finish receiving
        await Task.WhenAll(consumeTask1, consumeTask2);

        // Assert - messages should be distributed between the two consumers
        var totalReceived = received1.Count + received2.Count;
        await Assert.That(totalReceived).IsEqualTo(4);
        await Assert.That(received1.Count).IsGreaterThanOrEqualTo(1);
        await Assert.That(received2.Count).IsGreaterThanOrEqualTo(1);
    }
}
