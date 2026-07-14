using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using Dekaf.Admin;
using Dekaf.Consumer;
using Dekaf.Producer;
using Dekaf.Serialization;

namespace Dekaf.Tests.Integration.RealWorld;

/// <summary>
/// Pins the core offset-safety invariant: the consumer must never commit an offset
/// past messages the application has not consumed. Loss of this property silently
/// converts the client from at-least-once to at-most-once.
/// Covers issues #2059 (auto-commit never commits in-flight/prefetched offsets),
/// #2060 (deserializer failure never lets commits advance past the poison record),
/// and #2062 (uncommitted progress on revoked partitions is redelivered, never skipped).
/// </summary>
[Category("Consumer")]
public sealed class OffsetCommitSafetyTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    [Test]
    public async Task AutoCommit_InFlightAndPrefetchedMessages_NeverCommitted()
    {
        const int messageCount = 50;
        const int heldMessageIndex = 5;
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var groupId = $"offset-safety-inflight-{Guid.NewGuid():N}";
        var partition = new TopicPartition(topic, 0);

        await ProduceMessagesAsync(topic, messageCount);

        await using var admin = KafkaContainer.CreateAdminClient();

        await using (var consumer = await CreateAutoCommitConsumerAsync(groupId, TimeSpan.FromMilliseconds(100)))
        {
            consumer.Subscribe(topic);

            var consumed = 0;
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            await foreach (var message in consumer.ConsumeAsync(cts.Token))
            {
                consumed++;

                if (consumed == heldMessageIndex)
                {
                    // Hold this message in-flight while the 100ms auto-commit timer fires
                    // many times. Prefetch has buffered records far beyond this point, but
                    // the committed offset must never advance past the consumed boundary:
                    // committing the held message would be offset `heldMessageIndex`, so
                    // anything above `message.Offset` is a safety violation.
                    await AssertCommittedNeverExceedsAsync(
                        admin, groupId, partition, message.Offset, TimeSpan.FromSeconds(1.5), cts.Token);
                }

                if (consumed == messageCount)
                    break;
            }

            await consumer.CloseAsync();
        }

        // The loop broke while holding the final record (offset messageCount - 1), so it
        // is unproven and close-commit lands exactly one below the log end: higher would
        // skip messages on restart, lower would lose proven progress. The final record is
        // redelivered on restart — at-least-once, never a loss. (Call CommitAsync() before
        // close for an exact handoff.)
        var finalOffsets = await admin.ListConsumerGroupOffsetsAsync(groupId);
        await Assert.That(finalOffsets[partition]).IsEqualTo(messageCount - 1);
    }

    [Test]
    public async Task Rebalance_UncommittedProgress_RedeliveredNotSkipped()
    {
        const int partitions = 2;
        const int messagesPerPartition = 30;
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions);
        var groupId = $"offset-safety-rebalance-{Guid.NewGuid():N}";

        await ProduceMessagesAsync(topic, messagesPerPartition, partitions);

        // Auto-commit mode with an interval that never elapses: all progress made by the
        // first consumer stays uncommitted, so when the second consumer joins and takes
        // over a partition, everything after the last committed position must be
        // redelivered — a rebalance may create duplicates but must never create gaps.
        var seen = new ConcurrentDictionary<(int Partition, long Offset), byte>();
        using var stopConsuming = new CancellationTokenSource(TimeSpan.FromSeconds(120));

        await using var consumerA = await CreateAutoCommitConsumerAsync(groupId, TimeSpan.FromMinutes(10));
        consumerA.Subscribe(topic);
        // Consume slowly so the group rebalance happens mid-partition.
        var consumerATask = ConsumeIntoAsync(consumerA, seen, TimeSpan.FromMilliseconds(100), stopConsuming.Token);

        await WaitForConditionAsync(() => seen.Count >= 10, TimeSpan.FromSeconds(60));

        await using var consumerB = await CreateAutoCommitConsumerAsync(groupId, TimeSpan.FromMinutes(10));
        consumerB.Subscribe(topic);
        var consumerBTask = ConsumeIntoAsync(consumerB, seen, TimeSpan.Zero, stopConsuming.Token);

        await WaitForConditionAsync(
            () => seen.Count >= partitions * messagesPerPartition,
            TimeSpan.FromSeconds(90));

        stopConsuming.Cancel();
        await Task.WhenAll(consumerATask, consumerBTask);

        for (var p = 0; p < partitions; p++)
        {
            for (long offset = 0; offset < messagesPerPartition; offset++)
            {
                await Assert.That(seen.ContainsKey((p, offset)))
                    .IsTrue()
                    .Because($"message at partition {p} offset {offset} was skipped across the rebalance");
            }
        }
    }

    [Test]
    public async Task Resume_CommittedOffsetInsideBatch_StartsExactlyAtCommittedOffset()
    {
        const int messageCount = 10;
        const long committedOffset = 5;
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var groupId = $"offset-safety-midbatch-{Guid.NewGuid():N}";

        // ProduceAllAsync coalesces the messages into few (typically one) record batches,
        // so the committed offset lands inside a batch. The broker returns whole batches,
        // and the client must skip the leading records below the fetch position instead
        // of re-delivering already-committed records.
        await ProduceMessagesAsync(topic, messageCount);

        await using (var committer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithOffsetCommitMode(OffsetCommitMode.Manual)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory()).BuildAsync())
        {
            committer.Subscribe(topic);
            await committer.CommitAsync([new TopicPartitionOffset(topic, 0, committedOffset)]);
            await committer.CloseAsync();
        }

        await using var resumed = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithOffsetCommitMode(OffsetCommitMode.Manual)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory()).BuildAsync();

        resumed.Subscribe(topic);

        var offsets = new List<long>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await foreach (var message in resumed.ConsumeAsync(cts.Token))
        {
            offsets.Add(message.Offset);
            if (offsets.Count >= messageCount - committedOffset)
                break;
        }

        // Exactly the records from the committed offset onwards, in order — no
        // re-delivery of committed records, no skipped records.
        await Assert.That(offsets).IsEquivalentTo(
            Enumerable.Range((int)committedOffset, (int)(messageCount - committedOffset)).Select(i => (long)i).ToArray());
    }

    [Test]
    public async Task PoisonMessage_DeserializerThrows_SurfacesErrorAndNeverCommitsPastPoison()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();
        var groupId = $"offset-safety-poison-{Guid.NewGuid():N}";
        var partition = new TopicPartition(topic, 0);
        const long poisonOffset = 2;

        await using (var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync())
        {
            await producer.ProduceAllAsync(
                new[] { "ok-0", "ok-1", PoisonValueDeserializer.PoisonValue, "ok-3" }
                    .Select(value => new ProducerMessage<string, string>
                    {
                        Topic = topic,
                        Key = "key",
                        Value = value
                    }));
        }

        await using var admin = KafkaContainer.CreateAdminClient();

        await using (var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithOffsetCommitMode(OffsetCommitMode.Auto)
            .WithAutoCommitInterval(TimeSpan.FromMilliseconds(100))
            .WithValueDeserializer(new PoisonValueDeserializer())
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory()).BuildAsync())
        {
            consumer.Subscribe(topic);

            var receivedOffsets = new List<long>();
            Exception? surfaced = null;
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            try
            {
                await foreach (var message in consumer.ConsumeAsync(cts.Token))
                {
                    receivedOffsets.Add(message.Offset);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                surfaced = ex;
            }

            // The deserializer failure must surface, not be swallowed with the record skipped.
            await Assert.That(surfaced).IsNotNull();
            await Assert.That(receivedOffsets).IsEquivalentTo([0L, 1L]);

            // Let the auto-commit timer fire repeatedly: it may commit the records
            // delivered before the poison one, but never the poison record itself.
            await AssertCommittedNeverExceedsAsync(
                admin, groupId, partition, poisonOffset, TimeSpan.FromMilliseconds(500));

            await consumer.CloseAsync();
        }

        var finalOffsets = await admin.ListConsumerGroupOffsetsAsync(groupId);
        await Assert.That(finalOffsets[partition]).IsEqualTo(poisonOffset);

        // A restarted consumer must be redelivered the poison record — it was never
        // processed, so committing past it would have lost it.
        await using var resumed = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithOffsetCommitMode(OffsetCommitMode.Manual)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory()).BuildAsync();

        resumed.Subscribe(topic);

        using var resumeCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var redelivered = await resumed.ConsumeOneAsync(TimeSpan.FromSeconds(30), resumeCts.Token);

        await Assert.That(redelivered).IsNotNull();
        await Assert.That(redelivered!.Value.Offset).IsEqualTo(poisonOffset);
        await Assert.That(redelivered.Value.Value).IsEqualTo(PoisonValueDeserializer.PoisonValue);
    }

    private async Task ProduceMessagesAsync(string topic, int messagesPerPartition, int partitions = 1)
    {
        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        await producer.ProduceAllAsync(Enumerable.Range(0, partitions)
            .SelectMany(p => Enumerable.Range(0, messagesPerPartition)
                .Select(i => new ProducerMessage<string, string>
                {
                    Topic = topic,
                    Key = $"key-{i}",
                    Value = $"value-{i}",
                    Partition = p
                })));
    }

    /// <summary>
    /// Polls the broker-committed offset for the group over the given window, asserting it
    /// never exceeds <paramref name="upperBound"/>. Polls rather than delaying once so a
    /// violation is caught as soon as the auto-commit timer publishes it.
    /// </summary>
    private static async Task AssertCommittedNeverExceedsAsync(
        IAdminClient admin,
        string groupId,
        TopicPartition partition,
        long upperBound,
        TimeSpan window,
        CancellationToken cancellationToken = default)
    {
        var elapsed = Stopwatch.StartNew();
        while (elapsed.Elapsed < window)
        {
            var committedOffsets = await admin.ListConsumerGroupOffsetsAsync(groupId, cancellationToken);
            if (committedOffsets.TryGetValue(partition, out var committed))
            {
                await Assert.That(committed).IsLessThanOrEqualTo(upperBound);
            }

            await Task.Delay(100, cancellationToken);
        }
    }

    private async Task<IKafkaConsumer<string, string>> CreateAutoCommitConsumerAsync(
        string groupId,
        TimeSpan autoCommitInterval)
    {
        return await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithOffsetCommitMode(OffsetCommitMode.Auto)
            .WithAutoCommitInterval(autoCommitInterval)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();
    }

    private static Task ConsumeIntoAsync(
        IKafkaConsumer<string, string> consumer,
        ConcurrentDictionary<(int Partition, long Offset), byte> seen,
        TimeSpan perMessageDelay,
        CancellationToken cancellationToken)
    {
        return Task.Run(async () =>
        {
            try
            {
                await foreach (var message in consumer.ConsumeAsync(cancellationToken))
                {
                    seen.TryAdd((message.Partition, message.Offset), 0);

                    if (perMessageDelay > TimeSpan.Zero)
                        await Task.Delay(perMessageDelay, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when the test stops consumption.
            }
        }, CancellationToken.None);
    }

    /// <summary>
    /// String deserializer that throws on a specific payload, simulating a poison message.
    /// </summary>
    private sealed class PoisonValueDeserializer : IDeserializer<string>
    {
        public const string PoisonValue = "poison";

        public string Deserialize(ReadOnlyMemory<byte> data, SerializationContext context)
        {
            var value = Encoding.UTF8.GetString(data.Span);
            return value == PoisonValue
                ? throw new FormatException($"Simulated poison message: '{value}'.")
                : value;
        }
    }
}
