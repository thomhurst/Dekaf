using Dekaf.Consumer;
using Dekaf.Producer;

namespace Dekaf.Tests.Integration;

/// <summary>
/// Integration tests for StoreOffset functionality and EnableAutoOffsetStore configuration.
/// </summary>
[ClassDataSource<KafkaTestContainer>(Shared = SharedType.PerTestSession)]
public class StoreOffsetTests(KafkaTestContainer kafka)
{
    [Test]
    public async Task StoreOffset_WithAutoOffsetStoreDisabled_StoresOffsetManually()
    {
        // Arrange
        var topic = await kafka.CreateTestTopicAsync().ConfigureAwait(false);
        var groupId = $"test-group-{Guid.NewGuid():N}";

        // Produce messages
        await using var producer = Dekaf.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-producer")
            .Build();

        for (var i = 0; i < 5; i++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{i}",
                Value = $"value-{i}"
            }).ConfigureAwait(false);
        }

        // Act - consume with manual offset storage
        await using var consumer = Dekaf.CreateConsumer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-consumer")
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithSessionTimeout(10000)
            .WithAutoCommit(false)
            .WithAutoOffsetStore(false) // Manual offset storage
            .Build();

        consumer.Subscribe(topic);

        // Consume 3 messages but only store offset for the first 2
        var messages = new List<ConsumeResult<string, string>>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await foreach (var msg in consumer.ConsumeAsync(cts.Token).ConfigureAwait(false))
        {
            messages.Add(msg);

            // Only store offset for first 2 messages
            if (messages.Count <= 2)
            {
                consumer.StoreOffset(msg);
            }

            if (messages.Count >= 3) break;
        }

        // Commit the stored offsets
        await consumer.CommitAsync().ConfigureAwait(false);

        // Verify committed offset is 2 (offset of third message to consume)
        var committed = await consumer.GetCommittedOffsetAsync(new TopicPartition(topic, 0)).ConfigureAwait(false);

        // Assert - committed offset should be 2 (next offset after storing offset for msg at index 1)
        await Assert.That(committed).IsEqualTo(2);
    }

    [Test]
    public async Task StoreOffset_WithAutoOffsetStoreEnabled_CommitsAllConsumedOffsets()
    {
        // Arrange
        var topic = await kafka.CreateTestTopicAsync().ConfigureAwait(false);
        var groupId = $"test-group-{Guid.NewGuid():N}";

        // Produce messages
        await using var producer = Dekaf.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-producer")
            .Build();

        for (var i = 0; i < 5; i++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{i}",
                Value = $"value-{i}"
            }).ConfigureAwait(false);
        }

        // Act - consume with auto offset storage (default behavior)
        await using var consumer = Dekaf.CreateConsumer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-consumer")
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithSessionTimeout(10000)
            .WithAutoCommit(false)
            .WithAutoOffsetStore(true) // Auto offset storage (default)
            .Build();

        consumer.Subscribe(topic);

        // Consume 3 messages
        var count = 0;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await foreach (var msg in consumer.ConsumeAsync(cts.Token).ConfigureAwait(false))
        {
            count++;
            if (count >= 3) break;
        }

        // Commit - should commit all consumed positions
        await consumer.CommitAsync().ConfigureAwait(false);

        // Verify committed offset is 3 (position after consuming 3 messages)
        var committed = await consumer.GetCommittedOffsetAsync(new TopicPartition(topic, 0)).ConfigureAwait(false);

        // Assert - committed offset should be 3 (all consumed messages)
        await Assert.That(committed).IsEqualTo(3);
    }

    [Test]
    public async Task StoreOffset_StoredOffsetsArePersisted_NewConsumerStartsFromStoredOffset()
    {
        // Arrange
        var topic = await kafka.CreateTestTopicAsync().ConfigureAwait(false);
        var groupId = $"test-group-{Guid.NewGuid():N}";

        // Produce messages
        await using var producer = Dekaf.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-producer")
            .Build();

        for (var i = 0; i < 5; i++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{i}",
                Value = $"value-{i}"
            }).ConfigureAwait(false);
        }

        // First consumer: consume and store offset for message at index 2
        await using (var consumer1 = Dekaf.CreateConsumer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-consumer-1")
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithSessionTimeout(10000)
            .WithAutoCommit(false)
            .WithAutoOffsetStore(false)
            .Build())
        {
            consumer1.Subscribe(topic);

            var count = 0;
            using var cts1 = new CancellationTokenSource(TimeSpan.FromSeconds(30));

            await foreach (var msg in consumer1.ConsumeAsync(cts1.Token).ConfigureAwait(false))
            {
                count++;
                // Store offset for all consumed messages up to index 2
                consumer1.StoreOffset(msg);

                if (count >= 3) break;
            }

            // Commit stored offsets
            await consumer1.CommitAsync().ConfigureAwait(false);
        }

        // Second consumer: should start from offset 3
        await using var consumer2 = Dekaf.CreateConsumer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-consumer-2")
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithSessionTimeout(10000)
            .WithAutoCommit(false)
            .WithAutoOffsetStore(false)
            .Build();

        consumer2.Subscribe(topic);

        using var cts2 = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result = await consumer2.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts2.Token).ConfigureAwait(false);

        // Assert - should start from offset 3 (after the committed offset)
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value.Offset).IsEqualTo(3);
        await Assert.That(result.Value.Value).IsEqualTo("value-3");
    }

    [Test]
    public async Task StoreOffset_WithTopicPartitionOffset_StoresSpecificOffset()
    {
        // Arrange
        var topic = await kafka.CreateTestTopicAsync().ConfigureAwait(false);
        var groupId = $"test-group-{Guid.NewGuid():N}";

        // Produce messages
        await using var producer = Dekaf.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-producer")
            .Build();

        for (var i = 0; i < 5; i++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{i}",
                Value = $"value-{i}"
            }).ConfigureAwait(false);
        }

        // Act - store a specific offset using TopicPartitionOffset
        await using var consumer = Dekaf.CreateConsumer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-consumer")
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithSessionTimeout(10000)
            .WithAutoCommit(false)
            .WithAutoOffsetStore(false)
            .Build();

        consumer.Subscribe(topic);

        // Consume at least one message to ensure we have an assignment
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token).ConfigureAwait(false);
        await Assert.That(result).IsNotNull();

        // Store a specific offset (4)
        consumer.StoreOffset(new TopicPartitionOffset(topic, 0, 4));

        // Commit
        await consumer.CommitAsync().ConfigureAwait(false);

        // Verify committed offset
        var committed = await consumer.GetCommittedOffsetAsync(new TopicPartition(topic, 0)).ConfigureAwait(false);

        // Assert
        await Assert.That(committed).IsEqualTo(4);
    }

    [Test]
    public async Task StoreOffset_CalledMultipleTimes_LastOffsetWins()
    {
        // Arrange
        var topic = await kafka.CreateTestTopicAsync().ConfigureAwait(false);
        var groupId = $"test-group-{Guid.NewGuid():N}";

        // Produce messages
        await using var producer = Dekaf.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-producer")
            .Build();

        for (var i = 0; i < 5; i++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{i}",
                Value = $"value-{i}"
            }).ConfigureAwait(false);
        }

        // Act - store multiple offsets, last one should win
        await using var consumer = Dekaf.CreateConsumer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-consumer")
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithSessionTimeout(10000)
            .WithAutoCommit(false)
            .WithAutoOffsetStore(false)
            .Build();

        consumer.Subscribe(topic);

        // Consume messages and store offsets
        var messages = new List<ConsumeResult<string, string>>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await foreach (var msg in consumer.ConsumeAsync(cts.Token).ConfigureAwait(false))
        {
            messages.Add(msg);
            // Store offset for each message (last one should win for the partition)
            consumer.StoreOffset(msg);

            if (messages.Count >= 4) break;
        }

        // Commit
        await consumer.CommitAsync().ConfigureAwait(false);

        // Verify committed offset is 4 (last stored offset + 1)
        var committed = await consumer.GetCommittedOffsetAsync(new TopicPartition(topic, 0)).ConfigureAwait(false);

        // Assert - committed should be 4 (next offset after message at index 3)
        await Assert.That(committed).IsEqualTo(4);
    }

    [Test]
    public async Task StoreOffset_WithoutCommit_OffsetsNotPersisted()
    {
        // Arrange
        var topic = await kafka.CreateTestTopicAsync().ConfigureAwait(false);
        var groupId = $"test-group-{Guid.NewGuid():N}";

        // Produce messages
        await using var producer = Dekaf.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-producer")
            .Build();

        for (var i = 0; i < 5; i++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{i}",
                Value = $"value-{i}"
            }).ConfigureAwait(false);
        }

        // First consumer: store offset but don't commit
        await using (var consumer1 = Dekaf.CreateConsumer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-consumer-1")
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithSessionTimeout(10000)
            .WithAutoCommit(false)
            .WithAutoOffsetStore(false)
            .Build())
        {
            consumer1.Subscribe(topic);

            var count = 0;
            using var cts1 = new CancellationTokenSource(TimeSpan.FromSeconds(30));

            await foreach (var msg in consumer1.ConsumeAsync(cts1.Token).ConfigureAwait(false))
            {
                count++;
                consumer1.StoreOffset(msg);
                if (count >= 3) break;
            }

            // Intentionally NOT calling CommitAsync()
        }

        // Second consumer: should start from beginning (no committed offset)
        await using var consumer2 = Dekaf.CreateConsumer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-consumer-2")
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithSessionTimeout(10000)
            .WithAutoCommit(false)
            .Build();

        consumer2.Subscribe(topic);

        using var cts2 = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result = await consumer2.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts2.Token).ConfigureAwait(false);

        // Assert - should start from offset 0 (no committed offset exists)
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value.Offset).IsEqualTo(0);
    }

    [Test]
    public async Task StoreOffset_ClearedAfterCommit_SubsequentCommitDoesNothing()
    {
        // Arrange
        var topic = await kafka.CreateTestTopicAsync().ConfigureAwait(false);
        var groupId = $"test-group-{Guid.NewGuid():N}";

        // Produce messages
        await using var producer = Dekaf.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-producer")
            .Build();

        for (var i = 0; i < 5; i++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{i}",
                Value = $"value-{i}"
            }).ConfigureAwait(false);
        }

        await using var consumer = Dekaf.CreateConsumer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-consumer")
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithSessionTimeout(10000)
            .WithAutoCommit(false)
            .WithAutoOffsetStore(false)
            .Build();

        consumer.Subscribe(topic);

        // Consume and store offset for first 2 messages
        var count = 0;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await foreach (var msg in consumer.ConsumeAsync(cts.Token).ConfigureAwait(false))
        {
            count++;
            if (count <= 2)
            {
                consumer.StoreOffset(msg);
            }
            if (count >= 2) break;
        }

        // First commit
        await consumer.CommitAsync().ConfigureAwait(false);

        var committedAfterFirst = await consumer.GetCommittedOffsetAsync(new TopicPartition(topic, 0)).ConfigureAwait(false);
        await Assert.That(committedAfterFirst).IsEqualTo(2);

        // Second commit without storing any new offsets - should not change committed offset
        // In manual mode, stored offsets are cleared after commit
        await consumer.CommitAsync().ConfigureAwait(false);

        var committedAfterSecond = await consumer.GetCommittedOffsetAsync(new TopicPartition(topic, 0)).ConfigureAwait(false);
        await Assert.That(committedAfterSecond).IsEqualTo(2);
    }

    [Test]
    public async Task StoreOffset_MethodChaining_ReturnsConsumer()
    {
        // Arrange
        var topic = await kafka.CreateTestTopicAsync().ConfigureAwait(false);
        var groupId = $"test-group-{Guid.NewGuid():N}";

        // Produce a message
        await using var producer = Dekaf.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-producer")
            .Build();

        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "key",
            Value = "value"
        }).ConfigureAwait(false);

        await using var consumer = Dekaf.CreateConsumer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-consumer")
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithSessionTimeout(10000)
            .WithAutoCommit(false)
            .WithAutoOffsetStore(false)
            .Build();

        consumer.Subscribe(topic);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token).ConfigureAwait(false);

        await Assert.That(result).IsNotNull();

        // Act - verify method chaining works
        var returnedConsumer = consumer
            .StoreOffset(result!.Value)
            .StoreOffset(new TopicPartitionOffset(topic, 0, 10));

        // Assert - should return same consumer instance for chaining
        await Assert.That(returnedConsumer).IsSameReferenceAs(consumer);
    }

    [Test]
    public async Task StoreOffset_AutoOffsetStoreDisabled_ConsumeResultOverload_StoresNextOffset()
    {
        // This test verifies that StoreOffset(ConsumeResult) stores offset + 1
        // (the next offset to consume, not the consumed offset)

        // Arrange
        var topic = await kafka.CreateTestTopicAsync().ConfigureAwait(false);
        var groupId = $"test-group-{Guid.NewGuid():N}";

        // Produce messages
        await using var producer = Dekaf.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-producer")
            .Build();

        for (var i = 0; i < 3; i++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{i}",
                Value = $"value-{i}"
            }).ConfigureAwait(false);
        }

        await using var consumer = Dekaf.CreateConsumer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-consumer")
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithSessionTimeout(10000)
            .WithAutoCommit(false)
            .WithAutoOffsetStore(false)
            .Build();

        consumer.Subscribe(topic);

        // Consume first message (offset 0)
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token).ConfigureAwait(false);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value.Offset).IsEqualTo(0);

        // Store offset using ConsumeResult overload
        consumer.StoreOffset(result.Value);

        // Commit
        await consumer.CommitAsync().ConfigureAwait(false);

        // Verify committed offset is 1 (offset 0 + 1 = next offset to consume)
        var committed = await consumer.GetCommittedOffsetAsync(new TopicPartition(topic, 0)).ConfigureAwait(false);
        await Assert.That(committed).IsEqualTo(1);
    }
}
