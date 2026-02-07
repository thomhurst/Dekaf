using Dekaf.Consumer;
using Dekaf.Producer;

namespace Dekaf.Tests.Integration.RealWorld;

/// <summary>
/// Tests for auto-commit behavior edge cases.
/// Verifies that auto-commit works correctly with various intervals,
/// that it fires on close/dispose, and that committed offsets are respected on restart.
/// </summary>
public sealed class AutoCommitBehaviorTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    [Test]
    public async Task AutoCommit_ShortInterval_CommitsBeforeClose()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var groupId = $"auto-commit-{Guid.NewGuid():N}";

        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .Build();

        for (var i = 0; i < 10; i++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{i}",
                Value = $"value-{i}"
            });
        }

        // First consumer with short auto-commit interval
        await using (var consumer1 = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(groupId)
            .WithSessionTimeout(TimeSpan.FromMilliseconds(10000))
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithOffsetCommitMode(OffsetCommitMode.Auto)
            .WithAutoCommitInterval(TimeSpan.FromMilliseconds(100))
            .Build())
        {
            consumer1.Subscribe(topic);

            var count = 0;
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await foreach (var msg in consumer1.ConsumeAsync(cts.Token))
            {
                count++;
                if (count >= 5) break;
            }

            // Wait for auto-commit to fire
            await Task.Delay(500);

            await consumer1.CloseAsync();
        }

        // Second consumer should resume from committed offset
        await using var consumer2 = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(groupId)
            .WithSessionTimeout(TimeSpan.FromMilliseconds(10000))
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithOffsetCommitMode(OffsetCommitMode.Manual)
            .Build();

        consumer2.Subscribe(topic);

        using var cts2 = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var firstMsg = await consumer2.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts2.Token);

        await Assert.That(firstMsg).IsNotNull();
        // Auto-commit should have committed at least offset 5
        await Assert.That(firstMsg!.Value.Offset).IsGreaterThanOrEqualTo(5);
    }

    [Test]
    public async Task AutoCommit_OnClose_CommitsFinalOffsets()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var groupId = $"auto-close-{Guid.NewGuid():N}";

        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .Build();

        for (var i = 0; i < 10; i++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{i}",
                Value = $"value-{i}"
            });
        }

        // Consumer with long auto-commit interval - commits should happen on close
        await using (var consumer1 = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(groupId)
            .WithSessionTimeout(TimeSpan.FromMilliseconds(10000))
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithOffsetCommitMode(OffsetCommitMode.Auto)
            .WithAutoCommitInterval(TimeSpan.FromMinutes(10)) // Very long - won't fire during test
            .Build())
        {
            consumer1.Subscribe(topic);

            var count = 0;
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await foreach (var msg in consumer1.ConsumeAsync(cts.Token))
            {
                count++;
                if (count >= 10) break;
            }

            // CloseAsync should trigger final commit even though interval hasn't elapsed
            await consumer1.CloseAsync();
        }

        // Verify committed offset
        await using var consumer2 = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(groupId)
            .WithSessionTimeout(TimeSpan.FromMilliseconds(10000))
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithOffsetCommitMode(OffsetCommitMode.Manual)
            .Build();

        consumer2.Subscribe(topic);

        // Should get nothing since all 10 messages were committed
        using var cts2 = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var result = await consumer2.ConsumeOneAsync(TimeSpan.FromSeconds(5), cts2.Token);

        if (result.HasValue)
        {
            await Assert.That(result.Value.Offset).IsGreaterThanOrEqualTo(10);
        }
    }

    [Test]
    public async Task ManualCommit_WithAutoCommitDisabled_NoAutoCommitOccurs()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var groupId = $"manual-only-{Guid.NewGuid():N}";

        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .Build();

        for (var i = 0; i < 5; i++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{i}",
                Value = $"value-{i}"
            });
        }

        // First consumer: consume all but don't commit
        await using (var consumer1 = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(groupId)
            .WithSessionTimeout(TimeSpan.FromMilliseconds(10000))
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithOffsetCommitMode(OffsetCommitMode.Manual)
            .Build())
        {
            consumer1.Subscribe(topic);

            var count = 0;
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await foreach (var msg in consumer1.ConsumeAsync(cts.Token))
            {
                count++;
                if (count >= 5) break;
            }

            // Don't commit - just close
            await consumer1.CloseAsync();
        }

        // Second consumer should get all messages (nothing was committed)
        await using var consumer2 = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(groupId)
            .WithSessionTimeout(TimeSpan.FromMilliseconds(10000))
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithOffsetCommitMode(OffsetCommitMode.Manual)
            .Build();

        consumer2.Subscribe(topic);

        var messages = new List<ConsumeResult<string, string>>();
        using var cts2 = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await foreach (var msg in consumer2.ConsumeAsync(cts2.Token))
        {
            messages.Add(msg);
            if (messages.Count >= 5) break;
        }

        await Assert.That(messages).Count().IsEqualTo(5);
        await Assert.That(messages[0].Offset).IsEqualTo(0);
    }

    [Test]
    public async Task AutoCommit_PartialConsumption_CommitsOnlyConsumedOffsets()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var groupId = $"partial-auto-{Guid.NewGuid():N}";

        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .Build();

        for (var i = 0; i < 20; i++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{i}",
                Value = $"value-{i}"
            });
        }

        // Consume only 8 messages with auto-commit
        await using (var consumer1 = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(groupId)
            .WithSessionTimeout(TimeSpan.FromMilliseconds(10000))
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithOffsetCommitMode(OffsetCommitMode.Auto)
            .WithAutoCommitInterval(TimeSpan.FromMilliseconds(100))
            .Build())
        {
            consumer1.Subscribe(topic);

            var count = 0;
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await foreach (var msg in consumer1.ConsumeAsync(cts.Token))
            {
                count++;
                if (count >= 8) break;
            }

            await consumer1.CloseAsync();
        }

        // Second consumer should resume from around offset 8
        await using var consumer2 = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(groupId)
            .WithSessionTimeout(TimeSpan.FromMilliseconds(10000))
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithOffsetCommitMode(OffsetCommitMode.Manual)
            .Build();

        consumer2.Subscribe(topic);

        var messages = new List<ConsumeResult<string, string>>();
        using var cts2 = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await foreach (var msg in consumer2.ConsumeAsync(cts2.Token))
        {
            messages.Add(msg);
            if (messages.Count >= 12) break;
        }

        // Should get remaining messages (approximately 12)
        await Assert.That(messages).Count().IsEqualTo(12);
        await Assert.That(messages[0].Offset).IsGreaterThanOrEqualTo(8);
    }

    [Test]
    public async Task AutoCommit_MultiPartition_CommitsAllPartitions()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 3);
        var groupId = $"auto-multi-{Guid.NewGuid():N}";

        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .Build();

        // Produce to all 3 partitions
        for (var p = 0; p < 3; p++)
        {
            for (var i = 0; i < 5; i++)
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

        // Consume all 15 messages with auto-commit
        await using (var consumer1 = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(groupId)
            .WithSessionTimeout(TimeSpan.FromMilliseconds(10000))
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithOffsetCommitMode(OffsetCommitMode.Auto)
            .WithAutoCommitInterval(TimeSpan.FromMilliseconds(100))
            .Build())
        {
            consumer1.Subscribe(topic);

            var count = 0;
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await foreach (var msg in consumer1.ConsumeAsync(cts.Token))
            {
                count++;
                if (count >= 15) break;
            }

            await consumer1.CloseAsync();
        }

        // Second consumer should have no messages to read
        await using var consumer2 = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(groupId)
            .WithSessionTimeout(TimeSpan.FromMilliseconds(10000))
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithOffsetCommitMode(OffsetCommitMode.Manual)
            .Build();

        consumer2.Subscribe(topic);

        using var cts2 = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var result = await consumer2.ConsumeOneAsync(TimeSpan.FromSeconds(5), cts2.Token);

        // All partitions committed - no messages to read
        if (result.HasValue)
        {
            // If we get a message, it should be beyond what was consumed
            await Assert.That(result.Value.Offset).IsGreaterThanOrEqualTo(5);
        }
    }
}
