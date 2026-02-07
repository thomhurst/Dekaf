using Dekaf.Admin;
using Dekaf.Consumer;
using Dekaf.Errors;
using Dekaf.Producer;

namespace Dekaf.Tests.Integration;

/// <summary>
/// Integration tests for DeleteRecords (log truncation) admin API.
/// Closes #225
/// </summary>
public class DeleteRecordsTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    private IAdminClient CreateAdminClient()
    {
        return new AdminClientBuilder()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-admin-client")
            .Build();
    }

    /// <summary>
    /// Waits for a condition to become true with linear backoff.
    /// Admin operations in Kafka have eventual consistency - changes may not be immediately visible.
    /// </summary>
    private static async Task<T> WaitForConditionAsync<T>(
        Func<Task<T>> check,
        Func<T, bool> condition,
        int maxRetries = 10,
        int initialDelayMs = 500)
    {
        T result = default!;
        for (var i = 0; i < maxRetries; i++)
        {
            await Task.Delay(initialDelayMs * (i + 1)).ConfigureAwait(false);
            result = await check().ConfigureAwait(false);
            if (condition(result))
                return result;
        }
        return result;
    }

    /// <summary>
    /// Produces the specified number of messages to a single-partition topic.
    /// </summary>
    private async Task ProduceMessagesAsync(string topic, int count, int partition = 0)
    {
        await using var producer = Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-producer")
            .Build();

        for (var i = 0; i < count; i++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{i}",
                Value = $"value-{i}",
                Partition = partition
            }).ConfigureAwait(false);
        }
    }

    #region DeleteRecords Successfully Deletes Records

    [Test]
    public async Task DeleteRecordsAsync_DeletesRecordsBeforeSpecifiedOffset()
    {
        // Arrange - produce 10 messages
        var topic = await KafkaContainer.CreateTestTopicAsync().ConfigureAwait(false);
        await ProduceMessagesAsync(topic, 10).ConfigureAwait(false);

        await using var admin = CreateAdminClient();
        var tp = new TopicPartition(topic, 0);

        // Verify initial low watermark is 0
        var initialOffsets = await admin.ListOffsetsAsync([
            new TopicPartitionOffsetSpec
            {
                TopicPartition = tp,
                Spec = OffsetSpec.Earliest
            }
        ]).ConfigureAwait(false);
        await Assert.That(initialOffsets[tp].Offset).IsEqualTo(0);

        // Act - delete records before offset 5
        var result = await admin.DeleteRecordsAsync(
            new Dictionary<TopicPartition, long>
            {
                [tp] = 5
            }).ConfigureAwait(false);

        // Assert - result should indicate the low watermark moved
        await Assert.That(result).ContainsKey(tp);
        await Assert.That(result[tp]).IsGreaterThanOrEqualTo(5);

        // Verify low watermark moved via ListOffsets
        var afterOffsets = await WaitForConditionAsync(
            async () =>
            {
                var offsets = await admin.ListOffsetsAsync([
                    new TopicPartitionOffsetSpec
                    {
                        TopicPartition = tp,
                        Spec = OffsetSpec.Earliest
                    }
                ]).ConfigureAwait(false);
                return offsets[tp].Offset;
            },
            offset => offset >= 5).ConfigureAwait(false);

        await Assert.That(afterOffsets).IsGreaterThanOrEqualTo(5);
    }

    #endregion

    #region Low Watermark Moves Forward After DeleteRecords

    [Test]
    public async Task DeleteRecordsAsync_LowWatermarkMovesForward()
    {
        // Arrange - produce messages and check initial state
        var topic = await KafkaContainer.CreateTestTopicAsync().ConfigureAwait(false);
        await ProduceMessagesAsync(topic, 10).ConfigureAwait(false);

        await using var admin = CreateAdminClient();
        var tp = new TopicPartition(topic, 0);

        // Check initial low watermark is 0
        var initialOffsets = await admin.ListOffsetsAsync([
            new TopicPartitionOffsetSpec
            {
                TopicPartition = tp,
                Spec = OffsetSpec.Earliest
            }
        ]).ConfigureAwait(false);
        await Assert.That(initialOffsets[tp].Offset).IsEqualTo(0);

        // Check initial high watermark is 10
        var initialHighOffsets = await admin.ListOffsetsAsync([
            new TopicPartitionOffsetSpec
            {
                TopicPartition = tp,
                Spec = OffsetSpec.Latest
            }
        ]).ConfigureAwait(false);
        await Assert.That(initialHighOffsets[tp].Offset).IsEqualTo(10);

        // Act - delete records before offset 7
        await admin.DeleteRecordsAsync(
            new Dictionary<TopicPartition, long>
            {
                [tp] = 7
            }).ConfigureAwait(false);

        // Assert - low watermark should have moved to 7
        var newLowWatermark = await WaitForConditionAsync(
            async () =>
            {
                var offsets = await admin.ListOffsetsAsync([
                    new TopicPartitionOffsetSpec
                    {
                        TopicPartition = tp,
                        Spec = OffsetSpec.Earliest
                    }
                ]).ConfigureAwait(false);
                return offsets[tp].Offset;
            },
            offset => offset >= 7).ConfigureAwait(false);

        await Assert.That(newLowWatermark).IsGreaterThanOrEqualTo(7);

        // High watermark should remain at 10
        var newHighOffsets = await admin.ListOffsetsAsync([
            new TopicPartitionOffsetSpec
            {
                TopicPartition = tp,
                Spec = OffsetSpec.Latest
            }
        ]).ConfigureAwait(false);
        await Assert.That(newHighOffsets[tp].Offset).IsEqualTo(10);
    }

    #endregion

    #region Consumer with Earliest Starts from New Low Watermark

    [Test]
    public async Task DeleteRecordsAsync_ConsumerWithEarliestStartsFromNewLowWatermark()
    {
        // Arrange - produce 10 messages then delete first 5
        var topic = await KafkaContainer.CreateTestTopicAsync().ConfigureAwait(false);
        await ProduceMessagesAsync(topic, 10).ConfigureAwait(false);

        await using var admin = CreateAdminClient();
        var tp = new TopicPartition(topic, 0);

        // Delete records before offset 5
        await admin.DeleteRecordsAsync(
            new Dictionary<TopicPartition, long>
            {
                [tp] = 5
            }).ConfigureAwait(false);

        // Wait for deletion to propagate
        await WaitForConditionAsync(
            async () =>
            {
                var offsets = await admin.ListOffsetsAsync([
                    new TopicPartitionOffsetSpec
                    {
                        TopicPartition = tp,
                        Spec = OffsetSpec.Earliest
                    }
                ]).ConfigureAwait(false);
                return offsets[tp].Offset;
            },
            offset => offset >= 5).ConfigureAwait(false);

        // Act - create a new consumer with Earliest and consume
        await using var consumer = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .Build();

        consumer.Assign(tp);

        var messages = new List<ConsumeResult<string, string>>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            messages.Add(msg);
            if (messages.Count >= 5) break; // Expect 5 remaining messages (offsets 5-9)
        }

        // Assert - first message should be at offset 5 (new low watermark)
        await Assert.That(messages.Count).IsEqualTo(5);
        await Assert.That(messages[0].Offset).IsGreaterThanOrEqualTo(5);
        await Assert.That(messages[0].Value).IsEqualTo("value-5");

        // Verify we got all remaining messages in order
        for (var i = 0; i < 5; i++)
        {
            await Assert.That(messages[i].Value).IsEqualTo($"value-{i + 5}");
        }
    }

    #endregion

    #region Committed Offset Before Truncation Point

    [Test]
    public async Task DeleteRecordsAsync_CommittedOffsetBeforeTruncation_ConsumerResetsToEarliest()
    {
        // Arrange - produce 10 messages
        var topic = await KafkaContainer.CreateTestTopicAsync().ConfigureAwait(false);
        await ProduceMessagesAsync(topic, 10).ConfigureAwait(false);

        var tp = new TopicPartition(topic, 0);

        // Delete records before offset 5
        await using var admin = CreateAdminClient();
        await admin.DeleteRecordsAsync(
            new Dictionary<TopicPartition, long>
            {
                [tp] = 5
            }).ConfigureAwait(false);

        // Wait for deletion to propagate
        await WaitForConditionAsync(
            async () =>
            {
                var offsets = await admin.ListOffsetsAsync([
                    new TopicPartitionOffsetSpec
                    {
                        TopicPartition = tp,
                        Spec = OffsetSpec.Earliest
                    }
                ]).ConfigureAwait(false);
                return offsets[tp].Offset;
            },
            offset => offset >= 5).ConfigureAwait(false);

        // Act - create a consumer and seek to offset 2 (simulating a committed offset
        // that is now before the low watermark of 5). This triggers OffsetOutOfRange,
        // which with AutoOffsetReset.Earliest should reset to the new low watermark.
        await using var consumer = Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-consumer")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .Build();

        consumer.Assign(tp);

        // Seek to offset 2, which is before the truncation point (5)
        consumer.Seek(new TopicPartitionOffset(topic, 0, 2));

        var messages = new List<ConsumeResult<string, string>>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            messages.Add(msg);
            if (messages.Count >= 5) break; // Expect 5 remaining messages (offsets 5-9)
        }

        // Assert - consumer should have recovered via OffsetOutOfRange reset
        // and started from the new low watermark (offset 5)
        await Assert.That(messages.Count).IsEqualTo(5);
        await Assert.That(messages[0].Offset).IsGreaterThanOrEqualTo(5);
        await Assert.That(messages[0].Value).IsEqualTo("value-5");
    }

    #endregion

    #region DeleteRecords on Multiple Partitions Simultaneously

    [Test]
    public async Task DeleteRecordsAsync_MultiplePartitions_DeletesFromAllPartitions()
    {
        // Arrange - create a topic with 3 partitions
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 3).ConfigureAwait(false);

        // Produce messages to each partition
        await ProduceMessagesAsync(topic, 10, partition: 0).ConfigureAwait(false);
        await ProduceMessagesAsync(topic, 8, partition: 1).ConfigureAwait(false);
        await ProduceMessagesAsync(topic, 6, partition: 2).ConfigureAwait(false);

        await using var admin = CreateAdminClient();
        var tp0 = new TopicPartition(topic, 0);
        var tp1 = new TopicPartition(topic, 1);
        var tp2 = new TopicPartition(topic, 2);

        // Verify initial offsets
        var initialOffsets = await admin.ListOffsetsAsync([
            new TopicPartitionOffsetSpec { TopicPartition = tp0, Spec = OffsetSpec.Latest },
            new TopicPartitionOffsetSpec { TopicPartition = tp1, Spec = OffsetSpec.Latest },
            new TopicPartitionOffsetSpec { TopicPartition = tp2, Spec = OffsetSpec.Latest }
        ]).ConfigureAwait(false);

        await Assert.That(initialOffsets[tp0].Offset).IsEqualTo(10);
        await Assert.That(initialOffsets[tp1].Offset).IsEqualTo(8);
        await Assert.That(initialOffsets[tp2].Offset).IsEqualTo(6);

        // Act - delete records from all partitions simultaneously with different offsets
        var result = await admin.DeleteRecordsAsync(
            new Dictionary<TopicPartition, long>
            {
                [tp0] = 5,  // delete first 5 from partition 0
                [tp1] = 3,  // delete first 3 from partition 1
                [tp2] = 4   // delete first 4 from partition 2
            }).ConfigureAwait(false);

        // Assert - all partitions should be in the result
        await Assert.That(result).ContainsKey(tp0);
        await Assert.That(result).ContainsKey(tp1);
        await Assert.That(result).ContainsKey(tp2);

        // Verify low watermarks moved for all partitions
        var newEarliestOffsets = await WaitForConditionAsync(
            async () =>
            {
                var offsets = await admin.ListOffsetsAsync([
                    new TopicPartitionOffsetSpec { TopicPartition = tp0, Spec = OffsetSpec.Earliest },
                    new TopicPartitionOffsetSpec { TopicPartition = tp1, Spec = OffsetSpec.Earliest },
                    new TopicPartitionOffsetSpec { TopicPartition = tp2, Spec = OffsetSpec.Earliest }
                ]).ConfigureAwait(false);
                return offsets;
            },
            offsets =>
                offsets[tp0].Offset >= 5 &&
                offsets[tp1].Offset >= 3 &&
                offsets[tp2].Offset >= 4).ConfigureAwait(false);

        await Assert.That(newEarliestOffsets[tp0].Offset).IsGreaterThanOrEqualTo(5);
        await Assert.That(newEarliestOffsets[tp1].Offset).IsGreaterThanOrEqualTo(3);
        await Assert.That(newEarliestOffsets[tp2].Offset).IsGreaterThanOrEqualTo(4);

        // High watermarks should remain unchanged
        var highOffsets = await admin.ListOffsetsAsync([
            new TopicPartitionOffsetSpec { TopicPartition = tp0, Spec = OffsetSpec.Latest },
            new TopicPartitionOffsetSpec { TopicPartition = tp1, Spec = OffsetSpec.Latest },
            new TopicPartitionOffsetSpec { TopicPartition = tp2, Spec = OffsetSpec.Latest }
        ]).ConfigureAwait(false);

        await Assert.That(highOffsets[tp0].Offset).IsEqualTo(10);
        await Assert.That(highOffsets[tp1].Offset).IsEqualTo(8);
        await Assert.That(highOffsets[tp2].Offset).IsEqualTo(6);
    }

    #endregion
}
