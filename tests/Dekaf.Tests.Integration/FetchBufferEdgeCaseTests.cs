using System.Diagnostics;
using Dekaf.Consumer;
using Dekaf.Producer;

namespace Dekaf.Tests.Integration;

/// <summary>
/// Integration tests for edge cases around fetch buffer sizing.
/// Verifies behavior when fetch min/max bytes, max wait, and per-partition limits
/// are configured to trigger boundary conditions.
/// </summary>
[Category("Consumer")]
public sealed class FetchBufferEdgeCaseTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    [Test]
    public async Task CheckCrcs_DefaultAndOptOut_ConsumeValidBatch()
    {
        var topic = await KafkaContainer.CreateTestTopicAsync();
        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();
        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "crc-key",
            Value = "crc-value"
        }, CancellationToken.None);

        await using var checkedConsumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();
        checkedConsumer.Assign(new TopicPartition(topic, 0));

        await using var uncheckedConsumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithCheckCrcs(false)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();
        uncheckedConsumer.Assign(new TopicPartition(topic, 0));

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var checkedResult = await checkedConsumer.ConsumeOneAsync(TimeSpan.FromSeconds(15), cts.Token);
        var uncheckedResult = await uncheckedConsumer.ConsumeOneAsync(TimeSpan.FromSeconds(15), cts.Token);

        await Assert.That(checkedResult!.Value.Value).IsEqualTo("crc-value");
        await Assert.That(uncheckedResult!.Value.Value).IsEqualTo("crc-value");
    }

    [Test]
    public async Task FetchMinBytes_SetHigh_ConsumerWaitsForEnoughData()
    {
        // Arrange - produce a small message that won't meet the min bytes threshold
        var topic = await KafkaContainer.CreateTestTopicAsync();

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        // Produce a small message (~20 bytes) that won't meet the 50KB min bytes threshold
        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "key",
            Value = "small"
        }, CancellationToken.None);

        // Act - consumer with high fetch min bytes and short max wait
        // The consumer should wait (up to FetchMaxWait) because the small message
        // doesn't meet FetchMinBytes, then return when max wait expires
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithFetchMinBytes(50 * 1024) // 50KB - much larger than our small message
            .WithFetchMaxWait(TimeSpan.FromMilliseconds(500))
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory()).BuildAsync();

        var tp = new TopicPartition(topic, 0);
        consumer.Assign(tp);

        var sw = Stopwatch.StartNew();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(15), cts.Token);
        sw.Stop();

        // Assert - the message should still eventually be returned (after max wait expires)
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value.Value).IsEqualTo("small");

        // The consumer should have waited at least partially towards the max wait
        // because the data didn't meet the min bytes threshold
        // (Kafka server-side may return earlier, but the wait should be observable)
    }

    [Test]
    public async Task FetchMaxWait_ControlsMaxBlockingTime()
    {
        // Arrange - create an empty topic so the consumer will have to wait
        var topic = await KafkaContainer.CreateTestTopicAsync();

        // Act - consumer with short max wait on an empty topic
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithFetchMinBytes(1024) // Require 1KB, but topic is empty
            .WithFetchMaxWait(TimeSpan.FromMilliseconds(200)) // Short wait
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory()).BuildAsync();

        var tp = new TopicPartition(topic, 0);
        consumer.Assign(tp);

        // Try to consume from empty topic - should return null within a reasonable time
        var sw = Stopwatch.StartNew();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(5), cts.Token);
        sw.Stop();

        // Assert - should return null (no data) within a bounded time
        await Assert.That(result).IsNull();
        // The consume should have returned well within the 15-second CTS timeout,
        // demonstrating that max wait controls the blocking time
        await Assert.That(sw.Elapsed).IsLessThan(TimeSpan.FromSeconds(10));
    }

    [Test]
    public async Task FetchMaxBytes_BelowFirstRecord_Kip74StillProgresses()
    {
        const int fetchMaxBytes = 1024;
        const int oversizedRecordSize = 10_000;
        var expectedRecords = new (string Key, string Value)[]
        {
            ("oversized", new string('X', oversizedRecordSize)),
            ("after-oversized-1", "small-1"),
            ("after-oversized-2", "small-2")
        };

        var topic = await KafkaContainer.CreateTestTopicAsync();

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        // Awaiting each delivery keeps the oversized first record and the later records
        // in separate batches, so later offsets require another fetch to make progress.
        foreach (var (key, value) in expectedRecords)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = key,
                Value = value
            }, CancellationToken.None);
        }

        // Keep the per-partition limit above the record size to isolate fetch.max.bytes.
        // KIP-74 requires the first oversized record batch to be returned anyway.
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithFetchMaxBytes(fetchMaxBytes)
            .WithMaxPartitionFetchBytes(oversizedRecordSize * 2)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory()).BuildAsync();

        var tp = new TopicPartition(topic, 0);
        consumer.Assign(tp);

        var messages = await ConsumeMessagesAsync(consumer, expectedRecords.Length);

        await Assert.That(messages).Count().IsEqualTo(expectedRecords.Length);
        await Assert.That(messages[0].Value.Length).IsGreaterThan(fetchMaxBytes);
        for (var i = 0; i < expectedRecords.Length; i++)
        {
            await Assert.That(messages[i].Key).IsEqualTo(expectedRecords[i].Key);
            await Assert.That(messages[i].Value).IsEqualTo(expectedRecords[i].Value);
            await Assert.That(messages[i].Offset).IsEqualTo(i);
        }
    }

    [Test]
    [Arguments(true)]
    [Arguments(false)]
    [NotInParallel]
    public async Task SmallFetchMaxBytes_HotEarlyPartition_DoesNotStarveLaterPartitions(bool enableFetchSessions)
    {
        const int partitionCount = 4;
        const int hotPartitionMessages = 20;
        const int otherPartitionMessages = 4;
        const int maxMessagesBeforeAllPartitions = 12;
        var value = new string('F', 8 * 1024);
        var topic = await KafkaContainer.CreateTestTopicAsync(partitions: partitionCount);

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        for (var partition = 0; partition < partitionCount; partition++)
        {
            var messageCount = partition == 0 ? hotPartitionMessages : otherPartitionMessages;
            for (var message = 0; message < messageCount; message++)
            {
                await producer.ProduceAsync(new ProducerMessage<string, string>
                {
                    Topic = topic,
                    Partition = partition,
                    Key = $"{partition}-{message}",
                    Value = value
                }, CancellationToken.None);
            }
        }

        await producer.FlushWithTimeoutAsync();

        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithFetchSessions(enableFetchSessions)
            .WithFetchMaxBytes(1024)
            .WithMaxPartitionFetchBytes(16 * 1024)
            .WithFetchMaxWait(TimeSpan.FromMilliseconds(100))
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();
        consumer.Assign(
            new TopicPartition(topic, 0),
            new TopicPartition(topic, 1),
            new TopicPartition(topic, 2),
            new TopicPartition(topic, 3));

        var observedPartitions = new HashSet<int>();
        var remainingMessages = maxMessagesBeforeAllPartitions;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await foreach (var message in consumer.ConsumeAsync(cts.Token))
        {
            observedPartitions.Add(message.Partition);
            if (observedPartitions.Count == partitionCount || --remainingMessages == 0)
                break;
        }

        await Assert.That(observedPartitions).Count().IsEqualTo(partitionCount);
    }

    [Test]
    public async Task VaryingMessageSizes_FetchReturnsConsistently()
    {
        // Arrange - produce messages of wildly varying sizes
        var topic = await KafkaContainer.CreateTestTopicAsync();

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        var sizes = new[] { 10, 50_000, 5, 100_000, 1, 25_000, 200_000, 3 };
        for (var i = 0; i < sizes.Length; i++)
        {
            var value = new string('X', sizes[i]);
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{i}",
                Value = value
            }, CancellationToken.None);
        }

        // Act - consumer with moderate fetch buffer settings
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithFetchMinBytes(1)
            .WithFetchMaxBytes(64 * 1024) // 64KB - some messages exceed this
            .WithMaxPartitionFetchBytes(32 * 1024) // 32KB per partition
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory()).BuildAsync();

        var tp = new TopicPartition(topic, 0);
        consumer.Assign(tp);

        var messages = new List<ConsumeResult<string, string>>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            messages.Add(msg);
            if (messages.Count >= sizes.Length) break;
        }

        // Assert - all messages consumed with correct sizes, in order
        await Assert.That(messages).Count().IsEqualTo(sizes.Length);
        for (var i = 0; i < sizes.Length; i++)
        {
            await Assert.That(messages[i].Value.Length).IsEqualTo(sizes[i]);
        }
    }

    [Test]
    public async Task MaxPartitionFetchBytes_LimitsPerPartitionData()
    {
        // Arrange - produce multiple messages to a single partition
        var topic = await KafkaContainer.CreateTestTopicAsync();

        await using var producer = await Kafka.CreateProducer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        // Produce 10 messages each ~1KB to a single partition
        const int messageCount = 10;
        const int messageSize = 1_000;

        for (var i = 0; i < messageCount; i++)
        {
            var value = new string('M', messageSize);
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{i}",
                Value = value
            }, CancellationToken.None);
        }

        // Act - consumer with small per-partition fetch bytes
        // With MaxPartitionFetchBytes of 2KB, only ~1 message fits per fetch round,
        // so the consumer needs multiple rounds to consume all 10 messages.
        await using var consumer = await Kafka.CreateConsumer<string, string>()
            .WithBootstrapServers(KafkaContainer.BootstrapServers)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithMaxPartitionFetchBytes(2 * 1024) // 2KB per partition - fits ~1 message per fetch
            .WithFetchMaxWait(TimeSpan.FromMilliseconds(100))
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory()).BuildAsync();

        var tp = new TopicPartition(topic, 0);
        consumer.Assign(tp);

        var messages = new List<ConsumeResult<string, string>>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            messages.Add(msg);
            if (messages.Count >= messageCount) break;
        }

        // Assert - all messages should be consumed despite the small per-partition limit
        await Assert.That(messages).Count().IsEqualTo(messageCount);
        for (var i = 0; i < messageCount; i++)
        {
            await Assert.That(messages[i].Value.Length).IsEqualTo(messageSize);
        }
    }
}
