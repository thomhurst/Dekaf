using Dekaf.Consumer;
using Dekaf.Producer;

namespace Dekaf.Tests.Integration;

/// <summary>
/// Integration tests for consumer lag tracking via watermark offsets and position.
/// </summary>
public sealed class ConsumerLagTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    [Test]
    public async Task ConsumerLag_PartiallyConsumed_ReportsCorrectLag()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();
        const int totalMessages = 20;
        const int consumeCount = 10;

        // Produce messages
        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .Build();

        for (var i = 0; i < totalMessages; i++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{i}",
                Value = $"value-{i}"
            });
        }

        // Consume only half
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
            if (messages.Count >= consumeCount) break;
        }

        // Check position (next offset to consume)
        var position = consumer.GetPosition(tp);
        await Assert.That(position).IsNotNull();
        await Assert.That(position!.Value).IsEqualTo(consumeCount);

        // Check high watermark
        var watermarks = await consumer.QueryWatermarkOffsetsAsync(tp);
        await Assert.That(watermarks.High).IsEqualTo(totalMessages);

        // Lag = high watermark - position
        var lag = watermarks.High - position.Value;
        await Assert.That(lag).IsEqualTo(totalMessages - consumeCount);
    }

    [Test]
    public async Task ConsumerLag_FullyConsumed_LagIsZero()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();
        const int totalMessages = 5;

        // Produce messages
        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .Build();

        for (var i = 0; i < totalMessages; i++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{i}",
                Value = $"value-{i}"
            });
        }

        // Consume all messages
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
            if (messages.Count >= totalMessages) break;
        }

        var position = consumer.GetPosition(tp);
        var watermarks = await consumer.QueryWatermarkOffsetsAsync(tp);

        var lag = watermarks.High - position!.Value;
        await Assert.That(lag).IsEqualTo(0);
    }

    [Test]
    public async Task ConsumerLag_MultiPartition_PerPartitionLag()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 3);

        // Produce different amounts to each partition
        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .Build();

        // 5 messages to partition 0
        for (var i = 0; i < 5; i++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"p0-key-{i}",
                Value = $"p0-value-{i}",
                Partition = 0
            });
        }

        // 3 messages to partition 1
        for (var i = 0; i < 3; i++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"p1-key-{i}",
                Value = $"p1-value-{i}",
                Partition = 1
            });
        }

        // 2 messages to partition 2
        for (var i = 0; i < 2; i++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"p2-key-{i}",
                Value = $"p2-value-{i}",
                Partition = 2
            });
        }

        // Consume only from partitions 0 and 1, partially
        await using var consumer = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .Build();

        var tp0 = new TopicPartition(topic, 0);
        var tp1 = new TopicPartition(topic, 1);
        var tp2 = new TopicPartition(topic, 2);

        // Query watermarks from cluster for all partitions
        var wm0 = await consumer.QueryWatermarkOffsetsAsync(tp0);
        var wm1 = await consumer.QueryWatermarkOffsetsAsync(tp1);
        var wm2 = await consumer.QueryWatermarkOffsetsAsync(tp2);

        await Assert.That(wm0.High).IsEqualTo(5);
        await Assert.That(wm1.High).IsEqualTo(3);
        await Assert.That(wm2.High).IsEqualTo(2);

        // Total lag without consuming = sum of all watermarks
        var totalLag = wm0.High + wm1.High + wm2.High;
        await Assert.That(totalLag).IsEqualTo(10);
    }
}
