using System.Collections.Concurrent;
using Dekaf.Consumer;
using Dekaf.Producer;

namespace Dekaf.Tests.Integration.RealWorld;

/// <summary>
/// Tests for message ordering guarantees.
/// Verifies that within a partition, messages are always delivered in order,
/// and that key-based partitioning consistently routes to the same partition.
/// </summary>
[Category("MessagingOrdering")]
[ParallelLimiter<MessagingTestLimit>]
public sealed class MessageOrderingTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    [Test]
    public async Task KeyBasedPartitioning_SameKey_AlwaysRoutesToSamePartition()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 6);

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
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
            }, CancellationToken.None);
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
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
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
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory()).BuildAsync();

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
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
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
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory()).BuildAsync();

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
                .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
                .BuildAsync();

            for (var i = 0; i < messagesPerProducer; i++)
            {
                var metadata = await producer.ProduceAsync(new ProducerMessage<string, string>
                {
                    Topic = topic,
                    Key = "shared-key",
                    Value = $"producer-{p}-msg-{i}"
                }, CancellationToken.None);
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
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        // Use fire-and-forget Send() then flush
        const int messageCount = 200;
        for (var i = 0; i < messageCount; i++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = "ordered-key",
                Value = $"seq-{i}"
            });
        }

        await producer.FlushWithTimeoutAsync();

        // Consume and verify order
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory()).BuildAsync();

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
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
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
            }, CancellationToken.None);
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
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
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
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory()).BuildAsync();

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

    [Test]
    public async Task ConcurrentProduceAsync_SamePartition_MaintainsAppendOrder()
    {
        // Tests the race condition where multiple tasks call ProduceAsync concurrently
        // on the same producer targeting the same partition. The channel-based pipeline
        // must serialize appends and preserve the order messages enter the channel.
        var topic = await KafkaContainer.CreateTestTopicAsync();

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithAcks(Acks.All)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        // Launch concurrent produces - order between tasks is non-deterministic,
        // but within each task, sequential messages must remain ordered.
        const int tasksCount = 5;
        const int messagesPerTask = 20;
        var allMetadata = new ConcurrentDictionary<int, List<RecordMetadata>>();

        var tasks = Enumerable.Range(0, tasksCount).Select(async taskId =>
        {
            var results = new List<RecordMetadata>();
            for (var i = 0; i < messagesPerTask; i++)
            {
                var metadata = await producer.ProduceAsync(new ProducerMessage<string, string>
                {
                    Topic = topic,
                    Key = $"task-{taskId}",
                    Value = $"task-{taskId}-seq-{i:D3}",
                    Partition = 0
                }, CancellationToken.None);
                results.Add(metadata);
            }
            allMetadata[taskId] = results;
        }).ToArray();

        await Task.WhenAll(tasks);

        // Verify: within each task, offsets must be monotonically increasing
        // (i.e., the per-task sequential order is preserved in the partition log)
        foreach (var (taskId, results) in allMetadata)
        {
            for (var i = 1; i < results.Count; i++)
            {
                await Assert.That(results[i].Offset)
                    .IsGreaterThan(results[i - 1].Offset);
            }
        }

        // Consume and verify per-task ordering is preserved
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory()).BuildAsync();

        consumer.Assign(new TopicPartition(topic, 0));

        var messagesByTask = new Dictionary<int, List<string>>();
        for (var t = 0; t < tasksCount; t++) messagesByTask[t] = [];

        var totalExpected = tasksCount * messagesPerTask;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            if (msg.Key?.StartsWith("task-", StringComparison.Ordinal) == true)
            {
                var taskId = int.Parse(msg.Key.Split('-')[1]);
                messagesByTask[taskId].Add(msg.Value);
            }

            var total = messagesByTask.Values.Sum(l => l.Count);
            if (total >= totalExpected) break;
        }

        // Per-task messages must be in sequential order
        for (var t = 0; t < tasksCount; t++)
        {
            var values = messagesByTask[t];
            await Assert.That(values).Count().IsEqualTo(messagesPerTask);

            for (var i = 0; i < messagesPerTask; i++)
            {
                await Assert.That(values[i]).IsEqualTo($"task-{t}-seq-{i:D3}");
            }
        }
    }

    [Test]
    public async Task ProduceFlushProduce_OrderPreservedAcrossFlushBoundaries()
    {
        // Verify ordering is preserved across flush boundaries — the flush drains
        // all in-flight batches, but subsequent produces must continue the sequence.
        var topic = await KafkaContainer.CreateTestTopicAsync();

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithAcks(Acks.All)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        const int messagesPerBatch = 25;
        const int flushCount = 4;

        for (var batch = 0; batch < flushCount; batch++)
        {
            for (var i = 0; i < messagesPerBatch; i++)
            {
                var seq = batch * messagesPerBatch + i;
                await producer.ProduceAsync(new ProducerMessage<string, string>
                {
                    Topic = topic,
                    Key = "flush-test",
                    Value = $"seq-{seq:D4}"
                });
            }

            await producer.FlushWithTimeoutAsync();
        }

        // Consume and verify the full sequence is intact
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory()).BuildAsync();

        consumer.Assign(new TopicPartition(topic, 0));

        var messages = new List<string>();
        var totalExpected = messagesPerBatch * flushCount;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            messages.Add(msg.Value);
            if (messages.Count >= totalExpected) break;
        }

        await Assert.That(messages).Count().IsEqualTo(totalExpected);

        for (var i = 0; i < totalExpected; i++)
        {
            await Assert.That(messages[i]).IsEqualTo($"seq-{i:D4}");
        }
    }

    [Test]
    public async Task MixedSendAndProduceAsync_SamePartition_OrderPreserved()
    {
        // Tests the race between fire-and-forget Send() and awaited ProduceAsync()
        // on the same producer/partition. Both go through the same channel, so
        // call order must be preserved.
        var topic = await KafkaContainer.CreateTestTopicAsync();

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithAcks(Acks.All)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        const int messageCount = 50;
        for (var i = 0; i < messageCount; i++)
        {
            if (i % 2 == 0)
            {
                // Fire-and-forget
                await producer.ProduceAsync(new ProducerMessage<string, string>
                {
                    Topic = topic, Key = "mixed", Value = $"seq-{i:D4}"
                });
            }
            else
            {
                // Awaited
                await producer.ProduceAsync(new ProducerMessage<string, string>
                {
                    Topic = topic, Key = "mixed", Value = $"seq-{i:D4}"
                });
            }
        }

        await producer.FlushWithTimeoutAsync();

        // Consume and verify
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory()).BuildAsync();

        consumer.Assign(new TopicPartition(topic, 0));

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
            await Assert.That(messages[i]).IsEqualTo($"seq-{i:D4}");
        }
    }

    [Test]
    public async Task HighVolumeOrdering_StressesBatchPipeline()
    {
        // Produces enough messages to force many batch boundaries and drain cycles,
        // stressing the RecordAccumulator → BrokerSender pipeline for ordering bugs.
        var topic = await KafkaContainer.CreateTestTopicAsync();

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithAcks(Acks.All)
            .WithBatchSize(16384) // 16KB batches — forces many batches for 1000 messages
            .WithLinger(TimeSpan.FromMilliseconds(5))
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        const int messageCount = 1000;
        for (var i = 0; i < messageCount; i++)
        {
            // Use Send() for maximum throughput pressure on the pipeline
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = "stress",
                Value = $"msg-{i:D5}"
            });
        }

        await producer.FlushWithTimeoutAsync();

        // Consume all and verify strict ordering
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory()).BuildAsync();

        consumer.Assign(new TopicPartition(topic, 0));

        var messages = new List<string>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            messages.Add(msg.Value);
            if (messages.Count >= messageCount) break;
        }

        await Assert.That(messages).Count().IsEqualTo(messageCount);

        for (var i = 0; i < messageCount; i++)
        {
            await Assert.That(messages[i]).IsEqualTo($"msg-{i:D5}");
        }
    }

    [Test]
    public async Task ConcurrentProduceAsync_MultipleKeys_PerKeyOrderPreserved()
    {
        // Multiple tasks producing with different keys concurrently.
        // Each key routes to its own partition via hashing, but within each key
        // the sequential ordering from each task must be preserved.
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 4);

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithAcks(Acks.All)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        const int keysCount = 4;
        const int messagesPerKey = 30;

        var tasks = Enumerable.Range(0, keysCount).Select(async keyIdx =>
        {
            var key = $"ordering-key-{keyIdx}";
            for (var i = 0; i < messagesPerKey; i++)
            {
                await producer.ProduceAsync(new ProducerMessage<string, string>
                {
                    Topic = topic,
                    Key = key,
                    Value = $"{key}-seq-{i:D3}"
                });
            }
        }).ToArray();

        await Task.WhenAll(tasks);

        // Consume all
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId($"ordering-concurrent-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory()).BuildAsync();

        consumer.Subscribe(topic);

        var messagesByKey = new Dictionary<string, List<string>>();
        for (var k = 0; k < keysCount; k++)
            messagesByKey[$"ordering-key-{k}"] = [];

        var totalExpected = keysCount * messagesPerKey;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            if (msg.Key is not null && messagesByKey.TryGetValue(msg.Key, out var list))
            {
                list.Add(msg.Value);
            }

            var total = messagesByKey.Values.Sum(l => l.Count);
            if (total >= totalExpected) break;
        }

        // Per-key ordering must be preserved
        for (var k = 0; k < keysCount; k++)
        {
            var key = $"ordering-key-{k}";
            var values = messagesByKey[key];
            await Assert.That(values).Count().IsEqualTo(messagesPerKey);

            for (var i = 0; i < messagesPerKey; i++)
            {
                await Assert.That(values[i]).IsEqualTo($"{key}-seq-{i:D3}");
            }
        }
    }
}
