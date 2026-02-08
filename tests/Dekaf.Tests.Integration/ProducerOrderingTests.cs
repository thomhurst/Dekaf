using System.Collections.Concurrent;
using Dekaf.Consumer;
using Dekaf.Producer;

namespace Dekaf.Tests.Integration;

/// <summary>
/// Integration tests for producer ordering guarantees with max.in.flight.requests.
/// Verifies that idempotent producers with pipelined in-flight requests preserve
/// message ordering under various conditions including high concurrency,
/// multi-partition scenarios, and flush boundaries.
/// </summary>
public sealed class ProducerOrderingTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    [Test]
    public async Task IdempotentProducer_StrictOrdering_Preserved()
    {
        // Produce 100 sequenced messages with idempotence enabled
        // and verify consumption order matches production order exactly
        var topic = await KafkaContainer.CreateTestTopicAsync();
        const int messageCount = 100;

        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-strict-ordering")
            .WithAcks(Acks.All)
            .EnableIdempotence()
            .Build();

        // Produce sequentially to guarantee append order
        for (var i = 0; i < messageCount; i++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = "ordering-key",
                Value = $"seq-{i:D4}"
            });
        }

        // Consume and verify strict ordering
        await using var consumer = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .Build();

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

        // Verify strict value ordering
        for (var i = 0; i < messageCount; i++)
        {
            await Assert.That(messages[i].Value).IsEqualTo($"seq-{i:D4}");
        }

        // Verify monotonically increasing offsets
        for (var i = 1; i < messages.Count; i++)
        {
            await Assert.That(messages[i].Offset).IsGreaterThan(messages[i - 1].Offset);
        }
    }

    [Test]
    public async Task IdempotentProducer_HighThroughput_OrderingPreserved()
    {
        // Fire many concurrent produces and verify per-partition ordering is preserved
        // even when multiple in-flight requests are pipelined
        var topic = await KafkaContainer.CreateTestTopicAsync();
        const int messageCount = 500;

        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-high-throughput-ordering")
            .WithAcks(Acks.All)
            .EnableIdempotence()
            .WithLinger(TimeSpan.FromMilliseconds(5))
            .Build();

        // Fire all produces concurrently to stress pipelining
        var produceTasks = new List<ValueTask<RecordMetadata>>();
        for (var i = 0; i < messageCount; i++)
        {
            produceTasks.Add(producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = "throughput-key",
                Value = $"msg-{i:D4}"
            }));
        }

        // Await all to complete
        foreach (var task in produceTasks)
        {
            await task;
        }

        // Consume and verify ordering
        await using var consumer = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .Build();

        var tp = new TopicPartition(topic, 0);
        consumer.Assign(tp);

        var messages = new List<ConsumeResult<string, string>>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            messages.Add(msg);
            if (messages.Count >= messageCount) break;
        }

        await Assert.That(messages).Count().IsEqualTo(messageCount);

        // With idempotent producer and single partition, all messages should
        // be in the exact order they were submitted, even with concurrent produces
        for (var i = 0; i < messageCount; i++)
        {
            await Assert.That(messages[i].Value).IsEqualTo($"msg-{i:D4}");
        }
    }

    [Test]
    public async Task SinglePartition_OrderingAlwaysPreserved()
    {
        // Verify that messages to a single partition always maintain order
        // even with small batch sizes that force many in-flight batches
        var topic = await KafkaContainer.CreateTestTopicAsync();
        const int messageCount = 200;

        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-single-partition-ordering")
            .WithAcks(Acks.All)
            .EnableIdempotence()
            .WithBatchSize(512) // Very small to force many batches in flight
            .WithLinger(TimeSpan.FromMilliseconds(1))
            .Build();

        // Mix sequential and concurrent produces
        for (var i = 0; i < messageCount / 2; i++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = "single-part-key",
                Value = $"ordered-{i:D4}"
            });
        }

        // Second half: fire-and-forget then flush
        for (var i = messageCount / 2; i < messageCount; i++)
        {
            producer.Send(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = "single-part-key",
                Value = $"ordered-{i:D4}"
            });
        }

        await producer.FlushAsync();

        // Consume all and verify strict ordering
        await using var consumer = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .Build();

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

        for (var i = 0; i < messageCount; i++)
        {
            await Assert.That(messages[i].Value).IsEqualTo($"ordered-{i:D4}");
        }
    }

    [Test]
    public async Task MultiPartition_PerPartitionOrdering_Preserved()
    {
        // With multiple partitions, verify that ordering is maintained within each partition
        // when messages are produced concurrently across partitions
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 4);
        const int messagesPerPartition = 50;

        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-multi-partition-ordering")
            .WithAcks(Acks.All)
            .EnableIdempotence()
            .WithLinger(TimeSpan.FromMilliseconds(2))
            .Build();

        // Produce to all partitions concurrently using explicit partition assignment
        var produceTasks = new List<ValueTask<RecordMetadata>>();
        for (var p = 0; p < 4; p++)
        {
            for (var i = 0; i < messagesPerPartition; i++)
            {
                produceTasks.Add(producer.ProduceAsync(new ProducerMessage<string, string>
                {
                    Topic = topic,
                    Key = $"p{p}-msg-{i}",
                    Value = $"p{p}-seq-{i:D4}",
                    Partition = p
                }));
            }
        }

        foreach (var task in produceTasks)
        {
            await task;
        }

        // Consume from all partitions
        await using var consumer = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId($"ordering-mp-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .Build();

        consumer.Subscribe(topic);

        var messagesByPartition = new Dictionary<int, List<ConsumeResult<string, string>>>();
        for (var p = 0; p < 4; p++) messagesByPartition[p] = [];

        var totalExpected = 4 * messagesPerPartition;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            messagesByPartition[msg.Partition].Add(msg);
            var total = messagesByPartition.Values.Sum(l => l.Count);
            if (total >= totalExpected) break;
        }

        // Verify per-partition ordering
        for (var p = 0; p < 4; p++)
        {
            var partitionMessages = messagesByPartition[p];
            await Assert.That(partitionMessages).Count().IsEqualTo(messagesPerPartition);

            // Values within each partition must be in sequence order
            for (var i = 0; i < messagesPerPartition; i++)
            {
                await Assert.That(partitionMessages[i].Value).IsEqualTo($"p{p}-seq-{i:D4}");
            }

            // Offsets within each partition must be monotonically increasing
            for (var i = 1; i < partitionMessages.Count; i++)
            {
                await Assert.That(partitionMessages[i].Offset)
                    .IsGreaterThan(partitionMessages[i - 1].Offset);
            }
        }
    }

    [Test]
    public async Task ConcurrentProduces_WithFlush_OrderingPreserved()
    {
        // Multiple concurrent producers writing to the same topic,
        // verify per-key ordering is preserved within each partition
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 3);
        const int producerCount = 3;
        const int messagesPerProducer = 50;

        var allMetadata = new ConcurrentBag<(int ProducerId, int Sequence, RecordMetadata Metadata)>();

        var tasks = Enumerable.Range(0, producerCount).Select(async producerId =>
        {
            await using var producer = Kafka.CreateProducer<string, string>()
                .WithBootstrapServers(KafkaContainer.BootstrapServers)
                .WithClientId($"test-concurrent-flush-{producerId}")
                .WithAcks(Acks.All)
                .EnableIdempotence()
                .Build();

            for (var i = 0; i < messagesPerProducer; i++)
            {
                // Each producer uses a unique key to guarantee partition assignment
                var metadata = await producer.ProduceAsync(new ProducerMessage<string, string>
                {
                    Topic = topic,
                    Key = $"producer-{producerId}-key",
                    Value = $"producer-{producerId}-seq-{i:D4}"
                });
                allMetadata.Add((producerId, i, metadata));
            }

            await producer.FlushAsync();
        }).ToArray();

        await Task.WhenAll(tasks);

        await Assert.That(allMetadata.Count).IsEqualTo(producerCount * messagesPerProducer);

        // Consume all messages
        await using var consumer = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId($"concurrent-flush-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .Build();

        consumer.Subscribe(topic);

        var messagesByKey = new Dictionary<string, List<string>>();
        for (var p = 0; p < producerCount; p++)
        {
            messagesByKey[$"producer-{p}-key"] = [];
        }

        var totalExpected = producerCount * messagesPerProducer;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            if (msg.Key is not null && messagesByKey.TryGetValue(msg.Key, out var keyMessages))
            {
                keyMessages.Add(msg.Value);
            }

            var total = messagesByKey.Values.Sum(l => l.Count);
            if (total >= totalExpected) break;
        }

        // Verify per-producer ordering: each producer's messages must appear in sequence
        for (var p = 0; p < producerCount; p++)
        {
            var key = $"producer-{p}-key";
            var values = messagesByKey[key];
            await Assert.That(values).Count().IsEqualTo(messagesPerProducer);

            for (var i = 0; i < messagesPerProducer; i++)
            {
                await Assert.That(values[i]).IsEqualTo($"producer-{p}-seq-{i:D4}");
            }
        }
    }
}
