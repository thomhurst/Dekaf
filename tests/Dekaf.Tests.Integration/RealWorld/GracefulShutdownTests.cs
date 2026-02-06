using Dekaf.Consumer;
using Dekaf.Producer;

namespace Dekaf.Tests.Integration.RealWorld;

/// <summary>
/// Tests for graceful shutdown and restart patterns.
/// Verifies that consumers can stop cleanly and resume from the correct position,
/// which is critical for at-least-once and exactly-once processing guarantees.
/// </summary>
public sealed class GracefulShutdownTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    [Test]
    public async Task GracefulShutdown_CloseAndRestart_ResumesFromCommittedOffset()
    {
        // Simulate: service processes some messages, shuts down, new instance resumes
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var groupId = $"restart-group-{Guid.NewGuid():N}";

        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .Build();

        // Produce 10 messages
        for (var i = 0; i < 10; i++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{i}",
                Value = $"value-{i}"
            });
        }

        // First instance: process 5 messages, commit, shut down
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
                if (count >= 5)
                {
                    await consumer1.CommitAsync();
                    break;
                }
            }

            // Graceful close leaves the group immediately
            await consumer1.CloseAsync();
        }

        // Second instance: should resume from offset 5
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
        await Assert.That(firstMsg!.Value.Offset).IsEqualTo(5);
        await Assert.That(firstMsg.Value.Value).IsEqualTo("value-5");
    }

    [Test]
    public async Task GracefulShutdown_AtLeastOnceProcessing_NoMessageLoss()
    {
        // Simulate at-least-once: commit only after successful processing
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var groupId = $"at-least-once-{Guid.NewGuid():N}";

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

        var processedMessages = new List<string>();

        // First consumer: process 3 messages, commit after each
        await using (var consumer1 = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(groupId)
            .WithSessionTimeout(TimeSpan.FromMilliseconds(10000))
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithOffsetCommitMode(OffsetCommitMode.Manual)
            .Build())
        {
            consumer1.Subscribe(topic);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await foreach (var msg in consumer1.ConsumeAsync(cts.Token))
            {
                processedMessages.Add(msg.Value);
                await consumer1.CommitAsync();

                if (processedMessages.Count >= 3) break;
            }

            // Graceful close so the second consumer can join immediately
            await consumer1.CloseAsync();
        }

        // Second consumer: should get messages starting from offset 3
        await using var consumer2 = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(groupId)
            .WithSessionTimeout(TimeSpan.FromMilliseconds(10000))
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithOffsetCommitMode(OffsetCommitMode.Manual)
            .Build();

        consumer2.Subscribe(topic);

        using var cts2 = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await foreach (var msg in consumer2.ConsumeAsync(cts2.Token))
        {
            processedMessages.Add(msg.Value);
            await consumer2.CommitAsync();
            if (processedMessages.Count >= 10) break;
        }

        // All 10 messages should have been processed across both instances
        await Assert.That(processedMessages).Count().IsEqualTo(10);
        for (var i = 0; i < 10; i++)
        {
            await Assert.That(processedMessages[i]).IsEqualTo($"value-{i}");
        }
    }

    [Test]
    public async Task GracefulShutdown_UncommittedMessages_RedeliveredOnRestart()
    {
        // If consumer closes without committing, messages are redelivered
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var groupId = $"uncommitted-{Guid.NewGuid():N}";

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

        // First consumer: read 3 messages but DON'T commit (simulating incomplete processing)
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
                // Intentionally NOT committing to simulate incomplete processing
                if (count >= 3) break;
            }

            // Still close gracefully so the group is freed for the next consumer
            await consumer1.CloseAsync();
        }

        // Second consumer: should get ALL messages since nothing was committed
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

        // All messages redelivered from offset 0
        await Assert.That(messages).Count().IsEqualTo(5);
        await Assert.That(messages[0].Offset).IsEqualTo(0);
        await Assert.That(messages[0].Value).IsEqualTo("value-0");
    }

    [Test]
    public async Task GracefulShutdown_DisposeCommitsAutoOffsets()
    {
        // Auto-commit mode should commit on graceful dispose
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var groupId = $"auto-dispose-{Guid.NewGuid():N}";

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

        // First consumer with auto-commit: consume all 5, then dispose
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

            // CloseAsync should trigger final auto-commit
            await consumer1.CloseAsync();
        }

        // Second consumer should not re-read already committed messages
        await using var consumer2 = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(groupId)
            .WithSessionTimeout(TimeSpan.FromMilliseconds(10000))
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithOffsetCommitMode(OffsetCommitMode.Manual)
            .Build();

        consumer2.Subscribe(topic);

        // Try to consume - should get nothing since all offsets were committed
        using var cts2 = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var result = await consumer2.ConsumeOneAsync(TimeSpan.FromSeconds(5), cts2.Token);

        // Either null (no messages) or offset >= 5 (already committed)
        if (result.HasValue)
        {
            await Assert.That(result.Value.Offset).IsGreaterThanOrEqualTo(5);
        }
    }

    [Test]
    public async Task GracefulShutdown_ManualCommitPerMessage_PreciseResumePoint()
    {
        // Commit after each message for precise resume control
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var groupId = $"per-msg-commit-{Guid.NewGuid():N}";

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

        // Process exactly 7 messages with per-message commits
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
                // Commit specific offset (next offset to consume)
                await consumer1.CommitAsync(
                [
                    new TopicPartitionOffset(msg.Topic, msg.Partition, msg.Offset + 1)
                ]);

                if (count >= 7) break;
            }

            // Graceful close frees the group
            await consumer1.CloseAsync();
        }

        // Resume should start exactly at offset 7
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
        await Assert.That(firstMsg!.Value.Offset).IsEqualTo(7);
        await Assert.That(firstMsg.Value.Value).IsEqualTo("value-7");
    }

    [Test]
    public async Task GracefulShutdown_SeekAndReprocess_FullReplayFromBeginning()
    {
        // Simulate reprocessing: consumer seeks back to beginning to replay all events
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var groupId = $"reprocess-{Guid.NewGuid():N}";

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

        await using var consumer = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithOffsetCommitMode(OffsetCommitMode.Manual)
            .Build();

        consumer.Subscribe(topic);

        // First pass: consume all 5
        var firstPass = new List<string>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            firstPass.Add(msg.Value);
            if (firstPass.Count >= 5) break;
        }

        await Assert.That(firstPass).Count().IsEqualTo(5);

        // Seek back to beginning for reprocessing
        consumer.SeekToBeginning(new TopicPartition(topic, 0));

        // Second pass: replay all messages
        var secondPass = new List<string>();
        using var cts2 = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await foreach (var msg in consumer.ConsumeAsync(cts2.Token))
        {
            secondPass.Add(msg.Value);
            if (secondPass.Count >= 5) break;
        }

        await Assert.That(secondPass).Count().IsEqualTo(5);
        await Assert.That(secondPass).IsEquivalentTo(firstPass);
    }
}
