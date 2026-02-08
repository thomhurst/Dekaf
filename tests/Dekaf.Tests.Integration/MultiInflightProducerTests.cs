using Dekaf.Consumer;
using Dekaf.Producer;

namespace Dekaf.Tests.Integration;

/// <summary>
/// Integration tests for multiple in-flight batches per partition.
/// Verifies that idempotent producers with multiple in-flight batches preserve
/// per-partition ordering, deliver all messages, and that non-idempotent
/// producers retain single in-flight behavior.
/// </summary>
public sealed class MultiInflightProducerTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    [Test]
    public async Task MultiInflight_SinglePartition_OrderingPreserved()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();
        const int messageCount = 1000;

        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-multi-inflight-single-partition")
            .WithAcks(Acks.All)
            .EnableIdempotence()
            .WithBatchSize(512) // Small batch size to force many batches
            .WithLinger(TimeSpan.FromMilliseconds(1))
            .Build();

        for (var i = 0; i < messageCount; i++)
        {
            producer.Send(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = "ordering-key",
                Value = $"msg-{i:D4}"
            });
        }

        await producer.FlushAsync();

        await using var consumer = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .Build();

        consumer.Assign(new TopicPartition(topic, 0));

        var messages = new List<ConsumeResult<string, string>>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            messages.Add(msg);
            if (messages.Count >= messageCount) break;
        }

        await Assert.That(messages).Count().IsEqualTo(messageCount);

        for (var i = 0; i < messageCount; i++)
        {
            await Assert.That(messages[i].Value).IsEqualTo($"msg-{i:D4}");
        }

        // Verify monotonically increasing offsets
        for (var i = 1; i < messages.Count; i++)
        {
            await Assert.That(messages[i].Offset).IsGreaterThan(messages[i - 1].Offset);
        }
    }

    [Test]
    public async Task MultiInflight_MultiPartition_PerPartitionOrderingPreserved()
    {
        const int partitionCount = 8;
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: partitionCount);
        const int messagesPerPartition = 500;

        await using var producer = Kafka.CreateProducer<int, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-multi-inflight-multi-partition")
            .WithAcks(Acks.All)
            .EnableIdempotence()
            .WithLinger(TimeSpan.FromMilliseconds(2))
            .Build();

        // Produce messages keyed by partition index
        for (var p = 0; p < partitionCount; p++)
        {
            for (var i = 0; i < messagesPerPartition; i++)
            {
                producer.Send(new ProducerMessage<int, string>
                {
                    Topic = topic,
                    Partition = p,
                    Key = p,
                    Value = $"p{p}-msg-{i:D4}"
                });
            }
        }

        await producer.FlushAsync();

        // Consume each partition and verify ordering
        await using var consumer = Kafka.CreateConsumer<int, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .Build();

        var totalExpected = partitionCount * messagesPerPartition;
        var partitions = Enumerable.Range(0, partitionCount)
            .Select(p => new TopicPartition(topic, p))
            .ToList();
        consumer.Assign(partitions.ToArray());

        var byPartition = new Dictionary<int, List<ConsumeResult<int, string>>>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var totalConsumed = 0;

        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            if (!byPartition.TryGetValue(msg.Partition, out var list))
            {
                list = [];
                byPartition[msg.Partition] = list;
            }

            list.Add(msg);
            totalConsumed++;
            if (totalConsumed >= totalExpected) break;
        }

        await Assert.That(totalConsumed).IsEqualTo(totalExpected);

        // Verify per-partition ordering
        for (var p = 0; p < partitionCount; p++)
        {
            var partitionMessages = byPartition[p];
            await Assert.That(partitionMessages).Count().IsEqualTo(messagesPerPartition);

            for (var i = 0; i < messagesPerPartition; i++)
            {
                await Assert.That(partitionMessages[i].Value).IsEqualTo($"p{p}-msg-{i:D4}");
            }
        }
    }

    [Test]
    public async Task MultiInflight_ConcurrentProduction_AllMessagesDelivered()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();
        const int threadCount = 5;
        const int messagesPerThread = 200;

        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-multi-inflight-concurrent")
            .WithAcks(Acks.All)
            .EnableIdempotence()
            .WithLinger(TimeSpan.FromMilliseconds(2))
            .Build();

        // Multiple threads producing concurrently to the same partition
        var tasks = Enumerable.Range(0, threadCount).Select(async threadId =>
        {
            for (var i = 0; i < messagesPerThread; i++)
            {
                await producer.ProduceAsync(new ProducerMessage<string, string>
                {
                    Topic = topic,
                    Key = "same-key",
                    Value = $"t{threadId}-msg-{i:D3}"
                });
            }
        }).ToArray();

        await Task.WhenAll(tasks);

        // Consume and verify all messages delivered
        await using var consumer = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .Build();

        consumer.Assign(new TopicPartition(topic, 0));

        var messages = new List<ConsumeResult<string, string>>();
        var totalExpected = threadCount * messagesPerThread;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            messages.Add(msg);
            if (messages.Count >= totalExpected) break;
        }

        await Assert.That(messages).Count().IsEqualTo(totalExpected);

        // Verify monotonically increasing offsets (per-partition ordering)
        for (var i = 1; i < messages.Count; i++)
        {
            await Assert.That(messages[i].Offset).IsGreaterThan(messages[i - 1].Offset);
        }
    }

    [Test]
    public async Task MultiInflight_HighVolume_SmallBatches_NoDataLoss()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();
        const int messageCount = 2000;

        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-multi-inflight-high-volume")
            .WithAcks(Acks.All)
            .EnableIdempotence()
            .WithBatchSize(256) // Very small to force many batch rotations
            .WithLinger(TimeSpan.FromMilliseconds(1))
            .Build();

        for (var i = 0; i < messageCount; i++)
        {
            producer.Send(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = "volume-key",
                Value = $"vol-{i:D4}"
            });
        }

        await producer.FlushAsync();

        await using var consumer = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .Build();

        consumer.Assign(new TopicPartition(topic, 0));

        var messages = new List<ConsumeResult<string, string>>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            messages.Add(msg);
            if (messages.Count >= messageCount) break;
        }

        await Assert.That(messages).Count().IsEqualTo(messageCount);

        // Verify strict ordering
        for (var i = 0; i < messageCount; i++)
        {
            await Assert.That(messages[i].Value).IsEqualTo($"vol-{i:D4}");
        }
    }

    [Test]
    public async Task MultiInflight_FireAndForget_OrderingPreserved()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();
        const int messageCount = 1000;

        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-multi-inflight-fire-and-forget")
            .WithAcks(Acks.All)
            .EnableIdempotence()
            .WithLinger(TimeSpan.FromMilliseconds(2))
            .Build();

        for (var i = 0; i < messageCount; i++)
        {
            producer.Send(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = "faf-key",
                Value = $"faf-{i:D4}"
            });
        }

        await producer.FlushAsync();

        await using var consumer = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .Build();

        consumer.Assign(new TopicPartition(topic, 0));

        var messages = new List<ConsumeResult<string, string>>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            messages.Add(msg);
            if (messages.Count >= messageCount) break;
        }

        await Assert.That(messages).Count().IsEqualTo(messageCount);

        for (var i = 0; i < messageCount; i++)
        {
            await Assert.That(messages[i].Value).IsEqualTo($"faf-{i:D4}");
        }
    }

    [Test]
    public async Task NonIdempotent_SingleInflight_BehaviorUnchanged()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();
        const int messageCount = 100;

        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-non-idempotent-single-inflight")
            .WithAcks(Acks.All)
            .Build();

        for (var i = 0; i < messageCount; i++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = "non-idemp-key",
                Value = $"ni-{i:D3}"
            });
        }

        await using var consumer = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .Build();

        consumer.Assign(new TopicPartition(topic, 0));

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
            await Assert.That(messages[i].Value).IsEqualTo($"ni-{i:D3}");
        }
    }

    [Test]
    public async Task MultiInflight_WithCoalescing_OrderingPreserved()
    {
        const int partitionCount = 8;
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: partitionCount);
        const int messagesPerPartition = 500;

        await using var producer = Kafka.CreateProducer<int, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-multi-inflight-coalescing")
            .WithAcks(Acks.All)
            .EnableIdempotence()
            .WithLinger(TimeSpan.FromMilliseconds(5)) // Higher linger to trigger coalescing
            .Build();

        // Produce to all partitions to trigger coalescing
        for (var i = 0; i < messagesPerPartition; i++)
        {
            for (var p = 0; p < partitionCount; p++)
            {
                producer.Send(new ProducerMessage<int, string>
                {
                    Topic = topic,
                    Partition = p,
                    Key = p,
                    Value = $"coal-p{p}-{i:D4}"
                });
            }
        }

        await producer.FlushAsync();

        await using var consumer = Kafka.CreateConsumer<int, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .Build();

        var partitions = Enumerable.Range(0, partitionCount)
            .Select(p => new TopicPartition(topic, p))
            .ToList();
        consumer.Assign(partitions.ToArray());

        var totalExpected = partitionCount * messagesPerPartition;
        var byPartition = new Dictionary<int, List<ConsumeResult<int, string>>>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var totalConsumed = 0;

        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            if (!byPartition.TryGetValue(msg.Partition, out var list))
            {
                list = [];
                byPartition[msg.Partition] = list;
            }

            list.Add(msg);
            totalConsumed++;
            if (totalConsumed >= totalExpected) break;
        }

        await Assert.That(totalConsumed).IsEqualTo(totalExpected);

        // Verify per-partition ordering
        for (var p = 0; p < partitionCount; p++)
        {
            var partitionMessages = byPartition[p];
            await Assert.That(partitionMessages).Count().IsEqualTo(messagesPerPartition);

            for (var i = 0; i < messagesPerPartition; i++)
            {
                await Assert.That(partitionMessages[i].Value).IsEqualTo($"coal-p{p}-{i:D4}");
            }
        }
    }
}
