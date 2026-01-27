using Dekaf.Consumer;
using Dekaf.Producer;

namespace Dekaf.Tests.Integration;

/// <summary>
/// Integration tests for OffsetCommitMode behavior.
/// </summary>
[ClassDataSource<KafkaTestContainer>(Shared = SharedType.PerTestSession)]
public class OffsetCommitModeTests(KafkaTestContainer kafka)
{
    [Test]
    public async Task OffsetCommitMode_Manual_CommitAsync_CommitsAllConsumedOffsets()
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

        // Act - consume with manual commit mode
        await using var consumer = Dekaf.CreateConsumer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-consumer")
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithSessionTimeout(10000)
            .WithOffsetCommitMode(OffsetCommitMode.Manual)
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
    public async Task OffsetCommitMode_Manual_CommitAsyncWithOffsets_CommitsSpecificOffsets()
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

        // Act - consume with manual commit mode
        await using var consumer = Dekaf.CreateConsumer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-consumer")
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithSessionTimeout(10000)
            .WithOffsetCommitMode(OffsetCommitMode.Manual)
            .Build();

        consumer.Subscribe(topic);

        // Consume 3 messages but commit only offset 2
        var messages = new List<ConsumeResult<string, string>>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await foreach (var msg in consumer.ConsumeAsync(cts.Token).ConfigureAwait(false))
        {
            messages.Add(msg);
            if (messages.Count >= 3) break;
        }

        // Commit a specific offset (2)
        await consumer.CommitAsync([new TopicPartitionOffset(topic, 0, 2)]).ConfigureAwait(false);

        // Verify committed offset
        var committed = await consumer.GetCommittedOffsetAsync(new TopicPartition(topic, 0)).ConfigureAwait(false);

        // Assert - committed offset should be 2 (the specific offset we committed)
        await Assert.That(committed).IsEqualTo(2);
    }

    [Test]
    public async Task OffsetCommitMode_Manual_CommittedOffsetsArePersisted_NewConsumerStartsFromCommittedOffset()
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

        // First consumer: consume 3 messages and commit
        await using (var consumer1 = Dekaf.CreateConsumer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-consumer-1")
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithSessionTimeout(10000)
            .WithOffsetCommitMode(OffsetCommitMode.Manual)
            .Build())
        {
            consumer1.Subscribe(topic);

            var count = 0;
            using var cts1 = new CancellationTokenSource(TimeSpan.FromSeconds(30));

            await foreach (var msg in consumer1.ConsumeAsync(cts1.Token).ConfigureAwait(false))
            {
                count++;
                if (count >= 3) break;
            }

            // Commit all consumed offsets
            await consumer1.CommitAsync().ConfigureAwait(false);
        }

        // Second consumer: should start from offset 3
        await using var consumer2 = Dekaf.CreateConsumer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-consumer-2")
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithSessionTimeout(10000)
            .WithOffsetCommitMode(OffsetCommitMode.Manual)
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
    public async Task OffsetCommitMode_Manual_WithoutCommit_OffsetsNotPersisted()
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

        // First consumer: consume but don't commit
        await using (var consumer1 = Dekaf.CreateConsumer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-consumer-1")
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithSessionTimeout(10000)
            .WithOffsetCommitMode(OffsetCommitMode.Manual)
            .Build())
        {
            consumer1.Subscribe(topic);

            var count = 0;
            using var cts1 = new CancellationTokenSource(TimeSpan.FromSeconds(30));

            await foreach (var msg in consumer1.ConsumeAsync(cts1.Token).ConfigureAwait(false))
            {
                count++;
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
            .WithOffsetCommitMode(OffsetCommitMode.Manual)
            .Build();

        consumer2.Subscribe(topic);

        using var cts2 = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result = await consumer2.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts2.Token).ConfigureAwait(false);

        // Assert - should start from offset 0 (no committed offset exists)
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value.Offset).IsEqualTo(0);
    }

    [Test]
    public async Task OffsetCommitMode_Manual_MultipleCommits_CommitsLatestConsumedPosition()
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
            .WithOffsetCommitMode(OffsetCommitMode.Manual)
            .Build();

        consumer.Subscribe(topic);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var enumerator = consumer.ConsumeAsync(cts.Token).GetAsyncEnumerator();

        try
        {
            // Consume 2 messages and commit
            for (var i = 0; i < 2; i++)
            {
                await enumerator.MoveNextAsync().ConfigureAwait(false);
            }

            await consumer.CommitAsync().ConfigureAwait(false);

            var committedAfterFirst = await consumer.GetCommittedOffsetAsync(new TopicPartition(topic, 0)).ConfigureAwait(false);
            await Assert.That(committedAfterFirst).IsEqualTo(2);

            // Consume 2 more messages and commit again
            for (var i = 0; i < 2; i++)
            {
                await enumerator.MoveNextAsync().ConfigureAwait(false);
            }

            await consumer.CommitAsync().ConfigureAwait(false);

            var committedAfterSecond = await consumer.GetCommittedOffsetAsync(new TopicPartition(topic, 0)).ConfigureAwait(false);
            await Assert.That(committedAfterSecond).IsEqualTo(4);
        }
        finally
        {
            await enumerator.DisposeAsync().ConfigureAwait(false);
        }
    }

    [Test]
    public async Task OffsetCommitMode_Auto_CommitsAutomatically()
    {
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

        // First consumer: consume with auto commit
        await using (var consumer1 = Dekaf.CreateConsumer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-consumer-1")
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithSessionTimeout(10000)
            .WithAutoCommitInterval(100) // Fast auto-commit for testing
            .WithOffsetCommitMode(OffsetCommitMode.Auto)
            .Build())
        {
            consumer1.Subscribe(topic);

            var count = 0;
            using var cts1 = new CancellationTokenSource(TimeSpan.FromSeconds(30));

            await foreach (var msg in consumer1.ConsumeAsync(cts1.Token).ConfigureAwait(false))
            {
                count++;
                if (count >= 3) break;
            }

            // Wait for auto-commit to occur
            await Task.Delay(500).ConfigureAwait(false);
        }

        // Second consumer: should start after the auto-committed offset
        await using var consumer2 = Dekaf.CreateConsumer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-consumer-2")
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithSessionTimeout(10000)
            .WithOffsetCommitMode(OffsetCommitMode.Manual)
            .Build();

        consumer2.Subscribe(topic);

        // Check committed offset
        var committed = await consumer2.GetCommittedOffsetAsync(new TopicPartition(topic, 0)).ConfigureAwait(false);

        // Assert - auto-commit should have committed the consumed offsets
        await Assert.That(committed).IsNotNull();
        await Assert.That(committed!.Value).IsGreaterThanOrEqualTo(3);
    }
}
