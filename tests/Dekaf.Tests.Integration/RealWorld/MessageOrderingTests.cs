using System.Collections.Concurrent;
using Dekaf.Consumer;
using Dekaf.Producer;

namespace Dekaf.Tests.Integration.RealWorld;

/// <summary>
/// Tests for message ordering guarantees.
/// Verifies that within a partition, messages are always delivered in order,
/// and that key-based partitioning consistently routes to the same partition.
/// </summary>
[Category("Messaging")]
public sealed class MessageOrderingTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    [Test]
    public async Task KeyBasedPartitioning_SameKey_AlwaysRoutesToSamePartition()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 6);

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .BuildAsync();

        // Produce 50 messages with the same key - all should go to the same partition
        var results = new List<RecordMetadata>();
        for (var i = 0; i < 50; i++)
        {
            var metadata = await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = "consistent-key",
                Value = $"value-{i}"
            });
            results.Add(metadata);
        }

        // All messages should be on the same partition
        var partitions = results.Select(r => r.Partition).Distinct().ToList();
        await Assert.That(partitions).Count().IsEqualTo(1);
    }

    [Test]
    public async Task WithinPartition_MessagesDeliveredInOrder()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithAcks(Acks.All)
            .BuildAsync();

        // Produce 100 ordered messages
        const int messageCount = 100;
        for (var i = 0; i < messageCount; i++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{i}",
                Value = $"value-{i}"
            });
        }

        // Consume and verify order
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

        // Verify strict ordering: offsets should be monotonically increasing
        for (var i = 1; i < messages.Count; i++)
        {
            await Assert.That(messages[i].Offset).IsGreaterThan(messages[i - 1].Offset);
        }

        // Verify values are in the order they were produced
        for (var i = 0; i < messages.Count; i++)
        {
            await Assert.That(messages[i].Value).IsEqualTo($"value-{i}");
        }
    }

    [Test]
    public async Task MultipleKeys_EachKeyOrderedWithinItsPartition()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 3);

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithAcks(Acks.All)
            .BuildAsync();

        // Produce messages interleaving 3 different keys
        const int messagesPerKey = 20;
        string[] keys = ["order-A", "order-B", "order-C"];

        for (var i = 0; i < messagesPerKey; i++)
        {
            foreach (var key in keys)
            {
                await producer.ProduceAsync(new ProducerMessage<string, string>
                {
                    Topic = topic,
                    Key = key,
                    Value = $"{key}-seq-{i}"
                });
            }
        }

        // Consume all messages
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId($"ordering-group-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        consumer.Subscribe(topic);

        var messagesByKey = new Dictionary<string, List<string>>();
        foreach (var key in keys) messagesByKey[key] = [];

        var totalExpected = keys.Length * messagesPerKey;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            messagesByKey[msg.Key!].Add(msg.Value);
            var total = messagesByKey.Values.Sum(l => l.Count);
            if (total >= totalExpected) break;
        }

        // Verify per-key ordering is preserved
        foreach (var key in keys)
        {
            var values = messagesByKey[key];
            await Assert.That(values).Count().IsEqualTo(messagesPerKey);

            for (var i = 0; i < messagesPerKey; i++)
            {
                await Assert.That(values[i]).IsEqualTo($"{key}-seq-{i}");
            }
        }
    }

    [Test]
    public async Task ConcurrentProducers_SameKey_PartitionConsistent()
    {
        // Multiple producers sending with the same key should all route to the same partition
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 6);
        const int producerCount = 3;
        const int messagesPerProducer = 20;

        var allResults = new ConcurrentBag<RecordMetadata>();

        var tasks = Enumerable.Range(0, producerCount).Select(async p =>
        {
            await using var producer = await Kafka.CreateProducer<string, string>()
                .WithBootstrapServers(KafkaContainer.BootstrapServers)
                .BuildAsync();

            for (var i = 0; i < messagesPerProducer; i++)
            {
                var metadata = await producer.ProduceAsync(new ProducerMessage<string, string>
                {
                    Topic = topic,
                    Key = "shared-key",
                    Value = $"producer-{p}-msg-{i}"
                });
                allResults.Add(metadata);
            }
        }).ToArray();

        await Task.WhenAll(tasks);

        // All messages with "shared-key" should be on the same partition
        var partitions = allResults.Select(r => r.Partition).Distinct().ToList();
        await Assert.That(partitions).Count().IsEqualTo(1);
        await Assert.That(allResults.Count).IsEqualTo(producerCount * messagesPerProducer);
    }

    [Test]
    public async Task FireAndForget_WithFlush_MaintainsWithinPartitionOrder()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .BuildAsync();

        // Use fire-and-forget Send() then flush
        const int messageCount = 200;
        for (var i = 0; i < messageCount; i++)
        {
            producer.Send(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = "ordered-key",
                Value = $"seq-{i}"
            });
        }

        await producer.FlushAsync();

        // Consume and verify order
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

        // Values should be strictly ordered
        for (var i = 0; i < messages.Count; i++)
        {
            await Assert.That(messages[i].Value).IsEqualTo($"seq-{i}");
        }
    }

    [Test]
    public async Task DifferentKeys_DistributeAcrossPartitions()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 6);

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .BuildAsync();

        // Produce with many different keys - should distribute across partitions
        var partitionsSeen = new HashSet<int>();
        for (var i = 0; i < 100; i++)
        {
            var metadata = await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"unique-key-{i}",
                Value = $"value-{i}"
            });
            partitionsSeen.Add(metadata.Partition);
        }

        // With 100 different keys and 6 partitions, we should see messages across multiple partitions
        await Assert.That(partitionsSeen.Count).IsGreaterThan(1);
    }

    [Test]
    public async Task SequentialBatches_MaintainOrderAcrossBatches()
    {
        // Verify ordering is preserved even when messages span multiple batches
        var topic = await KafkaContainer.CreateTestTopicAsync();

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithAcks(Acks.All)
            .WithBatchSize(1024) // Small batch size to force multiple batches
            .WithLinger(TimeSpan.FromMilliseconds(1))
            .BuildAsync();

        const int messageCount = 50;
        for (var i = 0; i < messageCount; i++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = "batch-key",
                Value = $"batch-msg-{i:D4}" // Pad for consistent size
            });
        }

        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        var tp = new TopicPartition(topic, 0);
        consumer.Assign(tp);

        var messages = new List<string>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            messages.Add(msg.Value);
            if (messages.Count >= messageCount) break;
        }

        await Assert.That(messages).Count().IsEqualTo(messageCount);

        for (var i = 0; i < messageCount; i++)
        {
            await Assert.That(messages[i]).IsEqualTo($"batch-msg-{i:D4}");
        }
    }
}
