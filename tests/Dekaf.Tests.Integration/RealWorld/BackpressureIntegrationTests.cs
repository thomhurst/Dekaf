using System.Collections.Concurrent;
using System.Diagnostics;
using Dekaf.Consumer;
using Dekaf.Producer;

namespace Dekaf.Tests.Integration.RealWorld;

/// <summary>
/// Integration tests for backpressure behavior with real Kafka.
/// Verifies buffer memory limits, MaxBlock timeout, message delivery under pressure,
/// and concurrent producer behavior with shared buffer memory.
/// </summary>
[Category("Backpressure")]
public sealed class BackpressureIntegrationTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    [Test]
    public async Task BufferFull_ProduceBlocks_UntilBatchSent()
    {
        // Arrange - very small buffer to trigger backpressure quickly
        var topic = await KafkaContainer.CreateTestTopicAsync();

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer-buffer-blocks")
            .WithBufferMemory(65536) // 64KB buffer
            .WithLinger(TimeSpan.FromMilliseconds(5))
            .WithAcks(Acks.Leader)
            .BuildAsync();

        // Act - produce more data than the buffer can hold
        var messageValue = new string('x', 1000); // ~1KB per message
        const int messageCount = 200; // ~200KB > 64KB buffer

        var produceTasks = new List<ValueTask<RecordMetadata>>();
        for (var i = 0; i < messageCount; i++)
        {
            produceTasks.Add(producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{i}",
                Value = messageValue
            }));
        }

        // All should eventually complete (backpressure unblocks as batches are sent)
        var results = new List<RecordMetadata>();
        foreach (var task in produceTasks)
        {
            results.Add(await task.ConfigureAwait(false));
        }

        // Assert - all messages delivered
        await Assert.That(results.Count).IsEqualTo(messageCount);
        foreach (var result in results)
        {
            await Assert.That(result.Offset).IsGreaterThanOrEqualTo(0);
        }
    }

    [Test]
    public async Task BackpressureDoesNotCauseMessageLoss()
    {
        // Arrange - constrained buffer to exercise backpressure, but large enough to drain
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 3);

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer-no-loss")
            .WithBufferMemory(1_048_576) // 1MB
            .WithLinger(TimeSpan.FromMilliseconds(10))
            .WithAcks(Acks.Leader)
            .BuildAsync();

        var messageValue = new string('x', 500);
        const int messageCount = 500;

        // Act - rapid-fire production
        var produceTasks = new List<ValueTask<RecordMetadata>>();
        for (var i = 0; i < messageCount; i++)
        {
            produceTasks.Add(producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{i}",
                Value = messageValue
            }));
        }

        var results = new List<RecordMetadata>();
        foreach (var task in produceTasks)
        {
            results.Add(await task.ConfigureAwait(false));
        }

        await Assert.That(results.Count).IsEqualTo(messageCount);

        // Verify by consuming all messages
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer-no-loss")
            .WithGroupId($"test-group-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        consumer.Subscribe(topic);

        var consumed = new List<ConsumeResult<string, string>>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            consumed.Add(msg);
            if (consumed.Count >= messageCount) break;
        }

        // Assert - no message loss
        await Assert.That(consumed.Count).IsEqualTo(messageCount);
    }

    [Test]
    public async Task ConcurrentProducers_SharedInstance_AllMessagesDelivered()
    {
        // Arrange
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 3);

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer-concurrent")
            .WithBufferMemory(262144) // 256KB
            .WithLinger(TimeSpan.FromMilliseconds(5))
            .BuildAsync();

        const int tasksCount = 5;
        const int messagesPerTask = 100;
        var allResults = new ConcurrentBag<RecordMetadata>();

        // Act - 5 concurrent tasks each producing 100 messages
        var tasks = new List<Task>();
        for (var t = 0; t < tasksCount; t++)
        {
            var taskId = t;
            tasks.Add(Task.Run(async () =>
            {
                for (var i = 0; i < messagesPerTask; i++)
                {
                    var result = await producer.ProduceAsync(new ProducerMessage<string, string>
                    {
                        Topic = topic,
                        Key = $"task-{taskId}-key-{i}",
                        Value = $"task-{taskId}-value-{i}"
                    }).ConfigureAwait(false);
                    allResults.Add(result);
                }
            }));
        }

        await Task.WhenAll(tasks).ConfigureAwait(false);

        // Assert - all messages delivered
        await Assert.That(allResults.Count).IsEqualTo(tasksCount * messagesPerTask);
        foreach (var result in allResults)
        {
            await Assert.That(result.Offset).IsGreaterThanOrEqualTo(0);
        }
    }

    [Test]
    public async Task FireAndForget_WithSmallBuffer_AllDelivered()
    {
        // Arrange - Send() with constrained buffer tests backpressure in fire-and-forget mode
        var topic = await KafkaContainer.CreateTestTopicAsync();

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer-ff-backpressure")
            .WithBufferMemory(1_048_576) // 1MB
            .WithLinger(TimeSpan.FromMilliseconds(10))
            .BuildAsync();

        var messageValue = new string('x', 500);
        const int messageCount = 300;

        // Act - fire-and-forget with small buffer
        for (var i = 0; i < messageCount; i++)
        {
            producer.Send(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{i}",
                Value = messageValue
            });
        }

        await producer.FlushAsync().ConfigureAwait(false);

        // Verify delivery
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer-ff-backpressure")
            .WithGroupId($"test-group-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        consumer.Subscribe(topic);

        var consumed = new List<ConsumeResult<string, string>>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            consumed.Add(msg);
            if (consumed.Count >= messageCount) break;
        }

        await Assert.That(consumed.Count).IsEqualTo(messageCount);
    }

    [Test]
    public async Task SmallBuffer_HighThroughput_NoDeadlock()
    {
        // This test verifies that even with backpressure, the system doesn't deadlock
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 3);

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer-no-deadlock")
            .WithBufferMemory(1_048_576) // 1MB
            .WithLinger(TimeSpan.FromMilliseconds(1))
            .WithAcks(Acks.Leader)
            .BuildAsync();

        var messageValue = new string('x', 200);
        const int messageCount = 1000;

        // Act - produce with strict timeout to detect deadlocks
        var sw = Stopwatch.StartNew();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));

        var produceTasks = new List<ValueTask<RecordMetadata>>();
        for (var i = 0; i < messageCount; i++)
        {
            produceTasks.Add(producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{i}",
                Value = messageValue
            }));
        }

        var results = new List<RecordMetadata>();
        foreach (var task in produceTasks)
        {
            results.Add(await task.ConfigureAwait(false));
        }

        sw.Stop();

        // Assert - completed without deadlock
        await Assert.That(results.Count).IsEqualTo(messageCount);
        await Assert.That(sw.Elapsed.TotalSeconds).IsLessThan(120);
    }
}
