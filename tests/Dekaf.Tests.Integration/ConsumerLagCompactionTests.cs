using Dekaf.Admin;
using Dekaf.Consumer;
using Dekaf.Producer;

namespace Dekaf.Tests.Integration;

/// <summary>
/// Integration tests verifying consumer lag calculation accuracy when log compaction is active.
/// Log compaction removes older records with duplicate keys, which can affect watermark offsets
/// and lag calculations. These tests ensure the client correctly reports lag in compacted topics.
/// </summary>
public sealed class ConsumerLagCompactionTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    /// <summary>
    /// Creates a topic with log compaction enabled and aggressive compaction settings
    /// to encourage the log cleaner to compact quickly.
    /// </summary>
    private async Task<string> CreateCompactedTopicAsync()
    {
        var topicName = $"compacted-lag-{Guid.NewGuid():N}";

        await using var admin = Kafka.CreateAdminClient()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .Build();

        await admin.CreateTopicsAsync([
            new NewTopic
            {
                Name = topicName,
                NumPartitions = 1,
                ReplicationFactor = 1,
                Configs = new Dictionary<string, string>
                {
                    ["cleanup.policy"] = "compact",
                    ["min.cleanable.dirty.ratio"] = "0.01",
                    ["segment.ms"] = "100",
                    ["delete.retention.ms"] = "100",
                    ["min.compaction.lag.ms"] = "0",
                    ["max.compaction.lag.ms"] = "100"
                }
            }
        ]);

        // Wait for topic creation to propagate
        await Task.Delay(3000);

        return topicName;
    }

    /// <summary>
    /// Creates a topic with delete retention policy and very short retention to force log trimming.
    /// </summary>
    private async Task<string> CreateRetentionTopicAsync()
    {
        var topicName = $"retention-lag-{Guid.NewGuid():N}";

        await using var admin = Kafka.CreateAdminClient()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .Build();

        await admin.CreateTopicsAsync([
            new NewTopic
            {
                Name = topicName,
                NumPartitions = 1,
                ReplicationFactor = 1,
                Configs = new Dictionary<string, string>
                {
                    ["cleanup.policy"] = "delete",
                    ["retention.ms"] = "1000",
                    ["segment.ms"] = "100",
                    ["segment.bytes"] = "1048576"
                }
            }
        ]);

        // Wait for topic creation to propagate
        await Task.Delay(3000);

        return topicName;
    }

    [Test]
    public async Task CompactedTopic_WatermarkOffsets_AccurateAfterCompaction()
    {
        var topic = await CreateCompactedTopicAsync();
        const int uniqueKeys = 5;
        const int duplicatesPerKey = 10;
        var totalProduced = uniqueKeys * duplicatesPerKey;

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .BuildAsync();

        // Produce many messages with duplicate keys to encourage compaction.
        // Each key gets multiple values; after compaction only the latest per key survives.
        for (var round = 0; round < duplicatesPerKey; round++)
        {
            for (var key = 0; key < uniqueKeys; key++)
            {
                await producer.ProduceAsync(new ProducerMessage<string, string>
                {
                    Topic = topic,
                    Key = $"key-{key}",
                    Value = $"value-{key}-round-{round}"
                });
            }
        }

        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        var tp = new TopicPartition(topic, 0);

        // Immediately after producing, high watermark should equal total produced messages
        var watermarksBefore = await consumer.QueryWatermarkOffsetsAsync(tp);
        await Assert.That(watermarksBefore.High).IsEqualTo(totalProduced);
        await Assert.That(watermarksBefore.Low).IsEqualTo(0);

        // Wait for compaction to potentially run. Compaction is asynchronous and
        // may not always complete in test time, but the high watermark must remain
        // accurate regardless (Kafka never decreases the high watermark).
        await Task.Delay(10_000);

        var watermarksAfter = await consumer.QueryWatermarkOffsetsAsync(tp);

        // High watermark must always equal total produced offset count.
        // Log compaction does not change the high watermark; it only removes
        // older records with duplicate keys from the log segments.
        await Assert.That(watermarksAfter.High).IsEqualTo(totalProduced);

        // Low watermark may have advanced if compaction removed early segments,
        // but it must never exceed the high watermark
        await Assert.That(watermarksAfter.Low).IsGreaterThanOrEqualTo(0);
        await Assert.That(watermarksAfter.Low).IsLessThanOrEqualTo(watermarksAfter.High);
    }

    [Test]
    public async Task CompactedTopic_ConsumerLag_ReflectsActualMessages()
    {
        var topic = await CreateCompactedTopicAsync();
        const int uniqueKeys = 10;
        const int duplicatesPerKey = 5;
        var totalProduced = uniqueKeys * duplicatesPerKey;

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .BuildAsync();

        // Produce messages with duplicate keys
        for (var round = 0; round < duplicatesPerKey; round++)
        {
            for (var key = 0; key < uniqueKeys; key++)
            {
                await producer.ProduceAsync(new ProducerMessage<string, string>
                {
                    Topic = topic,
                    Key = $"key-{key}",
                    Value = $"value-round-{round}"
                });
            }
        }

        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        var tp = new TopicPartition(topic, 0);
        consumer.Assign(tp);

        // Consume half the messages
        var consumeCount = totalProduced / 2;
        var consumed = new List<ConsumeResult<string, string>>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            consumed.Add(msg);
            if (consumed.Count >= consumeCount) break;
        }

        await Assert.That(consumed).Count().IsEqualTo(consumeCount);

        // Check consumer position and watermarks
        var position = consumer.GetPosition(tp);
        var watermarks = await consumer.QueryWatermarkOffsetsAsync(tp);

        await Assert.That(position).IsNotNull();
        await Assert.That(position!.Value).IsEqualTo(consumeCount);

        // High watermark always reflects total produced, regardless of compaction
        await Assert.That(watermarks.High).IsEqualTo(totalProduced);

        // Lag is calculated as high watermark minus position.
        // Even with compaction, offset-based lag reflects the offset distance,
        // not the number of physically remaining messages.
        var lag = watermarks.High - position.Value;
        await Assert.That(lag).IsEqualTo(totalProduced - consumeCount);

        // Wait for compaction and re-check: lag calculation should still be consistent
        await Task.Delay(10_000);

        var watermarksAfterCompaction = await consumer.QueryWatermarkOffsetsAsync(tp);

        // High watermark must remain the same after compaction
        await Assert.That(watermarksAfterCompaction.High).IsEqualTo(totalProduced);

        // Position hasn't changed (we stopped consuming)
        var positionAfterCompaction = consumer.GetPosition(tp);
        await Assert.That(positionAfterCompaction).IsEqualTo(consumeCount);

        // Lag calculation remains consistent
        var lagAfterCompaction = watermarksAfterCompaction.High - positionAfterCompaction!.Value;
        await Assert.That(lagAfterCompaction).IsEqualTo(totalProduced - consumeCount);
    }

    [Test]
    public async Task RetentionPolicyTrim_WatermarkUpdatesCorrectly()
    {
        var topic = await CreateRetentionTopicAsync();
        const int messageCount = 50;

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .BuildAsync();

        // Produce messages
        for (var i = 0; i < messageCount; i++)
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

        // Initial watermarks
        var initialWatermarks = await consumer.QueryWatermarkOffsetsAsync(tp);
        await Assert.That(initialWatermarks.High).IsEqualTo(messageCount);
        await Assert.That(initialWatermarks.Low).IsEqualTo(0);

        // Wait for retention to potentially trim old segments.
        // With 1000ms retention and 100ms segment.ms, older segments should be eligible for deletion.
        await Task.Delay(15_000);

        // Produce one more message to force a new active segment and allow
        // cleanup of the old segments that have exceeded retention
        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "trigger-key",
            Value = "trigger-value"
        });

        // Wait for cleanup to run
        await Task.Delay(5_000);

        var watermarksAfterRetention = await consumer.QueryWatermarkOffsetsAsync(tp);

        // High watermark should now be messageCount + 1 (for the trigger message)
        await Assert.That(watermarksAfterRetention.High).IsEqualTo(messageCount + 1);

        // Low watermark may have advanced due to retention-based deletion.
        // Whether it actually advances depends on Kafka's internal cleanup timing,
        // but it must remain valid (>= 0 and <= high watermark).
        await Assert.That(watermarksAfterRetention.Low).IsGreaterThanOrEqualTo(0);
        await Assert.That(watermarksAfterRetention.Low).IsLessThanOrEqualTo(watermarksAfterRetention.High);

        // The range (high - low) represents the window of available offsets.
        // After retention trim, this range may be smaller than the total produced count.
        var availableRange = watermarksAfterRetention.High - watermarksAfterRetention.Low;
        await Assert.That(availableRange).IsGreaterThanOrEqualTo(0);
        await Assert.That(availableRange).IsLessThanOrEqualTo(messageCount + 1);
    }

    [Test]
    public async Task CompactedTopic_HighWatermark_StillAccurate()
    {
        var topic = await CreateCompactedTopicAsync();

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .BuildAsync();

        // Phase 1: Produce initial messages with duplicate keys
        const int initialUniqueKeys = 20;
        for (var key = 0; key < initialUniqueKeys; key++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{key}",
                Value = "initial-value"
            });
        }

        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .BuildAsync();

        var tp = new TopicPartition(topic, 0);

        // Verify initial high watermark
        var watermarks1 = await consumer.QueryWatermarkOffsetsAsync(tp);
        await Assert.That(watermarks1.High).IsEqualTo(initialUniqueKeys);

        // Phase 2: Overwrite all keys with new values (triggers compaction candidates)
        for (var key = 0; key < initialUniqueKeys; key++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{key}",
                Value = "updated-value"
            });
        }

        var totalProduced = initialUniqueKeys * 2;

        // High watermark should reflect all produced messages
        var watermarks2 = await consumer.QueryWatermarkOffsetsAsync(tp);
        await Assert.That(watermarks2.High).IsEqualTo(totalProduced);

        // Phase 3: Wait for compaction and verify high watermark stability
        await Task.Delay(10_000);

        var watermarks3 = await consumer.QueryWatermarkOffsetsAsync(tp);

        // High watermark must never decrease, even after compaction
        await Assert.That(watermarks3.High).IsEqualTo(totalProduced);

        // Phase 4: Produce additional messages and verify high watermark advances correctly
        const int additionalMessages = 5;
        for (var i = 0; i < additionalMessages; i++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"new-key-{i}",
                Value = $"new-value-{i}"
            });
        }

        var watermarks4 = await consumer.QueryWatermarkOffsetsAsync(tp);
        await Assert.That(watermarks4.High).IsEqualTo(totalProduced + additionalMessages);

        // Verify low watermark is still valid
        await Assert.That(watermarks4.Low).IsGreaterThanOrEqualTo(0);
        await Assert.That(watermarks4.Low).IsLessThanOrEqualTo(watermarks4.High);

        // Consume all available messages and verify we reach the end
        consumer.Assign(tp);

        var allConsumed = new List<ConsumeResult<string, string>>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            allConsumed.Add(msg);

            // Stop once we reach the high watermark
            if (msg.Offset + 1 >= watermarks4.High) break;
        }

        // After consuming everything, position should match high watermark
        var finalPosition = consumer.GetPosition(tp);
        await Assert.That(finalPosition).IsNotNull();
        await Assert.That(finalPosition!.Value).IsEqualTo(watermarks4.High);

        // Final lag should be zero
        var finalWatermarks = await consumer.QueryWatermarkOffsetsAsync(tp);
        var finalLag = finalWatermarks.High - finalPosition.Value;
        await Assert.That(finalLag).IsEqualTo(0);
    }
}
