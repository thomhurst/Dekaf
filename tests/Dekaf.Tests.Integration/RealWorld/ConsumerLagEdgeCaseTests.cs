using Dekaf.Consumer;
using Dekaf.Producer;

namespace Dekaf.Tests.Integration.RealWorld;

/// <summary>
/// Edge case tests for consumer lag tracking.
/// Tests lag during pause/resume, concurrent production,
/// multi-partition scenarios, and committed vs position-based lag.
/// </summary>
[Category("ConsumerLag")]
public sealed class ConsumerLagEdgeCaseTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    [Test]
    public async Task Lag_DuringPause_LagGrowsWhilePaused()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .BuildAsync();

        // Produce initial messages
        for (var i = 0; i < 5; i++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{i}",
                Value = $"value-{i}"
            });
        }

        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        var tp = new TopicPartition(topic, 0);
        consumer.Assign(tp);

        // Consume 3 messages
        var count = 0;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            count++;
            if (count >= 3) break;
        }

        // Position should be 3
        var positionBefore = consumer.GetPosition(tp);
        await Assert.That(positionBefore).IsEqualTo(3);

        // Pause the partition
        consumer.Pause(tp);

        // Produce more messages while paused
        for (var i = 5; i < 15; i++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{i}",
                Value = $"value-{i}"
            });
        }

        // Query watermarks - should show lag growing
        var watermarks = await consumer.QueryWatermarkOffsetsAsync(tp);
        await Assert.That(watermarks.High).IsEqualTo(15);

        var lag = watermarks.High - positionBefore!.Value;
        await Assert.That(lag).IsEqualTo(12); // 15 total - 3 consumed = 12 lag

        // Resume and consume rest
        consumer.Resume(tp);

        var remaining = new List<ConsumeResult<string, string>>();
        using var cts2 = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await foreach (var msg in consumer.ConsumeAsync(cts2.Token))
        {
            remaining.Add(msg);
            if (remaining.Count >= 12) break;
        }

        await Assert.That(remaining).Count().IsEqualTo(12);

        // Lag should now be zero
        var finalPosition = consumer.GetPosition(tp);
        var finalWatermarks = await consumer.QueryWatermarkOffsetsAsync(tp);
        var finalLag = finalWatermarks.High - finalPosition!.Value;
        await Assert.That(finalLag).IsEqualTo(0);
    }

    [Test]
    public async Task Lag_ConcurrentProduction_WatermarkUpdates()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .BuildAsync();

        // Produce initial batch
        for (var i = 0; i < 10; i++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{i}",
                Value = $"value-{i}"
            });
        }

        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        var tp = new TopicPartition(topic, 0);
        consumer.Assign(tp);

        // Consume 5 messages
        var consumed = 0;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            consumed++;
            if (consumed >= 5) break;
        }

        // Produce 10 more while consumer is running
        for (var i = 10; i < 20; i++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{i}",
                Value = $"value-{i}"
            });
        }

        // Check lag reflects new messages
        var watermarks = await consumer.QueryWatermarkOffsetsAsync(tp);
        var position = consumer.GetPosition(tp);

        await Assert.That(watermarks.High).IsEqualTo(20);
        await Assert.That(position).IsEqualTo(5);

        var lag = watermarks.High - position!.Value;
        await Assert.That(lag).IsEqualTo(15);
    }

    [Test]
    public async Task Lag_CommittedVsPosition_DifferentValues()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var groupId = $"lag-commit-{Guid.NewGuid():N}";

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .BuildAsync();

        for (var i = 0; i < 20; i++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{i}",
                Value = $"value-{i}"
            });
        }

        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithOffsetCommitMode(OffsetCommitMode.Manual)
            .BuildAsync();

        consumer.Subscribe(topic);

        // Consume 15 messages but only commit 10
        var count = 0;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            count++;
            if (count == 10)
            {
                await consumer.CommitAsync();
            }
            if (count >= 15) break;
        }

        var tp = new TopicPartition(topic, 0);
        var position = consumer.GetPosition(tp);
        var committed = await consumer.GetCommittedOffsetAsync(tp);
        var watermarks = await consumer.QueryWatermarkOffsetsAsync(tp);

        // Position reflects actual consumption (15)
        await Assert.That(position).IsEqualTo(15);

        // Committed reflects last commit (10)
        await Assert.That(committed).IsEqualTo(10);

        // Position-based lag: 20 - 15 = 5
        var positionLag = watermarks.High - position!.Value;
        await Assert.That(positionLag).IsEqualTo(5);

        // Committed-based lag: 20 - 10 = 10
        var committedLag = watermarks.High - committed!.Value;
        await Assert.That(committedLag).IsEqualTo(10);
    }

    [Test]
    public async Task Lag_EmptyPartition_LagIsZero()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();

        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        var tp = new TopicPartition(topic, 0);
        consumer.Assign(tp);

        var watermarks = await consumer.QueryWatermarkOffsetsAsync(tp);
        await Assert.That(watermarks.Low).IsEqualTo(0);
        await Assert.That(watermarks.High).IsEqualTo(0);

        // Lag on empty partition = 0
        var lag = watermarks.High - watermarks.Low;
        await Assert.That(lag).IsEqualTo(0);
    }

    [Test]
    public async Task Lag_MultiPartitionUnevenLoad_PerPartitionLagTracking()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 3);

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .BuildAsync();

        // Produce uneven amounts: p0=20, p1=5, p2=10
        for (var i = 0; i < 20; i++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic, Key = $"p0-{i}", Value = $"value", Partition = 0
            });
        }
        for (var i = 0; i < 5; i++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic, Key = $"p1-{i}", Value = $"value", Partition = 1
            });
        }
        for (var i = 0; i < 10; i++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic, Key = $"p2-{i}", Value = $"value", Partition = 2
            });
        }

        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        // Assign and consume partially from each
        consumer.Assign(
            new TopicPartition(topic, 0),
            new TopicPartition(topic, 1),
            new TopicPartition(topic, 2));

        // Consume 10 messages from p0 (leaving 10 lag)
        var p0Count = 0;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            if (msg.Partition == 0) p0Count++;
            // Consume until we have 10 from p0 plus whatever came from p1/p2
            if (p0Count >= 10) break;
        }

        // Check per-partition watermarks
        var wm0 = await consumer.QueryWatermarkOffsetsAsync(new TopicPartition(topic, 0));
        var wm1 = await consumer.QueryWatermarkOffsetsAsync(new TopicPartition(topic, 1));
        var wm2 = await consumer.QueryWatermarkOffsetsAsync(new TopicPartition(topic, 2));

        await Assert.That(wm0.High).IsEqualTo(20);
        await Assert.That(wm1.High).IsEqualTo(5);
        await Assert.That(wm2.High).IsEqualTo(10);
    }
}
