using Dekaf.Consumer;
using Dekaf.Producer;

namespace Dekaf.Tests.Integration.RealWorld;

/// <summary>
/// Tests for auto-commit behavior edge cases.
/// Verifies that auto-commit works correctly with various intervals,
/// that it fires on close/dispose, and that committed offsets are respected on restart.
/// </summary>
[Category("Consumer")]
public sealed class AutoCommitBehaviorTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    [Test]
    public async Task AutoCommit_ShortInterval_CommitsBeforeClose()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var groupId = $"auto-commit-{Guid.NewGuid():N}";

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        for (var i = 0; i < 10; i++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{i}",
                Value = $"value-{i}"
            }, CancellationToken.None);
        }

        // First consumer with short auto-commit interval
        await using (var consumer1 = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(groupId)
            .WithSessionTimeout(TimeSpan.FromMilliseconds(10000))
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithOffsetCommitMode(OffsetCommitMode.Auto)
            .WithAutoCommitInterval(TimeSpan.FromMilliseconds(100))
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory()).BuildAsync())
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
        await using var consumer2 = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(groupId)
            .WithSessionTimeout(TimeSpan.FromMilliseconds(10000))
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithOffsetCommitMode(OffsetCommitMode.Manual)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory()).BuildAsync();

        consumer2.Subscribe(topic);

        using var cts2 = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var firstMsg = await consumer2.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts2.Token);

        await Assert.That(firstMsg).IsNotNull();
        // At-least-once: breaking out of the loop leaves the 5th record (offset 4)
        // unproven, so auto-commit covers the proven prefix (offsets 0-3) and the
        // second consumer resumes at offset 4 — a duplicate, never a loss.
        await Assert.That(firstMsg!.Value.Offset).IsGreaterThanOrEqualTo(4);
    }

    [Test]
    public async Task AutoCommit_OnClose_CommitsFinalOffsets()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var groupId = $"auto-close-{Guid.NewGuid():N}";

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        for (var i = 0; i < 10; i++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{i}",
                Value = $"value-{i}"
            }, CancellationToken.None);
        }

        // Consumer with long auto-commit interval - commits should happen on close
        await using (var consumer1 = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(groupId)
            .WithSessionTimeout(TimeSpan.FromMilliseconds(10000))
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithOffsetCommitMode(OffsetCommitMode.Auto)
            .WithAutoCommitInterval(TimeSpan.FromMinutes(10)) // Very long - won't fire during test
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory()).BuildAsync())
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
        await using var consumer2 = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(groupId)
            .WithSessionTimeout(TimeSpan.FromMilliseconds(10000))
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithOffsetCommitMode(OffsetCommitMode.Manual)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory()).BuildAsync();

        consumer2.Subscribe(topic);

        // At-least-once: close commits the proven prefix (offsets 0-8). The 10th record
        // (offset 9) was consumed but never proven (the loop broke while holding it), so
        // it is redelivered — the second consumer sees offset 9 or nothing at all.
        using var cts2 = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var result = await consumer2.ConsumeOneAsync(TimeSpan.FromSeconds(5), cts2.Token);

        if (result.HasValue)
        {
            await Assert.That(result.Value.Offset).IsGreaterThanOrEqualTo(9);
        }
    }

    [Test]
    public async Task ManualCommit_WithAutoCommitDisabled_NoAutoCommitOccurs()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var groupId = $"manual-only-{Guid.NewGuid():N}";

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        for (var i = 0; i < 5; i++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{i}",
                Value = $"value-{i}"
            }, CancellationToken.None);
        }

        // First consumer: consume all but don't commit
        await using (var consumer1 = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(groupId)
            .WithSessionTimeout(TimeSpan.FromMilliseconds(10000))
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithOffsetCommitMode(OffsetCommitMode.Manual)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory()).BuildAsync())
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
        await using var consumer2 = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(groupId)
            .WithSessionTimeout(TimeSpan.FromMilliseconds(10000))
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithOffsetCommitMode(OffsetCommitMode.Manual)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory()).BuildAsync();

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

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        for (var i = 0; i < 20; i++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{i}",
                Value = $"value-{i}"
            }, CancellationToken.None);
        }

        // Consume only 8 messages with auto-commit
        await using (var consumer1 = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(groupId)
            .WithSessionTimeout(TimeSpan.FromMilliseconds(10000))
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithOffsetCommitMode(OffsetCommitMode.Auto)
            .WithAutoCommitInterval(TimeSpan.FromMilliseconds(100))
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory()).BuildAsync())
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
        await using var consumer2 = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(groupId)
            .WithSessionTimeout(TimeSpan.FromMilliseconds(10000))
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithOffsetCommitMode(OffsetCommitMode.Manual)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory()).BuildAsync();

        consumer2.Subscribe(topic);

        var messages = new List<ConsumeResult<string, string>>();
        using var cts2 = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await foreach (var msg in consumer2.ConsumeAsync(cts2.Token))
        {
            messages.Add(msg);
            if (messages.Count >= 12) break;
        }

        // Should get the remaining messages. At-least-once: the 8th record (offset 7)
        // was never proven (the loop broke while holding it), so the second consumer
        // resumes at offset 7 and redelivers it.
        await Assert.That(messages).Count().IsEqualTo(12);
        await Assert.That(messages[0].Offset).IsGreaterThanOrEqualTo(7);
    }

    [Test]
    public async Task AutoCommit_MultiPartition_CommitsAllPartitions()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 3);
        var groupId = $"auto-multi-{Guid.NewGuid():N}";

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

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
                }, CancellationToken.None);
            }
        }

        // Consume all 15 messages with auto-commit
        await using (var consumer1 = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(groupId)
            .WithSessionTimeout(TimeSpan.FromMilliseconds(10000))
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithOffsetCommitMode(OffsetCommitMode.Auto)
            .WithAutoCommitInterval(TimeSpan.FromMilliseconds(100))
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory()).BuildAsync())
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
        await using var consumer2 = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(groupId)
            .WithSessionTimeout(TimeSpan.FromMilliseconds(10000))
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithOffsetCommitMode(OffsetCommitMode.Manual)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory()).BuildAsync();

        consumer2.Subscribe(topic);

        using var cts2 = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var result = await consumer2.ConsumeOneAsync(TimeSpan.FromSeconds(5), cts2.Token);

        // At-least-once: the very last consumed record (offset 4 on one partition) was
        // never proven, so it may be redelivered; everything else is committed.
        if (result.HasValue)
        {
            await Assert.That(result.Value.Offset).IsGreaterThanOrEqualTo(4);
        }
    }

    [Test]
    public async Task AutoCommit_ProcessingFailureExitsLoop_FailedMessageRedelivered()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var groupId = $"auto-redeliver-{Guid.NewGuid():N}";

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        for (var i = 0; i < 5; i++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{i}",
                Value = $"value-{i}"
            }, CancellationToken.None);
        }

        // First consumer processes two messages, then "fails" on offset 2 with an
        // exception that exits the consume loop.
        await using (var consumer1 = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(groupId)
            .WithSessionTimeout(TimeSpan.FromMilliseconds(10000))
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithOffsetCommitMode(OffsetCommitMode.Auto)
            .WithAutoCommitInterval(TimeSpan.FromMilliseconds(100))
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory()).BuildAsync())
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            consumer1.Subscribe(topic);

            try
            {
                await foreach (var msg in consumer1.ConsumeAsync(cts.Token))
                {
                    if (msg.Offset == 2)
                        throw new InvalidOperationException("processing failed");
                }
            }
            catch (InvalidOperationException)
            {
                // Expected — the failure exits the loop without acknowledging offset 2.
            }

            await consumer1.CloseAsync();
        }

        // At-least-once: the failed message (offset 2) must be redelivered to the next
        // consumer in the group, not silently committed away.
        await using var consumer2 = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(groupId)
            .WithSessionTimeout(TimeSpan.FromMilliseconds(10000))
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithOffsetCommitMode(OffsetCommitMode.Manual)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory()).BuildAsync();

        consumer2.Subscribe(topic);

        using var cts2 = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var redelivered = await consumer2.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts2.Token);

        await Assert.That(redelivered).IsNotNull();
        await Assert.That(redelivered!.Value.Offset).IsEqualTo(2);
        await Assert.That(redelivered.Value.Value).IsEqualTo("value-2");
    }
}
