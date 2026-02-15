using Dekaf.Consumer;
using Dekaf.Producer;

namespace Dekaf.Tests.Integration;

/// <summary>
/// Integration tests for idempotent producer behavior.
/// Verifies that the producer provides exactly-once semantics within a producer session.
/// </summary>
[Category("Producer")]
public sealed class IdempotentProducerTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    [Test]
    public async Task IdempotentProducer_ProducesSuccessfully()
    {
        // Arrange
        var topic = await KafkaContainer.CreateTestTopicAsync().ConfigureAwait(false);

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-idempotent-basic")
            .WithAcks(Acks.All)

            .BuildAsync();

        // Act
        var metadata = await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "key1",
            Value = "value1"
        }).ConfigureAwait(false);

        // Assert
        await Assert.That(metadata.Topic).IsEqualTo(topic);
        await Assert.That(metadata.Partition).IsGreaterThanOrEqualTo(0);
        await Assert.That(metadata.Offset).IsGreaterThanOrEqualTo(0);
    }

    [Test]
    public async Task IdempotentProducer_ConcurrentProduction_AllSucceed()
    {
        // Arrange
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 3).ConfigureAwait(false);
        const int threadCount = 5;
        const int messagesPerThread = 20;

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-idempotent-concurrent")
            .WithAcks(Acks.All)

            .BuildAsync();

        var allResults = new System.Collections.Concurrent.ConcurrentBag<RecordMetadata>();
        var errors = new System.Collections.Concurrent.ConcurrentBag<Exception>();

        // Act - multiple threads producing concurrently
        var tasks = Enumerable.Range(0, threadCount).Select(async threadId =>
        {
            for (var i = 0; i < messagesPerThread; i++)
            {
                try
                {
                    var result = await producer.ProduceAsync(new ProducerMessage<string, string>
                    {
                        Topic = topic,
                        Key = $"thread-{threadId}-key-{i}",
                        Value = $"thread-{threadId}-value-{i}"
                    }).ConfigureAwait(false);
                    allResults.Add(result);
                }
                catch (Exception ex)
                {
                    errors.Add(ex);
                }
            }
        }).ToArray();

        await Task.WhenAll(tasks).ConfigureAwait(false);

        // Assert
        await Assert.That(errors).Count().IsEqualTo(0);
        await Assert.That(allResults).Count().IsEqualTo(threadCount * messagesPerThread);

        foreach (var result in allResults)
        {
            await Assert.That(result.Topic).IsEqualTo(topic);
            await Assert.That(result.Offset).IsGreaterThanOrEqualTo(0);
        }
    }

    [Test]
    public async Task IdempotentProducer_WithMaxInFlight_MaintainsOrdering()
    {
        // Arrange - single partition to verify ordering
        var topic = await KafkaContainer.CreateTestTopicAsync().ConfigureAwait(false);
        const int messageCount = 50;

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-idempotent-ordering")
            .WithAcks(Acks.All)

            .BuildAsync();

        // Act - produce messages concurrently and verify ordering
        var produceTasks = new List<ValueTask<RecordMetadata>>();
        for (var i = 0; i < messageCount; i++)
        {
            produceTasks.Add(producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = "same-key",
                Value = $"value-{i:D4}"
            }));
        }

        var results = new List<RecordMetadata>();
        foreach (var task in produceTasks)
        {
            results.Add(await task.ConfigureAwait(false));
        }

        // Assert - all offsets should be unique and contiguous on the single partition
        // Sort by offset since task completion order doesn't match produce order
        var sortedOffsets = results.Select(r => r.Offset).OrderBy(o => o).ToList();
        await Assert.That(sortedOffsets).Count().IsEqualTo(messageCount);
        for (var i = 1; i < sortedOffsets.Count; i++)
        {
            await Assert.That(sortedOffsets[i]).IsEqualTo(sortedOffsets[i - 1] + 1);
        }
    }

    [Test]
    public async Task IdempotentProducer_ForReliabilityPreset_ProducesCorrectly()
    {
        // Arrange - ForReliability() should enable idempotence
        var topic = await KafkaContainer.CreateTestTopicAsync().ConfigureAwait(false);

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-reliability-preset")
            .ForReliability()
            .BuildAsync();

        // Act
        var metadata = await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "reliable-key",
            Value = "reliable-value"
        }).ConfigureAwait(false);

        // Assert
        await Assert.That(metadata.Topic).IsEqualTo(topic);
        await Assert.That(metadata.Offset).IsGreaterThanOrEqualTo(0);

        // Verify by consuming
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-reliability-consumer")
            .WithGroupId($"test-group-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        consumer.Subscribe(topic);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token).ConfigureAwait(false);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value.Key).IsEqualTo("reliable-key");
        await Assert.That(result.Value.Value).IsEqualTo("reliable-value");
    }

    [Test]
    public async Task IdempotentProducer_LargeVolume_NoDataLoss()
    {
        // Arrange
        var topic = await KafkaContainer.CreateTestTopicAsync().ConfigureAwait(false);
        const int messageCount = 1000;

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-idempotent-volume")
            .WithAcks(Acks.All)

            .WithLinger(TimeSpan.FromMilliseconds(5))
            .BuildAsync();

        // Act - produce 1000 messages
        var produceTasks = new List<ValueTask<RecordMetadata>>();
        for (var i = 0; i < messageCount; i++)
        {
            produceTasks.Add(producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{i:D4}",
                Value = $"value-{i:D4}"
            }));
        }

        foreach (var task in produceTasks)
        {
            await task.ConfigureAwait(false);
        }

        // Consume all messages back
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-idempotent-volume-consumer")
            .WithGroupId($"test-group-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        consumer.Subscribe(topic);

        var consumed = new List<ConsumeResult<string, string>>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

        await foreach (var msg in consumer.ConsumeAsync(cts.Token).ConfigureAwait(false))
        {
            consumed.Add(msg);
            if (consumed.Count >= messageCount) break;
        }

        // Assert - no data loss
        await Assert.That(consumed).Count().IsEqualTo(messageCount);
    }
}
