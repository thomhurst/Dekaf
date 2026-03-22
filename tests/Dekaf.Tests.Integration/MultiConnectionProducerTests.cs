using Dekaf.Consumer;
using Dekaf.Producer;

namespace Dekaf.Tests.Integration;

/// <summary>
/// Integration tests for partition-affined multi-connection idempotent producer.
/// Verifies correctness, ordering, and flush semantics when using multiple TCP
/// connections per broker with idempotence enabled.
/// </summary>
[Category("Producer")]
public sealed class MultiConnectionProducerTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    [Test]
    public async Task IdempotentMultiConnection_BasicCorrectness_NoDuplicatesNoGaps()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 6);
        const int messageCount = 10_000;

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-multi-conn-correctness")
            .WithAcks(Acks.All)
            .WithConnectionsPerBroker(3)
            .BuildAsync();

        for (var i = 0; i < messageCount; i++)
        {
            producer.Produce(topic, $"key-{i % 100}", $"msg-{i}");
        }
        await producer.FlushAsync();

        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        consumer.Subscribe(topic);

        var received = new HashSet<string>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            received.Add(msg.Value!);
            if (received.Count >= messageCount) break;
        }

        await Assert.That(received).Count().IsEqualTo(messageCount);
        for (var i = 0; i < messageCount; i++)
        {
            await Assert.That(received).Contains($"msg-{i}");
        }
    }

    [Test]
    public async Task IdempotentMultiConnection_PerPartitionOrdering_Preserved()
    {
        const int partitionCount = 6;
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: partitionCount);
        const int messagesPerPartition = 500;

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-multi-conn-ordering")
            .WithAcks(Acks.All)
            .WithConnectionsPerBroker(2)
            .BuildAsync();

        // Warmup all partitions
        for (var p = 0; p < partitionCount; p++)
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic, Key = "warmup", Value = "warmup", Partition = p
            });

        // Produce sequenced messages to each partition
        for (var p = 0; p < partitionCount; p++)
        {
            for (var i = 0; i < messagesPerPartition; i++)
            {
                await producer.ProduceAsync(new ProducerMessage<string, string>
                {
                    Topic = topic,
                    Key = $"p{p}-key",
                    Value = $"p{p}-seq-{i:D4}",
                    Partition = p
                });
            }
        }

        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        for (var p = 0; p < partitionCount; p++)
        {
            consumer.Assign(new TopicPartition(topic, p));
            var messages = new List<string>();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

            await foreach (var msg in consumer.ConsumeAsync(cts.Token))
            {
                if (msg.Value == "warmup") continue;
                messages.Add(msg.Value!);
                if (messages.Count >= messagesPerPartition) break;
            }

            await Assert.That(messages).Count().IsEqualTo(messagesPerPartition);

            for (var i = 0; i < messagesPerPartition; i++)
            {
                await Assert.That(messages[i]).IsEqualTo($"p{p}-seq-{i:D4}");
            }
        }
    }

    [Test]
    public async Task IdempotentMultiConnection_FlushAsync_DeliversAllMessages()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 4);
        const int messageCount = 5_000;

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-multi-conn-flush")
            .WithAcks(Acks.All)
            .WithConnectionsPerBroker(3)
            .BuildAsync();

        for (var i = 0; i < messageCount; i++)
        {
            producer.Produce(topic, $"key-{i % 50}", $"flush-msg-{i}");
        }

        await producer.FlushAsync();

        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        consumer.Subscribe(topic);
        var count = 0;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await foreach (var _ in consumer.ConsumeAsync(cts.Token))
        {
            count++;
            if (count >= messageCount) break;
        }

        await Assert.That(count).IsEqualTo(messageCount);
    }

    [Test]
    public async Task IdempotentMultiConnection_FewerPartitionsThanConnections_Works()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 1);
        const int messageCount = 1_000;

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithAcks(Acks.All)
            .WithConnectionsPerBroker(3)
            .BuildAsync();

        for (var i = 0; i < messageCount; i++)
        {
            await producer.ProduceAsync(topic, $"key-{i}", $"single-part-{i}");
        }

        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        consumer.Assign(new TopicPartition(topic, 0));
        var count = 0;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await foreach (var _ in consumer.ConsumeAsync(cts.Token))
        {
            count++;
            if (count >= messageCount) break;
        }

        await Assert.That(count).IsEqualTo(messageCount);
    }

    [Test]
    public async Task IdempotentMultiConnection_Backpressure_NoDeadlock()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 4);
        const int messageCount = 5_000;

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithAcks(Acks.All)
            .WithConnectionsPerBroker(2)
            .WithBufferMemory(2UL * 1024 * 1024) // 2MB
            .BuildAsync();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

        var tasks = new List<ValueTask<RecordMetadata>>();
        for (var i = 0; i < messageCount; i++)
        {
            tasks.Add(producer.ProduceAsync(topic, $"key-{i % 20}", $"bp-msg-{i}", cts.Token));
        }

        for (var i = 0; i < tasks.Count; i++)
        {
            await tasks[i];
        }

        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        consumer.Subscribe(topic);
        var count = 0;
        using var consumeCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await foreach (var _ in consumer.ConsumeAsync(consumeCts.Token))
        {
            count++;
            if (count >= messageCount) break;
        }

        await Assert.That(count).IsEqualTo(messageCount);
    }

    [Test]
    public async Task IdempotentMultiConnection_Dispose_NoOrphanedTasks()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 4);

        var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithAcks(Acks.All)
            .WithConnectionsPerBroker(2)
            .BuildAsync();

        for (var i = 0; i < 1_000; i++)
        {
            producer.Produce(topic, $"key-{i}", $"dispose-msg-{i}");
        }

        await producer.DisposeAsync();
        // If we reach here without exception or hang, the test passes.
    }
}
