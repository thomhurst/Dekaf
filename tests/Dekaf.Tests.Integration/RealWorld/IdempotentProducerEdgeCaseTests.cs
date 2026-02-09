using System.Collections.Concurrent;
using Dekaf.Consumer;
using Dekaf.Producer;

namespace Dekaf.Tests.Integration.RealWorld;

/// <summary>
/// Edge case tests for idempotent producer behavior.
/// Verifies that idempotent producers correctly deduplicate messages,
/// handle concurrent access, and maintain ordering guarantees.
/// </summary>
[Category("Producer")]
public sealed class IdempotentProducerEdgeCaseTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    [Test]
    public async Task IdempotentProducer_SequentialMessages_AllDelivered()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithAcks(Acks.All)
            .BuildAsync();

        const int messageCount = 50;
        var offsets = new List<long>();

        for (var i = 0; i < messageCount; i++)
        {
            var metadata = await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{i}",
                Value = $"value-{i}"
            });
            offsets.Add(metadata.Offset);
        }

        // All offsets should be unique and monotonically increasing
        for (var i = 1; i < offsets.Count; i++)
        {
            await Assert.That(offsets[i]).IsGreaterThan(offsets[i - 1]);
        }

        // Verify all messages are consumable
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        var tp = new TopicPartition(topic, 0);
        consumer.Assign(tp);

        var messages = new List<ConsumeResult<string, string>>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            messages.Add(msg);
            if (messages.Count >= messageCount) break;
        }

        await Assert.That(messages).Count().IsEqualTo(messageCount);
    }

    [Test]
    public async Task IdempotentProducer_ConcurrentProduction_NoDuplicates()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithAcks(Acks.All)
            .BuildAsync();

        const int tasksCount = 5;
        const int messagesPerTask = 20;
        var allMetadata = new ConcurrentBag<RecordMetadata>();

        var tasks = Enumerable.Range(0, tasksCount).Select(async t =>
        {
            for (var i = 0; i < messagesPerTask; i++)
            {
                var metadata = await producer.ProduceAsync(new ProducerMessage<string, string>
                {
                    Topic = topic,
                    Key = $"task-{t}-key-{i}",
                    Value = $"task-{t}-value-{i}"
                });
                allMetadata.Add(metadata);
            }
        }).ToArray();

        await Task.WhenAll(tasks);

        // All offsets should be unique (no duplicates)
        var uniqueOffsets = allMetadata.Select(m => m.Offset).Distinct().ToList();
        await Assert.That(uniqueOffsets).Count().IsEqualTo(tasksCount * messagesPerTask);

        // Verify exact message count via consumer
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        var tp = new TopicPartition(topic, 0);
        consumer.Assign(tp);

        var messages = new List<ConsumeResult<string, string>>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            messages.Add(msg);
            if (messages.Count >= tasksCount * messagesPerTask) break;
        }

        await Assert.That(messages).Count().IsEqualTo(tasksCount * messagesPerTask);

        // Verify no duplicate values
        var uniqueValues = messages.Select(m => m.Value).Distinct().ToList();
        await Assert.That(uniqueValues).Count().IsEqualTo(tasksCount * messagesPerTask);
    }

    [Test]
    public async Task IdempotentProducer_FireAndForget_NoDuplicates()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithAcks(Acks.All)
            .BuildAsync();

        const int messageCount = 200;
        for (var i = 0; i < messageCount; i++)
        {
            producer.Send(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{i}",
                Value = $"ff-value-{i}"
            });
        }

        await producer.FlushAsync();

        // Consume and verify no duplicates
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        var tp = new TopicPartition(topic, 0);
        consumer.Assign(tp);

        var messages = new List<ConsumeResult<string, string>>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            messages.Add(msg);
            if (messages.Count >= messageCount) break;
        }

        await Assert.That(messages).Count().IsEqualTo(messageCount);

        var uniqueValues = messages.Select(m => m.Value).Distinct().ToList();
        await Assert.That(uniqueValues).Count().IsEqualTo(messageCount);
    }

    [Test]
    public async Task IdempotentProducer_MultiplePartitions_EachPartitionOrdered()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 4);

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithAcks(Acks.All)
            .BuildAsync();

        const int messagesPerPartition = 25;
        for (var p = 0; p < 4; p++)
        {
            for (var i = 0; i < messagesPerPartition; i++)
            {
                await producer.ProduceAsync(new ProducerMessage<string, string>
                {
                    Topic = topic,
                    Key = $"p{p}-key-{i}",
                    Value = $"p{p}-value-{i}",
                    Partition = p
                });
            }
        }

        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId($"idempotent-mp-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        consumer.Subscribe(topic);

        var messagesByPartition = new Dictionary<int, List<ConsumeResult<string, string>>>();
        for (var p = 0; p < 4; p++) messagesByPartition[p] = [];

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            messagesByPartition[msg.Partition].Add(msg);
            var total = messagesByPartition.Values.Sum(l => l.Count);
            if (total >= 4 * messagesPerPartition) break;
        }

        // Each partition should have ordered messages with increasing offsets
        for (var p = 0; p < 4; p++)
        {
            var partitionMessages = messagesByPartition[p];
            await Assert.That(partitionMessages).Count().IsEqualTo(messagesPerPartition);

            for (var i = 1; i < partitionMessages.Count; i++)
            {
                await Assert.That(partitionMessages[i].Offset)
                    .IsGreaterThan(partitionMessages[i - 1].Offset);
            }
        }
    }

    [Test]
    public async Task IdempotentProducer_RestartProducer_ContinuesWithNewEpoch()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();

        // First producer - produce some messages
        await using (var producer1 = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithAcks(Acks.All)
            .BuildAsync())
        {
            for (var i = 0; i < 10; i++)
            {
                await producer1.ProduceAsync(new ProducerMessage<string, string>
                {
                    Topic = topic,
                    Key = $"key-{i}",
                    Value = $"producer1-value-{i}"
                });
            }
        }

        // Second producer - should produce successfully after first is disposed
        await using (var producer2 = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithAcks(Acks.All)
            .BuildAsync())
        {
            for (var i = 10; i < 20; i++)
            {
                await producer2.ProduceAsync(new ProducerMessage<string, string>
                {
                    Topic = topic,
                    Key = $"key-{i}",
                    Value = $"producer2-value-{i}"
                });
            }
        }

        // Consume all 20 messages
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        var tp = new TopicPartition(topic, 0);
        consumer.Assign(tp);

        var messages = new List<ConsumeResult<string, string>>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            messages.Add(msg);
            if (messages.Count >= 20) break;
        }

        await Assert.That(messages).Count().IsEqualTo(20);

        // Verify all messages from both producers are present
        var producer1Msgs = messages.Where(m => m.Value.StartsWith("producer1", StringComparison.Ordinal)).ToList();
        var producer2Msgs = messages.Where(m => m.Value.StartsWith("producer2", StringComparison.Ordinal)).ToList();
        await Assert.That(producer1Msgs).Count().IsEqualTo(10);
        await Assert.That(producer2Msgs).Count().IsEqualTo(10);
    }

    [Test]
    public async Task IdempotentProducer_SmallBatchSize_MaintainsOrdering()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();

        // Use very small batch size to force many batches
        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithAcks(Acks.All)
            .WithBatchSize(256) // Very small - each message will be its own batch
            .WithLinger(TimeSpan.FromMilliseconds(0))
            .BuildAsync();

        const int messageCount = 30;
        for (var i = 0; i < messageCount; i++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = "same-key",
                Value = $"small-batch-{i}"
            });
        }

        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        var tp = new TopicPartition(topic, 0);
        consumer.Assign(tp);

        var messages = new List<ConsumeResult<string, string>>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            messages.Add(msg);
            if (messages.Count >= messageCount) break;
        }

        await Assert.That(messages).Count().IsEqualTo(messageCount);

        // Verify strict ordering even with small batches
        for (var i = 0; i < messageCount; i++)
        {
            await Assert.That(messages[i].Value).IsEqualTo($"small-batch-{i}");
        }
    }
}
