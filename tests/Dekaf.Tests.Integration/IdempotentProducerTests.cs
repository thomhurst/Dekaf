using Dekaf.Consumer;
using Dekaf.Producer;

namespace Dekaf.Tests.Integration;

/// <summary>
/// Integration tests for idempotent producer behavior.
/// Verifies that the producer provides exactly-once semantics within a producer session.
/// </summary>
[Category("Producer")]
public sealed class IdempotentProducerTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    [Test]
    public async Task IdempotentProducer_ProducesSuccessfully()
    {
        // Arrange
        var topic = await KafkaContainer.CreateTestTopicAsync().ConfigureAwait(false);

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-idempotent-basic")
            .WithAcks(Acks.All)

            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        // Act
        var metadata = await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "key1",
            Value = "value1"
        }, CancellationToken.None).ConfigureAwait(false);

        // Assert
        await Assert.That(metadata.Topic).IsEqualTo(topic);
        await Assert.That(metadata.Partition).IsGreaterThanOrEqualTo(0);
        await Assert.That(metadata.Offset).IsGreaterThanOrEqualTo(0);
    }

    [Test]
    public async Task IdempotentProducer_ConcurrentProduction_AllSucceed()
    {
        // Arrange
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 3).ConfigureAwait(false);
        const int threadCount = 5;
        const int messagesPerThread = 20;

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-idempotent-concurrent")
            .WithAcks(Acks.All)

            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        var allResults = new System.Collections.Concurrent.ConcurrentBag<RecordMetadata>();
        var errors = new System.Collections.Concurrent.ConcurrentBag<Exception>();

        // Act - multiple threads producing concurrently
        var tasks = Enumerable.Range(0, threadCount).Select(async threadId =>
        {
            for (var i = 0; i < messagesPerThread; i++)
            {
                try
                {
                    var result = await producer.ProduceAsync(new ProducerMessage<string, string>
                    {
                        Topic = topic,
                        Key = $"thread-{threadId}-key-{i}",
                        Value = $"thread-{threadId}-value-{i}"
                    }, CancellationToken.None).ConfigureAwait(false);
                    allResults.Add(result);
                }
                catch (Exception ex)
                {
                    errors.Add(ex);
                }
            }
        }).ToArray();

        await Task.WhenAll(tasks).ConfigureAwait(false);

        // Assert
        await Assert.That(errors).Count().IsEqualTo(0);
        await Assert.That(allResults).Count().IsEqualTo(threadCount * messagesPerThread);

        foreach (var result in allResults)
        {
            await Assert.That(result.Topic).IsEqualTo(topic);
            await Assert.That(result.Offset).IsGreaterThanOrEqualTo(0);
        }
    }

    [Test]
    public async Task IdempotentProducer_WithMaxInFlight_MaintainsOrdering()
    {
        // Arrange - single partition to verify ordering
        var topic = await KafkaContainer.CreateTestTopicAsync().ConfigureAwait(false);
        const int messageCount = 50;

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-idempotent-ordering")
            .WithAcks(Acks.All)

            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        // Act - produce messages concurrently and verify ordering
        var produceTasks = new List<ValueTask<RecordMetadata>>();
        for (var i = 0; i < messageCount; i++)
        {
            produceTasks.Add(producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = "same-key",
                Value = $"value-{i:D4}"
            }, CancellationToken.None));
        }

        var results = new List<RecordMetadata>();
        foreach (var task in produceTasks)
        {
            results.Add(await task.ConfigureAwait(false));
        }

        // Assert - all offsets should be unique and contiguous on the single partition
        // Sort by offset since task completion order doesn't match produce order
        var sortedOffsets = results.Select(r => r.Offset).OrderBy(o => o).ToList();
        await Assert.That(sortedOffsets).Count().IsEqualTo(messageCount);
        for (var i = 1; i < sortedOffsets.Count; i++)
        {
            await Assert.That(sortedOffsets[i]).IsEqualTo(sortedOffsets[i - 1] + 1);
        }
    }

    [Test]
    public async Task IdempotentProducer_ForReliabilityPreset_ProducesCorrectly()
    {
        // Arrange - ForReliability() should enable idempotence
        var topic = await KafkaContainer.CreateTestTopicAsync().ConfigureAwait(false);

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-reliability-preset")
            .ForReliability()
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        // Act
        var metadata = await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "reliable-key",
            Value = "reliable-value"
        }, CancellationToken.None).ConfigureAwait(false);

        // Assert
        await Assert.That(metadata.Topic).IsEqualTo(topic);
        await Assert.That(metadata.Offset).IsGreaterThanOrEqualTo(0);

        // Verify by consuming
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-reliability-consumer")
            .WithGroupId($"test-group-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory()).BuildAsync();

        consumer.Subscribe(topic);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token).ConfigureAwait(false);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value.Key).IsEqualTo("reliable-key");
        await Assert.That(result.Value.Value).IsEqualTo("reliable-value");
    }

    [Test]
    public async Task IdempotentProducer_EpochSpaceExhausted_ReplacesProducerIdAndKeepsDelivering()
    {
        // Arrange - single partition; the broker must observe sequence state under the first producer ID.
        var topic = await KafkaContainer.CreateTestTopicAsync().ConfigureAwait(false);
        const int messagesBeforeExhaustion = 5;
        const int messagesAfterExhaustion = 5;

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-idempotent-epoch-exhaustion")
            .WithAcks(Acks.All)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();
        var kafkaProducer = (KafkaProducer<string, string>)producer;

        for (var i = 0; i < messagesBeforeExhaustion; i++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{i}",
                Value = $"value-{i}"
            }, CancellationToken.None).ConfigureAwait(false);
        }

        var initialProducerId = kafkaProducer.RecordAccumulator.ProducerId;
        await Assert.That(initialProducerId).IsGreaterThanOrEqualTo(0L);

        // Exhaust the epoch space through the producer's own local bumps. The partition restarts
        // its sequences at 0 under short.MaxValue on its next send, which the broker accepts as a
        // new epoch; the sequence gap skipped after that batch is then rejected with
        // OutOfOrderSequenceNumber under an epoch that cannot be bumped any further — the signal
        // that must now replace the producer ID.
        while (kafkaProducer.RecordAccumulator.ProducerEpoch < short.MaxValue)
        {
            await kafkaProducer.BumpEpochForRecoveryAsync(
                kafkaProducer.RecordAccumulator.ProducerEpoch,
                CancellationToken.None).ConfigureAwait(false);
        }

        // Act - deliveries across the exhaustion must all succeed.
        for (var i = messagesBeforeExhaustion; i < messagesBeforeExhaustion + messagesAfterExhaustion; i++)
        {
            if (i == messagesBeforeExhaustion + 1)
            {
                await Assert.That(kafkaProducer.RecordAccumulator.ProducerId).IsEqualTo(initialProducerId);
                await Assert.That(kafkaProducer.RecordAccumulator.ProducerEpoch).IsEqualTo(short.MaxValue);
                kafkaProducer.RecordAccumulator.GetAndIncrementSequence(new TopicPartition(topic, 0), 3);
            }

            var metadata = await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{i}",
                Value = $"value-{i}"
            }, CancellationToken.None).ConfigureAwait(false);
            await Assert.That(metadata.Offset).IsEqualTo((long)i);
        }

        // Assert - a new producer ID at epoch 0 replaced the exhausted one.
        await Assert.That(kafkaProducer.RecordAccumulator.ProducerId).IsNotEqualTo(initialProducerId);
        await Assert.That(kafkaProducer.RecordAccumulator.ProducerId).IsGreaterThanOrEqualTo(0L);
        await Assert.That(kafkaProducer.RecordAccumulator.ProducerEpoch).IsEqualTo((short)0);

        // Assert - every message exactly once, in order.
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-idempotent-epoch-exhaustion-consumer")
            .WithGroupId($"test-group-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory()).BuildAsync();
        consumer.Subscribe(topic);

        var consumed = await ConsumeMessagesAsync(consumer, messagesBeforeExhaustion + messagesAfterExhaustion)
            .ConfigureAwait(false);
        var expectedKeys = Enumerable.Range(0, messagesBeforeExhaustion + messagesAfterExhaustion)
            .Select(i => $"key-{i}")
            .ToArray();
        await Assert.That(consumed.Select(message => message.Key ?? "").ToArray()).IsEquivalentTo(expectedKeys);
    }

    [Test]
    public async Task IdempotentProducer_EpochBumpOnOnePartition_OtherPartitionsContinueWithoutAnotherBump()
    {
        // Regression for #3342: an epoch bump restarted only the rejected partition's sequences.
        // Every other partition continued its old counter under the new epoch, which the broker
        // rejects ("Invalid sequence number for new epoch"), so one transient error cascaded into
        // a bump per active partition. Both partitions must deliver exactly once and in order, and
        // the producer must bump exactly once.
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: 2).ConfigureAwait(false);
        const int messagesPerPartition = 5;
        const int faultAtIndex = 2;

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-idempotent-multi-partition-bump")
            .WithAcks(Acks.All)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();
        var kafkaProducer = (KafkaProducer<string, string>)producer;

        for (var i = 0; i < messagesPerPartition; i++)
        {
            for (var partition = 0; partition < 2; partition++)
            {
                if (partition == 0 && i == faultAtIndex)
                {
                    // Skip sequences on partition 0 only: its next batch is out of order and the
                    // broker rejects it with OutOfOrderSequenceNumber, which bumps the epoch.
                    kafkaProducer.RecordAccumulator.GetAndIncrementSequence(new TopicPartition(topic, 0), 3);
                }

                var metadata = await producer.ProduceAsync(new ProducerMessage<string, string>
                {
                    Topic = topic,
                    Partition = partition,
                    Key = $"p{partition}-{i}",
                    Value = $"value-{i}"
                }, CancellationToken.None).ConfigureAwait(false);

                await Assert.That(metadata.Partition).IsEqualTo(partition);
                await Assert.That(metadata.Offset).IsEqualTo((long)i);

                // Exactly one bump: partition 1 restarts its sequences under the new epoch on its
                // own next send instead of being rejected and bumping again.
                var expectedEpoch = i >= faultAtIndex ? (short)1 : (short)0;
                await Assert.That(kafkaProducer.RecordAccumulator.ProducerEpoch).IsEqualTo(expectedEpoch);
            }
        }

        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-idempotent-multi-partition-bump-consumer")
            .WithGroupId($"test-group-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory()).BuildAsync();
        consumer.Subscribe(topic);

        var consumed = await ConsumeMessagesAsync(consumer, 2 * messagesPerPartition).ConfigureAwait(false);
        await Assert.That(consumed).Count().IsEqualTo(2 * messagesPerPartition);
        for (var partition = 0; partition < 2; partition++)
        {
            var keys = consumed
                .Where(message => message.Partition == partition)
                .OrderBy(message => message.Offset)
                .Select(message => message.Key ?? "")
                .ToArray();
            var expectedKeys = Enumerable.Range(0, messagesPerPartition)
                .Select(i => $"p{partition}-{i}")
                .ToArray();
            await Assert.That(keys).IsEquivalentTo(expectedKeys);
        }
    }

    [Test]
    public async Task IdempotentProducer_LargeVolume_NoDataLoss()
    {
        // Arrange
        var topic = await KafkaContainer.CreateTestTopicAsync().ConfigureAwait(false);
        const int messageCount = 1000;

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-idempotent-volume")
            .WithAcks(Acks.All)

            .WithLinger(TimeSpan.FromMilliseconds(5))
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        // Act - produce 1000 messages
        var produceTasks = new List<ValueTask<RecordMetadata>>();
        for (var i = 0; i < messageCount; i++)
        {
            produceTasks.Add(producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{i:D4}",
                Value = $"value-{i:D4}"
            }, CancellationToken.None));
        }

        foreach (var task in produceTasks)
        {
            await task.ConfigureAwait(false);
        }

        // Consume all messages back
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithClientId("test-idempotent-volume-consumer")
            .WithGroupId($"test-group-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory()).BuildAsync();

        consumer.Subscribe(topic);

        var consumed = new List<ConsumeResult<string, string>>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

        await foreach (var msg in consumer.ConsumeAsync(cts.Token).ConfigureAwait(false))
        {
            consumed.Add(msg);
            if (consumed.Count >= messageCount) break;
        }

        // Assert - no data loss
        await Assert.That(consumed).Count().IsEqualTo(messageCount);
    }
}
